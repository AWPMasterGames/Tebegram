using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using TebegramServer;
using TebegramServer.Classes;
using TebegramServer.Controllers;
using TebegramServer.Data;

var builder = WebApplication.CreateBuilder(args);

// Привязка порта задаётся в appsettings.json (Kestrel:Endpoints → http://0.0.0.0:5000).
// Раньше здесь стоял UseUrls("https://localhost:5000"), но Kestrel:Endpoints его молча
// переопределял, из-за чего порт 5000 фактически слушал HTTP, а конфиг ложно обещал HTTPS —
// это путало настройку devtunnel и приводило к зависанию соединения (красная лампочка).

// CORS — нужен веб-клиенту (GitHub Pages / PWA), десктопному клиенту не мешает
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Предел размера загружаемого файла. По умолчанию Kestrel обрывает запрос
// на 30 МБ — на этом молча ломалась отправка чего-нибудь крупного (установщик,
// архив, длинное видео): клиент получал 413 без внятного объяснения.
// Значение продублировано в клиенте (MessengerWindow.SendFileToServer,
// MaxUploadBytes) и в вебе (docs/app.js, MAX_UPLOAD_BYTES) — там файл
// отсеивается до отправки, чтобы не гнать сотни мегабайт впустую.
const long MaxUploadBytes = 256L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxUploadBytes);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    // Отдельный лимит для multipart/form-data: он свой и по умолчанию 128 МБ
    options.MultipartBodyLengthLimit = MaxUploadBytes;
});

var app = builder.Build();

app.UseCors();

app.UseWebSockets();

// ВАЖНО: Инициализируем данные пользователей ПЕРЕД запуском основной логики
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Запуск сервера TebegramServer...");
UsersData.Initialize(); // Принудительно инициализируем данные
// Чаты грузим СТРОГО после пользователей: участники ищутся по Id среди загруженных
ChatsData.Load();
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Данные пользователей загружены, запускаем веб-сервер...");

Thread thread = new Thread(() => {
    Console.WriteLine("Запущен поток чистки голосых каналов.");
    VoiceRoomsController.CheckEmptyVoices();
}) { IsBackground = true };
thread.Start();

// ─── Раздача веб-клиента (PWA) по адресу /app ────────────────────────────────
// При запуске из репозитория — папка docs (всегда свежая, единый источник для GitHub Pages),
// в опубликованном сервере — wwwroot рядом с exe.
string[] webRootCandidates =
{
    Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "docs")),
    Path.Combine(AppContext.BaseDirectory, "wwwroot")
};
string? webRoot = webRootCandidates.FirstOrDefault(Directory.Exists);
if (webRoot != null)
{
    var webFileProvider = new PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webFileProvider, RequestPath = "/app" });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = webFileProvider,
        RequestPath = "/app",
        // no-cache: браузеры (особенно iPhone) неделями держали старые app.js/index.html
        // в эвристическом HTTP-кэше — обновления «не доезжали» до пользователей.
        // Файлы маленькие, перепроверка на каждый запуск не мешает.
        OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
    });
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Веб-клиент доступен по /app (папка: {webRoot})");
}

// Определение MIME-типа по расширению. Провайдер строит словарь на ~380 записей,
// поэтому создаём его один раз, а не на каждый запрос файла.
var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();

// Типы, которые браузер показывает САМ — только они отдаются как inline.
// Список согласован с клиентами (win: Classes/Message.cs, веб: docs/app.js FILE_KINDS):
// там такие файлы предлагают «открыть», а всё прочее — «скачать», и заголовок
// не должен обещать иного. Редкие контейнеры (mkv, avi, wmv) сюда НЕ входят:
// браузер их не проигрывает, честнее сразу отдать вложением.
// SVG намеренно вне списка: inline-SVG с пользовательским содержимым — это
// исполнение скрипта в контексте нашего домена (XSS), отдаём только вложением.
var inlineTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "image/png", "image/jpeg", "image/gif", "image/bmp", "image/webp",
    "video/mp4", "video/webm", "video/ogg", "video/quicktime",
    "audio/mpeg", "audio/wav", "audio/x-wav", "audio/ogg", "audio/mp4", "audio/aac", "audio/webm",
    "application/pdf"
};

