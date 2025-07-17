using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Net.Http.Headers;
using Tebegrammmm.ChatsFoldersRedactsWindows;
using Tebegrammmm.Classes;
using System.Threading.Tasks;

namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для MessengerWindow.xaml
    /// </summary>
    public partial class MessengerWindow : Window
    {
        static HttpClient httpClient = new HttpClient();
        string serverAdress = "https://localhost:7034";
        private static object thisLock = new();
        User User { get; set; }
        Contact Contact { get; set; }

        TcpClient Client { get; set; }

        TcpListener tcpListener = null;
        Thread Thread { get; set; }
        bool IsRunning { get; set; }
        public MessengerWindow(User user)
        {
            InitializeComponent();
            GridMessege.Visibility = Visibility.Hidden;
            GridContactPanel.Visibility = Visibility.Hidden;
            this.User = user;

            LBChatsLoders.ItemsSource = User.ChatsFolders;
            LBChatsLoders.SelectedIndex = 0;

            StartListner();

<<<<<<< Updated upstream
            Thread = new Thread(new ThreadStart(ReceiveMessage));
=======
            Thread = new Thread(new ThreadStart(GetNewMessages));
>>>>>>> Stashed changes
            Thread.Start();


        }

        private void StartListner()
        {
            IPEndPoint endP = new IPEndPoint(IPAddress.Any, User.Port);
            tcpListener = new TcpListener(endP);
            tcpListener.Start();
            IsRunning = true;
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
            if (LBChats.SelectedItem == null)
            {
                return;
            }
            Contact = LBChats.SelectedItem as Contact;
            GridChat.DataContext = Contact;
            LBMessages.ItemsSource = Contact.Messages;
            GridMessege.Visibility = Visibility.Visible;
            GridContactPanel.Visibility = Visibility.Visible;
        }

<<<<<<< Updated upstream
        void ReceiveMessage()
=======
        public MessageType GetMessageType(string type)
        {
            switch (type)
            {
                case "Text": return MessageType.Text;
                case "Image": return MessageType.Image;
                case "File": return MessageType.File;
            }
            return MessageType.Text;
        }

        void AddMessageToUser(string MessageData)
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
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        contact.Messages.Add(message);
                    }));
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
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{serverAdress}/messages/{User.Id}");
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
                        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{serverAdress}/NewMessages/{User.Id}");
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
                                    AddMessageToUser(Messages[i]);
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

        /*void ReceiveMessage()
>>>>>>> Stashed changes
        {
            try
            {
                while (IsRunning)
                {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    StreamReader sr = new StreamReader(client.GetStream(), Encoding.Unicode);
                    string s = sr.ReadToEnd();

                    string[] messageData = s.Split('▫');
                    foreach (Contact contact in User.ChatsFolders[0].Contacts)
                    {
                        if (messageData[0] == contact.IPAddress.ToString() & Convert.ToInt32(messageData[1]) == contact.Port)
                        {
                            if (messageData[2] == "Text")
                            {
                                string text = messageData[5];
                                for (int i = 6; i < messageData.Length; i++)
                                {
                                    text += messageData[i];
                                }
                                Message message = new Message(contact.Name, text, messageData[3]);
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    contact.Messages.Add(message);
                                }));
                                SaveMessageToFile(contact.Name, s, false);
                            }
                            if (messageData[2] == "File")
                            {
                                Message message = new Message(contact.Name, messageData[5], messageData[3], MessageType.File, messageData[4]);
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    contact.Messages.Add(message);
                                }));
                                SaveMessageToFile(contact.Name, s, false);
                            }
                        }
                    }

                    client.Close();
                }
            }
            catch (SocketException ex)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show($"Sockets error: {ex.Message}");
                }));
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }));
            }
        }
<<<<<<< Updated upstream
        private void SendMessageToUser(Message message)
        {
            try
            {
                IPEndPoint endP = new IPEndPoint(Contact.IPAddress, Contact.Port);
                Client = new TcpClient();
                Client.Connect(endP);
                NetworkStream nw = Client.GetStream();
=======
        private async Task SendMessageToUserAsync(Message message)
        {
            try
            {
                StringContent content = new StringContent(message.ToString());
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{serverAdress}/messages");
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
        private async Task SendMessageToUserAsync1(Message message)
        {

            try
            {
                Log.Save($"[SendMessageToUser] Starting send message process");
                Log.Save($"[SendMessageToUser] Current Contact: {Contact?.Name} ({Contact?.Username})");
>>>>>>> Stashed changes

                string mes = string.Empty;

                mes += $"{User.IpAddress.ToString()}▫";
                mes += $"{User.Port}▫";
                mes += $"{message.MessageType}▫";
                mes += $"{message.Time}▫";
                mes += $"{message.ServerAdress}▫";
                mes += $"{message.Text}";


<<<<<<< Updated upstream
                byte[] buffer = Encoding.Unicode.GetBytes(mes);
                nw.Write(buffer, 0, buffer.Length);
                Client.Close();
                SaveMessageToFile(User.Name, mes);
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Sockets error: {ex.Message}");
=======
                // Пробуем доставить сообщение получателю
                bool isOnline = await CheckUserOnlineAsync(Contact.Username);

                if (isOnline)
                {
                    // Пробуем отправить через разные URL
                    string[] sendUrls = {
                        $"{serverAdress}/messages", 
                        /*$"http://127.0.0.1:{Contact.Port}/",
                        $"http://{Contact.IPAddress}:{Contact.Port}/"*/ 
                    };

                    StringContent content = new StringContent(mes, Encoding.Unicode);
                    bool messageSent = false;

                    foreach (string url in sendUrls)
                    {
                        try
                        {
                            Log.Save($"[HTTP] Попытка отправки сообщения на {url}");

                            using (HttpResponseMessage response = await httpClient.PostAsync(url, content))
                            {
                                Log.Save($"[HTTP] Статус ответа от {url}: {response.StatusCode}");

                                if (response.IsSuccessStatusCode)
                                {
                                    Log.Save($"[HTTP] Сообщение успешно отправлено пользователю {Contact.Name}");
                                    messageSent = true;
                                    break;
                                }
                            }
                        }
                        catch (Exception urlEx)
                        {
                            Log.Save($"[HTTP] Ошибка отправки на {url}: {urlEx.Message}");
                            continue;
                        }
                    }

                    if (!messageSent)
                    {
                        Log.Save($"[SendMessageToUser] Не удалось доставить сообщение {Contact.Name} - остается серым");
                    }
                }
                else
                {
                    Log.Save($"[SendMessageToUser] User {Contact.Name} offline - сообщение остается серым до получения");
                }

                // НЕ меняем статус сообщения здесь - он изменится только когда получатель загрузит историю
                UpdateChatDisplay();

                // Запускаем немедленную проверку статуса через 2 секунды (дать время получателю обработать)
                _ = Task.Delay(2000).ContinueWith(async _ => await CheckPendingMessagesStatus());
