using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.FileProviders;
using TebegramServer.Data;
using TebegramServer.Classes;
<<<<<<< Updated upstream
using System.Text;

var builder = WebApplication.CreateBuilder(args);
=======
using System.Collections.ObjectModel;
using System.Net;
using TebegramServer;
using Microsoft.AspNetCore.Http;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// загруска данных поьзователей из файла
void LoadUsersData()
{

}

// Настройка порта
builder.WebHost.UseUrls("http://localhost:5000");

>>>>>>> Stashed changes
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

    await context.Response.WriteAsync("���� ������� ���������");
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
        await Context.Response.WriteAsync("������������ � ����� ������� �� ����������");
    }
    else if (UsersData.Authorize(UserLogin, UserPassword) != null)
    {
        await Context.Response.WriteAsync("Succes");
    }
    else await Context.Response.WriteAsync("�������� ������");
});

app.MapGet("/register/{UserLogin}-{UserPassword}", async (HttpContext Context, string UserLogin, string UserPassword) =>
{
    if (UsersData.IsExistUser(UserLogin))
    {
        await Context.Response.WriteAsync("������������ � ����� ������� ��� ����������");
    }
    else if (!UsersData.IsExistUser(UserLogin))
    {
        UsersData.AddUser(new User(UserLogin,UserPassword));
        await Context.Response.WriteAsync("Succes");
    }
});

<<<<<<< Updated upstream
=======
app.MapGet("/messages/{id}", async (HttpContext Context,int id) =>
{
    User user = UsersData.FindUserById(id);

    ChatFolder Folder = user.ChatsFolders[0];


    string Messegas = string.Empty;
    for (int i = 0; i < Folder.Contacts.Count; i++)
    {
        Messegas += $"{Folder.Contacts[i].GetAllMeseges()}";
    }

    await Context.Response.WriteAsync(Messegas);
});
app.MapGet("/newMessages/{id}", async (HttpContext Context, int id) =>
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

    ReciverUser?.FindContactByUsername(message.Sender).Messages.Add(message);
    ReciverUser?.NewMessages.Add(message);
    SenderUser?.FindContactByUsername(message.Reciver).Messages.Add(message);
    SenderUser?.NewMessages.Add(message);

    return Context.Response.StatusCode = 200;
});

// Endpoint для сохранения сообщения на сервере
app.MapPost("/messages/save", async (HttpContext context) =>
{
    try
    {
        string json = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var messageData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        
        string fromUser = messageData["fromUser"]?.ToString() ?? "";
        string toUser = messageData["toUser"]?.ToString() ?? "";
        string messageText = messageData["message"]?.ToString() ?? "";
        string timestamp = messageData["timestamp"]?.ToString() ?? "";
        string messageType = messageData["messageType"]?.ToString() ?? "";
        string status = messageData["status"]?.ToString() ?? "Sent";
        
        // Сохраняем сообщение в файл для восстановления с статусом
        await MessageStorage.SaveMessage(fromUser, toUser, messageText, timestamp, messageType, status);
        
        await context.Response.WriteAsync("Message saved");
        Logs.Save($"Сообщение сохранено: {fromUser} -> {toUser} (Status: {status})");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка сохранения сообщения: {ex.Message}");
    }
});

// Endpoint для сохранения сообщения с разными статусами для отправителя и получателя
app.MapPost("/messages/save-with-dual-status", async (HttpContext context) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        string requestBody = await reader.ReadToEndAsync();
        
        var messageData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);
        
        if (messageData == null) 
        {
            await context.Response.WriteAsync("Invalid message data");
            return;
        }
        
        string fromUser = messageData["fromUser"]?.ToString() ?? "";
        string toUser = messageData["toUser"]?.ToString() ?? "";
        string messageText = messageData["message"]?.ToString() ?? "";
        string timestamp = messageData["timestamp"]?.ToString() ?? DateTime.Now.ToString("o");
        string messageType = messageData["messageType"]?.ToString() ?? "Text";
        string senderStatus = messageData["senderStatus"]?.ToString() ?? "Pending";
        string receiverStatus = messageData["receiverStatus"]?.ToString() ?? "Sent";
        
        // Сохраняем сообщение для отправителя с его статусом
        await MessageStorage.SaveMessage(fromUser, toUser, messageText, timestamp, messageType, senderStatus);
        
        // Сохраняем сообщение для получателя с его статусом
        await MessageStorage.SaveMessage(toUser, fromUser, messageText, timestamp, messageType, receiverStatus);
        
        await context.Response.WriteAsync("Message saved with dual status");
        Logs.Save($"Сообщение сохранено с двойным статусом: {fromUser} -> {toUser} (Sender: {senderStatus}, Receiver: {receiverStatus})");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка сохранения сообщения с двойным статусом: {ex.Message}");
    }
});

