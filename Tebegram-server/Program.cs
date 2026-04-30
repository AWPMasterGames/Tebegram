using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.ObjectModel;
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

var app = builder.Build();

/*var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};*/

app.UseWebSockets();

var connections = new List<WebSocket>();

Thread thread = new Thread(() => {
    Console.WriteLine("Запущен поток чистки голосых каналов.");
    VoiceRoomsController.CheckEmptyVoices();
});

// ВАЖНО: Инициализируем данные пользователей ПЕРЕД запуском основной логики
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Запуск сервера TebegramServer...");
UsersData.Initialize(); // Принудительно инициализируем данные
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Данные пользователей загружены, запускаем веб-сервер...");

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

app.MapPost("/avatars/{UserId}", async (HttpContext context, int UserId) =>
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
        Logs.Save($"Загружен файл {FName}");
        UsersData.FindUserById(UserId).Avatar = FName;
    }
    UsersData.SaveUserToFile();
    await context.Response.WriteAsync(FName);

});

app.MapGet("/avatarsFileName/{UserId}", async (HttpContext context, int UserId) =>
{
    await context.Response.WriteAsync(UsersData.FindUserById(UserId).Avatar);
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

        string payload = user.ToClientSend();
        if (string.IsNullOrEmpty(payload))
        {
            Context.Response.StatusCode = 500;
            await Context.Response.WriteAsync("Ошибка: ToClientSend вернул пустую строку");
            return;
        }

        await Context.Response.WriteAsync(payload);
        Logs.Save($"Пользователь {UserLogin} авторизировался");
    }
    catch (Exception ex)
    {
        Logs.Save($"[Login] Исключение для {UserLogin}: {ex.GetType().Name}: {ex.Message}");
        Context.Response.StatusCode = 500;
        await Context.Response.WriteAsync($"Ошибка сервера: {ex.Message}");
    }
});

app.MapGet("/register/{UserLogin}-{UserPassword}-{Username}-{Name}", async (HttpContext Context, string UserLogin, string UserPassword, string Username, string Name) =>
{
    if (UsersData.IsExistUser(UserLogin))
    {
        await Context.Response.WriteAsync("Пользователь с таким логином уже существует");
    }
    else if (!UsersData.IsExistUser(UserLogin))
    {
        User NewUser = new User(UsersData.UsersCount + 1, UserLogin, UserPassword, Name, Username,
                new ObservableCollection<ChatFolder> {
                new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                        }, "💬", false)
                }, "");
        UsersData.AddUser(NewUser);
        UsersData.SaveUserToFile();
        await Context.Response.WriteAsync(NewUser.ToClientSend());
        Logs.Save($"Пользователь {UserLogin} зарегрестрировался");
    }
});

app.MapGet("/UserName/{username}", async (HttpContext Context, string username) =>
{
    User user = UsersData.FindUserByUsername(username);

    await Context.Response.WriteAsync($"{user.Id}▫{user.Name}");
});

app.MapGet("/messages/{id}", async (HttpContext Context, int id) =>
{
    User user = UsersData.FindUserById(id);

    ChatFolder Folder = user.ChatsFolders[0];

    user.NewMessages.Clear();
    string messages = string.Empty;
    for (int i = 0; i < Folder.Contacts.Count; i++)
    {
        messages += $"{Folder.Contacts[i].GetAllMeseges()}";
    }

    await Context.Response.WriteAsync(messages);
});
app.MapGet("/NewMessages/{id}", async (HttpContext Context, int id) =>
{
    User user = UsersData.FindUserById(id);

    await Context.Response.WriteAsync(user.GetNewMessages());
});
app.MapPost("/messages", async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();
    string[] messageData = Request.Split('▫');
    Message message = null;
    if (messageData[2] == "Text")
    {
        string text = messageData[5];
        for (int i = 6; i < messageData.Length; i++)
        {
            text += messageData[i];
        }
        message = new Message(messageData[0], messageData[1], text, messageData[3]);
    }
    else if (messageData[2] == "File")
    {
        message = new Message(messageData[0], messageData[1], messageData[5], messageData[3], MessageType.File, messageData[4]);
    }
    User ReciverUser = UsersData.FindUserByUsername(message.Reciver);
    User SenderUser = UsersData.FindUserByUsername(message.Sender);

    ReciverUser?.AddMessage(message);
    ReciverUser?.NewMessages.Add(message);
    SenderUser?.AddMessage(message);
    SenderUser?.NewMessages.Add(message);
    UsersData.SaveUserToFile();

    return Context.Response.StatusCode = 200;
});

