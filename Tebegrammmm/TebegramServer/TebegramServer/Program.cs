using Microsoft.Extensions.FileProviders;
using TebegramServer.Data;
using TebegramServer.Classes;
using System.Collections.ObjectModel;
using System.Net;
using TebegramServer;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

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
        await Context.Response.WriteAsync(UsersData.FindUser(UserLogin).ToClientSend());
        Logs.Save($"Пользователь {UserLogin} авторизировался");
    }
    else await Context.Response.WriteAsync("Неверный пароль");
});

app.MapGet("/register/{UserLogin}-{UserPassword}", async (HttpContext Context, string UserLogin, string UserPassword) =>
{
    if (UsersData.IsExistUser(UserLogin))
    {
        await Context.Response.WriteAsync("Пользователь с таким логином уже существует");
    }
    else if (!UsersData.IsExistUser(UserLogin))
    {
        User NewUser = new User(1, UserLogin, UserPassword, UserLogin, "127.0.0.1", 4004,
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

app.Run();