// Отдача файла клиенту (общая для /upload/{имя} и /avatars/{имя}).
//
// Значение заголовка Content-Disposition обязано быть ASCII. Раньше имя файла
// подставлялось как есть, и на кириллице («Без_названия_(2).jpg») Kestrel кидал
// InvalidOperationException: Invalid non-ASCII character in header — запрос падал,
// файл не доходил вообще (у фото с русскими именами были пустые пузыри, у видео —
// ошибка загрузки). По RFC 6266 отдаём два варианта имени: ASCII-фолбэк в filename=
// и UTF-8 в filename*= — второй понимают все актуальные браузеры.
//
// Медиа помечаем inline, чтобы браузер ПОКАЗЫВАЛ фото/видео/аудио (клик по чипу
// файла в win-клиенте и открытие ссылки в вебе), остальное остаётся attachment.
async Task SendFileToClientAsync(HttpContext context, Microsoft.Extensions.FileProviders.IFileInfo fileInfo, string fileName, bool longLived = false)
{
    // Неизвестное расширение → application/octet-stream: браузер не станет гадать
    // и просто скачает файл (тот же сценарий, что показывают клиенты в карточке)
    if (!contentTypeProvider.TryGetContentType(fileName, out string? contentType))
        contentType = "application/octet-stream";

    bool showInline = inlineTypes.Contains(contentType);
    // Не-ASCII, кавычки и обратные слэши в фолбэке заменяем подчёркиванием
    string asciiName = new string(fileName.Select(ch => ch > 127 || ch == '"' || ch == '\\' ? '_' : ch).ToArray());

    context.Response.Headers.ContentDisposition =
        $"{(showInline ? "inline" : "attachment")}; filename=\"{asciiName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    // Запрет MIME-угадывания: без него браузер может «передумать» и выполнить файл
    // с чужим типом. Для неизвестных файлов это ещё и гарантия скачивания.
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

    // Вложения из /upload лежат под УНИКАЛЬНЫМ именем (см. CreateUniqueFile) и
    // никогда не меняются, поэтому их можно кэшировать «навсегда»: браузер и
    // веб-клиент перестают перекачивать одно и то же фото при каждом открытии
    // чата. Для app.js/index.html так делать нельзя — там стоит no-cache, иначе
    // обновления не доезжают до пользователей.
    if (longLived)
        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";

    context.Response.ContentType = contentType;
    await context.Response.SendFileAsync(fileInfo);
}