app.MapPost("/Contact", async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();
    string[] Data = Request.Split('▫');
    User UContact = UsersData.FindUserByUsername(Data[1]);
    if (UContact == null)
    {
        Context.Response.StatusCode = 404;
        await Context.Response.WriteAsync("Пользователь не найден");
        return;
    }
    Contact contact;
    if (Data[2].Trim().Length < 1) contact = new Contact(UContact.Id, UContact.Username, UContact.Name);
    else contact = new Contact(UContact.Id, UContact.Username, Data[2]);
    UsersData.FindUserById(int.Parse(Data[0]))?.AddContact(contact);
    UsersData.SaveUserToFile();
    Context.Response.StatusCode = 200;
    await Context.Response.WriteAsync(contact.ToString());
});
app.MapPut("/Contact", async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();
    string[] Data = Request.Split('▫');
    UsersData.FindUserById(int.Parse(Data[0]))?.FindContactByUsername(Data[1]).ChangeName(Data[2]);
    UsersData.SaveUserToFile();
    return Context.Response.StatusCode = 200;
});
app.MapDelete("/Contact", async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();
    string[] Data = Request.Split('▫');
    User user = UsersData.FindUserById(int.Parse(Data[0]));
    user.RemoveContact(user.FindContactByUsername(Data[1]));
    UsersData.SaveUserToFile();
    return Context.Response.StatusCode = 200;
});







#region Voices
// Голосовые каналы

app.MapGet("/Voice/CreateRoom/{userId}-{calledUserUsername}", async (HttpContext Context, int userId, string calledUserUsername) =>
{
    User user = UsersData.FindUserById(userId);
    User calledUser = UsersData.FindUserByUsername(calledUserUsername);
    string token = VoiceRoomsController.CreateRoom(user.Username + calledUser.Username);

    user.CallToken = token;
    calledUser.CallToken =$"{user.Username}▫{token}";

    await Context.Response.WriteAsync(token);
});

app.MapGet("/Voice/GetCallToken/{userId}", async (HttpContext Context, int userId) =>
{
    User user = UsersData.FindUserById(userId);

    string response;

    if (string.IsNullOrEmpty(user.CallToken))
    {
        response = "NotFound";
    }
    else {
        response = user.CallToken;
    }

    await Context.Response.WriteAsync(response);
});

app.MapGet("/Voice/DeclineCall/{userId}-{token}", async (HttpContext Context, int userId, string token) =>
{
    User user = UsersData.FindUserById(userId);

    user.CallToken = "";

    VoiceRoomsController.VoiceRooms[token].SendTextToRoom("CloseConnection");

    string response;

    if (string.IsNullOrEmpty(user.CallToken))
    {
        response = "NotFound";
    }
    else
    {
        response = "ok";
    }

    await Context.Response.WriteAsync(response);
});

app.Map("/Voice/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var userID = context.Request.Query["userId"];
        var Token = context.Request.Query["roomToken"];

        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        User user = UsersData.FindUserById(int.Parse(userID));

        VoiceRoomsController.ConnectingToRoom(ws, Token, user);
        if (VoiceRoomsController.GetRoomId(Token) == -1) return;
        Console.WriteLine($"Пользователь {user.Username} Подключился к комнате Id: {VoiceRoomsController.GetRoomId(Token)}");

        await ReceiveMessage(ws,
            async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    VoiceRoomsController.VoiceRooms[Token].SendVoiceToRoom(ws, buffer);
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    string text = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    VoiceRoomsController.VoiceRooms[Token].SendTextToOthers(ws, text);
                    Console.WriteLine($"[{user.Username}] -> {text}");
                }
                else if (result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted)
                {
                    Console.WriteLine($"Пользователь {user.Username} отключился от комнаты Id: {VoiceRoomsController.GetRoomId(Token)}");
                    await VoiceRoomsController.DisconnectFromRoom(ws, Token, result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
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
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer),
            CancellationToken.None);
        handleMessage(result, buffer);
    }
}

/*async Task Broadcast(string message)
{
    var bytes = Encoding.UTF8.GetBytes(message);
    foreach (var soket in connections)
    {
        if (soket.State == WebSocketState.Open)
        {
            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
            await soket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}*/
#endregion

app.Run();
