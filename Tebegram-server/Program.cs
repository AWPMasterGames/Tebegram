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
// переопределял, из-за чего порт 5000 фактически слушал HTTP, а конфиг ложно обещал HTTPS -
// это путало настройку devtunnel и приводило к зависанию соединения (красная лампочка).

// Необязательный HTTPS-эндпоинт, по умолчанию выключен. Основная схема работы
// прежняя: HTTP за туннелем, TLS терминирует сам туннель.
//
// Нужен ровно для одного случая - открыть веб-клиент с другого устройства локальной
// сети. Доступ к микрофону браузер выдаёт только в защищённом контексте, и исключение
// сделано единственно для localhost. По адресу вида http://192.168.0.5:5000 объект
// navigator.mediaDevices отсутствует, поэтому звонок в браузере не начинается вовсе.
//
// Включается переменной окружения HttpsPort, например HttpsPort=5001. Сертификат
// берётся из dotnet dev-certs. Без переменной поведение сервера не меняется.
int httpsPort = builder.Configuration.GetValue<int>("HttpsPort");
if (httpsPort > 0)
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(httpsPort, listen => listen.UseHttps());
    });
    Console.WriteLine($"[Kestrel] HTTPS запрошен на порту {httpsPort}");
}

// CORS - нужен веб-клиенту (GitHub Pages / PWA), десктопному клиенту не мешает
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Предел размера загружаемого файла - 16 МБ. Ограничение задаёт не Kestrel, а
// туннель devtunnel: тело запроса свыше 16 МБ он обрывает почти в самом конце.
//
// Значение продублировано в трёх местах: здесь, в MessengerWindow.MaxUploadBytes
// и в MAX_UPLOAD_BYTES файла docs/app.js. Клиенты отсеивают файл до отправки и
// сообщают о причине. При переходе с туннеля на прямой хостинг предел поднимается
// во всех трёх местах.
const long MaxUploadBytes = 16L * 1024 * 1024;
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
// При запуске из репозитория - папка docs (всегда свежая, единый источник для GitHub Pages),
// в опубликованном сервере - wwwroot рядом с exe.
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
        // в эвристическом HTTP-кэше - обновления «не доезжали» до пользователей.
        // Файлы маленькие, перепроверка на каждый запуск не мешает.
        OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
    });
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Веб-клиент доступен по /app (папка: {webRoot})");
}

// Определение MIME-типа по расширению. Провайдер строит словарь на ~380 записей,
// поэтому создаём его один раз, а не на каждый запрос файла.
var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();

// Типы, которые браузер отображает без сторонних средств. Только они отдаются
// как inline. Список согласован с клиентами: Classes/Message.cs в приложении
// Windows и FILE_KINDS в docs/app.js. Там такие файлы предлагаются к открытию,
// остальные к загрузке, и заголовок ответа не должен этому противоречить.
//
// Редкие контейнеры mkv, avi и wmv в список не входят: браузер их не проигрывает.
// SVG исключён намеренно. Файл с произвольным содержимым, показанный как inline,
// исполняет скрипт в контексте домена сервера, то есть открывает XSS.
var inlineTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "image/png", "image/jpeg", "image/gif", "image/bmp", "image/webp",
    "video/mp4", "video/webm", "video/ogg", "video/quicktime",
    "audio/mpeg", "audio/wav", "audio/x-wav", "audio/ogg", "audio/mp4", "audio/aac", "audio/webm",
    "application/pdf"
};