// Уведомление о прочтении: reader открыл чат с sender, поэтому сообщения sender'а
// этому reader'у считаются увиденными. Шлём sender'у SEEN▫#▫{reader} на все его
// открытые сессии — его клиент пометит те сообщения двумя галочками.
async Task NotifySeen(string reader, string sender)
{
    User? senderUser = UsersData.FindUserByUsername(sender);
    if (senderUser == null || string.IsNullOrEmpty(reader)) return;

    byte[] payload = Encoding.UTF8.GetBytes($"SEEN▫#▫{reader}");
    foreach (WebSocket session in senderUser.ChatsSessions.ToList())
    {
        if (session.State != WebSocketState.Open) continue;
        try
        {
            await session.SendAsync(new ArraySegment<byte>(payload),
                WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (WebSocketException)
        {
            // Сокет умер между проверкой и отправкой — пропускаем
        }
    }
}

// Создаёт файл с УНИКАЛЬНЫМ именем и возвращает открытый поток (имя — в savedName).
//
// Раньше файл сохранялся под исходным именем, и второй «photo.jpg» затирал первый:
// у старого сообщения внезапно менялась картинка, а у пользователей с одинаковыми
// названиями снимков (IMG_0001.jpg с телефона — обычное дело) фото перемешивались.
// Уникальность нужна и клиентскому кэшу: он хранит файлы по имени, и повторное
// использование имени означало бы, что на экране остаётся устаревшая картинка.
//
// FileMode.CreateNew с повтором, а не «проверил Exists и создал»: два одновременных
// запроса успевают выбрать одно и то же свободное имя между проверкой и созданием.
FileStream CreateUniqueFile(string directory, string originalName, out string savedName)
{
    // Path.GetFileName отрезает возможные пути в имени файла (защита от ../)
    string name = Path.GetFileName(originalName).Replace(" ", "_");
    if (string.IsNullOrWhiteSpace(name)) name = "file";

    string stem = Path.GetFileNameWithoutExtension(name);
    string ext = Path.GetExtension(name);

    for (int n = 0; n < 10000; n++)
    {
        string candidate = n == 0 ? name : $"{stem}_{n}{ext}";
        try
        {
            FileStream stream = new FileStream(Path.Combine(directory, candidate), FileMode.CreateNew);
            savedName = candidate;
            return stream;
        }
        catch (IOException)
        {
            // Имя занято — пробуем следующее
        }
    }

    throw new IOException($"Не удалось подобрать свободное имя для {name}");
}

app.MapGet("/", async (HttpContext context) =>
{
    await context.Response.WriteAsync("HI!");
});
app.MapGet("/Test", async (HttpContext context) =>
{
    await context.Response.WriteAsync("HI!");
});

app.MapPost("/upload", async (HttpContext context) =>
{
    IFormFileCollection files = context.Request.Form.Files;
    var uploadFiles = $"{Directory.GetCurrentDirectory()}/uploads";
    Directory.CreateDirectory(uploadFiles);

    string FName = string.Empty;

    foreach (var file in files)
    {
        // Имя подбирается свободное: одинаковые названия снимков больше не затирают
        // друг друга (клиенту возвращается то имя, под которым файл реально лёг)
        using var fileStream = CreateUniqueFile(uploadFiles, file.FileName, out FName);
        await file.CopyToAsync(fileStream);
        Logs.Save($"Загружен файл {FName}");
    }

    await context.Response.WriteAsync(FName);

});

app.MapGet("/upload/{FileName}", async (HttpContext context, string FileName) =>
{
    string safeName = Path.GetFileName(FileName);
    var fileProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
    var fileInfo = fileProvider.GetFileInfo($"uploads/{safeName}");

    if (!fileInfo.Exists)
    {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await context.Response.WriteAsync("Файл не найден");
        return;
    }

    // longLived: имя файла уникально и содержимое неизменно — пусть браузер
    // и веб-клиент держат его у себя и не качают повторно
    await SendFileToClientAsync(context, fileInfo, safeName, longLived: true);
});

app.MapPost("/avatars/{UserId:int}", async (HttpContext context, int UserId) =>
{
    User? avatarUser = UsersData.FindUserById(UserId);
    if (avatarUser == null)
    {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await context.Response.WriteAsync("Пользователь не найден");
        return;
    }

    IFormFileCollection files = context.Request.Form.Files;
    var uploadFiles = $"{Directory.GetCurrentDirectory()}/avatars";
    Directory.CreateDirectory(uploadFiles);

    string FName = string.Empty;

    foreach (var file in files)
    {
        // Уникальное имя важно и здесь: раньше два пользователя, залившие «me.jpg»,
        // получали ОДИН файл на двоих — второй затирал аватарку первого
        using var fileStream = CreateUniqueFile(uploadFiles, file.FileName, out FName);
        await file.CopyToAsync(fileStream);
        Logs.Save($"Загружен файл {FName}");
        avatarUser.Avatar = FName;
    }

    await context.Response.WriteAsync(FName);

});

app.MapGet("/avatarsFileName/{UserId:int}", async (HttpContext context, int UserId) =>
{
    User? user = UsersData.FindUserById(UserId);
    if (user == null)
    {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await context.Response.WriteAsync("Пользователь не найден");
        return;
    }
    await context.Response.WriteAsync(user.Avatar ?? "");
});

app.MapGet("/avatars/{FileName}", async (HttpContext context, string FileName) =>
{
    // Раньше файл искали в Data/avatars, а загружали в avatars — аватарки никогда не находились.
    string safeName = Path.GetFileName(FileName);
    var fileProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
    var fileInfo = fileProvider.GetFileInfo($"avatars/{safeName}");

    if (!fileInfo.Exists)
    {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await context.Response.WriteAsync("Файл не найден");
        return;
    }

    await SendFileToClientAsync(context, fileInfo, safeName);
});

app.MapGet("/login/{UserLogin}-{UserPassword}", async (HttpContext Context, string UserLogin, string UserPassword) =>
{
    // Коды ответов: раньше ЛЮБАЯ ошибка отдавалась с кодом 200 и текстом в теле,
    // и клиенту приходилось угадывать её по началу строки. Теперь код честный
    // (401 — неверные данные), а ТЕКСТ остался прежним — иначе сломались бы
    // выпущенные клиенты, которые опознают ошибку именно по тексту.
    if (!UsersData.IsExistUser(UserLogin))
    {
        Context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        await Context.Response.WriteAsync("Пользователь с таким логином не существует");
    }
    else if (UsersData.Authorize(UserLogin, UserPassword) != null)
    {
        var user = UsersData.FindUserByLogin(UserLogin);
        if (user != null)
        {
            await Context.Response.WriteAsync(user.ToClientSend());
            Logs.Save($"Пользователь {UserLogin} авторизировался");
        }
        else
        {
            Context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await Context.Response.WriteAsync("Ошибка при поиске пользователя");
        }
    }
    else
    {
        Context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        await Context.Response.WriteAsync("Неверный пароль");
    }
});

app.MapGet("/register/{UserLogin}-{UserPassword}-{Username}-{Name}", async (HttpContext Context, string UserLogin, string UserPassword, string Username, string Name) =>
{
    // Проверка ДО создания пользователя: поля подставляются прямо в адрес запроса,
    // где разделителем служит дефис, поэтому опасные символы должны отсекаться
    // здесь — на клиенте это лишь удобство, старый клиент проверку не сделает
    string? validationError = Tebegram.Shared.UserValidation.CheckRegistration(UserLogin, UserPassword, Username, Name);

    // Как и во входе: код ответа честный (400 — данные не годятся, 409 — занято),
    // текст прежний, чтобы выпущенные клиенты продолжали его понимать
    if (string.IsNullOrWhiteSpace(UserLogin) || string.IsNullOrWhiteSpace(UserPassword) ||
        string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Name))
    {
        Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await Context.Response.WriteAsync("Все поля должны быть заполнены");
    }
    else if (validationError != null)
    {
        Logs.Save($"Регистрация отклонена ({UserLogin}): {validationError}");
        Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await Context.Response.WriteAsync($"Ошибка: {validationError}");
    }
    else if (UsersData.IsExistUser(UserLogin))
    {
        Context.Response.StatusCode = (int)HttpStatusCode.Conflict;
        await Context.Response.WriteAsync("Пользователь с таким логином уже существует");
    }
    else if (UsersData.FindUserByUsername(Username) != null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.Conflict;
        await Context.Response.WriteAsync("Пользователь с таким именем уже существует");
    }
    else
    {
        // GetNextUserId вместо UsersCount + 1 — иначе после удаления пользователей Id дублировались
        User NewUser = new User(UsersData.GetNextUserId(), UserLogin, UserPassword, Name, Username,
                new ObservableCollection<ChatFolder> {
                new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                        }, "💬", false)
                }, "");
        NewUser.EnsureFavorites(); // сразу появляется чат «Избранное» с самим собой
        UsersData.AddUser(NewUser);
        await Context.Response.WriteAsync(NewUser.ToClientSend());
        Logs.Save($"Пользователь {UserLogin} зарегрестрировался");
    }
});

