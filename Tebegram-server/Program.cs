using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.WebSockets;
using TebegramServer;
using TebegramServer.Classes;
using TebegramServer.Controllers;
using TebegramServer.Data;
using TebegramServer.Data.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("https://localhost:5000");

// Singleton-фабрика — нужна для статического UsersData.Initialize()
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite("Data Source=tebegram.db"), ServiceLifetime.Singleton);

// Scoped-контекст — инъектируется напрямую в эндпоинты (AppDbContext db)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=tebegram.db"));

var app = builder.Build();

// Применяем миграции при старте — создаёт tebegram.db если его нет
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Загружаем пользователей из БД в память
var dbFactory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
UsersData.Initialize(dbFactory);

app.UseWebSockets();

Thread thread = new Thread(() => {
    Console.WriteLine("Запущен поток чистки голосых каналов.");
    VoiceRoomsController.CheckEmptyVoices();
});

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Сервер TebegramServer запущен.");
thread.Start();

app.MapGet("/", async (HttpContext context) =>
{
    await context.Response.WriteAsync("HI!");
});

app.MapGet("/Test", async (HttpContext context) =>
{
    await context.Response.WriteAsync("HI!");
});

app.MapGet("/download", async (HttpContext context) =>
{
    var fileProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
    var fieInfo = fileProvider.GetFileInfo($"uploads/1.zip");
    context.Response.Headers.ContentEncoding = "Unicode";
    context.Response.Headers.ContentDisposition = $"attachment; filename=1.zip";
    await context.Response.SendFileAsync(fieInfo);
});

app.MapPost("/upload", async (HttpContext context) =>
{
    IFormFileCollection files = context.Request.Form.Files;
    var uploadFiles = $"{Directory.GetCurrentDirectory()}/uploads";
    Directory.CreateDirectory(uploadFiles);

    string FName = string.Empty;
    foreach (var file in files)
    {
        FName = file.FileName.Replace(" ", "_");
        string filePath = $"{uploadFiles}/{FName}";
        using var fileStream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(fileStream);
        Logs.Save($"Загружен файл {FName}");
    }

    await context.Response.WriteAsync(FName);
});

app.MapGet("/upload/{FileName}", async (HttpContext context, string FileName) =>
{
    if (FileName.Contains("..") || FileName.Contains('/') || FileName.Contains('\\'))
    {
        context.Response.StatusCode = 400;
        return;
    }

    var fileProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
    var fieInfo = fileProvider.GetFileInfo($"uploads/{FileName}");

    string ext = Path.GetExtension(FileName).TrimStart('.').ToLower();
    string mime = ext switch
    {
        "jpg" or "jpeg" => "image/jpeg",
        "png" => "image/png",
        "gif" => "image/gif",
        "webp" => "image/webp",
        "bmp" => "image/bmp",
        _ => "application/octet-stream"
    };
    context.Response.ContentType = mime;
    context.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(FileName)}";
    await context.Response.SendFileAsync(fieInfo);
});

app.MapPost("/avatars/{UserId}", async (HttpContext context, int UserId, AppDbContext db) =>
{
    IFormFileCollection files = context.Request.Form.Files;
    var uploadFiles = $"{Directory.GetCurrentDirectory()}/avatars";
    Directory.CreateDirectory(uploadFiles);

    string FName = string.Empty;
    foreach (var file in files)
    {
        FName = file.FileName.Replace(" ", "_");
        string filePath = $"{uploadFiles}/{FName}";
        using var fileStream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(fileStream);
        Logs.Save($"Загружен аватар {FName}");
    }

    UsersData.UpdateAvatar(UserId, FName, db);
    await context.Response.WriteAsync(FName);
});

app.MapGet("/avatarsFileName/{UserId}", async (HttpContext context, int UserId) =>
{
    await context.Response.WriteAsync(UsersData.FindUserById(UserId)?.Avatar ?? "");
});

app.MapGet("/avatars/{FileName}", async (HttpContext context, string FileName) =>
{
    if (FileName.Contains("..") || FileName.Contains('/') || FileName.Contains('\\'))
    {
        context.Response.StatusCode = 400;
        return;
    }

    var fileProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
    var fieInfo = fileProvider.GetFileInfo($"avatars/{FileName}");

    string ext = Path.GetExtension(FileName).TrimStart('.').ToLower();
    string mime = ext switch
    {
        "jpg" or "jpeg" => "image/jpeg",
        "png" => "image/png",
        "gif" => "image/gif",
        "webp" => "image/webp",
        "bmp" => "image/bmp",
        _ => "application/octet-stream"
    };
    context.Response.ContentType = mime;
    context.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(FileName)}";
    await context.Response.SendFileAsync(fieInfo);
});

