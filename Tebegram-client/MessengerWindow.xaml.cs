#nullable disable
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
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
        private ClientWebSocket ws;
        private volatile bool _isClosing = false;
        private static object thisLock = new();
        Contact Contact { get; set; }
        Thread Thread { get; set; }
        Thread CaltokenThread { get; set; }

        public MessengerWindow()
        {
            InitializeComponent();

            // Размер главного окна — от рабочей области монитора, но в разумной
            // вилке (см. UiSizes): не во весь экран на ноутбуке и не «марка» на 4K
            UiSizes.ApplyAndCenter(this, UiSizes.MessengerWidth, UiSizes.MessengerHeight);

            LoadStyle();
            _fullChatsTemplate = LBChats.ItemTemplate; // сохраняем полный шаблон для переключения режимов
            GridMessege.Visibility = Visibility.Collapsed;
            GridContactPanel.Visibility = Visibility.Collapsed;

            Log.Save($"[MessengerWindow] Инициализация для пользователя: {UserData.User.Name} ({UserData.User.Username})");

            LBChatsLoders.ItemsSource = UserData.User.ChatsFolders;
            LBChatsLoders.SelectedIndex = 0;

            TempContacts = UserData.User.Contacts;

            // Подтягиваем папки контактов, сохранённые на сервере
            _ = LoadFoldersFromServerAsync();
            // Загружаем историю сообщений с сервера
            InitChatWebSocket();

            GetMessages();

            /*Thread = new Thread(new ThreadStart(GetNewMessages)) { IsBackground = true };
            Thread.Start();*/

            TBMessage.IsEnabled = true;
            //GetCallToken();

            Log.Save($"[MessengerWindow] Инициализация завершена");
            CaltokenThread = new Thread(new ThreadStart(GetCallToken)) { IsBackground = true };
            CaltokenThread.Start();

            // По завершении звонка (у звонившего) пишем в чат «📞 Аудиозвонок (м:сс)» —
            // как запись в истории, видна обеим сторонам
            VoiceRoom.CallEnded = (contact, duration) => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (contact == null) return;
                string text = duration.TotalSeconds < 1
                    ? "📞 Пропущенный звонок"
                    : $"📞 Аудиозвонок ({(int)duration.TotalMinutes}:{duration.Seconds:D2})";
                _ = SendMessageToContactAsync(contact, text);
            }));

            // Файл настроек устройства читаем из AppData; старый файл рядом с exe — для миграции
            string devicePath = File.Exists(AppPaths.DeviceDataFile) ? AppPaths.DeviceDataFile
                              : File.Exists("userDevice.data") ? "userDevice.data"
                              : null;
            if (devicePath != null)
            {
                UserData.User.SelectedDeviceName = File.ReadAllText(devicePath);
                // Индекс ищем в нумерации WaveInEvent — той же, что у VoiceRoom.
                // Раньше индекс брался из списка MMDeviceEnumerator (WASAPI), а его
                // порядок ДРУГОЙ — в звонок мог уходить не тот микрофон
                int deviceIdx = Classes.AudioDevices.FindByName(UserData.User.SelectedDeviceName);
                if (deviceIdx >= 0) UserData.User.SelectedDeviceNum = deviceIdx;
            }
        }

        private async void InitChatWebSocket()
        {
            // Цикл с переподключением: при обрыве связи или рестарте сервера
            // клиент сам восстанавливает соединение, а не молча перестаёт получать сообщения
            while (!_isClosing)
            {
                try
                {
                    await ServerData.Ready;
                    ws = new ClientWebSocket();
                    // https → wss (раньше подставлялось ws:// на https-туннель и соединение не устанавливалось)
                    string wsAddress = ServerData.ServerAdress.Replace("https:", "wss:").Replace("http:", "ws:");
                    await ws.ConnectAsync(new Uri($"{wsAddress}/Chat/ws?userId={UserData.User.Id}"),
                        CancellationToken.None);
                    Log.Save("[ChatWS] Соединение установлено");
                    await ReceiveMessagesAsync();
                }
                catch (Exception ex)
                {
                    Log.Save($"[ChatWS] Ошибка соединения: {ex.Message}");
                }

                if (_isClosing) return;
                await Task.Delay(3000);
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[4096];
            while (ws.State == WebSocketState.Open && !_isClosing)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    // Копим фрагменты до EndOfMessage — раньше длинные сообщения (>1 КБ) резались на куски
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string textMessage = Encoding.UTF8.GetString(ms.ToArray());
                    //распределение сообщений в чаты
                    try
                    {
                        // Команда удаления сообщения у собеседника
                        if (textMessage.StartsWith("DEL▫#▫"))
                        {
                            HandleDeleteNotification(textMessage);
                            continue;
                        }

                        // Конверт команд из main-dev (64bc1ab): «команда▫$▫данные».
                        // Наш сервер пока шлёт сообщения БЕЗ конверта — понимаем оба
                        // формата: старый не ломается, к новому серверу уже готовы
                        if (textMessage.StartsWith("addMessage▫$▫"))
                        {
                            AddMessageToUser(textMessage.Substring("addMessage▫$▫".Length));
                            continue;
                        }
                        if (textMessage.StartsWith("addChat▫$▫"))
                        {
                            HandleAddChat(textMessage.Substring("addChat▫$▫".Length));
                            continue;
                        }

                        AddMessageToUser(textMessage);
                    }
                    catch (Exception ex)
                    {
                        Log.Save($"[GetMessage] Error: {ex.Message}");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (ws.State == WebSocketState.CloseReceived)
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    break;
                }
            }
        }

        private void LoadStyle()
        {
            // Фон чата теперь задаётся в XAML (сплошной по теме + узор-обои),
            // раньше здесь принудительно ставился тёмный градиент даже в светлой теме.
        }

        private async void GetCallToken()
        {
            try
            {
                while (!_isClosing)
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
                            if (data.Length < 2)
                            {
                                // Свой же токен исходящего звонка (без ▫) — это не входящий звонок.
                                // Раньше data[1] бросал IndexOutOfRange и убивал поток опроса звонков.
                                Thread.Sleep(1500);
                                continue;
                            }
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
                    catch (HttpRequestException)
                    {
                        // Сервер недоступен — ждём, иначе цикл долбит его без паузы
                        Thread.Sleep(3000);
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

            // Результат глобального поиска: пользователя ещё нет в контактах —
            // сначала добавляем его на сервере, потом открываем чат
            if (Contact != null && Contact.IsGlobalResult)
            {
                _ = AddGlobalResultAsync(Contact);
                return;
            }

            GridChat.DataContext = Contact;
            SetMessagesSource(Contact);
            GridMessege.Visibility = Visibility.Visible;
            GridContactPanel.Visibility = Visibility.Visible;
            EmptyChatPlaceholder.Visibility = Visibility.Collapsed;

            // Прокручиваем к последнему сообщению после отрисовки списка
            Dispatcher.BeginInvoke(new Action(() => ScrollToLastMessageIfCurrent(Contact)),
                System.Windows.Threading.DispatcherPriority.Loaded);

            // Восстанавливаем черновик для нового контакта
            if (TBMessage != null)
            {
                TBMessage.Text = Contact.Draft ?? string.Empty;
                Log.Save($"[LBChats_SelectionChanged] Restored draft for {Contact.Name}: '{Contact.Draft}'");
            }
        }

        /// <summary>
        /// Клик по результату глобального поиска: добавляем пользователя в контакты
        /// и открываем с ним чат. Поиск при этом сбрасывается — контакт уже «свой».
        /// </summary>
        private async Task AddGlobalResultAsync(Contact found)
        {
            string username = found.Username;
            bool ok = await SendAddNewContactRequest($"{UserData.User.Id}▫{username}▫");
            if (!ok) return; // при 404 SendAddNewContactRequest сам покажет сообщение

            Contact added = UserData.User.FindContactByUsername(username);
            SearchContactBarTB.Text = string.Empty; // выходим из режима поиска
            if (added != null) OpenContact(added);
        }

        /// <summary>
        /// Приём нового чата от сервера (перенос из main-dev, коммит 64bc1ab, с фиксами:
        /// в оригинале в AddChat передавался ТИП Chat вместо переменной chat — это даже
        /// не компилируется, — а сам чат добавлялся дважды: и в папку напрямую, и через
        /// User.AddChat, который кладёт в ту же коллекцию).
        /// Формат данных: id&name&isGroup&avatar&ownerId. Чаты пока «спящая» сущность
        /// (переписка живёт в Contact.Messages), поэтому просто кладём в коллекцию.
        /// </summary>
        private void HandleAddChat(string payload)
        {
            try
            {
                string[] chatData = payload.Split('&');
                bool iOwner = chatData.Length > 4 && chatData[4] != "None" &&
                              int.TryParse(chatData[4], out int ownerId) && ownerId == UserData.User.Id;

                var chat = new Classes.Chat(int.Parse(chatData[0]), chatData[1],
                    bool.Parse(chatData[2]), chatData[3], iOwner);

                Dispatcher.Invoke(new Action(() =>
                {
                    // Дедуп: сервер может продублировать уведомление при переподключении
                    if (UserData.User.FindChatById(chat.Id) == null)
                        UserData.User.AddChat(chat);
                }));
            }
            catch (Exception ex)
            {
                Log.Save($"[HandleAddChat] {ex.GetType().Name}: {ex.Message}");
            }
        }

        // ПЕРЕХОД НА ChatId: сейчас поле [0] — username отправителя, и сообщение
        // раскладывается ПОИСКОМ КОНТАКТА по нему. В протоколе v2 первым полем
        // придёт ChatId — тогда: 1) все индексы ниже сдвигаются на +1;
        // 2) маршрутизация меняется на UserData.User.FindChatById(chatId) и
        // chat.Messages.Add(...) — поиск контакта останется только как фолбэк
        // для старых сообщений без ChatId (см. историю в GetMessages).
        private async void AddMessageToUser(string MessageData)
        {
            string[] messageData = MessageData.Split('▫');
            if (messageData[0] == UserData.User.Username)
            {
                Contact contact = UserData.User.FindContactByUsername(messageData[1]);
                if (contact == null)
                {
                    // Эхо своего сообщения адресату, которого нет в контактах (например, отправлено с другого устройства)
                    using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/UserName/{messageData[1]}");
                    using HttpResponseMessage resp = await httpClient.SendAsync(req);
                    if (!resp.IsSuccessStatusCode) return;
                    string[] cdata = (await resp.Content.ReadAsStringAsync()).Split("▫");
                    contact = new Contact(int.Parse(cdata[0]), messageData[1], cdata[1]);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        UserData.User.AddContact(contact);
                    }));
                }
                if (messageData[2] == "Text")
                {
                    // Хвост склеиваем с разделителем — раньше символ ▫ из текста терялся
                    string text = string.Join('▫', messageData.Skip(5));
                    Message message = new Message(UserData.User.Name, UserData.User.Username, text, messageData[3]);
                    message.Status = MessageStatus.Sent; // Все сообщения просто сохраняются
                    message.IsOutgoing = true;

                    Dispatcher.Invoke(new Action(() =>
                    {
                        contact.Messages.Add(message);
                        ScrollToLastMessageIfCurrent(contact);
                    }));

                    // НЕ сохраняем на сервер - это уже сделал отправитель!
                    Log.Save($"[AddMessageToUser] Получено сообщение от {messageData[0]}: {text}");
                }
                else if (messageData[2] == "File")
                {
                    Message message = new Message(UserData.User.Name, messageData[1], messageData[5], messageData[3], MessageType.File, $"{ServerData.ServerAdress}/upload/{messageData[5]}");
                    message.Status = MessageStatus.Sent; // Файлы тоже просто сохраняются
                    message.IsOutgoing = true;

                    Dispatcher.Invoke(new Action(() =>
                    {
                        contact.Messages.Add(message);
                        ScrollToLastMessageIfCurrent(contact);
                    }));

                    // НЕ сохраняем на сервер - это уже сделал отправитель!
                    Log.Save($"[AddMessageToUser] Получен файл от {messageData[0]}: {messageData[4]}");
                }
            }
            else if (UserData.User.FindContactByUsername(messageData[0]) == null)
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/UserName/{messageData[0]}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                // Отправитель мог быть удалён на сервере — иначе int.Parse ниже падал
                // с «Необработанной ошибкой» на тексте «Пользователь не найден»
                if (!response.IsSuccessStatusCode) return;
                string[] content = (await response.Content.ReadAsStringAsync()).Split("▫");
                Contact contact = new Contact(int.Parse(content[0]), messageData[0], content[1]);
                if (messageData[2] == "Text")
                {
                    string text = string.Join('▫', messageData.Skip(5));
                    // Входящее от нового контакта — отправитель это контакт, а не мы
                    Message message = new Message(contact.Name, UserData.User.Username, text, messageData[3]);
                    message.Status = MessageStatus.Sent; // Все сообщения просто сохраняются
                    Dispatcher.Invoke(new Action(() =>
                    {
                        contact.Messages.Add(message);
                        ScrollToLastMessageIfCurrent(contact);
                    }));
                    // НЕ сохраняем на сервер - это уже сделал отправитель!
                    Log.Save($"[AddMessageToUser] Получено сообщение от {messageData[0]}: {text}");
                }
                else if (messageData[2] == "File")
                {
                    Message message = new Message(contact.Name, messageData[1], messageData[5], messageData[3], MessageType.File, messageData[4]);
                    message.Status = MessageStatus.Sent; // Файлы тоже просто сохраняются
                    Dispatcher.Invoke(new Action(() =>
                    {
                        contact.Messages.Add(message);
                        ScrollToLastMessageIfCurrent(contact);
                    }));
                    // НЕ сохраняем на сервер - это уже сделал отправитель!
                    Log.Save($"[AddMessageToUser] Получен файл от {messageData[0]}: {messageData[4]}");
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
                            string text = string.Join('▫', messageData.Skip(5));

                            Message message = new Message(contact.Name, UserData.User.Username, text, messageData[3]);
                            message.Status = MessageStatus.Sent; // Все сообщения просто сохраняются

                            Dispatcher.Invoke(new Action(() =>
                            {
                                contact.Messages.Add(message);
                                ScrollToLastMessageIfCurrent(contact);
                            }));

                            // НЕ сохраняем на сервер - это уже сделал отправитель!
                            Log.Save($"[AddMessageToUser] Получено сообщение от {contact.Name}: {text}");
                        }
                        else if (messageData[2] == "File")
                        {
                            Message message = new Message(contact.Name, messageData[1], messageData[5], messageData[3], MessageType.File, messageData[4]);
                            message.Status = MessageStatus.Sent; // Файлы тоже просто сохраняются

                            Dispatcher.Invoke(new Action(() =>
                            {
                                contact.Messages.Add(message);
                                ScrollToLastMessageIfCurrent(contact);
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
                return; //Добавляем return, чтобы прекратить выполнение
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
        /// <summary>
        /// Отправка текстового сообщения КОНКРЕТНОМУ контакту (не зависит от выбранного
        /// чата): WS-команда SEND (живая доставка) + POST /messages (сохранение) +
        /// локальное добавление в чат. Используется для сообщений о звонках.
        /// </summary>
        private async Task SendMessageToContactAsync(Contact contact, string text)
        {
            try
            {
                Message message = new Message(UserData.User.Username, contact.Username, text,
                    DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                message.IsOutgoing = true;

                if (ws != null && ws.State == WebSocketState.Open)
                {
                    // ПЕРЕХОД НА ChatId: вместо 0 подставить реальный chat.Id
                    // (сервер сейчас сам ищет/создаёт чат в CheckIsExist по username)
                    string request = $"SEND▫#▫0▫#▫{contact.Username}▫#▫{message}";
                    ArraySegment<byte> buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(request));
                    await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                await SendMessageToUserAsync(message);
            }
            catch (Exception ex)
            {
                Log.Save($"[SendMessageToContact] {ex.Message}");
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

            // Полная дата + время (для разделителей по дням). В пузыре показывается только ЧЧ:ММ.
            Message Message = new Message(UserData.User.Username, Contact.Username, message, DateTime.Now.ToString("dd.MM.yyyy HH:mm"), messageType, ServerFilePath);

            Log.Save($"[SendMessage] Message added to local contact. Sending to UserData.User...");

            if (ws == null || ws.State != WebSocketState.Open)
            {
                MessageBox.Show("Нет соединения с сервером. Сообщение не отправлено — попробуйте ещё раз через пару секунд.");
                return;
            }

            try
            {
                // ПЕРЕХОД НА ChatId: вместо 0 подставить реальный chat.Id (см. комментарий выше)
                string request = $"SEND▫#▫0▫#▫{Contact.Username}▫#▫{Message.ToString()}";
                ArraySegment<byte> buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(request));
                await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.Save($"[SendMessage] Ошибка отправки по WebSocket: {ex.Message}");
                MessageBox.Show("Не удалось отправить сообщение — соединение прервано. Попробуйте ещё раз.");
                return;
            }

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
                            // Сравниваем по Username (уникален), а не по отображаемому имени —
                            // иначе удалялся чужой контакт с таким же именем
                            if (UserData.User.ChatsFolders[i].Contacts[j].Username == contact.Username)
                            {
                                UserData.User.ChatsFolders[i].Contacts[j].Messages.Clear();
                                UserData.User.ChatsFolders[i].Contacts.RemoveAt(j);
                                j--; // после RemoveAt следующий элемент сдвигается на место j
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
        private void ScrollToLastMessageIfCurrent(Contact contact)
        {
            if (!ReferenceEquals(contact, Contact)) return;
            if (LBMessages.Items.Count > 0)
                LBMessages.ScrollIntoView(LBMessages.Items[LBMessages.Items.Count - 1]);
        }

        /// <summary>Привязывает сообщения контакта с группировкой по дням (для разделителей-дат).</summary>
        private void SetMessagesSource(Contact contact)
        {
            var view = new System.Windows.Data.CollectionViewSource { Source = contact.Messages };
            view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription("DateKey"));
            LBMessages.ItemsSource = view.View;
        }

        // ── Удаление сообщений ───────────────────────────────────────────────
        private Message MessageFromMenu(object sender)
        {
            if (sender is MenuItem mi && mi.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement fe)
                return fe.DataContext as Message;
            return null;
        }

        private void DeleteMessage_ForMe_Click(object sender, RoutedEventArgs e)
            => _ = DeleteMessageAsync(MessageFromMenu(sender), "self");

        private void DeleteMessage_ForAll_Click(object sender, RoutedEventArgs e)
            => _ = DeleteMessageAsync(MessageFromMenu(sender), "all");

        private async Task DeleteMessageAsync(Message message, string scope)
        {
            if (message == null || Contact == null) return;

            // Убираем из текущего чата сразу
            Contact.Messages.Remove(message);

            try
            {
                string body = $"{UserData.User.Id}▫{Contact.Username}▫{scope}▫{message.Time}▫{message.Text}";
                using var request = new HttpRequestMessage(HttpMethod.Delete, $"{ServerData.ServerAdress}/message")
                {
                    Content = new StringContent(body)
                };
                await httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                Log.Save($"[DeleteMessage] {ex.Message}");
            }
        }

        /// <summary>Обрабатывает уведомление сервера об удалении сообщения у собеседника.</summary>
        private void HandleDeleteNotification(string raw)
        {
            string[] parts = raw.Split(new[] { "▫#▫" }, 4, StringSplitOptions.None);
            if (parts.Length < 4) return;
            string contactUsername = parts[1];
            string time = parts[2];
            string text = parts[3];

            Dispatcher.Invoke(() =>
            {
                Contact contact = UserData.User.FindContactByUsername(contactUsername);
                if (contact == null) return;
                for (int i = contact.Messages.Count - 1; i >= 0; i--)
                {
                    if (contact.Messages[i].Time == time && contact.Messages[i].Text == text)
                    {
                        contact.Messages.RemoveAt(i);
                        break;
                    }
                }
            });
        }

        private void Button_Click_RemoveContact(object sender, RoutedEventArgs e)
        {
            GridContactPanel.Visibility = Visibility.Collapsed;
            GridMessege.Visibility = Visibility.Collapsed;
            EmptyChatPlaceholder.Visibility = Visibility.Visible;

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
                    // Изменяем имя контакта на новое — ChangeName шлёт PropertyChanged,
                    // список и шапка обновятся через binding.
                    // ВАЖНО: НЕ присваивать TBChat_Name.Text напрямую — локальное значение
                    // затирает binding {Binding Name}, и после первого переименования
                    // заголовок чата переставал меняться при переключении чатов
                    Contact.ChangeName(newName);

                    // Обновляем интерфейс
                    UserData.User.ChatsFolders[0].RemoveContact(Contact);
                    UserData.User.ChatsFolders[0].AddContact(Contact);
                    LBChats.SelectedIndex = LBChats.Items.Count - 1;
                    GridChat.DataContext = Contact;

                    // Системное окно «Имя контакта изменено» убрано — новое имя сразу видно в списке
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
            if (Contact == null) return;

            string oldName = Contact.Name;
            Contact target = Contact;

            EditContactNameWindow editWindow = new EditContactNameWindow(target);
            if (editWindow.ShowDialog() != true) return;

            if (editWindow.DeleteRequested)
            {
                // Удаление контакта перенесено сюда из шапки чата
                GridContactPanel.Visibility = Visibility.Collapsed;
                GridMessege.Visibility = Visibility.Collapsed;
                EmptyChatPlaceholder.Visibility = Visibility.Visible;
                SendRemoveContactRequest(target);
            }
            else
            {
                SendEditContactRequest(editWindow.NewName, oldName);
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Флаг останавливает циклы переподключения и опроса звонков.
            // Раньше здесь был Process.Kill() — он же убивал приложение при «Выйти из аккаунта».
            _isClosing = true;
            try
            {
                if (ws != null && ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.Save($"[Window_Closing] {ex.Message}");
            }
        }

        private async void Button_Click_FoldersMenu(object sender, RoutedEventArgs e)
        {
            RedactcionChatsFoldersWindow RCFW = new RedactcionChatsFoldersWindow(UserData.User.ChatsFolders);
            RCFW.ShowDialog();
            // Диалог закрыт — сохраняем папки на сервер, чтобы не терялись при перезапуске
            await SyncFoldersToServerAsync();
        }

        /// <summary>
        /// Загружает пользовательские папки с сервера. Контакты в папках — те же
        /// объекты, что в «Все чаты» (по username), чтобы переписка была общей.
        /// </summary>
        private async Task LoadFoldersFromServerAsync()
        {
            try
            {
                await ServerData.Ready;
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/Folders/{UserData.User.Id}");
                using var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return;
                string content = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(content)) return;

                Dispatcher.Invoke(() =>
                {
                    while (UserData.User.ChatsFolders.Count > 1)
                        UserData.User.ChatsFolders.RemoveAt(UserData.User.ChatsFolders.Count - 1);

                    foreach (string folderRaw in content.Split('❂'))
                    {
                        string[] f = folderRaw.Split('▫');
                        if (f.Length < 3 || string.IsNullOrWhiteSpace(f[0])) continue;

                        var contacts = new ObservableCollection<Contact>();
                        foreach (string username in f[2].Split('&'))
                        {
                            Contact known = UserData.User.FindContactByUsername(username.Trim());
                            if (known != null) contacts.Add(known);
                        }
                        UserData.User.ChatsFolders.Add(new ChatFolder(f[0], contacts, f[1]));
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Save($"[LoadFolders] {ex.Message}");
            }
        }

        private async Task SyncFoldersToServerAsync()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 1; i < UserData.User.ChatsFolders.Count; i++)
                {
                    ChatFolder f = UserData.User.ChatsFolders[i];
                    if (sb.Length > 0) sb.Append('❂');
                    string usernames = string.Join("&", f.Contacts.Select(c => c.Username));
                    sb.Append($"{f.FolderName}▫{f.Icon}▫{usernames}");
                }

                using var request = new HttpRequestMessage(HttpMethod.Put, $"{ServerData.ServerAdress}/Folders/{UserData.User.Id}");
                request.Content = new StringContent(sb.ToString());
                using var response = await httpClient.SendAsync(request);
                Log.Save($"[SyncFolders] Папки сохранены, статус {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                Log.Save($"[SyncFolders] {ex.Message}");
            }
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
            this.Dispatcher.Invoke(new Action(() => { SendMessage(Path.GetFileName(filePath).Replace(" ", "_"), MessageType.File, $"{ServerData.ServerAdress}/upload/{Path.GetFileName(filePath).Replace(" ", "_")}"); }));
            // Системное окно с ответом сервера (именем файла) убрано — файл и так появляется в чате
            Log.Save($"[SendFileToServer] Загружен файл: {ResponseText}");
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

        // Открытые окна просмотра фото (чтобы одно фото не открывалось дважды)
        private readonly System.Collections.Generic.Dictionary<string, ImageViewerWindow> _openImageViewers = new();

        private static bool IsImageFile(string name)
        {
            string ext = Path.GetExtension(name ?? "").ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg"
                || ext == ".gif" || ext == ".bmp" || ext == ".webp";
        }

        /// <summary>
        /// Выделение в списке сообщений нам не нужно — только сбрасываем его.
        /// Вложения открываются по КЛИКУ (Attachment_Click): выделение может
        /// меняться и программно (например, при смене чата), и тогда файл
        /// открывался бы сам собой.
        /// </summary>
        private void LBMessages_SelectionChangeMessage(object sender, SelectionChangedEventArgs e)
        {
            if (LBMessages.SelectedIndex != -1) LBMessages.SelectedIndex = -1;
        }

        /// <summary>
        /// Клик по вложению в пузыре: фото и видео открываем во встроенном
        /// просмотрщике, аудио — системным плеером, неизвестный файл — скачиваем.
        /// </summary>
        private void Attachment_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not Message msg) return;
            if (msg.MessageType != MessageType.File) return;

            if (msg.IsUnknownFile)
            {
                _ = DownloadFileAsync(msg);
                return;
            }

            if (msg.IsImageFile || ImageViewerWindow.IsVideoFile(msg.Text))
            {
                OpenInViewer(msg);
                return;
            }

            // Аудио — системным приложением
            string url = msg.FileUrl;
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Save($"[Attachment_Click] {ex.Message}");
            }
        }

        /// <summary>
        /// Открывает фото или видео в просмотрщике. Одно и то же вложение не
        /// открывается дважды — повторный клик поднимает уже открытое окно.
        /// </summary>
        private void OpenInViewer(Message msg)
        {
            string fileUrl = msg.FileUrl;
            if (string.IsNullOrEmpty(fileUrl)) return;

            if (_openImageViewers.TryGetValue(fileUrl, out var existing))
            {
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }

            var viewer = new ImageViewerWindow(fileUrl, msg.Text);
            viewer.Closed += (_, __) => _openImageViewers.Remove(fileUrl);
            _openImageViewers[fileUrl] = viewer;
            viewer.Show();
        }

        /// <summary>ПКМ по фото/файлу → «Сохранить»: выбор папки и скачивание.</summary>
        private void SaveFile_Click(object sender, RoutedEventArgs e)
            => _ = DownloadFileAsync(MessageFromMenu(sender));

        /// <summary>Клик по чипу файла — та же логика, что и по превью (см. Attachment_Click).</summary>
        private void FileChip_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => Attachment_Click(sender, e);

        private async Task DownloadFileAsync(Message msg)
        {
            if (msg == null || msg.MessageType != MessageType.File) return;

            OpenFolderDialog openFolderDialog = new OpenFolderDialog();
            if (openFolderDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(openFolderDialog.FolderName))
                return;

            try
            {
                using var response = await httpClient.GetStreamAsync(msg.FileUrl);
                // FileMode.Create вместо OpenOrCreate: перезапись более длинного старого файла
                // не оставляет «хвост» из его прежних байтов
                using var fs = new FileStream(Path.Combine(openFolderDialog.FolderName, msg.Text), FileMode.Create);
                await response.CopyToAsync(fs);

                Log.Save($"[DownloadFile] Файл {msg.Text} сохранён в {openFolderDialog.FolderName}");
            }
            catch (Exception ex)
            {
                Log.Save($"[DownloadFile] Error: {ex.Message}");
                MessageBox.Show($"Ошибка при скачивании файла\nПодробнее об ошибке можно узнать в краш логах");
            }
        }

        private void Button_Click_Settings(object sender, RoutedEventArgs e)
        {
            SettingsPanelWindow SPW = new SettingsPanelWindow();
            if (SPW.ShowDialog() == true)
            {
                try
                {
                    // Удаляем сохранённый вход целиком (раньше в файл писался мусор "\0")
                    if (File.Exists(AppPaths.UserDataFile)) File.Delete(AppPaths.UserDataFile);
                    if (File.Exists("user.data")) File.Delete("user.data");
                }
                catch (Exception ex)
                {
                    Log.Save($"[Logout] {ex.Message}");
                }
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
        }

        private async void Button_Click_CallContact(object sender, RoutedEventArgs e)
        {
            if (Contact == null) return;

            // async void: недоступный сервер кидал здесь HttpRequestException и приложение
            // падало при попытке позвонить. Теперь — понятное сообщение вместо краша
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/Voice/CreateRoom/{UserData.User.Id}-{Contact.Username}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string token = (await response.Content.ReadAsStringAsync()).Trim();

                if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(token))
                {
                    Log.Save($"[CallContact] Сервер не выдал токен: {(int)response.StatusCode}");
                    TbgDialogWindow.Show("Не удалось начать звонок — сервер не ответил. Проверь соединение.", "Звонок");
                    return;
                }

                VoiceRoom VR = new VoiceRoom(Mode.ActiveCall, Contact, token);
                UserData.User.InCall = true;
                VR.Show();
            }
            catch (Exception ex)
            {
                Log.Save($"[CallContact] {ex.GetType().Name}: {ex.Message}");
                TbgDialogWindow.Show("Не удалось начать звонок. Проверь соединение с сервером.", "Звонок");
            }
        }

        ObservableCollection<Contact> TempContacts;
        ObservableCollection<Contact> FindedContacts;
        private bool _IsInSearch = false;
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Плейсхолдер прячем, когда есть текст
            if (SearchPlaceholder != null)
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchContactBarTB.Text) ? Visibility.Visible : Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(SearchContactBarTB.Text))
            {
                if (_IsInSearch)
                {
                    LBChats.ItemsSource = TempContacts;
                    _IsInSearch = false;
                }
                SetSearchHint(null);
            }
            else
            {
                _IsInSearch = true;
                // Папка может быть не выбрана — раньше здесь падало исключение,
                // и до глобального поиска ниже дело уже не доходило
                TempContacts = (LBChatsLoders.SelectedItem as ChatFolder)?.Contacts
                               ?? UserData.User.ChatsFolders[0].Contacts;

                string q = SearchContactBarTB.Text.ToLower().TrimStart('@');

                // Порядок выдачи: точное совпадение логина → логины, начинающиеся
                // с запроса → остальные вхождения (в логине или имени) в конце
                var exact = new System.Collections.Generic.List<Contact>();
                var prefix = new System.Collections.Generic.List<Contact>();
                var rest = new System.Collections.Generic.List<Contact>();

                foreach (Contact contact in UserData.User.Contacts)
                {
                    string username = contact.Username?.ToLower() ?? "";
                    string name = contact.Name?.ToLower() ?? "";

                    if (username == q) exact.Add(contact);
                    else if (username.StartsWith(q)) prefix.Add(contact);
                    else if (username.Contains(q) || name.Contains(q)) rest.Add(contact);
                }

                FindedContacts = new ObservableCollection<Contact>(exact.Concat(prefix).Concat(rest));
                LBChats.ItemsSource = FindedContacts;

                // Запрос с @ — ищем ещё и среди ВСЕХ пользователей сервера (как в вебе)
                ScheduleGlobalSearch();
            }
        }

        // ── Живой глобальный поиск по @логину (как в веб-клиенте) ───────────────
        // Пока пользователь печатает, ждём паузу в 300 мс и только потом идём на
        // сервер: иначе на каждый символ уходил бы отдельный запрос.
        private System.Windows.Threading.DispatcherTimer _globalSearchTimer;
        private int _globalSearchSeq;

        /// <summary>Подсказка под списком чатов (null — спрятать).</summary>
        private void SetSearchHint(string text)
        {
            if (SearchEmptyHint == null) return;
            SearchEmptyHint.Text = text ?? string.Empty;
            SearchEmptyHint.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ScheduleGlobalSearch()
        {
            string raw = SearchContactBarTB.Text.Trim();
            // Ищем только по явному @ и минимум двум символам — как на сервере
            // (/Users/find отвечает пустотой на запрос короче двух символов)
            if (!raw.StartsWith("@") || raw.Length < 3)
            {
                _globalSearchTimer?.Stop();
                SetSearchHint(raw.StartsWith("@") && raw.Length == 2
                    ? "Введи ещё хотя бы один символ" : null);
                return;
            }

            SetSearchHint("Ищем на сервере…");

            if (_globalSearchTimer == null)
            {
                _globalSearchTimer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(300) };
                _globalSearchTimer.Tick += (_, __) =>
                {
                    _globalSearchTimer.Stop();
                    _ = RunGlobalSearchAsync(SearchContactBarTB.Text.Trim().TrimStart('@'));
                };
            }

            _globalSearchTimer.Stop();  // сбрасываем отсчёт на каждый новый символ
            _globalSearchTimer.Start();
        }

        private async Task RunGlobalSearchAsync(string query)
        {
            int seq = ++_globalSearchSeq;
            string raw;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{ServerData.ServerAdress}/Users/find/{Uri.EscapeDataString(query)}");
                using var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    if (seq == _globalSearchSeq) SetSearchHint("Сервер не ответил на поиск");
                    return;
                }
                raw = (await response.Content.ReadAsStringAsync()).Trim();
            }
            catch (Exception ex)
            {
                Log.Save($"[GlobalSearch] {ex.GetType().Name}: {ex.Message}");
                if (seq == _globalSearchSeq) SetSearchHint("Нет связи с сервером");
                return;
            }

            // Пока ждали ответ, запрос мог смениться — не рисуем устаревшее
            if (seq != _globalSearchSeq) return;
            string current = SearchContactBarTB.Text.Trim();
            if (!current.StartsWith("@") || current.TrimStart('@') != query) return;

            int added = 0;
            foreach (string entry in string.IsNullOrEmpty(raw)
                     ? Array.Empty<string>() : raw.Split('❂'))
            {
                string[] parts = entry.Split('▫');
                if (parts.Length < 3 || !int.TryParse(parts[0], out int id)) continue;

                string username = parts[1];
                if (username == UserData.User.Username) continue;                 // себя не предлагаем
                if (UserData.User.FindContactByUsername(username) != null) continue; // уже в контактах — он выше
                if (FindedContacts.Any(c => c.Username == username)) continue;    // не дублируем

                FindedContacts.Add(new Contact(id, username, parts[2]) { IsGlobalResult = true });
                added++;
            }

            // Итог поиска показываем явно — иначе пустой список читается как «не работает»
            if (FindedContacts.Count > 0)
                SetSearchHint(null);
            else
                SetSearchHint($"На сервере нет пользователей с «{query}» в логине или имени");
        }

        /// <summary>
        /// Enter в поиске: открыть найденный контакт, либо найти НОВОГО пользователя
        /// по логину (можно с @) на сервере и начать с ним чат.
        /// </summary>
        private async void SearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            string query = SearchContactBarTB.Text.Trim().TrimStart('@');
            if (string.IsNullOrEmpty(query)) return;

            // Уже в контактах — просто открываем
            Contact existing = UserData.User.FindContactByUsername(query);
            if (existing != null)
            {
                OpenContact(existing);
                SearchContactBarTB.Text = string.Empty;
                return;
            }

            // Ищем нового пользователя на сервере и добавляем в контакты
            bool ok = await SendAddNewContactRequest($"{UserData.User.Id}▫{query}▫");
            if (ok)
            {
                Contact added = UserData.User.FindContactByUsername(query);
                SearchContactBarTB.Text = string.Empty;
                if (added != null) OpenContact(added);
            }
            // при 404 SendAddNewContactRequest сам покажет «не найден»
        }

        // ── Ширина списка чатов меняется перетаскиванием (GridSplitter) ──────
        // Уже 140px — компактный режим «только аватарки», шире — полный список.
        private const double CompactThreshold = 140;
        private const double CompactWidth = 72;

        private bool _compactChats = false;
        private System.Windows.DataTemplate _fullChatsTemplate;

        private void ChatListPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool compact = e.NewSize.Width < CompactThreshold;
            if (compact == _compactChats) return;
            _compactChats = compact;

            if (compact)
            {
                SearchBox.Visibility = Visibility.Collapsed;
                LBChats.ItemTemplate = (System.Windows.DataTemplate)FindResource("LBChatsCompactTemplate");
                SearchContactBarTB.Text = string.Empty; // сбрасываем поиск при сворачивании
            }
            else
            {
                SearchBox.Visibility = Visibility.Visible;
                LBChats.ItemTemplate = _fullChatsTemplate;
            }
        }

        private const double StandardWidth = 250;

        private void ChatSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // Два фиксатора: «только аватарки» (72) и стандартная ширина (250).
            // Узкая зона прилипает к 72, зона вокруг стандартной — к 250, шире — свободно.
            double w = ChatListColumn.ActualWidth;
            if (w < CompactThreshold)
                ChatListColumn.Width = new GridLength(CompactWidth);
            else if (w < StandardWidth + 40)
                ChatListColumn.Width = new GridLength(StandardWidth);
        }

        /// <summary>Сбрасывает поиск, переключает на «Все чаты» и открывает контакт.</summary>
        private void OpenContact(Contact contact)
        {
            _IsInSearch = false;
            LBChatsLoders.SelectedIndex = 0; // «Все чаты» — там точно есть новый контакт
            LBChats.ItemsSource = UserData.User.ChatsFolders[0].Contacts;
            LBChats.SelectedItem = contact;
            LBChats.ScrollIntoView(contact);
        }
    }
}