// Глобальный поиск пользователей по подстроке логина/имени (для поиска через @).
// Ответ: id▫username▫name❂id▫username▫name❂…  Точное совпадение логина — первым.
app.MapGet("/Users/find/{query}", async (HttpContext Context, string query) =>
{
    string q = query.Trim().TrimStart('@');
    if (q.Length < 2)
    {
        await Context.Response.WriteAsync("");
        return;
    }

    StringBuilder sb = new StringBuilder();
    foreach (User found in UsersData.FindUsers(q))
    {
        if (sb.Length > 0) sb.Append('❂');
        sb.Append($"{found.Id}▫{found.Username}▫{found.Name}");
    }
    await Context.Response.WriteAsync(sb.ToString());
});

app.MapGet("/UserName/{username}", async (HttpContext Context, string username) =>
{
    User? user = UsersData.FindUserByUsername(username);
    if (user == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await Context.Response.WriteAsync("Пользователь не найден");
        return;
    }

    await Context.Response.WriteAsync($"{user.Id}▫{user.Name}");
});

app.MapGet("/messages/{id:int}", async (HttpContext Context, int id) =>
{
    User? user = UsersData.FindUserById(id);
    if (user == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await Context.Response.WriteAsync("Пользователь не найден");
        return;
    }

    ChatFolder Folder = user.ChatsFolders[0];

    user.NewMessages.Clear();
    StringBuilder Messegas = new StringBuilder();
    foreach (Contact contact in Folder.Contacts.ToList())
    {
        Messegas.Append(contact.GetAllMeseges());
    }

    await Context.Response.WriteAsync(Messegas.ToString());
});
app.MapGet("/NewMessages/{id:int}", async (HttpContext Context, int id) =>
{
    User? user = UsersData.FindUserById(id);
    if (user == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await Context.Response.WriteAsync("NotFound");
        return;
    }

    await Context.Response.WriteAsync(user.GetNewMessages());
});
app.MapPost("/messages", async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();
    Message? message = ParseMessage(Request);
    if (message == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await Context.Response.WriteAsync("Неверный формат сообщения");
        return;
    }

    User? ReciverUser = UsersData.FindUserByUsername(message.Reciver);
    User? SenderUser = UsersData.FindUserByUsername(message.Sender);

    if (ReferenceEquals(ReciverUser, SenderUser))
    {
        // Чат с собой (Избранное) — сохраняем один раз, иначе дублировалось
        SenderUser?.AddMessage(message);
    }
    else
    {
        ReciverUser?.AddMessage(message);
        ReciverUser?.NewMessages.Add(message);
        SenderUser?.AddMessage(message);
        SenderUser?.NewMessages.Add(message);
    }

    Context.Response.StatusCode = 200;
});

