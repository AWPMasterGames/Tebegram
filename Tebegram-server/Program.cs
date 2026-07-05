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

// Настройка порта
builder.WebHost.UseUrls("https://localhost:5000");

// CORS — нужен веб-клиенту (GitHub Pages / PWA), десктопному клиенту не мешает
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseCors();

app.UseWebSockets();

// ВАЖНО: Инициализируем данные пользователей ПЕРЕД запуском основной логики
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Запуск сервера TebegramServer...");
UsersData.Initialize(); // Принудительно инициализируем данные
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
    app.UseStaticFiles(new StaticFileOptions { FileProvider = webFileProvider, RequestPath = "/app" });
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Веб-клиент доступен по /app (папка: {webRoot})");
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
        // Path.GetFileName отрезает возможные пути в имени файла (защита от ../)
        FName = Path.GetFileName(file.FileName).Replace(" ", "_");
        string filePath = $"{uploadFiles}/{FName}";

        using var fileStream = new FileStream(filePath, FileMode.Create);
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

    context.Response.Headers.ContentDisposition = $"attachment; filename={safeName}";
    await context.Response.SendFileAsync(fileInfo);
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
        FName = Path.GetFileName(file.FileName).Replace(" ", "_");
        string filePath = $"{uploadFiles}/{FName}";

        using var fileStream = new FileStream(filePath, FileMode.Create);
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

    context.Response.Headers.ContentDisposition = $"attachment; filename={safeName}";
    await context.Response.SendFileAsync(fileInfo);
});

app.MapGet("/login/{UserLogin}-{UserPassword}", async (HttpContext Context, string UserLogin, string UserPassword) =>
{
    if (!UsersData.IsExistUser(UserLogin))
    {
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
            await Context.Response.WriteAsync("Ошибка при поиске пользователя");
        }
    }
    else await Context.Response.WriteAsync("Неверный пароль");
});

app.MapGet("/register/{UserLogin}-{UserPassword}-{Username}-{Name}", async (HttpContext Context, string UserLogin, string UserPassword, string Username, string Name) =>
{
    if (string.IsNullOrWhiteSpace(UserLogin) || string.IsNullOrWhiteSpace(UserPassword) ||
        string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Name))
    {
        await Context.Response.WriteAsync("Все поля должны быть заполнены");
    }
    else if (UsersData.IsExistUser(UserLogin))
    {
        await Context.Response.WriteAsync("Пользователь с таким логином уже существует");
    }
    else if (UsersData.FindUserByUsername(Username) != null)
    {
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
                        case "SEND":
                            if (data.Length < 4 || !int.TryParse(data[1], out int requestedChatId)) break;
                            int chatId = ChatsController.CheckIsExist(requestedChatId, user, data[2]);
                            if (chatId != -1) await ChatsController.SendMessage(chatId, data[3]);
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
