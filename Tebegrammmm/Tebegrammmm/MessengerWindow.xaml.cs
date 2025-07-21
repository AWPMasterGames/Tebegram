#nullable disable
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Net.Http.Headers;
using Tebegrammmm.ChatsFoldersRedactsWindows;
using Tebegrammmm.Classes;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using TebegramServer.Data;

namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для MessengerWindow.xaml
    /// </summary>
    public partial class MessengerWindow : Window
    {
        static HttpClient httpClient = new HttpClient();
        private static object thisLock = new();
        User User { get; set; }
        Contact Contact { get; set; }

        HttpListener httpListener = null;
        Thread Thread { get; set; }
        bool IsRunning { get; set; }
        private string lastSelectedContactName = "";

        public MessengerWindow(User user)
        {
            InitializeComponent();
            LoadStyle();
            GridMessege.Visibility = Visibility.Hidden;
            GridContactPanel.Visibility = Visibility.Hidden;
            this.User = user;

            Log.Save($"[MessengerWindow] Инициализация для пользователя: {user.Name} ({user.Username})");

            LBChatsLoders.ItemsSource = User.ChatsFolders;
            LBChatsLoders.SelectedIndex = 0;

            // Загружаем историю сообщений с сервера
            GetMessages();

            Thread = new Thread(new ThreadStart(GetNewMessages));
            Thread.Start();


            // Загружаем последний выбранный контакт
            string lastContact = LoadLastSelectedContact();
            if (!string.IsNullOrEmpty(lastContact))
            {
                lastSelectedContactName = lastContact;
                Log.Save($"[MessengerWindow] Последний выбранный контакт: {lastContact}");
            }

            Log.Save($"[MessengerWindow] Инициализация завершена");
        }

        private void LoadStyle()
        {
            LinearGradientBrush LGB = (LinearGradientBrush)this.TryFindResource("ChatBackground");
            ResourceDictionary resourceDictionary = new ResourceDictionary();

            LBMessages.Background = LGB;
        }

        private void LBChatsLoders_SelectionChangeFolder(object sender, SelectionChangedEventArgs e)
        {
            if (LBChatsLoders.SelectedItem == null)
            {
                return;
            }
            LBChats.ItemsSource = (LBChatsLoders.SelectedItem as ChatFolder).Contacts;
        }

        private void LBChats_SelectionChangedChat(object sender, SelectionChangedEventArgs e)
        {
            // Сохраняем черновик для предыдущего контакта
            if (Contact != null && TBMessage != null)
            {
                Contact.Draft = TBMessage.Text;
                Log.Save($"[LBChats_SelectionChanged] Saved draft for {Contact.Name}: '{Contact.Draft}'");
            }

            if (LBChats.SelectedItem == null)
            {
                Log.Save("[LBChats_SelectionChanged] No item selected");
                return;
            }

            Contact = LBChats.SelectedItem as Contact;
            Log.Save($"[LBChats_SelectionChanged] Selected contact: {Contact?.Name} ({Contact?.Username})");

            // Запускаем проверку статуса сообщений если еще не запущена
            if (messageStatusTimer == null)
            {
                StartMessageStatusChecker();
            }

            GridChat.DataContext = Contact;
            LBMessages.ItemsSource = Contact.Messages;
            GridMessege.Visibility = Visibility.Visible;
            GridContactPanel.Visibility = Visibility.Visible;

            // Восстанавливаем черновик для нового контакта
            if (TBMessage != null)
            {
                TBMessage.Text = Contact.Draft ?? string.Empty;
                Log.Save($"[LBChats_SelectionChanged] Restored draft for {Contact.Name}: '{Contact.Draft}'");
            }

            // Сохраняем последний выбранный контакт
            SaveLastSelectedContact(Contact.Name);

            // Уведомляем сервер об открытии чата
            _ = NotifyServerOpenChat(Contact.Name);
        }

         private async void AddMessageToUser(string MessageData)
        {
            string[] messageData = MessageData.Split('▫');
            if (messageData[0] == User.Username)
            {
                Contact contact = User.FindContactByUsername(messageData[1]);
                if (messageData[2] == "Text")
                {
                    string text = messageData[5];
                    for (int i = 6; i < messageData.Length; i++)
                    {
                        text += messageData[i];
                    }
                    Message message = new Message(User.Name, User.Username, text, messageData[2]);
                    message.Status = MessageStatus.Sent; // Все сообщения просто сохраняются
                    contact.Messages.Add(message);
                    // НЕ сохраняем на сервер - это уже сделал отправитель!
                    Log.Save($"[AddMessageToUser] Получено сообщение от {messageData[0]}: {text}");
                }
                else if (messageData[2] == "File")
                {
                    Message message = new Message(User.Name, messageData[1], messageData[5], messageData[3], MessageType.File, messageData[4]);
                    message.Status = MessageStatus.Sent; // Файлы тоже просто сохраняются
                    contact.Messages.Add(message);
                    // НЕ сохраняем на сервер - это уже сделал отправитель!
                    Log.Save($"[AddMessageToUser] Получен файл от {messageData[0]}: {messageData[4]}");
                }
            }
            else if(User.FindContactByUsername(messageData[0]) == null)
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/UserName/{messageData[0]}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();
                Contact contact = new Contact(messageData[0], content);
                if (messageData[2] == "Text")
                {
                    string text = messageData[5];
                    for (int i = 6; i < messageData.Length; i++)
                    {
                        text += messageData[i];
                    }
                    Message message = new Message(User.Name, User.Username, text, messageData[2]);
                    message.Status = MessageStatus.Sent; // Все сообщения просто сохраняются
                    contact.Messages.Add(message);
                    // НЕ сохраняем на сервер - это уже сделал отправитель!
                    Log.Save($"[AddMessageToUser] Получено сообщение от {messageData[0]}: {text}");
                }
                else if (messageData[2] == "File")
                {
                    Message message = new Message(User.Name, messageData[1], messageData[5], messageData[3], MessageType.File, messageData[4]);
                    message.Status = MessageStatus.Sent; // Файлы тоже просто сохраняются
                    contact.Messages.Add(message);
                    // НЕ сохраняем на сервер - это уже сделал отправитель!
                    Log.Save($"[AddMessageToUser] Получен файл от {messageData[0]}: {messageData[4]}");
                }
                User.AddContact(contact);
            }
            else foreach (Contact contact in User.ChatsFolders[0].Contacts)
                {
                    if (messageData[0] == contact.Username)
                    {
                        if (messageData[2] == "Text")
                        {
                            string text = messageData[5];
                            for (int i = 6; i < messageData.Length; i++)
                            {
                                text += messageData[i];
                            }

                            Message message = new Message(contact.Name, User.Username, text, messageData[2]);
                            message.Status = MessageStatus.Sent; // Все сообщения просто сохраняются

                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                contact.Messages.Add(message);
                                if (Contact == contact)
                                {
                                    UpdateChatDisplay();
                                }
                            }));
                            SaveMessageToFile(contact.Name, MessageData, false);

                            // НЕ сохраняем на сервер - это уже сделал отправитель!
                            Log.Save($"[AddMessageToUser] Получено сообщение от {contact.Name}: {text}");
                        }
                        else if (messageData[2] == "File")
                        {
                            Message message = new Message(contact.Name, messageData[1], messageData[5], messageData[3], MessageType.File, messageData[4]);
                            message.Status = MessageStatus.Sent; // Файлы тоже просто сохраняются

                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                contact.Messages.Add(message);
                                if (Contact == contact)
                                {
                                    UpdateChatDisplay();
                                }
                            }));
                            SaveMessageToFile(contact.Name, MessageData, false);

                            // НЕ сохраняем на сервер - это уже сделал отправитель!
                            Log.Save($"[AddMessageToUser] Получен файл от {contact.Name}: {messageData[4]}");
                        }
                    }
                }
        }

        async void GetMessages()
        {
            try
            {
                // запрос для получения сообщений с сервера
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/messages/{User.Id}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();

                //распределение сообщений в чаты
                try
                {
                    string[] Messages = content.Split('❂');
                    for (int i = 0; i < Messages.Length - 1; i++)
                    {
                        AddMessageToUser(Messages[i]);
                    }
                }
                catch (Exception ex)
                {
                    Log.Save($"[GetMessage] Error: {ex.Message}");
                    MessageBox.Show("Ошибка при попытке получения сообщений\nПодробнее от ошибке можно узнать в краш логах");
                    return;
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Save($"[GetMessage] Error: {ex.Message}");
                MessageBox.Show("Ошибка при попытке авторизации\nПодробнее от ошибке можно узнать в краш логах");
                return; // Добавляем return, чтобы прекратить выполнение
            }
        }
        async void GetNewMessages()
        {
            try
            {
                while (true)
                {
                    try
                    {

                        // запрос для получения сообщений с сервера
                        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/NewMessages/{User.Id}");
                        using HttpResponseMessage response = await httpClient.SendAsync(request);
                        string content = await response.Content.ReadAsStringAsync();
                        if (content != "NotFound")
                        {

                            //распределение сообщений в чаты
                            try
                            {
                                string[] Messages = content.Split('❂');
                                for (int i = 0; i < Messages.Length; i++)
                                {
                                    if (i == Messages.Length) continue;
                                    await this.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        AddMessageToUser(Messages[i]);
                                    }));
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Save($"[GetMessage] Error: {ex.Message}");
                                //MessageBox.Show("Ошибка при попытке получения сообщений\nПодробнее от ошибке можно узнать в краш логах");
                                continue;
                            }
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        continue;
                    }
                    // задержка перед новым запросом
                    Thread.Sleep(2000);
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[GetMessage] Error: {ex.Message}");
                MessageBox.Show("Ошибка при попытке авторизации\nПодробнее от ошибке можно узнать в краш логах");
                return; // Добавляем return, чтобы прекратить выполнение
            }
        }


        private async Task<bool> CheckUserOnlineAsync(string Username)
        {
            try
            {
                // Пробуем сначала через localhost, затем через IP
                string[] checkUrls = { 
                    /*$"http://localhost:{port}/", 
                    $"http://127.0.0.1:{port}/",
                    $"http://{ip}:{port}/" */
                };

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMilliseconds(800); // Очень быстрый таймаут для проверки

                    foreach (string url in checkUrls)
                    {
                        try
                        {
                            HttpResponseMessage response = await client.GetAsync(url);
                            if (response.IsSuccessStatusCode)
                            {
                                Log.Save($"[CheckUserOnline] User online at {url}");
                                return true;
                            }
                        }
                        catch
                        {
                            // Пробуем следующий URL
                            continue;
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
        private async Task SendMessageToUserAsync(Message message)
        {
            try
            {
                StringContent content = new StringContent(message.ToString());
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{ServerData.ServerAdress}/messages");
                request.Content = content;
                using var response = await httpClient.SendAsync(request);
                string responseText = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Log.Save($"[SendMessageToUser] Error: {ex.Message}");
                // В случае ошибки сообщение остается Pending (серым)
            }
        }
        private void SaveMessageToFile(string ContactName, string messageData, bool IsMe = true)
        {
            try
            {
                string[] MessegeData = messageData.Split('▫');
                string MessegeDataInFile = $"{MessegeData[0]}▫{ContactName}▫{MessegeData[1]}▫{MessegeData[2]}▫{MessegeData[3]}▫{MessegeData[4]}▫{MessegeData[4]}";

                string userId = User.Id.ToString();
                string dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

                if (!Directory.Exists(dataFolder))
                {
                    Directory.CreateDirectory(dataFolder);
                }

                string userFolder = Path.Combine(dataFolder, userId);
                if (!Directory.Exists(userFolder))
                {
                    Directory.CreateDirectory(userFolder);
                }
                string ContactFolder;
                if (IsMe)
                {
                    ContactFolder = Path.Combine(userFolder, Contact.Name);
                }
                else
                {
                    ContactFolder = Path.Combine(userFolder, ContactName);
                }

                if (!Directory.Exists(ContactFolder))
                {
                    Directory.CreateDirectory(ContactFolder);
                }
                DateTime dateTime = DateTime.Now;

                string fileName = $"{dateTime.ToString("dd.MM.yyyy")}.txt";
                string filePath = Path.Combine(ContactFolder, fileName);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(MessegeDataInFile);
                lock (thisLock)
                {
                    File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[SaveMessageToFile] Error: {ex.Message}");
                MessageBox.Show("Ошибка при сохранении сообщения\nПодробнее об ошибке можно узнать в краш логах");
            }
        }

        private async Task SaveMessageToServer(Message message)
        {
            try
            {
                // Используем Username вместо Name для корректной идентификации пользователя
                string toUserLogin = !string.IsNullOrEmpty(Contact.Username) ? Contact.Username : Contact.Name;

                var messageData = new
                {
                    fromUser = User.Login,
                    toUser = toUserLogin,
                    message = message.Text,
                    timestamp = message.Time,
                    messageType = message.MessageType.ToString(),
                    status = message.Status.ToString()
                };

                string json = System.Text.Json.JsonSerializer.Serialize(messageData);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await httpClient.PostAsync($"{ServerData.ServerAdress}/messages/save", content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        Log.Save($"[SaveMessageToServer] Сообщение сохранено на сервере для {toUserLogin}");
                    }
                    else
                    {
                        Log.Save($"[SaveMessageToServer] Ошибка сохранения на сервере: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[SaveMessageToServer] Error: {ex.Message}");
            }
        }

        private async void SendMessage(string message, MessageType messageType = MessageType.Text, string? ServerFilePath = null)
        {
            Log.Save($"[SendMessage] Starting send message: '{message}' to contact: {Contact?.Name}");

            if (string.IsNullOrWhiteSpace(message))
            {
                TBMessage.Text = string.Empty;
                return;
            }

            if (Contact == null)
            {
                MessageBox.Show("Ошибка: не выбран получатель сообщения");
                Log.Save("[SendMessage] Error: Contact is null");
                return;
            }

            Message Message = new Message(User.Username, Contact.Username, message, DateTime.Now.ToString("hh:mm"), messageType, ServerFilePath);
            //Contact.Messages.Add(Message);

            // Обновляем интерфейс
            UpdateChatDisplay();

            Log.Save($"[SendMessage] Message added to local contact. Sending to user...");
            await SendMessageToUserAsync(Message);
            TBMessage.Text = string.Empty;
            Contact.Draft = string.Empty; // Очищаем черновик после отправки
        }

        private void Button_Click_SendMessage(object sender, RoutedEventArgs e)
        {
            Log.Save($"[Button_Click_SendMessage] Button clicked. Selected item: {LBChats.SelectedItem?.GetType()?.Name}");

            if (LBChats.SelectedItem == null)
            {
                MessageBox.Show("Выберите контакт для отправки сообщения");
                Log.Save("[Button_Click_SendMessage] No chat selected");
                return;
            }

            Log.Save($"[Button_Click_SendMessage] Sending message: '{TBMessage.Text}'");
            SendMessage(TBMessage.Text);
            TBMessage.Focus();
        }

        private void TBMessage_KeyDown_SendMessage(object sender, KeyEventArgs e)
        {
            if (LBChats.SelectedItem == null)
            {
                return;
            }
            if (e.Key == Key.Enter)
            {
                SendMessage(TBMessage.Text);
            }
        }

        private async void SendAddNewContactRequest(string data)
        {
            try
            {
                StringContent content = new StringContent(data);
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{ServerData.ServerAdress}/Contact");
                request.Content = content;
                using var response = await httpClient.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string[] temp = data.Split('▫');
                    User.AddContact(new Contact(temp[1], temp[2]));
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[SendAddNewContactRequest] Error: {ex.Message}");
                // В случае ошибки сообщение остается Pending (серым)
            }
        }

        private void Button_Click_AddContact(object sender, RoutedEventArgs e)
        {
            string data = $"{User.Id}";
            Contact contact = new();
            AddContact addContact = new AddContact(contact);

            if (addContact.ShowDialog() == true)
            {
                SendAddNewContactRequest($"{User.Id}▫{contact.Username}▫{contact.Name}");
            }
        }

        private async void SendRemoveContactRequest(Contact contact)
        {
            try
            {
                StringContent content = new StringContent($"{User.Id}▫{contact.Username}");
                using var request = new HttpRequestMessage(HttpMethod.Delete, $"{ServerData.ServerAdress}/Contact");
                request.Content = content;
                using var response = await httpClient.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ;

                    for (int i = 0; i < User.ChatsFolders.Count; i++)
                    {
                        for (int j = 0; j < User.ChatsFolders[i].Contacts.Count; j++)
                        {
                            if (User.ChatsFolders[i].Contacts[j].Name == contact.Name)
                            {
                                User.ChatsFolders[i].Contacts[j].Messages.Clear();
                                User.ChatsFolders[i].Contacts.RemoveAt(j);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[SendAddNewContactRequest] Error: {ex.Message}");
                // В случае ошибки сообщение остается Pending (серым)
            }
        }
        private void Button_Click_RemoveContact(object sender, RoutedEventArgs e)
        {
            GridContactPanel.Visibility = Visibility.Hidden;
            GridMessege.Visibility = Visibility.Hidden;

            Contact contact = (LBChats.SelectedItem as Contact);
            SendRemoveContactRequest(contact);
        }

        private async void SendEditContactRequest(string newName, string oldName)
        {
            try
            {
                StringContent content = new StringContent($"{User.Id}▫{Contact.Username}▫{newName}");
                using var request = new HttpRequestMessage(HttpMethod.Put, $"{ServerData.ServerAdress}/Contact");
                request.Content = content;
                using var response = await httpClient.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // Изменяем имя контакта на новое
                    Contact.ChangeName(newName);

                    // Сохраняем изменения в настройках пользователя
                    SaveContactNameChange(Contact, oldName, Contact.Name);

                    // Обновляем интерфейс
                    User.ChatsFolders[0].RemoveContact(Contact);
                    User.ChatsFolders[0].AddContact(Contact);
                    LBChats.SelectedIndex = LBChats.Items.Count - 1;
                    GridChat.DataContext = Contact;
                    TBChat_Name.Text = Contact.Name;

                    MessageBox.Show("Имя контакта изменено");
                    Log.Save($"[ContactEdit] Контакт изменен: {oldName} -> {Contact.Name}");
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[SendEditContactRequest] Error: {ex.Message}");
                // В случае ошибки сообщение остается Pending (серым)
            }
        }

        private void Button_Click_ContactRedact(object sender, RoutedEventArgs e)
        {
            if (LBChats.SelectedItem == null)
            {
                return;
            }
            // Сохраняем старое имя до изменения
            string oldName = Contact.Name;

            // Используем специальное окно для редактирования имени
            EditContactNameWindow editWindow = new EditContactNameWindow(Contact);

            if (editWindow.ShowDialog() == true)
            {

                SendEditContactRequest(editWindow.NewName, oldName);
            }



        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsRunning = false;

            // Останавливаем таймер проверки статуса сообщений
            if (messageStatusTimer != null)
            {
                messageStatusTimer.Stop();
                messageStatusTimer.Dispose();
                Log.Save("[Window_Closing] Остановлен таймер проверки статуса сообщений");
            }

            if (httpListener != null && httpListener.IsListening)
            {
                httpListener.Stop();
            }
            Process.GetCurrentProcess().Kill();
        }

        private void Button_Click_FoldersMenu(object sender, RoutedEventArgs e)
        {
            RedactcionChatsFoldersWindow RCFW = new RedactcionChatsFoldersWindow(User.ChatsFolders);
            RCFW.ShowDialog();
        }

        private async Task SendFileToServer(string filePath)
        {
            string mimeType = MIME.GetMimeType(Path.GetExtension(filePath));
            if (mimeType == "application/octet-stream")
            {
                MessageBox.Show("Неизвестный тип файла");
                return;
            }

            using var multipar = new MultipartFormDataContent();
            var fileStream = new StreamContent(File.OpenRead(filePath));
            fileStream.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            multipar.Add(fileStream, name: "file", fileName: Path.GetFileName(filePath));

            using var response = await httpClient.PostAsync($"{ServerData.ServerAdress}/upload", multipar);
            var ResponseText = await response.Content.ReadAsStringAsync();
            this.Dispatcher.Invoke(new Action(() => { SendMessage(Path.GetFileName(filePath), MessageType.File, $"{ServerData.ServerAdress}/upload/{Path.GetFileName(filePath)}"); }));
            MessageBox.Show(ResponseText);
        }

        private async void Button_Click_SelectFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();

            if (fileDialog.ShowDialog() != true)
            {
                return;
            }
            else if (string.IsNullOrEmpty(fileDialog.FileName))
            {
                return;
            }

            await SendFileToServer(fileDialog.FileName);
        }

        private async void LBMessages_SelectionChangeMessage(object sender, SelectionChangedEventArgs e)
        {
            if (LBMessages.SelectedItem == null)
            {
                LBMessages.SelectedIndex = -1;
                return;
            }
            else if ((LBMessages.SelectedItem as Message).MessageType == MessageType.File)
            {
                OpenFolderDialog openFolderDialog = new OpenFolderDialog();

                if (openFolderDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(openFolderDialog.FolderName))
                {
                    MessageBox.Show("Ошибка сервера");
                    return;
                }

                string fileName = (LBMessages.SelectedItem as Message).Text;
                var fileUrl = $"{ServerData.ServerAdress}/upload/{fileName}";

                try
                {
                    using var response = await httpClient.GetStreamAsync(fileUrl);
                    using var fs = new FileStream($"{openFolderDialog.FolderName}/{fileName}", FileMode.OpenOrCreate);
                    await response.CopyToAsync(fs);

                    MessageBox.Show($"Файл {fileName} скачен");
                }
                catch (Exception ex)
                {
                    Log.Save($"[LBMessages_SelectionChangeMessage] Error: {ex.Message}");
                    MessageBox.Show($"Ошибка при скачивании файла\nПодробнее от ошибке можно узнать в краш логах");
                }
            }
        }

        private void Button_Click_Settings(object sender, RoutedEventArgs e)
        {
            SettingsPanelWindow SPW = new SettingsPanelWindow(User);
            SPW.ShowDialog();
        }

        /*private async Task LoadMessageHistoryAsync()
        {
            try
            {
                Log.Save($"[LoadMessageHistory] Загрузка истории сообщений для {User.Login}");
                
                // Очищаем все сообщения перед загрузкой истории чтобы избежать дублирования
                foreach (var folder in User.ChatsFolders)
                {
                    foreach (var contact in folder.Contacts)
                    {
                        contact.Messages.Clear();
                    }
                }
                Log.Save($"[LoadMessageHistory] Очищены локальные сообщения перед загрузкой истории");
                
                using (HttpResponseMessage response = await httpClient.GetAsync($"{ServerData.ServerAdress}/messages/{User.Login}"))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        var messages = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
                        
                        if (messages != null && messages.Count > 0)
                        {
                            // Сортируем сообщения по времени сохранения
                            messages = messages.OrderBy(m => 
                            {
                                if (m.TryGetValue("SavedAt", out var savedAt) && DateTime.TryParse(savedAt?.ToString(), out var date))
                                    return date;
                                return DateTime.MinValue;
                            }).ToList();

                            foreach (var msgData in messages)
                            {
                                RestoreMessageFromServer(msgData);
                            }
                            Log.Save($"[LoadMessageHistory] Загружено {messages.Count} сообщений");
                            
                            // Отмечаем время загрузки сообщений пользователем
                            await UpdateUserLastActivity();
                        }
                        else
                        {
                            Log.Save($"[LoadMessageHistory] Нет сохраненных сообщений для пользователя");
                            // Все равно отмечаем активность
                            await UpdateUserLastActivity();
                        }
                    }
                    else
                    {
                        Log.Save($"[LoadMessageHistory] Ошибка загрузки истории: {response.StatusCode}");
                    }
                }
                
                // После загрузки истории пробуем восстановить последний выбранный чат
                this.Dispatcher.BeginInvoke(new Action(() => {
                    RestoreLastSelectedContact();
                }));
            }
            catch (Exception ex)
            {
                Log.Save($"[LoadMessageHistory] Error: {ex.Message}");
            }
        }*/

        /*private Task RestoreMessageFromServer(Dictionary<string, object> messageData)
        {
            try
            {
                string fromUser = messageData["FromUser"]?.ToString() ?? "";
                string toUser = messageData["ToUser"]?.ToString() ?? "";
                string messageText = messageData["Message"]?.ToString() ?? "";
                string timestamp = messageData["Timestamp"]?.ToString() ?? "";
                string messageType = messageData["MessageType"]?.ToString() ?? "Text";
                string statusString = messageData["Status"]?.ToString() ?? "Sent";

                // Определяем, кто отправитель относительно текущего пользователя
                string contactLogin = fromUser == User.Login ? toUser : fromUser;
                string senderName = fromUser == User.Login ? User.Name : fromUser;

                Log.Save($"[RestoreMessage] Обработка сообщения: {fromUser} -> {toUser}");

                // Находим контакт в списке по логину или имени
                Contact? targetContact = null;
                foreach (var folder in User.ChatsFolders)
                {
                    // Сначала ищем по Username (логину)
                    targetContact = folder.Contacts.FirstOrDefault(c => 
                        !string.IsNullOrEmpty(c.Username) && c.Username == contactLogin);
                    
                    // Если не найден по Username, ищем по имени
                    if (targetContact == null)
                    {
                        targetContact = folder.Contacts.FirstOrDefault(c => c.Name == contactLogin);
                    }
                    
                    if (targetContact != null) 
                    {
                        Log.Save($"[RestoreMessage] Найден контакт: {targetContact.Name} (Username: {targetContact.Username})");
                        break;
                    }
                }

                if (targetContact != null)
                {
                    MessageType msgType = Enum.TryParse(messageType, out MessageType parsedType) ? parsedType : MessageType.Text;
                    MessageStatus msgStatus = Enum.TryParse(statusString, out MessageStatus parsedStatus) ? parsedStatus : MessageStatus.Sent;
                    
                    Message message = new Message(senderName, messageText, timestamp, msgType);
                    message.Status = msgStatus;
                    
                    // Добавляем в UI thread
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        targetContact.Messages.Add(message);
                        if (Contact == targetContact)
                        {
                            UpdateChatDisplay();
                        }
                    }));
                    
                    Log.Save($"[RestoreMessage] Восстановлено сообщение: {senderName} -> {targetContact.Name}");
                }
                else
                {
                    Log.Save($"[RestoreMessage] Контакт не найден для логина: {contactLogin}");
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[RestoreMessage] Error: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }*/

        private void UpdateChatDisplay()
        {
            if (Contact != null)
            {
                LBMessages.ItemsSource = null;
                LBMessages.ItemsSource = Contact.Messages;
                Log.Save($"[UpdateChatDisplay] Обновлен интерфейс чата для {Contact.Name} - всего сообщений: {Contact.Messages.Count}");
            }
        }

        private void SaveContactNameChange(Contact contact, string oldName, string newName)
        {
            try
            {
                // Сохраняем изменение имени в настройках пользователя
                // Можно реализовать сохранение в файл или на сервер
                Log.Save($"[SaveContactNameChange] Сохранено изменение имени контакта: {oldName} -> {newName}");

                // TODO: Здесь можно добавить сохранение в базу данных или файл настроек
                // Пока просто логируем изменение
            }
            catch (Exception ex)
            {
                Log.Save($"[SaveContactNameChange] Error: {ex.Message}");
            }
        }

        private void SaveLastSelectedContact(string contactName)
        {
            try
            {
                string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
                if (!Directory.Exists(settingsPath))
                {
                    Directory.CreateDirectory(settingsPath);
                }

                string userSettingsFile = Path.Combine(settingsPath, $"{User.Login}_settings.txt");
                File.WriteAllText(userSettingsFile, $"LastSelectedContact:{contactName}");
                Log.Save($"[SaveLastSelectedContact] Сохранен последний выбранный контакт: {contactName}");
            }
            catch (Exception ex)
            {
                Log.Save($"[SaveLastSelectedContact] Error: {ex.Message}");
            }
        }

        private string LoadLastSelectedContact()
        {
            try
            {
                string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
                string userSettingsFile = Path.Combine(settingsPath, $"{User.Login}_settings.txt");

                if (File.Exists(userSettingsFile))
                {
                    string content = File.ReadAllText(userSettingsFile);
                    if (content.StartsWith("LastSelectedContact:"))
                    {
                        string contactName = content.Substring("LastSelectedContact:".Length);
                        Log.Save($"[LoadLastSelectedContact] Загружен последний выбранный контакт: {contactName}");
                        return contactName;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[LoadLastSelectedContact] Error: {ex.Message}");
            }
            return "";
        }

        private void RestoreLastSelectedContact()
        {
            try
            {
                if (!string.IsNullOrEmpty(lastSelectedContactName))
                {
                    // Ищем контакт по имени в списке чатов
                    foreach (var folder in User.ChatsFolders)
                    {
                        var contact = folder.Contacts.FirstOrDefault(c => c.Name == lastSelectedContactName);
                        if (contact != null)
                        {
                            // Выбираем найденный контакт
                            LBChats.SelectedItem = contact;
                            Log.Save($"[RestoreLastSelectedContact] Восстановлен выбор контакта: {lastSelectedContactName}");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[RestoreLastSelectedContact] Error: {ex.Message}");
            }
        }

        // Таймер для периодической проверки статуса сообщений
        private System.Timers.Timer messageStatusTimer;

        private void StartMessageStatusChecker()
        {
            messageStatusTimer = new System.Timers.Timer(5000); // Проверяем каждые 5 секунд для быстрого отклика
            messageStatusTimer.Elapsed += async (sender, e) => await CheckPendingMessagesStatus();
            messageStatusTimer.AutoReset = true;
            messageStatusTimer.Start();
            Log.Save("[MessageStatusChecker] Запущена периодическая проверка статуса сообщений (каждые 5 сек)");
        }

        private async Task CheckPendingMessagesStatus()
        {
            try
            {
                foreach (var folder in User.ChatsFolders)
                {
                    foreach (var contact in folder.Contacts)
                    {
                        // Проверяем, есть ли у контакта сообщения с статусом Pending (серые)
                        var pendingMessages = contact.Messages.Where(m =>
                            m.Status == MessageStatus.Pending &&
                            m.Sender == User.Name).ToList(); // Только наши исходящие сообщения

                        if (pendingMessages.Any())
                        {
                            // Проверяем, загрузил ли получатель сообщения с сервера
                            bool messagesDelivered = await CheckIfMessagesDelivered(contact, pendingMessages);

                            if (messagesDelivered)
                            {
                                Log.Save($"[MessageStatusChecker] Пользователь {contact.Name} получил сообщения - обновляем статус {pendingMessages.Count} сообщений на белый");

                                // Обновляем статус всех Pending сообщений на Sent (белые)
                                foreach (var message in pendingMessages)
                                {
                                    message.Status = MessageStatus.Sent;

                                    // Обновляем статус на сервере
                                    var originalContact = Contact;
                                    Contact = contact;
                                    await SaveMessageToServer(message);
                                    Contact = originalContact;
                                }

                                // Обновляем UI
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    UpdateChatDisplay();
                                }));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[MessageStatusChecker] Error: {ex.Message}");
            }
        }

        private async Task<bool> CheckIfMessagesDelivered(Contact contact, List<Message> pendingMessages)
        {
            try
            {
                // Простая логика: проверяем, онлайн ли получатель
                bool isOnline = await CheckUserOnlineAsync(contact.Username);

                Log.Save($"[CheckIfMessagesDelivered] Контакт {contact.Name}: онлайн {isOnline}");
                return isOnline;
            }
            catch (Exception ex)
            {
                Log.Save($"[CheckIfMessagesDelivered] Error: {ex.Message}");
                return false;
            }
        }

        private async Task UpdateUserLastActivity()
        {
            try
            {
                var activityData = new
                {
                    userId = User.Login,
                    lastMessageLoad = DateTime.Now.ToString("o") // ISO 8601 format
                };

                string json = System.Text.Json.JsonSerializer.Serialize(activityData);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await httpClient.PostAsync($"{ServerData.ServerAdress}/users/activity", content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        Log.Save($"[UpdateUserLastActivity] Обновлена активность пользователя {User.Login}");
                    }
                    else
                    {
                        Log.Save($"[UpdateUserLastActivity] Ошибка обновления активности: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[UpdateUserLastActivity] Error: {ex.Message}");
            }
        }

        private async Task NotifyServerOpenChat(string chatWithUser)
        {
            try
            {
                var chatData = new
                {
                    userId = User.Login,
                    chatWith = chatWithUser
                };

                string json = System.Text.Json.JsonSerializer.Serialize(chatData);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await httpClient.PostAsync($"{ServerData.ServerAdress}/users/open-chat", content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        Log.Save($"[NotifyServerOpenChat] Уведомили сервер об открытии чата с {chatWithUser}");
                    }
                    else
                    {
                        Log.Save($"[NotifyServerOpenChat] Ошибка уведомления: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[NotifyServerOpenChat] Error: {ex.Message}");
            }
        }

        private async Task NotifyServerCloseChat()
        {
            try
            {
                var chatData = new
                {
                    userId = User.Login
                };

                string json = System.Text.Json.JsonSerializer.Serialize(chatData);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await httpClient.PostAsync($"{ServerData.ServerAdress}/users/close-chat", content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        Log.Save($"[NotifyServerCloseChat] Уведомили сервер о закрытии чата");
                    }
                    else
                    {
                        Log.Save($"[NotifyServerCloseChat] Ошибка уведомления: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[NotifyServerCloseChat] Error: {ex.Message}");
            }
        }

        private async Task UpdateMessageStatusOnServer(string fromUser, string toUser, string messageText, string timestamp, MessageStatus newStatus)
        {
            try
            {
                var updateData = new
                {
                    fromUser = fromUser,
                    toUser = toUser,
                    message = messageText,
                    timestamp = timestamp,
                    newStatus = newStatus.ToString()
                };

                string json = System.Text.Json.JsonSerializer.Serialize(updateData);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await httpClient.PostAsync($"{ServerData.ServerAdress}/messages/update-status", content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        Log.Save($"[UpdateMessageStatusOnServer] Обновлен статус сообщения: {fromUser} -> {toUser} на {newStatus}");
                    }
                    else
                    {
                        Log.Save($"[UpdateMessageStatusOnServer] Ошибка обновления статуса: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[UpdateMessageStatusOnServer] Error: {ex.Message}");
            }
        }
    }
    /*private async Task CheckForNewMessages()
    {
        try
        {
            using (HttpResponseMessage response = await httpClient.GetAsync($"{ServerData.ServerAdress}/messages/{User.Login}"))
            {
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var messages = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

                    if (messages != null && messages.Count > 0)
                    {
                        // Фильтруем только новые сообщения (после последней проверки)
                        var newMessages = messages.Where(m =>
                        {
                            if (m.TryGetValue("SavedAt", out var savedAt) && DateTime.TryParse(savedAt?.ToString(), out var savedDate))
                            {
                                return savedDate > lastMessageCheck;
                            }
                            return false;
                        }).ToList();

                        if (newMessages.Any())
                        {
                            Log.Save($"[CheckForNewMessages] Найдено {newMessages?.Count} новых сообщений");

                            foreach (var msgData in newMessages)
                            {
                                await ProcessNewMessage(msgData);
                            }

                            // Обновляем время последней проверки
                            lastMessageCheck = DateTime.Now;

                            // Обновляем интерфейс если нужно
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (Contact != null)
                                {
                                    UpdateChatDisplay();
                                }
                            }));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Save($"[CheckForNewMessages] Error: {ex.Message}");
        }
    }*/

    /*private async Task ProcessNewMessage(Dictionary<string, object> messageData)
    {
        try
        {
            string fromUser = messageData["FromUser"]?.ToString() ?? "";
            string toUser = messageData["ToUser"]?.ToString() ?? "";
            string messageText = messageData["Message"]?.ToString() ?? "";
            string timestamp = messageData["Timestamp"]?.ToString() ?? "";
            string messageType = messageData["MessageType"]?.ToString() ?? "Text";
            string statusString = messageData["Status"]?.ToString() ?? "Sent";

            // Определяем, кто отправитель относительно текущего пользователя
            string contactLogin = fromUser == User.Login ? toUser : fromUser;
            string senderName = fromUser == User.Login ? User.Name : fromUser;

            // Проверяем, это входящее сообщение (не от нас)
            if (fromUser != User.Login)
            {
                // Находим контакт в списке по логину или имени
                Contact? targetContact = null;
                foreach (var folder in User.ChatsFolders)
                {
                    // Сначала ищем по Username (логину)
                    targetContact = folder.Contacts.FirstOrDefault(c =>
                        !string.IsNullOrEmpty(c.Username) && c.Username == contactLogin);

                    // Если не найден по Username, ищем по имени
                    if (targetContact == null)
                    {
                        targetContact = folder.Contacts.FirstOrDefault(c => c.Name == contactLogin);
                    }

                    if (targetContact != null)
                    {
                        break;
                    }
                }

                if (targetContact != null)
                {
                    // Проверяем, есть ли уже такое сообщение в локальном чате
                    bool messageExists = targetContact.Messages.Any(m =>
                        m.Text == messageText &&
                        m.Time == timestamp &&
                        m.Sender == senderName);

                    if (!messageExists)
                    {
                        MessageType msgType = Enum.TryParse(messageType, out MessageType parsedType) ? parsedType : MessageType.Text;
                        MessageStatus msgStatus = Enum.TryParse(statusString, out MessageStatus parsedStatus) ? parsedStatus : MessageStatus.Sent;

                        Message message = new Message(senderName, messageText, timestamp, msgType);
                        message.Status = msgStatus;

                        // Добавляем в UI thread
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            targetContact.Messages.Add(message);
                        }));

                        Log.Save($"[ProcessNewMessage] Добавлено новое входящее сообщение: {senderName} -> {targetContact.Name}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Save($"[ProcessNewMessage] Error: {ex.Message}");
        }
    }*/
}
/* {
     try
     {
         Log.Save("[StartPeriodicMessageCheck] Запуск периодической проверки новых сообщений");

         while (true)
         {
             await Task.Delay(5000); // Проверяем каждые 5 секунд

             try
             {
                 await CheckForNewMessages();
             }
             catch (Exception ex)
             {
                 Log.Save($"[StartPeriodicMessageCheck] Ошибка проверки сообщений: {ex.Message}");
             }
         }
     }
     catch (Exception ex)
     {
         Log.Save($"[StartPeriodicMessageCheck] Error: {ex.Message}");
     }
 }*/
/*private async Task CheckForNewMessages()
{
    try
    {
        using (HttpResponseMessage response = await httpClient.GetAsync($"{ServerData.ServerAdress}/messages/{User.Login}"))
        {
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                var messages = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

                if (messages != null && messages.Count > 0)
                {
                    // Фильтруем только новые сообщения (после последней проверки)
                    var newMessages = messages.Where(m => 
                    {
                        if (m.TryGetValue("SavedAt", out var savedAt) && DateTime.TryParse(savedAt?.ToString(), out var savedDate))
                        {
                            return savedDate > lastMessageCheck;
                        }
                        return false;
                    }).ToList();

                    if (newMessages.Any())
                    {
                        Log.Save($"[CheckForNewMessages] Найдено {newMessages.Count} новых сообщений");

                        foreach (var msgData in newMessages)
                        {
                            await ProcessNewMessage(msgData);
                        }

                        // Обновляем время последней проверки
                        lastMessageCheck = DateTime.Now;

                        // Обновляем интерфейс если нужно
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (Contact != null)
                            {
                                UpdateChatDisplay();
                            }
                        }));
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Log.Save($"[CheckForNewMessages] Error: {ex.Message}");
    }
}

private async Task ProcessNewMessage(Dictionary<string, object> messageData)
{
    try
    {
        string fromUser = messageData["FromUser"]?.ToString() ?? "";
        string toUser = messageData["ToUser"]?.ToString() ?? "";
        string messageText = messageData["Message"]?.ToString() ?? "";
        string timestamp = messageData["Timestamp"]?.ToString() ?? "";
        string messageType = messageData["MessageType"]?.ToString() ?? "Text";
        string statusString = messageData["Status"]?.ToString() ?? "Sent";

        // Определяем, кто отправитель относительно текущего пользователя
        string contactLogin = fromUser == User.Login ? toUser : fromUser;
        string senderName = fromUser == User.Login ? User.Name : fromUser;

        // Проверяем, это входящее сообщение (не от нас)
        if (fromUser != User.Login)
        {
            // Находим контакт в списке по логину или имени
            Contact? targetContact = null;
            foreach (var folder in User.ChatsFolders)
            {
                // Сначала ищем по Username (логину)
                targetContact = folder.Contacts.FirstOrDefault(c => 
                    !string.IsNullOrEmpty(c.Username) && c.Username == contactLogin);

                // Если не найден по Username, ищем по имени
                if (targetContact == null)
                {
                    targetContact = folder.Contacts.FirstOrDefault(c => c.Name == contactLogin);
                }

                if (targetContact != null) 
                {
                    break;
                }
            }

            if (targetContact != null)
            {
                // Проверяем, есть ли уже такое сообщение в локальном чате
                bool messageExists = targetContact.Messages.Any(m => 
                    m.Text == messageText && 
                    m.Time == timestamp && 
                    m.Sender == senderName);

                if (!messageExists)
                {
                    MessageType msgType = Enum.TryParse(messageType, out MessageType parsedType) ? parsedType : MessageType.Text;
                    MessageStatus msgStatus = Enum.TryParse(statusString, out MessageStatus parsedStatus) ? parsedStatus : MessageStatus.Sent;

                    Message message = new Message(senderName, messageText, timestamp, msgType);
                    message.Status = msgStatus;

                    // Добавляем в UI thread
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        targetContact.Messages.Add(message);
                    }));

                    Log.Save($"[ProcessNewMessage] Добавлено новое входящее сообщение: {senderName} -> {targetContact.Name}");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Log.Save($"[ProcessNewMessage] Error: {ex.Message}");
    }
}*/