// ─── Удаление сообщения ──────────────────────────────────────────────────────
// Тело: ownerId▫contactUsername▫scope▫time▫text   (scope = "self" | "all")
app.MapDelete("/message", async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string[] d = (await reader.ReadToEndAsync()).Split('▫');
    if (d.Length < 5 || !int.TryParse(d[0], out int ownerId))
    {
        Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
    }
    string contactUsername = d[1];
    string scope = d[2];
    string time = d[3];
    // текст мог содержать ▫ — склеиваем хвост
    string text = string.Join('▫', d.Skip(4));

    User? owner = UsersData.FindUserById(ownerId);
    if (owner == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        return;
    }

    owner.DeleteMessage(contactUsername, time, text);

    if (scope == "all" && contactUsername != owner.Username)
    {
        // Удаляем и у собеседника + уведомляем его клиент, чтобы сообщение исчезло вживую
        User? other = UsersData.FindUserByUsername(contactUsername);
        if (other != null)
        {
            other.DeleteMessage(owner.Username, time, text);
            string notify = $"DEL▫#▫{owner.Username}▫#▫{time}▫#▫{text}";
            var bytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(notify));
            foreach (var session in other.ChatsSessions.ToList())
            {
                if (session.State == WebSocketState.Open)
                {
                    try { await session.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
                    catch (WebSocketException) { }
                }
            }
        }
    }

    Context.Response.StatusCode = 200;
});

app.MapPost("/Contact", async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();
    string[] Data = Request.Split('▫');
    if (Data.Length < 3 || !int.TryParse(Data[0], out int ownerId))
    {
        Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await Context.Response.WriteAsync("Неверный формат запроса");
        return;
    }
    User? UContact = UsersData.FindUserByUsername(Data[1]);
    if (UContact == null)
    {
        Context.Response.StatusCode = 404;
        await Context.Response.WriteAsync("Пользователь не найден");
        return;
    }
    Contact contact;
    if (Data[2].Trim().Length < 1) contact = new Contact(UContact.Id, UContact.Username, UContact.Name);
    else contact = new Contact(UContact.Id, UContact.Username, Data[2]);
    UsersData.FindUserById(ownerId)?.AddContact(contact);
    Context.Response.StatusCode = 200;
    await Context.Response.WriteAsync(contact.ToString());
});
app.MapPut("/Contact", async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();
    string[] Data = Request.Split('▫');
    if (Data.Length < 3 || !int.TryParse(Data[0], out int ownerId))
    {
        Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
    }
    User? owner = UsersData.FindUserById(ownerId);
    Contact? contactToRename = owner?.FindContactByUsername(Data[1]);
    if (contactToRename == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await Context.Response.WriteAsync("Контакт не найден");
        return;
    }
    contactToRename.ChangeName(Data[2]);
    Context.Response.StatusCode = 200;
});
app.MapDelete("/Contact", async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();
    string[] Data = Request.Split('▫');
    if (Data.Length < 2 || !int.TryParse(Data[0], out int ownerId))
    {
        Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
    }
    User? user = UsersData.FindUserById(ownerId);
    Contact? contactToRemove = user?.FindContactByUsername(Data[1]);
    if (user == null || contactToRemove == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        return;
    }
    user.RemoveContact(contactToRemove);
    Context.Response.StatusCode = 200;
});

#region Folders
// ─── Папки контактов ─────────────────────────────────────────────────────────
// Формат: папки разделяются ❂, поля папки ▫ (Название▫Иконка▫username1&username2),
// папка [0] «Все чаты» не передаётся и не изменяется.

