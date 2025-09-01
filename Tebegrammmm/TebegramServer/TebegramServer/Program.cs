using Microsoft.Extensions.FileProviders;
using TebegramServer.Data;
using TebegramServer.Classes;
using System.Collections.ObjectModel;
using TebegramServer;

var builder = WebApplication.CreateBuilder(args);

// Настройка порта
builder.WebHost.UseUrls("https://localhost:5000");

var app = builder.Build();

// ВАЖНО: Инициализируем данные пользователей ПЕРЕД запуском основной логики
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Запуск сервера TebegramServer...");
UsersData.Initialize(); // Принудительно инициализируем данные
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Данные пользователей загружены, запускаем веб-сервер...");

app.MapGet("/", async (HttpContext context) =>
{
    await context.Response.WriteAsync("HI!");
});

app.MapPost("/upload", async (HttpContext context) =>
{
    IFormFileCollection files = context.Request.Form.Files;
    var uploadFiles = $"{Directory.GetCurrentDirectory()}/uploads";
    Directory.CreateDirectory(uploadFiles);

    foreach (var file in files)
    {
        string filePath = $"{uploadFiles}/{file.FileName}";

        using var fileStream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(fileStream);
        Logs.Save($"Загружен файл {file.FileName}");
    }

    await context.Response.WriteAsync("файл успешно отправлен");
    
});

app.MapGet("/upload/{FileName}", async (HttpContext context, string FileName) =>
{
    var fileProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
    var fieInfo = fileProvider.GetFileInfo($"uploads/{FileName}");

    context.Response.Headers.ContentEncoding = "Unicode";
    context.Response.Headers.ContentDisposition = $"attachment; filename={FileName}";
    await context.Response.SendFileAsync(fieInfo);
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

app.MapGet("/register/{UserLogin}-{Username}-{UserPassword}", async (HttpContext Context, string UserLogin, string Username, string UserPassword) =>
{
    if (UsersData.IsExistUser(UserLogin))
    {
        await Context.Response.WriteAsync("Пользователь с таким логином уже существует");
    }
    else if (!UsersData.IsExistUser(UserLogin))
    {
        User NewUser = new User(UsersData.UsersCount+1, UserLogin, UserPassword, UserLogin, Username,
                new ObservableCollection<ChatFolder> {
                new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                        }, "💬", false)
                });
        UsersData.AddUser(NewUser);
        await Context.Response.WriteAsync(NewUser.ToClientSend());
        Logs.Save($"Пользователь {UserLogin} зарегрестрировался");
    }
});

app.MapGet("/UserName/{username}", async (HttpContext Context, string username) =>
{
    User user = UsersData.FindUserByUsername(username);

    await Context.Response.WriteAsync(user.Name);
});

app.MapGet("/messages/{id}", async (HttpContext Context,int id) =>
{
    User user = UsersData.FindUserById(id);

    ChatFolder Folder = user.ChatsFolders[0];

    user.NewMessages.Clear();
    string Messegas = string.Empty;
    for (int i = 0; i < Folder.Contacts.Count; i++)
    {
        Messegas += $"{Folder.Contacts[i].GetAllMeseges()}";
    }

    await Context.Response.WriteAsync(Messegas);
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

    return Context.Response.StatusCode = 200;
});

app.MapPost("/Contact",async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();
    string[] Data = Request.Split('▫');
    User UContact = UsersData.FindUserByUsername(Data[1]);
    if (UContact == null)
    {
        return Context.Response.StatusCode = 404;
    }
    Contact contact;
    if (Data[2].Trim().Length < 1) contact = new Contact(UContact.Username, UContact.Name);
    else contact = new Contact(UContact.Username, Data[2]);
    UsersData.FindUserById(int.Parse(Data[0]))?.AddContact(contact);
    return Context.Response.StatusCode = 200;
});
app.MapPut("/Contact", async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();
    string[] Data = Request.Split('▫');
    UsersData.FindUserById(int.Parse(Data[0]))?.FindContactByUsername(Data[1]).ChangeName(Data[2]);
    return Context.Response.StatusCode = 200;
});
app.MapDelete("/Contact", async (HttpContext Context) =>
{
    using StreamReader reader = new StreamReader(Context.Request.Body);
    string Request = await reader.ReadToEndAsync();
    string[] Data = Request.Split('▫');
    User user = UsersData.FindUserById(int.Parse(Data[0]));
    user.RemoveContact(user.FindContactByUsername(Data[1]));
    return Context.Response.StatusCode = 200;
});
app.Run();