>>>>>>> Stashed changes
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void SaveMessageToFile(string ContactName, string messageData, bool IsMe = true)
        {
            try
            {
                string[] MessegeData = messageData.Split('▫');
                string MessegeDataInFile = $"{MessegeData[0]}▫{MessegeData[1]}▫{ContactName}▫{MessegeData[2]}▫{MessegeData[3]}▫{MessegeData[4]}▫{MessegeData[5]}";

<<<<<<< Updated upstream
<<<<<<< Updated upstream
                if (parts.Length < 5)
                {
                    MessageBox.Show("Некорректный формат сообщения.");
                    return;
                }

                string ip = parts[0];
                string port = parts[1];
                string type = parts[2];
                string time = parts[3];

                string text = parts[4];

                for (int i = 5; i < parts.Length; i++)
                {
                    text += parts[i];
                }

                string userName = User.Name;
=======
                string userId = User.Id.ToString();
>>>>>>> Stashed changes
=======
                string userId = User.Id.ToString();
>>>>>>> Stashed changes
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
                MessageBox.Show($"Ошибка при сохранении сообщения: {ex.Message}");
            }
        }
        private void SendMessage(string message, MessageType messageType = MessageType.Text, string ServerFilePath = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                TBMessage.Text = string.Empty;
                return;
            }

<<<<<<< Updated upstream
            Message Message = new Message(User.Name, message, DateTime.Now.ToString("HH:mm"), messageType, ServerFilePath);
            Contact.Messages.Add(Message);
            SendMessageToUser(Message);