// Endpoint для получения истории сообщений
/*app.MapGet("/messages/{userLogin}", async (HttpContext context, string userLogin) =>
{
    try
    {
        var messages = await MessageStorage.GetUserMessages(userLogin);
        string jsonMessages = System.Text.Json.JsonSerializer.Serialize(messages);
        await context.Response.WriteAsync(jsonMessages);
        Logs.Save($"Отправлена история сообщений для {userLogin}");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка получения истории: {ex.Message}");
    }
});*/

// Endpoint для очистки истории сообщений пользователя
app.MapDelete("/messages/{userLogin}", async (HttpContext context, string userLogin) =>
{
    try
    {
        await MessageStorage.ClearUserMessages(userLogin);
        await context.Response.WriteAsync("Messages cleared");
        Logs.Save($"История сообщений очищена для {userLogin}");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка очистки истории: {ex.Message}");
    }
});

// Endpoint для обновления активности пользователя
app.MapPost("/users/activity", async (HttpContext context) =>
{
    try
    {
        string json = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var activityData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        
        string userId = activityData?["userId"]?.ToString() ?? "";
        string lastMessageLoad = activityData?["lastMessageLoad"]?.ToString() ?? "";
        
        // Сохраняем активность пользователя
        await UserActivityStorage.UpdateActivity(userId, lastMessageLoad);
        
        await context.Response.WriteAsync("Activity updated");
        Logs.Save($"Обновлена активность пользователя {userId}: {lastMessageLoad}");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка обновления активности: {ex.Message}");
    }
});

// Endpoint для получения последней активности пользователя
app.MapGet("/users/{userId}/last-activity", async (HttpContext context, string userId) =>
{
    try
    {
        var activity = await UserActivityStorage.GetUserActivity(userId);
        string jsonActivity = System.Text.Json.JsonSerializer.Serialize(activity);
        await context.Response.WriteAsync(jsonActivity);
        Logs.Save($"Отправлена активность для {userId}");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка получения активности: {ex.Message}");
    }
});

// Endpoint для уведомления о том, что пользователь открыл чат
app.MapPost("/users/open-chat", async (HttpContext context) =>
{
    try
    {
        string json = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var chatData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        
        string userId = chatData?["userId"]?.ToString() ?? "";
        string chatWith = chatData?["chatWith"]?.ToString() ?? "";
        
        // Сохраняем информацию об открытом чате
        await UserActivityStorage.SetOpenChat(userId, chatWith);
        
        await context.Response.WriteAsync("Chat opened");
        Logs.Save($"Пользователь {userId} открыл чат с {chatWith}");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка открытия чата: {ex.Message}");
    }
});

// Endpoint для уведомления о том, что пользователь закрыл чат
app.MapPost("/users/close-chat", async (HttpContext context) =>
{
    try
    {
        string json = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var chatData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        
        string userId = chatData?["userId"]?.ToString() ?? "";
        
        // Очищаем информацию об открытом чате
        await UserActivityStorage.ClearOpenChat(userId);
        
        await context.Response.WriteAsync("Chat closed");
        Logs.Save($"Пользователь {userId} закрыл чат");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка закрытия чата: {ex.Message}");
    }
});

// Endpoint для проверки, открыт ли чат у пользователя
app.MapGet("/users/{userId}/open-chat", async (HttpContext context, string userId) =>
{
    try
    {
        var openChat = await UserActivityStorage.GetOpenChat(userId);
        string jsonChat = System.Text.Json.JsonSerializer.Serialize(openChat);
        await context.Response.WriteAsync(jsonChat);
        Logs.Save($"Отправлена информация об открытом чате для {userId}");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка получения открытого чата: {ex.Message}");
    }
});

