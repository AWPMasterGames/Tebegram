#nullable disable
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tebegrammmm.ChatsFoldersRedactsWindows;
using Tebegrammmm.Classes;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для MessengerWindow.xaml
    /// </summary>
    public partial class MessengerWindow : Window
    {
        static HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });
        private static object thisLock = new();
        private bool _loggingOut = false;
        private bool _isLoadingHistory = false;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Dictionary<string, ImageViewerWindow> _openImageViewers = new();
        Contact Contact { get; set; }
        Thread Thread { get; set; }
        Thread CaltokenThread { get; set; }

        public MessengerWindow()
        {
            InitializeComponent();
            LoadStyle();
            GridMessege.Visibility = Visibility.Collapsed;
            GridContactPanel.Visibility = Visibility.Collapsed;

            Log.Save($"[MessengerWindow] Инициализация для пользователя: {UserData.User.Name} ({UserData.User.Username})");

            LBChatsLoders.ItemsSource = UserData.User.ChatsFolders;
            LBChatsLoders.SelectedIndex = 0;
            BtnAllChats.Visibility = Visibility.Collapsed;

            TempContacts = UserData.User.Contacts;

            // Загружаем историю сообщений с сервера
            GetMessages();

            Thread = new Thread(new ThreadStart(GetNewMessages)) { IsBackground = true };
            Thread.Start();

            TBMessage.IsEnabled = true;
            //GetCallToken();

            Log.Save($"[MessengerWindow] Инициализация завершена");
            CaltokenThread = new Thread(new ThreadStart(GetCallToken)) { IsBackground = true };
            CaltokenThread.Start();

            // Читаем сохранённый номер устройства без COM-запроса к аудиоподсистеме.
            // EnumerateAudioEndPoints() — медленный COM-вызов (1-3 с), его нельзя
            // вызывать на UI-потоке. Если сохранённый индекс окажется невалидным,
            // NAudio упадёт с понятным исключением, а пользователь сможет
            // перевыбрать устройство в настройках.
            if (File.Exists(AppPaths.DeviceDataFile) &&
                int.TryParse(File.ReadAllText(AppPaths.DeviceDataFile), out int savedDevice))
            {
                UserData.User.SelectedDeviceNum = savedDevice;
            }
        }

        private void LoadStyle()
        {
            LBMessages.Background = (Brush)this.TryFindResource("ChatBackground");
        }

        private void OpenLastChat()
        {
            // Если пользователь уже сам выбрал чат пока грузилась история — не перебиваем его
            if (Contact != null) return;
            try
            {
                if (!File.Exists(AppPaths.LastChatFile)) return;
                string lastUsername = File.ReadAllText(AppPaths.LastChatFile).Trim();
                if (string.IsNullOrEmpty(lastUsername)) return;

                var contacts = UserData.User.Contacts;
                for (int i = 0; i < contacts.Count; i++)
                {
                    if (contacts[i].Username == lastUsername)
                    {
                        LBChats.SelectedIndex = i;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[OpenLastChat] Error: {ex.Message}");
            }
        }

        private async void GetCallToken()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        if (UserData.User.InCall)
                        {
                            Thread.Sleep(1000);
                            continue;
                        }
                        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/Voice/GetCallToken/{UserData.User.Id}");
                        using HttpResponseMessage response = await httpClient.SendAsync(request);
                        string Content = await response.Content.ReadAsStringAsync();
                        if (Content != "NotFound")
                        {
                            string[] data = Content.Split('▫');
                            if (data.Length < 2) { Thread.Sleep(1500); continue; }

                            string CallerUsername = data[0];
                            string token = data[1];

                            try
                            {
                                Contact contact = UserData.User.FindContactByUsername(CallerUsername);
                                Dispatcher.Invoke(new Action(() =>
                                {
                                    VoiceRoom VR = new VoiceRoom(Mode.AcceptCall, contact, token);
                                    UserData.User.InCall = true;
                                    VR.Show();
                                }));
                            }
                            catch (Exception ex)
                            {
                                Log.Save($"[GetCallToken] Error: {ex.Message}");
                                MessageBox.Show("Ошибка при попытке получения сообщений\nПодробнее от ошибке можно узнать в краш логах");
                                continue;
                            }
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        continue;
                    }
                    // задержка перед новым запросом
                    Thread.Sleep(1500);
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[GetCallToken] Error: {ex.Message}");
                MessageBox.Show("Ошибка при попытке авторизации\nПодробнее от ошибке можно узнать в краш логах");
                return; // Добавляем return, чтобы прекратить выполнение
            }
        }

        private void ImageContainer_Loaded(object sender, RoutedEventArgs e)
        {
            var grid = (System.Windows.Controls.Grid)sender;
            void UpdateClip()
            {
                grid.Clip = new System.Windows.Media.RectangleGeometry(
                    new Rect(0, 0, grid.ActualWidth, grid.ActualHeight), 12, 12);
            }
            UpdateClip();
            grid.SizeChanged += (s, _) => UpdateClip();
        }

        private void BtnAllChats_Click(object sender, RoutedEventArgs e)
        {
            if (UserData.User?.ChatsFolders?.Count > 0)
            {
                LBChats.ItemsSource = UserData.User.ChatsFolders[0].Contacts;
                _IsInSearch = false;
                SearchContactBarTB.Text = string.Empty;
            }
        }

        private void LBChatsLoders_SelectionChangeFolder(object sender, SelectionChangedEventArgs e)
        {
            if (LBChatsLoders.SelectedItem == null)
            {
                return;
            }
            LBChats.ItemsSource = (LBChatsLoders.SelectedItem as ChatFolder).Contacts;
            _IsInSearch = false;
            SearchContactBarTB.Text = string.Empty;
        }

        private async void LBChats_SelectionChangedChat(object sender, SelectionChangedEventArgs e)
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

            var selected = LBChats.SelectedItem as Contact;

            // Если это preview-контакт (из поиска на сервере) — добавляем его
            if (selected != null && selected.IsSearchPreview)
            {
                LBChats.SelectedIndex = -1;
                bool added = await SendAddNewContactRequest($"{UserData.User.Id}▫{selected.Username}▫{selected.Name}");
                if (added)
                {
                    SearchContactBarTB.Text = string.Empty;
                    // Открываем только что добавленный контакт
                    var newContact = UserData.User.FindContactByUsername(selected.Username);
                    if (newContact != null)
                    {
                        LBChats.ItemsSource = UserData.User.Contacts;
                        LBChats.SelectedItem = newContact;
                    }
                }
                return;
            }

            Contact = selected;
            Log.Save($"[LBChats_SelectionChanged] Selected contact: {Contact?.Name} ({Contact?.Username})");

            // Запоминаем последний открытый чат
            try { File.WriteAllText(AppPaths.LastChatFile, Contact.Username); } catch { }

            GridChat.DataContext = Contact;
            LBMessages.ItemsSource = Contact.Messages;
            GridMessege.Visibility = Visibility.Visible;
            GridContactPanel.Visibility = Visibility.Visible;
            Dispatcher.InvokeAsync(ScrollMessagesToBottom, System.Windows.Threading.DispatcherPriority.Loaded);

            // Восстанавливаем черновик для нового контакта
            if (TBMessage != null)
            {
                TBMessage.Text = Contact.Draft ?? string.Empty;
                Log.Save($"[LBChats_SelectionChanged] Restored draft for {Contact.Name}: '{Contact.Draft}'");
            }
        }

        private async void AddMessageToUser(string MessageData)
        {
            string[] messageData = MessageData.Split('▫');
            if (messageData[0] == UserData.User.Username)
            {
                Contact contact = UserData.User.FindContactByUsername(messageData[1]);
                if (messageData[2] == "Text")
                {
                    string text = messageData[5];
                    for (int i = 6; i < messageData.Length; i++)
                    {
                        text += messageData[i];
                    }
                    Message message = new Message(UserData.User.Name, UserData.User.Username, text, messageData[3]);
                    message.Status = MessageStatus.Sent;
                    message.IsOutgoing = true;

                    Dispatcher.Invoke(new Action(() =>
                    {
                        contact.Messages.Add(message);
                        if (contact == Contact && !_isLoadingHistory) ScrollMessagesToBottom();
                    }));

                    Log.Save($"[AddMessageToUser] Получено сообщение от {messageData[0]}: {text}");
                }
                else if (messageData[2] == "File" || messageData[2] == "Image")
                {
                    MessageType mt = DetectTypeByFilename(messageData[5]);
                    Message message = new Message(UserData.User.Name, messageData[1], messageData[5], messageData[3], mt, $"{ServerData.ServerAdress}/upload/{Uri.EscapeDataString(messageData[5])}");
                    message.Status = MessageStatus.Sent;
                    message.IsOutgoing = true;

                    Dispatcher.Invoke(new Action(() =>
                    {
                        contact.Messages.Add(message);
                        if (contact == Contact && !_isLoadingHistory) ScrollMessagesToBottom();
                    }));

                    Log.Save($"[AddMessageToUser] Получен файл/изображение от {messageData[0]}: {messageData[5]}");
                }
            }
            else if (UserData.User.FindContactByUsername(messageData[0]) == null)
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/UserName/{messageData[0]}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string[] content = (await response.Content.ReadAsStringAsync()).Split("▫");
                if (content.Length < 2 || string.IsNullOrEmpty(content[0])) return;
                Contact contact = new Contact(int.Parse(content[0]), messageData[0], content[1]);
                if (messageData[2] == "Text")
                {
                    string text = messageData[5];
                    for (int i = 6; i < messageData.Length; i++)
                    {
                        text += messageData[i];
                    }
                    Message message = new Message(UserData.User.Name, UserData.User.Username, text, messageData[3]);
                    message.Status = MessageStatus.Sent; // Все сообщения просто сохраняются
                    Dispatcher.Invoke(new Action(() =>
                    {
                        contact.Messages.Add(message);
                        if (contact == Contact && !_isLoadingHistory) ScrollMessagesToBottom();
                    }));
                    // НЕ сохраняем на сервер - это уже сделал отправитель!
                    Log.Save($"[AddMessageToUser] Получено сообщение от {messageData[0]}: {text}");
                }
                else if (messageData[2] == "File" || messageData[2] == "Image")
                {
                    MessageType mt = DetectTypeByFilename(messageData[5]);
                    Message message = new Message(UserData.User.Name, messageData[1], messageData[5], messageData[3], mt, $"{ServerData.ServerAdress}/upload/{Uri.EscapeDataString(messageData[5])}");
                    message.Status = MessageStatus.Sent;
                    Dispatcher.Invoke(new Action(() =>
                    {
                        contact.Messages.Add(message);
                        if (contact == Contact && !_isLoadingHistory) ScrollMessagesToBottom();
                    }));
                    Log.Save($"[AddMessageToUser] Получен файл/изображение от {messageData[0]}: {messageData[5]}");
                }
                Dispatcher.Invoke(new Action(() =>
                {
                    UserData.User.AddContact(contact);
                }));
            }
            else foreach (Contact contact in UserData.User.ChatsFolders[0].Contacts)
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

                            Message message = new Message(contact.Name, UserData.User.Username, text, messageData[3]);
                            message.Status = MessageStatus.Sent; // Все сообщения просто сохраняются

                            Dispatcher.Invoke(new Action(() =>
                            {
                                contact.Messages.Add(message);
                                if (contact == Contact && !_isLoadingHistory) ScrollMessagesToBottom();
                            }));

                            // НЕ сохраняем на сервер - это уже сделал отправитель!
                            Log.Save($"[AddMessageToUser] Получено сообщение от {contact.Name}: {text}");
                        }
                        else if (messageData[2] == "File" || messageData[2] == "Image")
                        {
                            MessageType mt = DetectTypeByFilename(messageData[5]);
                            Message message = new Message(contact.Name, messageData[1], messageData[5], messageData[3], mt, $"{ServerData.ServerAdress}/upload/{Uri.EscapeDataString(messageData[5])}");
                            message.Status = MessageStatus.Sent;

                            Dispatcher.Invoke(new Action(() =>
                            {
                                contact.Messages.Add(message);
                                if (contact == Contact && !_isLoadingHistory) ScrollMessagesToBottom();
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
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/messages/{UserData.User.Id}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();

                //распределение сообщений в чаты
                try
                {
                    string[] Messages = content.Split('❂');
                    _isLoadingHistory = true;
                    for (int i = 0; i < Messages.Length - 1; i++)
                    {
                        AddMessageToUser(Messages[i]);
                        // Каждые 10 сообщений отдаём поток UI — пользователь может взаимодействовать
                        if (i % 10 == 0) await Task.Yield();
                    }
                    _isLoadingHistory = false;
                    OpenLastChat();
                    MessagesLoadingOverlay.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    _isLoadingHistory = false;
                    OpenLastChat();
                    MessagesLoadingOverlay.Visibility = Visibility.Collapsed;
                    Log.Save($"[GetMessage] Error: {ex.Message}");
                    MessageBox.Show("Ошибка при попытке получения сообщений\nПодробнее от ошибке можно узнать в краш логах");
                    return;
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Save($"[GetMessage] Error: {ex.Message}");
                MessageBox.Show("Ошибка при попытке авторизации\nПодробнее от ошибке можно узнать в краш логах");
                return; //Добавляем return, чтобы прекратить выполнение
            }
        }
        async void GetNewMessages()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        // запрос для получения сообщений с сервера
                        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/NewMessages/{UserData.User.Id}");
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
                    Thread.Sleep(1500);
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[GetMessage] Error: {ex.Message}");
                MessageBox.Show("Ошибка при попытке авторизации\nПодробнее от ошибке можно узнать в краш логах");
                return; // Добавляем return, чтобы прекратить выполнение
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

                string userId = UserData.User.Id.ToString();
                string dataFolder = Path.Combine(AppPaths.AppDataDir, "Data");

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

            Message Message = new Message(UserData.User.Username, Contact.Username, message, DateTime.Now.ToString("dd.MM.yyyy HH:mm"), messageType, ServerFilePath);

            Log.Save($"[SendMessage] Message added to local contact. Sending to UserData.User...");
            await SendMessageToUserAsync(Message);
            TBMessage.Text = string.Empty;
            Contact.Draft = string.Empty;
        }

        private void ScrollMessagesToBottom()
        {
            if (LBMessages.Items.Count == 0) return;
            var sv = GetScrollViewer(LBMessages);
            sv?.ScrollToBottom();
        }

        private static readonly System.Collections.Generic.HashSet<string> _imageExtensions =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tiff", ".ico" };

        private static MessageType DetectTypeByFilename(string filename) =>
            _imageExtensions.Contains(System.IO.Path.GetExtension(filename))
                ? MessageType.Image
                : MessageType.File;

        private static System.Windows.Controls.ScrollViewer GetScrollViewer(System.Windows.DependencyObject o)
        {
            if (o is System.Windows.Controls.ScrollViewer sv) return sv;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(o, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
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

        private async Task<bool> SendAddNewContactRequest(string data)
        {
            try
            {
                StringContent content = new StringContent(data);
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{ServerData.ServerAdress}/Contact");
                request.Content = content;
                using var response = await httpClient.SendAsync(request);
                string ResponseContent = await response.Content.ReadAsStringAsync();
                string[] temp = ResponseContent.Split('▫');
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (data.Split('▫')[2].Trim().Length < 1)
                    {
                        UserData.User.AddContact(new Contact(int.Parse(temp[0]), temp[2], temp[1]));
                        return true;
                    }
                    UserData.User.AddContact(new Contact(int.Parse(temp[0]), temp[2], data.Split('▫')[2].Trim()));
                    return true;
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    MessageBox.Show($"{temp[2]} не найден", "ошибка");
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[SendAddNewContactRequest] Error: {ex.Message}");
                // В случае ошибки сообщение остается Pending (серым)
            }
            return false;
        }

        private async void Button_Click_AddContact(object sender, RoutedEventArgs e)
        {
            string data = $"{UserData.User.Id}";
            Contact contact = new();
            while (true)
            {
                AddContact addContact = new AddContact(contact);
                if (addContact.ShowDialog() == true)
                {
                    if (UserData.User.FindContactByUsername(contact.Username) == null)
                    {
                        bool result = await SendAddNewContactRequest($"{UserData.User.Id}▫{contact.Username}▫{contact.Name}");
                        if (result) return;
                    }
                    else
                    {
                        for (int i = 0; i < UserData.User.Contacts.Count; i++)
                        {
                            if (contact.Username == UserData.User.Contacts[i].Username)
                            {
                                LBChats.SelectedIndex = i;
                                return;
                            }
                        }
                    }
                }
                else { return; }
            }
        }

        private async void SendRemoveContactRequest(Contact contact)
        {
            try
            {
                StringContent content = new StringContent($"{UserData.User.Id}▫{contact.Username}");
                using var request = new HttpRequestMessage(HttpMethod.Delete, $"{ServerData.ServerAdress}/Contact");
                request.Content = content;
                using var response = await httpClient.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ;

                    for (int i = 0; i < UserData.User.ChatsFolders.Count; i++)
                    {
                        for (int j = 0; j < UserData.User.ChatsFolders[i].Contacts.Count; j++)
                        {
                            if (UserData.User.ChatsFolders[i].Contacts[j].Name == contact.Name)
                            {
                                UserData.User.ChatsFolders[i].Contacts[j].Messages.Clear();
                                UserData.User.ChatsFolders[i].Contacts.RemoveAt(j);
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
            GridContactPanel.Visibility = Visibility.Collapsed;
            GridMessege.Visibility = Visibility.Collapsed;

            Contact contact = (LBChats.SelectedItem as Contact);
            SendRemoveContactRequest(contact);
        }

        private async void SendEditContactRequest(string newName, string oldName)
        {
            try
            {
                StringContent content = new StringContent($"{UserData.User.Id}▫{Contact.Username}▫{newName}");
                using var request = new HttpRequestMessage(HttpMethod.Put, $"{ServerData.ServerAdress}/Contact");
                request.Content = content;
                using var response = await httpClient.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // Изменяем имя контакта на новое
                    Contact.ChangeName(newName);

                    // Обновляем интерфейс
                    UserData.User.ChatsFolders[0].RemoveContact(Contact);
                    UserData.User.ChatsFolders[0].AddContact(Contact);
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
            _cts.Cancel();
            if (!_loggingOut)
                Process.GetCurrentProcess().Kill();
        }

        private void Button_Click_FoldersMenu(object sender, RoutedEventArgs e)
        {
            RedactcionChatsFoldersWindow RCFW = new RedactcionChatsFoldersWindow(UserData.User.ChatsFolders);
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

            bool isImage = mimeType.StartsWith("image/");
            MessageType msgType = isImage ? MessageType.Image : MessageType.File;

            using var multipar = new MultipartFormDataContent();
            var fileStream = new StreamContent(File.OpenRead(filePath));
            fileStream.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            multipar.Add(fileStream, name: "file", fileName: Path.GetFileName(filePath));

            using var response = await httpClient.PostAsync($"{ServerData.ServerAdress}/upload", multipar);
            var ResponseText = await response.Content.ReadAsStringAsync();
            string serverFileName = Path.GetFileName(filePath).Replace(" ", "_");
            this.Dispatcher.Invoke(new Action(() => { SendMessage(serverFileName, msgType, $"{ServerData.ServerAdress}/upload/{Uri.EscapeDataString(serverFileName)}"); }));
        }


        private async void ImageContextMenu_Download(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var menu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            var grid = menu?.PlacementTarget as System.Windows.Controls.Grid;
            var message = grid?.DataContext as Message;
            if (message == null) return;

            OpenFolderDialog openFolderDialog = new OpenFolderDialog();
            if (openFolderDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(openFolderDialog.FolderName))
                return;

            string fileName = message.Text;
            string fileUrl = !string.IsNullOrEmpty(message.ServerAdress)
                ? message.ServerAdress
                : $"{ServerData.ServerAdress}/upload/{Uri.EscapeDataString(fileName)}";
            try
            {
                using var response = await httpClient.GetStreamAsync(fileUrl);
                using var fs = new FileStream(Path.Combine(openFolderDialog.FolderName, fileName), FileMode.Create);
                await response.CopyToAsync(fs);
                MessageBox.Show($"Изображение {fileName} сохранено");
            }
            catch (Exception ex)
            {
                Log.Save($"[ImageContextMenu_Download] Error: {ex.Message}");
                MessageBox.Show($"Ошибка при скачивании:\n{ex.Message}");
            }
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
            if (Mouse.RightButton == MouseButtonState.Pressed)
            {
                LBMessages.SelectedIndex = -1;
                return;
            }

            if (LBMessages.SelectedItem == null)
            {
                LBMessages.SelectedIndex = -1;
                return;
            }

            var message = LBMessages.SelectedItem as Message;
            LBMessages.SelectedIndex = -1;

            if (message.MessageType == MessageType.Image)
            {
                string fileName = message.Text;
                string fileUrl = !string.IsNullOrEmpty(message.ServerAdress)
                    ? message.ServerAdress
                    : $"{ServerData.ServerAdress}/upload/{Uri.EscapeDataString(fileName)}";

                // Это фото уже открыто — поднимаем существующее окно
                if (_openImageViewers.TryGetValue(fileUrl, out var existing))
                {
                    if (existing.WindowState == WindowState.Minimized)
                        existing.WindowState = WindowState.Normal;
                    existing.Activate();
                    return;
                }

                // Открываем новое окно просмотра
                var viewer = new ImageViewerWindow(fileUrl, fileName);
                viewer.Closed += (_, _) => _openImageViewers.Remove(fileUrl);
                _openImageViewers[fileUrl] = viewer;
                viewer.Show();
            }
            else if (message.MessageType == MessageType.File)
            {
                OpenFolderDialog openFolderDialog = new OpenFolderDialog();

                if (openFolderDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(openFolderDialog.FolderName))
                    return;

                string fileName = message.Text;
                try
                {
                    using var response = await httpClient.GetStreamAsync($"{ServerData.ServerAdress}/upload/{fileName}");
                    using var fs = new FileStream($"{openFolderDialog.FolderName}/{fileName}", FileMode.OpenOrCreate);
                    await response.CopyToAsync(fs);
                    MessageBox.Show($"Файл {fileName} скачен");
                }
                catch (Exception ex)
                {
                    Log.Save($"[LBMessages_SelectionChangeMessage] File download error: {ex.Message}");
                    MessageBox.Show("Ошибка при скачивании файла\nПодробнее в краш-логах");
                }
            }
        }

        private void Button_Click_Settings(object sender, RoutedEventArgs e)
        {
            SettingsPanelWindow SPW = new SettingsPanelWindow();
            if (SPW.ShowDialog() == true)
            {
                AppPaths.EnsureDir();
                File.WriteAllText(AppPaths.UserDataFile, string.Empty);
                _loggingOut = true;
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
        }

        private async void Button_Click_CallContact(object sender, RoutedEventArgs e)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/Voice/CreateRoom/{UserData.User.Id}-{Contact.Username}");
            using HttpResponseMessage response = await httpClient.SendAsync(request);
            string token = await response.Content.ReadAsStringAsync();

            VoiceRoom VR = new VoiceRoom(Mode.ActiveCall, Contact, token);
            UserData.User.InCall = true;
            VR.Show();
        }

        ObservableCollection<Contact> TempContacts;
        ObservableCollection<Contact> FindedContacts;
        private bool _IsInSearch = false;
        private CancellationTokenSource _searchCts;

        private async void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = SearchContactBarTB.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                if (_IsInSearch)
                {
                    LBChats.ItemsSource = TempContacts;
                    _IsInSearch = false;
                }
                return;
            }

            _IsInSearch = true;
            TempContacts = (LBChatsLoders.SelectedItem as ChatFolder)?.Contacts ?? UserData.User.Contacts;

            bool isLoginSearch = text.StartsWith("@");
            string query = isLoginSearch ? text.Substring(1).ToLower() : text.ToLower();

            var results = new ObservableCollection<Contact>();

            foreach (Contact contact in UserData.User.Contacts)
            {
                bool match = isLoginSearch
                    ? contact.Username.ToLower().StartsWith(query)
                    : contact.Name.ToLower().Contains(query) || contact.Username.ToLower().Contains(query);
                if (match) results.Add(contact);
            }

            LBChats.ItemsSource = results;

            if (isLoginSearch && query.Length >= 1)
            {
                _searchCts?.Cancel();
                _searchCts = new CancellationTokenSource();
                var cts = _searchCts;

                try
                {
                    await Task.Delay(350, cts.Token);
                    if (cts.IsCancellationRequested) return;

                    using var request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/UserName/{query}");
                    using var response = await httpClient.SendAsync(request, cts.Token);
                    if (cts.IsCancellationRequested) return;

                    string raw = await response.Content.ReadAsStringAsync();
                    string[] parts = raw.Split('▫');

                    if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[0]) && parts[0] != "NotFound"
                        && !results.Any(c => c.Username.ToLower() == query))
                    {
                        var preview = new Contact(int.Parse(parts[0]), query, parts[1])
                        {
                            IsSearchPreview = true
                        };
                        results.Add(preview);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Log.Save($"[Search] {ex.Message}"); }
            }
        }

        private const double _collapseThreshold = 150;
        private bool _isNarrow = false;

        private void ChatListContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool shouldBeNarrow = e.NewSize.Width < _collapseThreshold;
            if (shouldBeNarrow == _isNarrow) return;
            _isNarrow = shouldBeNarrow;

            if (shouldBeNarrow)
            {
                SearchArea.Visibility = Visibility.Collapsed;
                BtnAddNarrow.Visibility = Visibility.Visible;
                LBChats.Tag = "narrow";
            }
            else
            {
                BtnAddNarrow.Visibility = Visibility.Collapsed;
                SearchArea.Visibility = Visibility.Visible;
                LBChats.Tag = null;
            }
        }
    }
}