app.MapGet("/Folders/{userId:int}", async (HttpContext Context, int userId) =>
{
    User? user = UsersData.FindUserById(userId);
    if (user == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await Context.Response.WriteAsync("Пользователь не найден");
        return;
    }

    StringBuilder sb = new StringBuilder();
    foreach (ChatFolder folder in user.ChatsFolders.ToList().Skip(1))
    {
        if (sb.Length > 0) sb.Append('❂');
        string usernames = string.Join('&', folder.Contacts.ToList().Select(c => c.Username));
        sb.Append($"{folder.FolderName}▫{folder.Icon}▫{usernames}");
    }

    await Context.Response.WriteAsync(sb.ToString());
});

app.MapPut("/Folders/{userId:int}", async (HttpContext Context, int userId) =>
{
    User? user = UsersData.FindUserById(userId);
    if (user == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await Context.Response.WriteAsync("Пользователь не найден");
        return;
    }

    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();

    // Полная замена пользовательских папок (кроме «Все чаты»)
    while (user.ChatsFolders.Count > 1)
        user.ChatsFolders.RemoveAt(user.ChatsFolders.Count - 1);

    if (!string.IsNullOrWhiteSpace(Request))
    {
        foreach (string folderRaw in Request.Split('❂'))
        {
            string[] f = folderRaw.Split('▫');
            if (f.Length < 3 || string.IsNullOrWhiteSpace(f[0])) continue;

            var contacts = new ObservableCollection<Contact>();
            foreach (string username in f[2].Split('&'))
            {
                Contact? known = user.FindContactByUsername(username.Trim());
                // Лёгкая копия без сообщений — история хранится только в «Все чаты»
                if (known != null) contacts.Add(new Contact(known.UserId, known.Username, known.Name));
            }

            user.ChatsFolders.Add(new ChatFolder(f[0].Trim(), contacts, f[1], true));
        }
    }

    Logs.Save($"Пользователь {user.Username} сохранил папки: {user.ChatsFolders.Count - 1} шт.");
    Context.Response.StatusCode = 200;
});
#endregion

// Разбор сообщения из строки протокола: Sender▫Reciver▫Type▫Time▫ServerAdress▫Text
// Текст может содержать ▫, поэтому склеиваем хвост обратно с разделителем.
static Message? ParseMessage(string raw)
{
    string[] messageData = raw.Split('▫');
    if (messageData.Length < 6) return null;

    if (messageData[2] == "Text")
    {
        string text = string.Join('▫', messageData.Skip(5));
        return new Message(messageData[0], messageData[1], text, messageData[3]);
    }
    if (messageData[2] == "File")
    {
        return new Message(messageData[0], messageData[1], messageData[5], messageData[3], MessageType.File, messageData[4]);
    }
    return null;
}

#region Voices
// Голосовые каналы

app.MapGet("/Voice/CreateRoom/{userId:int}-{calledUserUsername}", async (HttpContext Context, int userId, string calledUserUsername) =>
{
    User? user = UsersData.FindUserById(userId);
    User? calledUser = UsersData.FindUserByUsername(calledUserUsername);
    if (user == null || calledUser == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await Context.Response.WriteAsync("Пользователь не найден");
        return;
    }
    string token = VoiceRoomsController.CreateRoom(user.Username + calledUser.Username);

    user.CallToken = token;
    calledUser.CallToken = $"{user.Username}▫{token}";

    await Context.Response.WriteAsync(token);
});

app.MapGet("/Voice/GetCallToken/{userId:int}", async (HttpContext Context, int userId) =>
{
    User? user = UsersData.FindUserById(userId);

    string response;

    if (user == null || string.IsNullOrEmpty(user.CallToken))
    {
        response = "NotFound";
    }
    else {
        response = user.CallToken;
    }

    await Context.Response.WriteAsync(response);
});

app.MapGet("/Voice/DeclineCall/{userId:int}-{token}", async (HttpContext Context, int userId, string token) =>
{
    User? user = UsersData.FindUserById(userId);

    if (user != null) user.CallToken = "";

    // Токен зависал у второй стороны звонка — чистим у всех участников
    UsersData.ClearCallTokens(token);

    // Комната могла уже быть удалена — раньше тут падал KeyNotFoundException
    if (VoiceRoomsController.VoiceRooms.TryGetValue(token, out var room))
    {
        await room.SendTextToRoom("CloseConnection");
    }

    await Context.Response.WriteAsync("ok");
});

