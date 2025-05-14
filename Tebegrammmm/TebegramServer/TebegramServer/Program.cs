using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.FileProviders;
using TebegramServer.Data;
using TebegramServer.Classes;

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

    context.Response.Headers.ContentDisposition = $"attachment; filename={FileName}";
    await context.Response.SendFileAsync(fieInfo);
});

app.MapGet("/login/{UserLogin}-{UserPassword}", async(HttpContext Context, string UserLogin, string UserPassword) =>
{
    if(UsersData.Authorize(UserLogin, UserPassword) != null)
    {
        await Context.Response.WriteAsync("Succes");
    }
});

app.Run();