app.MapGet("/login/{UserLogin}-{UserPassword}", async (HttpContext Context, string UserLogin, string UserPassword) =>
{
    try
    {
        if (!UsersData.IsExistUser(UserLogin))
        {
            await Context.Response.WriteAsync("Пользователь с таким логином не существует");
            return;
        }

        if (UsersData.Authorize(UserLogin, UserPassword) == null)
        {
            await Context.Response.WriteAsync("Неверный пароль");
            return;
        }

        var user = UsersData.FindUserByLogin(UserLogin);
        if (user == null)
        {
            await Context.Response.WriteAsync("Ошибка при поиске пользователя");
            return;
        }

        await Context.Response.WriteAsync(user.ToClientSend());
        Logs.Save($"Пользователь {UserLogin} авторизировался");
    }
    catch (Exception ex)
    {
        Logs.Save($"[Login] Исключение для {UserLogin}: {ex.GetType().Name}: {ex.Message}");
        Context.Response.StatusCode = 500;
        await Context.Response.WriteAsync($"Ошибка сервера: {ex.Message}");
    }
});

app.MapGet("/register/{UserLogin}-{UserPassword}-{Username}-{Name}", async (HttpContext Context, AppDbContext db,
    string UserLogin, string UserPassword, string Username, string Name) =>
{
    if (UsersData.IsExistUser(UserLogin))
    {
        await Context.Response.WriteAsync("Пользователь с таким логином уже существует");
        return;
    }

    int newId = UsersData.UsersCount + 1;
    var entity = new UserEntity
    {
        Id = newId,
        Login = UserLogin,
        Password = UserPassword,
        Name = Name,
        Username = Username,
        Avatar = ""
    };
    db.Users.Add(entity);
    await db.SaveChangesAsync();

    var newUser = new User(newId, UserLogin, UserPassword, Name, Username,
        new ObservableCollection<ChatFolder> {
            new ChatFolder("Все чаты", new ObservableCollection<Contact>(), "", false)
        }, "");

    UsersData.AddToMemory(newUser);
    await Context.Response.WriteAsync(newUser.ToClientSend());
    Logs.Save($"Пользователь {UserLogin} зарегистрировался");
});

app.MapGet("/UserName/{username}", async (HttpContext Context, string username) =>
{
    User? user = UsersData.FindUserByUsername(username);
    if (user == null) { Context.Response.StatusCode = 404; return; }
    await Context.Response.WriteAsync($"{user.Id}▫{user.Name}");
});

// Все сообщения пользователя — читаем из БД
app.MapGet("/messages/{id}", async (HttpContext Context, int id, AppDbContext db) =>
{
    User? user = UsersData.FindUserById(id);
    if (user == null) { Context.Response.StatusCode = 404; return; }

    user.NewMessages.Clear();

    var messages = await db.Messages
        .Where(m => m.FromUsername == user.Username || m.ToUsername == user.Username)
        .OrderBy(m => m.SentAt)
        .ToListAsync();

    string result = string.Join("❂", messages.Select(m =>
        $"{m.FromUsername}▫{m.ToUsername}▫{m.Type}▫{m.SentAt.ToLocalTime():dd.MM.yyyy HH:mm}▫{m.ServerAddress ?? ""}▫{m.Content ?? ""}"));

    if (result.Length > 0) result += "❂";

    await Context.Response.WriteAsync(result);
});

// Новые сообщения с момента последнего запроса — только in-memory очередь
app.MapGet("/NewMessages/{id}", async (HttpContext Context, int id) =>
{
    User? user = UsersData.FindUserById(id);
    if (user == null) { Context.Response.StatusCode = 404; return; }
    await Context.Response.WriteAsync(user.GetNewMessages());
});

// Отправка сообщения — сохраняем в БД и уведомляем получателя через in-memory
app.MapPost("/messages", async (HttpContext Context, AppDbContext db) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string request = await reader.ReadToEndAsync();
    string[] parts = request.Split('▫');

    Message? message = null;
    if (parts[2] == "Text")
    {
        string text = parts[5];
        for (int i = 6; i < parts.Length; i++) text += parts[i];
        message = new Message(parts[0], parts[1], text, parts[3]);
    }
    else if (parts[2] == "File" || parts[2] == "Image")
    {
        var type = parts[2] == "Image" ? MessageType.Image : MessageType.File;
        message = new Message(parts[0], parts[1], parts[5], parts[3], type, parts[4]);
    }

    if (message == null) { Context.Response.StatusCode = 400; return; }

    // Сохраняем в БД
    await db.Messages.AddAsync(new MessageEntity
    {
        FromUsername = message.Sender,
        ToUsername = message.Reciver,
        Type = message.MessageType.ToString(),
        Content = message.Text,
        ServerAddress = message.ServerAdress,
        SentAt = DateTime.UtcNow
    });

    // Автоматически добавляем друг друга в контакты при первом сообщении
    User? receiverUser = UsersData.FindUserByUsername(message.Reciver);
    User? senderUser = UsersData.FindUserByUsername(message.Sender);

    if (senderUser != null && senderUser.FindContactByUsername(message.Reciver) == null && receiverUser != null)
        UsersData.AddContact(senderUser.Id, new Contact(receiverUser.Id, receiverUser.Username, receiverUser.Name), db);

    if (receiverUser != null && receiverUser.FindContactByUsername(message.Sender) == null && senderUser != null)
        UsersData.AddContact(receiverUser.Id, new Contact(senderUser.Id, senderUser.Username, senderUser.Name), db);

    await db.SaveChangesAsync();

    // Уведомляем через in-memory очередь
    receiverUser?.NewMessages.Add(message);
    senderUser?.NewMessages.Add(message);

    Context.Response.StatusCode = 200;
});