app.Map("/Voice/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
    }

    string userIdRaw = context.Request.Query["userId"].ToString();
    string Token = context.Request.Query["roomToken"].ToString();

    User? user = int.TryParse(userIdRaw, out int userId) ? UsersData.FindUserById(userId) : null;
    if (user == null || VoiceRoomsController.GetRoomId(Token) == -1)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();

    VoiceRoomsController.ConnectingToRoom(ws, Token, user);
    Console.WriteLine($"Пользователь {user.Username} Подключился к комнате Id: {VoiceRoomsController.GetRoomId(Token)}");

    try
    {
        await ReceiveMessage(ws,
            async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Пересылаем ровно столько байт, сколько пришло — раньше уходил весь буфер 4096 с мусором в хвосте
                    if (VoiceRoomsController.VoiceRooms.TryGetValue(Token, out var room))
                    {
                        byte[] voice = new byte[result.Count];
                        Array.Copy(buffer, voice, result.Count);
                        await room.SendVoiceToRoom(ws, voice);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine($"Пользователь {user.Username} отключился от комнаты Id: {VoiceRoomsController.GetRoomId(Token)}");
                    await VoiceRoomsController.DisconnectFromRoom(ws, Token, result.CloseStatus ?? WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription, CancellationToken.None);
                }
            });
    }
    finally
    {
        // Клиент мог закрыться аварийно (без Close-фрейма) — убираем его из комнаты в любом случае,
        // иначе комната держит мёртвый сокет и не удаляется
        await VoiceRoomsController.DisconnectFromRoom(ws, Token, WebSocketCloseStatus.NormalClosure, "Соединение разорвано", CancellationToken.None);
    }
});

// Принимает сообщения, пока сокет открыт. Дожидается EndOfMessage,
// чтобы длинные сообщения (>4096 байт) не разрезались на части.
static async Task ReceiveMessage(WebSocket socket, Func<WebSocketReceiveResult, byte[], Task> handleMessage)
{
    var buffer = new byte[4096];
    while (socket.State == WebSocketState.Open)
    {
        WebSocketReceiveResult result;
        using var ms = new MemoryStream();
        try
        {
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
        }
        catch (WebSocketException)
        {
            // Аварийный разрыв соединения (клиент убит/потерял сеть) — выходим, cleanup сделает вызывающий код
            break;
        }

        byte[] whole = ms.ToArray();
        var wholeResult = new WebSocketReceiveResult(whole.Length, result.MessageType, true, result.CloseStatus, result.CloseStatusDescription);
        await handleMessage(wholeResult, whole);

        if (result.MessageType == WebSocketMessageType.Close) break;
    }
}
#endregion

#region Chat

// Создание чата (перенос из main-dev, коммит 64bc1ab, с фиксами).
// {usernames} — один или несколько логинов через ▫: до двух участников — личный чат,
// три и больше — группа (владелец — создатель, имя — из имён участников).
// Наши клиенты этот эндпоинт пока не вызывают (чаты — «спящая» сущность),
// но клиент из main-dev уже умеет. Фиксы против оригинала:
// — FindUserByUsername может вернуть null: в оригинале null попадал в members
//   и ронял CreateChat (NRE), здесь — понятная ошибка 400;
// — при НЕпустых Name/Avatar чата в ответ шли пустые строки (переменные
//   инициализировались empty и заполнялись только когда поля чата пусты);
// — для чата с собой после дедупа участник один — оригинальный members[1]
//   кидал ArgumentOutOfRangeException.
// Название группы приходит ОТДЕЛЬНЫМ параметром запроса (?name=…), а не в пути:
// в пути разделителем служит дефис, и любое имя с дефисом сдвинуло бы разбор —
// ровно так рождались мусорные аккаунты при регистрации.
app.MapGet("/Chat/Create/{userId}-{usernames}", async (HttpContext Context, int userId, string usernames) =>
{
    User? creator = UsersData.FindUserById(userId);
    if (creator == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await Context.Response.WriteAsync("Пользователь не найден");
        return;
    }

    List<User> members = new List<User> { creator };
    foreach (string username in usernames.Split('▫'))
    {
        User? member = UsersData.FindUserByUsername(username);
        if (member == null)
        {
            Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await Context.Response.WriteAsync($"Пользователь {username} не найден");
            return;
        }
        members.Add(member);
    }

    string groupName = Context.Request.Query["name"].ToString();
    int chatId = ChatsController.CreateChat(members, groupName);
    Chat chat = ChatsController.Chats[chatId];
    string owner = chat.Owner != null ? $"{chat.Owner.Id}" : "None";

    // Личный чат в ответе показываем как собеседника (у самого чата имя/аватар пустые);
    // чат с собой («Избранное») — как себя
    string name = chat.Name;
    string avatar = chat.Avatar;
    if (!chat.IsGroup)
    {
        User other = chat.Members.FirstOrDefault(m => m.Id != creator.Id) ?? creator;
        if (string.IsNullOrEmpty(name)) name = other.Name;
        if (string.IsNullOrEmpty(avatar)) avatar = other.Avatar;
    }

    await Context.Response.WriteAsync($"{chat.Id}&{name}&{chat.IsGroup}&{avatar}&{owner}");
});

