using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.FileProviders;
using TebegramServer.Data;
using TebegramServer.Classes;
using System.Text;

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
        await Context.Response.WriteAsync("Succes");
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
        UsersData.AddUser(new User(UserLogin, UserPassword));
        await Context.Response.WriteAsync("Succes");
    }
});

app.Run();