app.MapPost("/Contact", async (HttpContext Context, AppDbContext db) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string request = await reader.ReadToEndAsync();
    string[] data = request.Split('▫');

    User? uContact = UsersData.FindUserByUsername(data[1]);
    if (uContact == null)
    {
        Context.Response.StatusCode = 404;
        await Context.Response.WriteAsync("Пользователь не найден");
        return;
    }

    string displayName = data[2].Trim().Length < 1 ? uContact.Name : data[2];
    var contact = new Contact(uContact.Id, uContact.Username, displayName);

    UsersData.AddContact(int.Parse(data[0]), contact, db);

    Context.Response.StatusCode = 200;
    await Context.Response.WriteAsync(contact.ToString());
});

app.MapPut("/Contact", async (HttpContext Context, AppDbContext db) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string request = await reader.ReadToEndAsync();
    string[] data = request.Split('▫');
    UsersData.UpdateContactName(int.Parse(data[0]), data[1], data[2], db);
    Context.Response.StatusCode = 200;
});

app.MapDelete("/Contact", async (HttpContext Context, AppDbContext db) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string request = await reader.ReadToEndAsync();
    string[] data = request.Split('▫');
    UsersData.RemoveContact(int.Parse(data[0]), data[1], db);
    Context.Response.StatusCode = 200;
});


#region Voices

app.MapGet("/Voice/CreateRoom/{userId}-{calledUserUsername}", async (HttpContext Context, int userId, string calledUserUsername) =>
{
    User? user = UsersData.FindUserById(userId);
    User? calledUser = UsersData.FindUserByUsername(calledUserUsername);
    string token = VoiceRoomsController.CreateRoom(user!.Username + calledUser!.Username);

    user.CallToken = token;
    calledUser.CallToken = $"{user.Username}▫{token}";

    await Context.Response.WriteAsync(token);
});

app.MapGet("/Voice/GetCallToken/{userId}", async (HttpContext Context, int userId) =>
{
    User? user = UsersData.FindUserById(userId);
    string response = string.IsNullOrEmpty(user?.CallToken) ? "NotFound" : user.CallToken;
    await Context.Response.WriteAsync(response);
});

app.MapGet("/Voice/DeclineCall/{userId}-{token}", async (HttpContext Context, int userId, string token) =>
{
    User? user = UsersData.FindUserById(userId);
    if (user != null) user.CallToken = "";

    VoiceRoomsController.VoiceRooms[token].SendTextToRoom("CloseConnection");
    await Context.Response.WriteAsync("ok");
});

app.Map("/Voice/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var userID = context.Request.Query["userId"];
        var Token = context.Request.Query["roomToken"];

        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        User? user = UsersData.FindUserById(int.Parse(userID!));

        VoiceRoomsController.ConnectingToRoom(ws, Token!, user!);
        if (VoiceRoomsController.GetRoomId(Token!) == -1) return;
        Console.WriteLine($"Пользователь {user!.Username} подключился к комнате Id: {VoiceRoomsController.GetRoomId(Token!)}");

        await ReceiveMessage(ws, async (result, buffer) =>
        {
            if (result.MessageType == WebSocketMessageType.Binary)
                VoiceRoomsController.VoiceRooms[Token!].SendVoiceToRoom(ws, buffer);
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                string text = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                VoiceRoomsController.VoiceRooms[Token!].SendTextToOthers(ws, text);
                Console.WriteLine($"[{user.Username}] -> {text}");
            }
            else if (result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted)
            {
                Console.WriteLine($"Пользователь {user.Username} отключился от комнаты Id: {VoiceRoomsController.GetRoomId(Token!)}");
                await VoiceRoomsController.DisconnectFromRoom(ws, Token!, result.CloseStatus!.Value, result.CloseStatusDescription, CancellationToken.None);
            }
        });
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

async Task ReceiveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
{
    var buffer = new byte[4096];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        handleMessage(result, buffer);
    }
}

#endregion

app.Run();