// Список чатов пользователя. ОТДЕЛЬНЫЙ эндпоинт, а не расширение ответа логина:
// формат /login разбирают все выпущенные клиенты (контакты читаются с индекса 9
// до конца), и дописать туда чаты — значит сломать их. Новый клиент просто
// делает ещё один запрос, старый про него не знает.
// Формат: чаты через ❂, поля чата — id&имя&группа?&аватар&владелец&участники.
app.MapGet("/Chats/{userId:int}", async (HttpContext Context, int userId) =>
{
    User? user = UsersData.FindUserById(userId);
    if (user == null)
    {
        Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await Context.Response.WriteAsync("Пользователь не найден");
        return;
    }

    StringBuilder sb = new StringBuilder();
    foreach (int chatId in user.Chats.ToList())
    {
        if (!ChatsController.Chats.TryGetValue(chatId, out Chat? chat)) continue;
        if (!chat.IsGroup) continue; // личные чаты клиент уже видит как контакты
        if (sb.Length > 0) sb.Append('❂');
        sb.Append(ChatsController.ChatToLine(chat));
    }
    await Context.Response.WriteAsync(sb.ToString());
});

// История группового чата: сообщения через ❂ в том же виде, что приходят по WS
// (без конверта — конверт нужен только чтобы отличить чат в живом потоке).
app.MapGet("/Chat/History/{chatId:int}", async (HttpContext Context, int chatId) =>
{
    if (!ChatsController.Chats.TryGetValue(chatId, out Chat? chat))
    {
        Context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await Context.Response.WriteAsync("Чат не найден");
        return;
    }

    StringBuilder sb = new StringBuilder();
    foreach (Message message in chat.Messages.ToList())
    {
        if (sb.Length > 0) sb.Append('❂');
        sb.Append(message.ToString());
    }
    await Context.Response.WriteAsync(sb.ToString());
});

app.Map("/Chat/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
    }

    string userIdRaw = context.Request.Query["userId"].ToString();
    User? user = int.TryParse(userIdRaw, out int userId) ? UsersData.FindUserById(userId) : null;
    if (user == null)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    user.ChatsSessions.Add(ws);

    try
    {
        await ReceiveMessage(ws,
            async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string request = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    string[] data = request.Split("▫#▫");

                    switch (data[0].ToUpper())
                    {
                        // ПЕРЕХОД НА ChatId: клиенты сейчас шлют SEND▫#▫0▫#▫username▫#▫payload
                        // (chatId всегда 0, чат ищется по username в CheckIsExist).
                        // В v2 клиент шлёт реальный chatId — поле username останется
                        // фолбэком для старых клиентов, ломать формат кадра не нужно.
                        case "SEND":
                            if (data.Length < 4 || !int.TryParse(data[1], out int requestedChatId)) break;
                            int chatId = ChatsController.CheckIsExist(requestedChatId, user, data[2]);
                            if (chatId != -1) await ChatsController.SendMessage(chatId, data[3]);
                            break;

                        // SEEN▫#▫{кто-открыл}▫#▫{чьи-сообщения-прочитаны}: получатель
                        // открыл чат с отправителем. Пересылаем отправителю SEEN▫#▫{кто-открыл},
                        // и у того его сообщения этому человеку станут двумя галочками.
                        case "SEEN":
                            if (data.Length >= 3) await NotifySeen(data[1], data[2]);
                            break;

                        // DELETEChat▫#▫{ChatId}
                        // Удаление чата
                        // В данный момент пользователь может удалить Группу если он является владельцем
                        case "DELETECHAT":
                            ChatsController.DeleteChat(int.Parse(data[1]), user);
                            break;
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine($"Пользователь {user.Username} закрыл клиент");
                    if (ws.State == WebSocketState.CloseReceived)
                    {
                        await ws.CloseAsync(result.CloseStatus ?? WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription, CancellationToken.None);
                    }
                }
            });
    }
    finally
    {
        // Убираем сессию всегда — даже при аварийном разрыве.
        // Раньше мёртвые сокеты копились в ChatsSessions навсегда.
        user.ChatsSessions.Remove(ws);
    }
});

#endregion

app.Run();