// Отдача файла клиенту, общая для /upload/{имя} и /avatars/{имя}.
//
// Значение заголовка Content-Disposition обязано быть в кодировке ASCII. Ранее имя
// подставлялось без преобразования, и на кириллице Kestrel выбрасывал
// InvalidOperationException: Invalid non-ASCII character in header. Запрос падал
// целиком, файл до клиента не доходил: фотографии с русскими именами показывались
// пустыми пузырями, видео сообщало об ошибке загрузки.
//
// По RFC 6266 передаются оба варианта имени: транслитерация в filename= и исходное
// имя в кодировке UTF-8 в filename*=. Второй вариант понимают актуальные браузеры.
//
// Медиафайлы помечаются как inline, чтобы браузер отображал их вместо загрузки.
// Остальные типы остаются attachment.
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
    // чата. Для app.js/index.html так делать нельзя - там стоит no-cache, иначе
    // обновления не доезжают до пользователей.
    if (longLived)
        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";

    context.Response.ContentType = contentType;

    // ── Частичная отдача (HTTP Range) ────────────────────────────────────────
    // Без неё элемент <video> не поддерживает перемотку: ползунок перемещается,
    // а кадр остаётся прежним. На телефоне это не проявлялось, поскольку ролик
    // загружался целиком, на компьютере перемотка не работала. По той же причине
    // не действовал preload="metadata": кадр предпросмотра не строился, и видео
    // отображалось в чате строкой с именем файла.
    //
    // SendFileAsync принимает смещение и длину, поэтому достаточно разобрать
    // заголовок Range и ответить кодом 206 с заголовком Content-Range.
    long fileLength = fileInfo.Length;
    context.Response.Headers.AcceptRanges = "bytes";

    string rangeHeader = context.Request.Headers.Range.ToString();
    if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
    {
        // Поддерживаем один диапазон: "bytes=НАЧАЛО-КОНЕЦ", "bytes=НАЧАЛО-", "bytes=-ХВОСТ".
        // Составные диапазоны (через запятую) браузеры для видео не используют.
        string spec = rangeHeader.Substring("bytes=".Length).Split(',')[0].Trim();
        int dash = spec.IndexOf('-');
        if (dash >= 0 && fileLength > 0)
        {
            string fromRaw = spec.Substring(0, dash).Trim();
            string toRaw = spec.Substring(dash + 1).Trim();

            long start, end;
            bool ok;
            if (fromRaw.Length == 0)
            {
                // "-500" - последние 500 байт
                ok = long.TryParse(toRaw, out long tail) && tail > 0;
                start = ok ? Math.Max(0, fileLength - tail) : 0;
                end = fileLength - 1;
            }
            else
            {
                ok = long.TryParse(fromRaw, out start) && start >= 0 && start < fileLength;
                end = fileLength - 1;
                if (ok && toRaw.Length > 0)
                {
                    ok = long.TryParse(toRaw, out end) && end >= start;
                    if (end > fileLength - 1) end = fileLength - 1;
                }
            }

            if (ok)
            {
                long count = end - start + 1;
                context.Response.StatusCode = (int)HttpStatusCode.PartialContent;   // 206
                context.Response.Headers.ContentRange = $"bytes {start}-{end}/{fileLength}";
                context.Response.ContentLength = count;
                await context.Response.SendFileAsync(fileInfo, start, count);
                return;
            }

            // Диапазон за пределами файла - по RFC отвечаем 416 и говорим реальный размер
            context.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
            context.Response.Headers.ContentRange = $"bytes */{fileLength}";
            return;
        }
    }

    context.Response.ContentLength = fileLength;
    await context.Response.SendFileAsync(fileInfo);
}

// Уведомление о прочтении: reader открыл чат с sender, поэтому сообщения sender'а
// этому reader'у считаются увиденными. Шлём sender'у SEEN▫#▫{reader} на все его
// открытые сессии - его клиент пометит те сообщения двумя галочками.
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
            // Сокет умер между проверкой и отправкой - пропускаем
        }
    }
}