=======
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
>>>>>>> Stashed changes
            TBMessage.Text = string.Empty;
        }

        private void Button_Click_SendMessage(object sender, RoutedEventArgs e)
        {
            if (LBChats.SelectedItem == null)
            {
                return;
            }
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

        private void Button_Click_AddContact(object sender, RoutedEventArgs e)
        {
            Contact newContact = new Contact();
            AddContact addContact = new AddContact(newContact, "Добавить контакт");

            if (addContact.ShowDialog() == true)
            {
                User.ChatsFolders[0].Contacts.Add(newContact);
            }
        }
        private void Button_Click_RemoveContact(object sender, RoutedEventArgs e)
        {
            GridContactPanel.Visibility = Visibility.Hidden;
            GridMessege.Visibility = Visibility.Hidden;
            Contact contact = (LBChats.SelectedItem as Contact);
            for (int i = 0; i < User.ChatsFolders.Count; i++)
            {
                for (int j = 0; j < User.ChatsFolders[i].Contacts.Count; j++)
                {
                    if (User.ChatsFolders[i].Contacts[j].Name == contact.Name)
                    {
                        User.ChatsFolders[i].Contacts.RemoveAt(j);
                    }
                }
            }
        }

        private void Button_Click_ContactRedact(object sender, RoutedEventArgs e)
        {
            if (LBChats.SelectedItem == null)
            {
                return;
            }
            AddContact addContact = new AddContact(Contact, "Редактировать контакт");

            if (addContact.ShowDialog() == true)
            {
                User.ChatsFolders[0].RemoveContact(Contact);
                User.ChatsFolders[0].AddContact(Contact);
                LBChats.SelectedIndex = LBChats.Items.Count - 1;
                MessageBox.Show("Контакт изменён");
                GridChat.DataContext = Contact;
                TBChat_Name.Text = Contact.Name;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }

        private void Button_Click_FoldersMenu(object sender, RoutedEventArgs e)
        {
            RedactcionChatsFoldersWindow RCFW = new RedactcionChatsFoldersWindow(User.ChatsFolders);
            RCFW.ShowDialog();
        }

        private async Task SendFileToServer(string filePath)
        {
            string mimeType = MIME.GetMimeType(Path.GetFileName(Path.GetExtension(filePath)));
            if (mimeType == "application/octet-stream")
            {
                MessageBox.Show("Неизвестный тип файла");
                return;
            }

            using var multipar = new MultipartFormDataContent();
            var fileStream = new StreamContent(File.OpenRead(filePath));
            fileStream.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            multipar.Add(fileStream, name: "file", fileName: Path.GetFileName(filePath));

            using var response = await httpClient.PostAsync($"{serverAdress}/upload", multipar);
            var ResponseText = await response.Content.ReadAsStringAsync();
            this.Dispatcher.Invoke(new Action(() => { SendMessage(Path.GetFileName(filePath), MessageType.File, $"{serverAdress}/upload/{Path.GetFileName(filePath)}"); }));
            MessageBox.Show(ResponseText);
        }

        private async void Button_Click_SelectFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            //fileDialog.ShowDialog();


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
                //openFolderDialog.ShowDialog();

                if (openFolderDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(openFolderDialog.FolderName))
                {
                    MessageBox.Show("Ошибка сервера");
                    return;
                }

                string fileName = (LBMessages.SelectedItem as Message).Text;
                var fileUrl = $"{serverAdress}/upload/{fileName}";

                /*using var response = await httpClient.GetStreamAsync(fileUrl);
                using var fs = new FileStream($"{openFolderDialog.FolderName}/{fileName}",FileMode.OpenOrCreate);
                await response.CopyToAsync(fs);

                MessageBox.Show($"Файл {fileName} скачен");*/

                try
                {
                    using var response = await httpClient.GetStreamAsync(fileUrl);
                    using var fs = new FileStream($"{openFolderDialog.FolderName}/{fileName}", FileMode.OpenOrCreate);
                    await response.CopyToAsync(fs);

                    MessageBox.Show($"Файл {fileName} скачен");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при скачивании файла: {ex.Message}");
                }
            }
        }

        private void Button_Click_Settings(object sender, RoutedEventArgs e)
        {
            SettingsPanelWindow SPW = new SettingsPanelWindow(User);
            SPW.ShowDialog();
        }
    }
}