// Endpoint для получения информации об открытом чате пользователя
app.MapGet("/users/open-chat/{userId}", async (HttpContext context, string userId) =>
{
    try
    {
        var openChatInfo = await UserActivityStorage.GetOpenChat(userId);
        string json = System.Text.Json.JsonSerializer.Serialize(openChatInfo);
        await context.Response.WriteAsync(json);
        Logs.Save($"Отправлена информация об открытом чате для пользователя {userId}");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка получения информации об открытом чате: {ex.Message}");
    }
});

// Endpoint для обновления статуса сообщения
app.MapPost("/messages/update-status", async (HttpContext context) =>
{
    try
    {
        string json = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var updateData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        
        string fromUser = updateData?["fromUser"]?.ToString() ?? "";
        string toUser = updateData?["toUser"]?.ToString() ?? "";
        string messageText = updateData?["message"]?.ToString() ?? "";
        string timestamp = updateData?["timestamp"]?.ToString() ?? "";
        string newStatus = updateData?["newStatus"]?.ToString() ?? "Sent";
        
        // Обновляем статус сообщения
        await MessageStorage.UpdateMessageStatus(fromUser, toUser, messageText, timestamp, newStatus);
        
        await context.Response.WriteAsync("Status updated");
        Logs.Save($"Обновлен статус сообщения: {fromUser} -> {toUser} на {newStatus}");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка обновления статуса: {ex.Message}");
    }
});

// Endpoint для отметки сообщений как доставленных когда получатель открывает чат
app.MapPost("/messages/mark-as-delivered", async (HttpContext context) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        string requestBody = await reader.ReadToEndAsync();
        
        var requestData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);
        
        if (requestData == null) 
        {
            await context.Response.WriteAsync("Invalid request data");
            return;
        }
        
        string fromUser = requestData["fromUser"]?.ToString() ?? "";
        string toUser = requestData["toUser"]?.ToString() ?? "";
        
        // Обновляем статус всех сообщений от fromUser к toUser с Pending на Sent
        await MessageStorage.MarkMessagesAsDelivered(fromUser, toUser);
        
        await context.Response.WriteAsync("Messages marked as delivered");
        Logs.Save($"Сообщения от {fromUser} к {toUser} помечены как доставленные");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка отметки сообщений как доставленных: {ex.Message}");
    }
});

// Endpoint для установки открытого чата пользователя
app.MapPost("/users/set-open-chat", async (HttpContext context) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        string requestBody = await reader.ReadToEndAsync();
        
        var requestData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);
        
        if (requestData == null) 
        {
            await context.Response.WriteAsync("Invalid request data");
            return;
        }
        
        string userId = requestData["userId"]?.ToString() ?? "";
        string openChatWith = requestData["openChatWith"]?.ToString() ?? "";
        
        await UserActivityStorage.SetOpenChat(userId, openChatWith);
        
        await context.Response.WriteAsync("Open chat updated");
        Logs.Save($"Пользователь {userId} открыл чат с {openChatWith}");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка установки открытого чата: {ex.Message}");
    }
});

// Endpoint для очистки открытого чата пользователя
app.MapPost("/users/clear-open-chat", async (HttpContext context) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        string requestBody = await reader.ReadToEndAsync();
        
        var requestData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);
        
        if (requestData == null) 
        {
            await context.Response.WriteAsync("Invalid request data");
            return;
        }
        
        string userId = requestData["userId"]?.ToString() ?? "";
        
        await UserActivityStorage.ClearOpenChat(userId);
        
        await context.Response.WriteAsync("Open chat cleared");
        Logs.Save($"Очищен открытый чат для пользователя {userId}");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}");
        Logs.Save($"Ошибка очистки открытого чата: {ex.Message}");
    }
});

app.MapGet("/", () => "Сервер работает. Используйте доступные маршруты.");

>>>>>>> Stashed changes
app.Run();