// Создаёт файл с уникальным именем и возвращает открытый поток. Имя записывается
// в savedName.
//
// Ранее файл сохранялся под исходным именем, и повторный photo.jpg затирал прежний:
// у старого сообщения менялось изображение. Телефоны выдают снимкам одинаковые
// имена вида IMG_0001.jpg, поэтому файлы разных пользователей перемешивались.
// Уникальность требуется и клиентскому кэшу: он хранит файлы по имени, и повторное
// имя означало бы показ устаревшего изображения.
//
// Используется FileMode.CreateNew с повтором, а не проверка File.Exists перед
// созданием: два одновременных запроса успевают выбрать одно свободное имя в
// промежутке между проверкой и созданием.
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
            // Имя занято - пробуем следующее
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
    IFormFileCollection files;
    try
    {
        // Разбор multipart падает, если соединение оборвалось до конца тела
        // (телефон свернули, туннель разорвал долгую заливку видео). Раньше это
        // всплывало пятисоткой со стеком, и клиент показывал безликое «500».
        files = context.Request.Form.Files;
    }
    catch (Exception ex)
    {
        Logs.Save($"Загрузка не принята: {ex.Message}");
        Console.WriteLine($"[Upload] Тело запроса пришло не полностью: {ex.Message}");
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await context.Response.WriteAsync("Загрузка прервана - файл дошёл не полностью");
        return;
    }

    var uploadFiles = $"{Directory.GetCurrentDirectory()}/uploads";
    Directory.CreateDirectory(uploadFiles);

    string FName = string.Empty;

    foreach (var file in files)
    {
        // Имя подбирается свободное: одинаковые названия снимков больше не затирают
        // друг друга (клиенту возвращается то имя, под которым файл реально лёг)
        string savedName;
        string savedPath;
        long written;
        try
        {
            using (var fileStream = CreateUniqueFile(uploadFiles, file.FileName, out savedName))
            {
                savedPath = fileStream.Name;
                await file.CopyToAsync(fileStream);
                written = fileStream.Length;
            }
        }
        catch (Exception ex)
        {
            // Соединение оборвалось посреди заливки (частый случай на больших видео
            // через туннель или с телефона): раньше на диске навсегда оставался
            // огрызок, а причина нигде не фиксировалась.
            Logs.Save($"Загрузка файла {file.FileName} прервана: {ex.Message}");
            Console.WriteLine($"[Upload] Прервана загрузка {file.FileName}: {ex.Message}");
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("Загрузка прервана - файл дошёл не полностью");
            return;
        }

        // Клиент объявляет размер в multipart-заголовке. Если записали меньше - 
        // файл неполный (битое видео/фото), отдавать такой в чат нельзя.
        if (file.Length > 0 && written != file.Length)
        {
            try { File.Delete(savedPath); } catch { /* уже нет - не мешаем ответу */ }
            Logs.Save($"Файл {file.FileName} дошёл не полностью: {written} из {file.Length} байт");
            Console.WriteLine($"[Upload] {file.FileName}: получено {written} из {file.Length} байт - файл удалён");
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync($"Файл дошёл не полностью ({written} из {file.Length} байт)");
            return;
        }

        FName = savedName;
        Logs.Save($"Загружен файл {FName} ({written} байт)");
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

    // longLived: имя файла уникально и содержимое неизменно - пусть браузер
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
        // получали ОДИН файл на двоих - второй затирал аватарку первого
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
    // Раньше файл искали в Data/avatars, а загружали в avatars - аватарки никогда не находились.
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
    // (401 - неверные данные), а ТЕКСТ остался прежним - иначе сломались бы
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
    // здесь - на клиенте это лишь удобство, старый клиент проверку не сделает
    string? validationError = Tebegram.Shared.UserValidation.CheckRegistration(UserLogin, UserPassword, Username, Name);

    // Как и во входе: код ответа честный (400 - данные не годятся, 409 - занято),
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
        // GetNextUserId вместо UsersCount + 1 - иначе после удаления пользователей Id дублировались
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
// Ответ: id▫username▫name❂id▫username▫name❂…  Точное совпадение логина - первым.
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
        // Чат с собой (Избранное) - сохраняем один раз, иначе дублировалось
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
    // текст мог содержать ▫ - склеиваем хвост
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
                // Лёгкая копия без сообщений - история хранится только в «Все чаты»
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
        // Текст чистится и на сервере, хотя оба клиента уже вызывают SanitizeMessage
        // перед отправкой. Проверка на одной стороне защищает только от опечаток:
        // запрос в обход клиента доставит ❂ в историю, и одно сообщение при чтении
        // разделится на два, а хвост придёт мусором.
        string text = Tebegram.Shared.UserValidation.SanitizeMessage(string.Join('▫', messageData.Skip(5)));
        if (text == null) return null;
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
    // Платформа звонящего (?platform=win|web). Звонок пойдёт ТОЛЬКО на такую же
    // платформу собеседника: win звонит в win, веб - в веб. Причина простая - 
    // звук и сигнализация у платформ разные, а раньше токен был один на
    // пользователя, и вызов звонил сразу везде, где человек залогинен.
    string callerPlatform = Context.Request.Query["platform"].ToString();
    if (string.IsNullOrWhiteSpace(callerPlatform)) callerPlatform = "legacy";

    // Собеседник должен быть в сети НА ТОЙ ЖЕ платформе, иначе звонить некуда
    if (callerPlatform != "legacy" && !calledUser.IsOnlineOn(callerPlatform))
    {
        Context.Response.StatusCode = (int)HttpStatusCode.Conflict;
        await Context.Response.WriteAsync("Собеседник сейчас не в сети в этом приложении");
        return;
    }

    string token = VoiceRoomsController.CreateRoom(user.Username + calledUser.Username);

    user.CallToken = token;
    // Третьим полем - платформа, для которой предназначен вызов. Старые клиенты
    // читают только первые два поля, поэтому формат для них не изменился.
    calledUser.CallToken = $"{user.Username}▫{token}▫{callerPlatform}";

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
    else
    {
        response = user.CallToken;

        // Клиент сообщает свою платформу - отдаём вызов только «своей».
        // Без параметра (старый клиент) поведение прежнее: получает всё.
        string asking = Context.Request.Query["platform"].ToString();
        if (!string.IsNullOrWhiteSpace(asking))
        {
            string[] parts = response.Split('▫');
            // parts: [звонящий, токен, платформа]. Платформы нет - вызов от старого
            // клиента, его показываем всем, иначе он вообще никому не дозвонится.
            if (parts.Length >= 3 && parts[2] != "legacy" && parts[2] != asking)
                response = "NotFound";
        }
    }

    await Context.Response.WriteAsync(response);
});

// Сегмент принимаем ЦЕЛИКОМ и делим по первому дефису вручную.
// Причина: TokenGenerator отдаёт URL-safe Base64 ('+'→'-', '/'→'_'), то есть сам
// токен почти всегда содержит дефисы. На маршруте "{userId:int}-{token}" такой
// запрос не совпадал и возвращал 404 - сервер не чистил CallToken и не рассылал
// "CloseConnection", из-за чего у второй стороны звонок не завершался.
// Форма URL осталась прежней, менять клиенты не нужно.
app.MapGet("/Voice/DeclineCall/{data}", async (HttpContext Context, string data) =>
{
    int sep = data.IndexOf('-');
    if (sep <= 0 || !int.TryParse(data.Substring(0, sep), out int userId))
    {
        Context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await Context.Response.WriteAsync("Некорректный запрос");
        return;
    }
    string token = data.Substring(sep + 1);

    User? user = UsersData.FindUserById(userId);

    if (user != null) user.CallToken = "";

    // Токен зависал у второй стороны звонка - чистим у всех участников
    UsersData.ClearCallTokens(token);

    // Комната могла уже быть удалена - раньше тут падал KeyNotFoundException
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

    // Как только в комнате стало двое - разговор реально состоялся. До этого
    // клиент показывает «Соединяем…», а по этому событию запускает отсчёт времени
    // (раньше таймер стартовал сразу после подключения СВОЕГО сокета, то есть ещё
    // до того, как собеседник взял трубку).
    if (VoiceRoomsController.VoiceRooms.TryGetValue(Token, out var joinedRoom)
        && joinedRoom.RoomMembers.Count >= 2)
    {
        await joinedRoom.SendTextToRoom("CallConnected");
    }

    try
    {
        await ReceiveMessage(ws,
            async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Пересылаем ровно столько байт, сколько пришло - раньше уходил весь буфер 4096 с мусором в хвосте
                    if (VoiceRoomsController.VoiceRooms.TryGetValue(Token, out var room))
                    {
                        byte[] voice = new byte[result.Count];
                        Array.Copy(buffer, voice, result.Count);
                        await room.SendVoiceToRoom(ws, voice);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Состояние микрофона участника: MIC:1 - включен, MIC:0 - выключен.
                    // Пересылаем ОСТАЛЬНЫМ, чтобы у них значок рядом с аватаром этого
                    // человека показывал именно ЕГО микрофон.
                    //
                    // Ретранслируем только префикс MIC: - иначе клиент мог бы прислать
                    // служебное «CloseConnection» и повесить трубку всей комнате.
                    string voiceText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (voiceText.StartsWith("MIC:")
                        && VoiceRoomsController.VoiceRooms.TryGetValue(Token, out var micRoom))
                    {
                        await micRoom.SendTextToRoomExcept(ws, voiceText);
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
        // Клиент мог закрыться аварийно (без Close-фрейма) - убираем его из комнаты в любом случае,
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
            // Аварийный разрыв соединения (клиент убит/потерял сеть) - выходим, cleanup сделает вызывающий код
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

// Создание чата. Перенесено из ветки main-dev, коммит 64bc1ab, с исправлениями.
//
// {usernames} - логины через разделитель ▫. Двое участников образуют личный чат,
// трое и более - группу, владельцем которой становится создатель.
//
// Название группы передаётся параметром запроса ?name, а не в составе пути:
// в пути разделителем служит дефис, поэтому название с дефисом сдвинуло бы разбор.
// По этой же причине испорченные учётные записи возникали при регистрации.
//
// Исправления относительно исходной версии:
// FindUserByUsername может вернуть null. Ранее null попадал в список участников и
// вызывал NullReferenceException внутри CreateChat, теперь возвращается ошибка 400.
// При заполненных полях Name и Avatar ответ содержал пустые строки: переменные
// инициализировались пустыми и заполнялись только для незаданных полей чата.
// В чате с самим собой после удаления повторов остаётся один участник, и обращение
// к members[1] вызывало ArgumentOutOfRangeException.
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
    // чат с собой («Избранное») - как себя
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
// до конца), и дописать туда чаты - значит сломать их. Новый клиент просто
// делает ещё один запрос, старый про него не знает.
// Формат: чаты через ❂, поля чата - id&имя&группа?&аватар&владелец&участники.
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
// (без конверта - конверт нужен только чтобы отличить чат в живом потоке).
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
    // Платформа нужна для маршрутизации звонков (см. User.IsOnlineOn).
    // Параметра нет - старый клиент, такая сессия принимает звонки с любой платформы.
    user.AddSession(ws, context.Request.Query["platform"].ToString());

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
                        // В v2 клиент шлёт реальный chatId - поле username останется
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

                        // DELETEChat▫#▫{ChatId} - удаление чата (пока только группы,
                        // и только владельцем). int.TryParse + await: битый id больше
                        // не роняет соединение, а исключение из DeleteChat не теряется.
                        case "DELETECHAT":
                            if (data.Length >= 2 && int.TryParse(data[1], out int delChatId))
                                await ChatsController.DeleteChat(delChatId, user);
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
        // Убираем сессию всегда - даже при аварийном разрыве.
        // Раньше мёртвые сокеты копились в ChatsSessions навсегда.
        user.RemoveSession(ws);
    }
});

#endregion

app.Run();
