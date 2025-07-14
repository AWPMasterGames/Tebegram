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

namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для MessengerWindow.xaml
    /// </summary>
    public partial class MessengerWindow : Window
    {
        static HttpClient httpClient = new HttpClient();
        string serverAdress = GetServerAddress(); // Динамическое определение адреса
        private static object thisLock = new();
        User User { get; set; }
        Contact Contact { get; set; }

        HttpListener httpListener = null;
        Thread Thread { get; set; }
        bool IsRunning { get; set; }
        private string lastSelectedContactName = "";
        private System.Timers.Timer tunnelCheckTimer;
        private Process localtunnelProcess;

        public MessengerWindow(User user)
        {
            DateTime constructorStartTime = DateTime.Now;
            Log.Save($"[MessengerWindow] 🏗️ СТАРТ КОНСТРУКТОРА: {constructorStartTime:HH:mm:ss.fff}");
            
            DateTime initStartTime = DateTime.Now;
            InitializeComponent();
            DateTime initEndTime = DateTime.Now;
            TimeSpan initDuration = initEndTime - initStartTime;
            Log.Save($"[MessengerWindow] 🎨 InitializeComponent() завершен за {initDuration.TotalMilliseconds}ms");
            
            DateTime styleStartTime = DateTime.Now;
            LoadStyle();
            DateTime styleEndTime = DateTime.Now;
            TimeSpan styleDuration = styleEndTime - styleStartTime;
            Log.Save($"[MessengerWindow] 🎭 LoadStyle() завершен за {styleDuration.TotalMilliseconds}ms");
            
            GridMessege.Visibility = Visibility.Hidden;
            GridContactPanel.Visibility = Visibility.Hidden;
            Log.Save($"[MessengerWindow] 👁️ Панели скрыты (GridMessege, GridContactPanel)");
            
            this.User = user;
            Log.Save($"[MessengerWindow] 👤 Пользователь установлен: {user.Name}");
            Log.Save($"[MessengerWindow] 🆔 ID: {user.Id}");
            Log.Save($"[MessengerWindow] 🔑 Login: {user.Login}");
            Log.Save($"[MessengerWindow] 🌐 IP: {user.IpAddress}");
            Log.Save($"[MessengerWindow] 🔌 Port: {user.Port}");
            Log.Save($"[MessengerWindow] 📁 Папок чатов: {user.ChatsFolders?.Count ?? 0}");

            DateTime uiSetupStartTime = DateTime.Now;
            LBChatsLoders.ItemsSource = User.ChatsFolders;
            LBChatsLoders.SelectedIndex = 0;
            DateTime uiSetupEndTime = DateTime.Now;
            TimeSpan uiSetupDuration = uiSetupEndTime - uiSetupStartTime;
            Log.Save($"[MessengerWindow] 📋 UI настройка (ListBox) завершена за {uiSetupDuration.TotalMilliseconds}ms");

            Log.Save($"[MessengerWindow] 🚀 Запускаем LocalTunnel...");
            DateTime localtunnelStartTime = DateTime.Now;
            // Запускаем localtunnel для публичного доступа к серверу
            StartLocaltunnel();
            DateTime localtunnelEndTime = DateTime.Now;
            TimeSpan localtunnelDuration = localtunnelEndTime - localtunnelStartTime;
            Log.Save($"[MessengerWindow] 🌐 StartLocaltunnel() завершен за {localtunnelDuration.TotalMilliseconds}ms");

            Log.Save($"[MessengerWindow] 👂 Запускаем Listener...");
            DateTime listenerStartTime = DateTime.Now;
            StartListener();
            DateTime listenerEndTime = DateTime.Now;
            TimeSpan listenerDuration = listenerEndTime - listenerStartTime;
            Log.Save($"[MessengerWindow] 🎧 StartListener() завершен за {listenerDuration.TotalMilliseconds}ms");

            Log.Save($"[MessengerWindow] 🧵 Создаем и запускаем поток для получения сообщений...");
            DateTime threadStartTime = DateTime.Now;
            Thread = new Thread(new ThreadStart(ReceiveMessage));
            Thread.Start();
            DateTime threadEndTime = DateTime.Now;
            TimeSpan threadDuration = threadEndTime - threadStartTime;
            Log.Save($"[MessengerWindow] 🏃 Поток запущен за {threadDuration.TotalMilliseconds}ms");
            Log.Save($"[MessengerWindow] 🆔 ID потока: {Thread.ManagedThreadId}");
            Log.Save($"[MessengerWindow] 🔧 Состояние потока: {Thread.ThreadState}");
            
            Log.Save($"[MessengerWindow] 📚 Запускаем загрузку истории сообщений...");
            DateTime historyStartTime = DateTime.Now;
            // Загружаем историю сообщений с сервера
            _ = LoadMessageHistoryAsync();
            DateTime historyEndTime = DateTime.Now;
            TimeSpan historyDuration = historyEndTime - historyStartTime;
            Log.Save($"[MessengerWindow] 📖 LoadMessageHistoryAsync() инициирован за {historyDuration.TotalMilliseconds}ms");
            
            Log.Save($"[MessengerWindow] 💾 Загружаем последний выбранный контакт...");
            DateTime lastContactStartTime = DateTime.Now;
            // Загружаем последний выбранный контакт
            string lastContact = LoadLastSelectedContact();
            DateTime lastContactEndTime = DateTime.Now;
            TimeSpan lastContactDuration = lastContactEndTime - lastContactStartTime;
            Log.Save($"[MessengerWindow] 👥 LoadLastSelectedContact() завершен за {lastContactDuration.TotalMilliseconds}ms");
            
            if (!string.IsNullOrEmpty(lastContact))
            {
                lastSelectedContactName = lastContact;
                Log.Save($"[MessengerWindow] ✅ Последний выбранный контакт восстановлен: {lastContact}");
            }
            else
            {
                Log.Save($"[MessengerWindow] ℹ️ Последний контакт не найден или пуст");
            }
            
            DateTime constructorEndTime = DateTime.Now;
            TimeSpan totalConstructorDuration = constructorEndTime - constructorStartTime;
            Log.Save($"[MessengerWindow] 🏁 КОНСТРУКТОР ЗАВЕРШЕН за {totalConstructorDuration.TotalMilliseconds}ms");
            Log.Save($"[MessengerWindow] ⏰ Время завершения: {constructorEndTime:HH:mm:ss.fff}");
        }

        private void StartLocaltunnel()
        {
            try
            {
                DateTime startTime = DateTime.Now;
                Log.Save($"[StartLocaltunnel] ⏱️ СТАРТ ЗАПУСКА LOCALTUNNEL: {startTime:HH:mm:ss.fff}");
                
                // Получаем информацию о текущих директориях
                string currentDir = Directory.GetCurrentDirectory();
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                Log.Save($"[StartLocaltunnel] 📁 Текущая директория: {currentDir}");
                Log.Save($"[StartLocaltunnel] 📁 Базовая директория приложения: {baseDir}");
                
                // Батник теперь находится в серверной папке
                string serverFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "TebegramServer", "TebegramServer");
                string batchPath = Path.Combine(serverFolder, "start_localtunnel.bat");
                string fullBatchPath = Path.GetFullPath(batchPath);
                
                Log.Save($"[StartLocaltunnel] 🎯 Основной путь к batch-файлу: {fullBatchPath}");
                Log.Save($"[StartLocaltunnel] 🔍 Проверяем существование основного пути...");
                
                bool mainPathExists = File.Exists(fullBatchPath);
                Log.Save($"[StartLocaltunnel] ✅ Основной путь существует: {mainPathExists}");
                
                // Проверяем альтернативные пути, если основной не найден
                if (!mainPathExists)
                {
                    Log.Save($"[StartLocaltunnel] 🔄 Основной путь не найден, проверяем альтернативные...");
                    
                    string[] alternatePaths = {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "TebegramServer", "TebegramServer", "start_localtunnel.bat"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TebegramServer", "TebegramServer", "start_localtunnel.bat"),
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "TebegramServer", "TebegramServer", "start_localtunnel.bat"),
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "TebegramServer", "TebegramServer", "start_localtunnel.bat"),
                        @"c:\Users\makar\Documents\GitHub\Tebegram\Tebegrammmm\TebegramServer\TebegramServer\start_localtunnel.bat"
                    };
                    
                    Log.Save($"[StartLocaltunnel] 📋 Проверяем {alternatePaths.Length} альтернативных путей:");
                    
                    for (int i = 0; i < alternatePaths.Length; i++)
                    {
                        string altPath = alternatePaths[i];
                        string fullAltPath = Path.GetFullPath(altPath);
                        bool altExists = File.Exists(fullAltPath);
                        
                        Log.Save($"[StartLocaltunnel] 📋 Путь {i + 1}: {fullAltPath} - Существует: {altExists}");
                        
                        if (altExists)
                        {
                            batchPath = altPath;
                            fullBatchPath = fullAltPath;
                            Log.Save($"[StartLocaltunnel] 🎯 НАЙДЕН АЛЬТЕРНАТИВНЫЙ ПУТЬ: {fullBatchPath}");
                            break;
                        }
                    }
                }
                
                bool finalPathExists = File.Exists(fullBatchPath);
                Log.Save($"[StartLocaltunnel] 🏁 Финальный путь: {fullBatchPath}, существует: {finalPathExists}");
                
                if (finalPathExists)
                {
                    Log.Save($"[StartLocaltunnel] 🚀 Подготавливаем запуск процесса...");
                    
                    DateTime processStartTime = DateTime.Now;
                    
                    ProcessStartInfo startInfo = new ProcessStartInfo()
                    {
                        FileName = fullBatchPath,
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WindowStyle = ProcessWindowStyle.Normal, // Изменяем на Normal для видимости
                        WorkingDirectory = Path.GetDirectoryName(fullBatchPath)
                    };
                    
                    Log.Save($"[StartLocaltunnel] 📝 Конфигурация процесса:");
                    Log.Save($"[StartLocaltunnel]   - FileName: {startInfo.FileName}");
                    Log.Save($"[StartLocaltunnel]   - WorkingDirectory: {startInfo.WorkingDirectory}");
                    Log.Save($"[StartLocaltunnel]   - UseShellExecute: {startInfo.UseShellExecute}");
                    
                    Log.Save($"[StartLocaltunnel] ⏰ Запускаем процесс в {processStartTime:HH:mm:ss.fff}...");
                    
                    localtunnelProcess = Process.Start(startInfo);
                    
                    DateTime processLaunchedTime = DateTime.Now;
                    TimeSpan launchDuration = processLaunchedTime - processStartTime;
                    
                    if (localtunnelProcess != null)
                    {
                        Log.Save($"[StartLocaltunnel] ✅ Процесс успешно запущен!");
                        Log.Save($"[StartLocaltunnel] 🔢 PID процесса: {localtunnelProcess.Id}");
                        Log.Save($"[StartLocaltunnel] ⏱️ Время запуска процесса: {launchDuration.TotalMilliseconds}ms");
                        Log.Save($"[StartLocaltunnel] 🌐 Ожидаемый публичный URL: http://tebegrammmm.loca.lt");
                        
                        // Даем время на запуск localtunnel
                        Log.Save($"[StartLocaltunnel] ⏳ Ожидаем 5 секунд для полного запуска туннеля...");
                        DateTime waitStartTime = DateTime.Now;
                        
                        Thread.Sleep(5000);
                        
                        DateTime waitEndTime = DateTime.Now;
                        TimeSpan waitDuration = waitEndTime - waitStartTime;
                        Log.Save($"[StartLocaltunnel] ✅ Ожидание завершено за {waitDuration.TotalMilliseconds}ms");
                        
                        // Проверяем, что процесс все еще работает
                        if (!localtunnelProcess.HasExited)
                        {
                            Log.Save($"[StartLocaltunnel] 💚 Процесс локального туннеля активен и работает");
                        }
                        else
                        {
                            Log.Save($"[StartLocaltunnel] ❌ ВНИМАНИЕ: Процесс локального туннеля завершился с кодом: {localtunnelProcess.ExitCode}");
                        }
                        
                        // Запускаем таймер для проверки доступности туннеля
                        Log.Save($"[StartLocaltunnel] 🕐 Запускаем мониторинг туннеля...");
                        StartTunnelMonitoring();
                    }
                    else
                    {
                        Log.Save($"[StartLocaltunnel] ❌ КРИТИЧЕСКАЯ ОШИБКА: Process.Start вернул null!");
                    }
                }
                else
                {
                    Log.Save($"[StartLocaltunnel] ❌ КРИТИЧЕСКАЯ ОШИБКА: Файл start_localtunnel.bat НЕ НАЙДЕН!");
                    Log.Save($"[StartLocaltunnel] 📝 Итоговый проверенный путь: {fullBatchPath}");
                    
                    // Проверяем существование директории
                    string directory = Path.GetDirectoryName(fullBatchPath);
                    bool dirExists = Directory.Exists(directory);
                    Log.Save($"[StartLocaltunnel] 📂 Директория {directory} существует: {dirExists}");
                    
                    if (dirExists)
                    {
                        string[] filesInDir = Directory.GetFiles(directory);
                        Log.Save($"[StartLocaltunnel] 📄 Файлы в директории ({filesInDir.Length}):");
                        foreach (string file in filesInDir)
                        {
                            Log.Save($"[StartLocaltunnel]   - {Path.GetFileName(file)}");
                        }
                    }
                    
                    MessageBox.Show($"Файл start_localtunnel.bat не найден в серверной папке.\n\nОжидаемое расположение: TebegramServer/TebegramServer/start_localtunnel.bat\n\nЗапустите localtunnel вручную на сервере:\nlt --port 5000 --subdomain tebegrammmm", 
                                  "LocalTunnel не найден", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                
                DateTime endTime = DateTime.Now;
                TimeSpan totalDuration = endTime - startTime;
                Log.Save($"[StartLocaltunnel] ⏱️ ОБЩЕЕ ВРЕМЯ ВЫПОЛНЕНИЯ: {totalDuration.TotalMilliseconds}ms");
            }
            catch (Exception ex)
            {
                DateTime errorTime = DateTime.Now;
                Log.Save($"[StartLocaltunnel] ❌ ИСКЛЮЧЕНИЕ в {errorTime:HH:mm:ss.fff}: {ex.GetType().Name}");
                Log.Save($"[StartLocaltunnel] 📄 Сообщение: {ex.Message}");
                Log.Save($"[StartLocaltunnel] 📚 Stack trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    Log.Save($"[StartLocaltunnel] 🔗 Inner exception: {ex.InnerException.Message}");
                }
                
                MessageBox.Show($"Ошибка запуска localtunnel: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartTunnelMonitoring()
        {
            tunnelCheckTimer = new System.Timers.Timer(30000); // Проверяем каждые 30 секунд
            tunnelCheckTimer.Elapsed += async (sender, e) => await CheckTunnelStatus();
            tunnelCheckTimer.AutoReset = true;
            tunnelCheckTimer.Start();
            Log.Save("[TunnelMonitoring] Запущен мониторинг состояния туннеля");
        }

        private async Task CheckTunnelStatus()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    try
                    {
                        HttpResponseMessage response = await client.GetAsync($"{serverAdress}/health");
                        if (response.IsSuccessStatusCode)
                        {
                            Log.Save("[TunnelCheck] Туннель работает нормально");
                        }
                        else
                        {
                            Log.Save($"[TunnelCheck] Проблема с туннелем: {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Save($"[TunnelCheck] Туннель недоступен: {ex.Message}");
                        // При необходимости можно добавить логику переподключения
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[TunnelCheck] Error: {ex.Message}");
            }
        }

        private void LoadStyle()
        {
            LinearGradientBrush LGB = (LinearGradientBrush)this.TryFindResource("ChatBackground");
            ResourceDictionary resourceDictionary = new ResourceDictionary();
            
            LBMessages.Background = LGB;
        }

        private void StartListener()
        {
            try
            {
                // Запускаем HTTP-слушатель на localhost (не требует прав администратора)
                httpListener = new HttpListener();
                string prefix = $"http://localhost:{User.Port}/";
                httpListener.Prefixes.Add(prefix);
                httpListener.Start();
                IsRunning = true;
                Log.Save($"[StartListener] HTTP listener started on localhost:{User.Port}");
            }
            catch (Exception ex)
            {
                Log.Save($"[StartListener] Error: {ex.Message}");
                MessageBox.Show("Ошибка при запуске HTTP-сервера\nПодробнее об ошибке можно узнать в краш логах");
            }
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
            Log.Save($"[LBChats_SelectionChanged] Selected contact: {Contact?.Name} ({Contact?.IPAddress}:{Contact?.Port})");
            
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

        void ReceiveMessage()
        {
            // Теперь сообщения принимаются только через сервер
            // Запускаем периодическую проверку новых сообщений
            _ = StartPeriodicMessageCheck();
        }

        private async Task StartPeriodicMessageCheck()
        {
            try
            {
                Log.Save("[StartPeriodicMessageCheck] Запуск периодической проверки новых сообщений");
                
                while (IsRunning)
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
        }
        
        private async Task CheckForNewMessages()
        {
            try
            {
                using (HttpResponseMessage response = await httpClient.GetAsync($"{serverAdress}/messages/{User.Login}"))
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
                                    return savedDate > DateTime.Now.AddMinutes(-1); // Простая проверка за последнюю минуту
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
        }
        
        private async Task FetchMessagesFromServer()
        {
            try
            {
                // Запрос всех новых сообщений с сервера
                string url = $"{serverAdress}/messages?userId={User.Id}";
                HttpResponseMessage response = await httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    string messagesJson = await response.Content.ReadAsStringAsync();
                    ProcessMessagesFromServer(messagesJson);
                }
                else
                {
                    Log.Save($"[FetchMessagesFromServer] HTTP Error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[FetchMessagesFromServer] Error: {ex.Message}");
            }
        }

        private void ProcessMessagesFromServer(string messagesData)
        {
            if (string.IsNullOrEmpty(messagesData))
                return;
                
            // Обработка полученных сообщений
            string[] messages = messagesData.Split('\n');
            
            foreach (string messageData in messages)
            {
                if (string.IsNullOrEmpty(messageData))
                    continue;
                    
                string[] parts = messageData.Split('▫');
                
                // Ищем контакт по имени пользователя
                string senderUsername = parts[0]; // Предполагается, что первое поле - имя отправителя
                
                foreach (ChatFolder folder in User.ChatsFolders)
                {
                    foreach (Contact contact in folder.Contacts)
                    {
                        if (contact.Username == senderUsername || contact.Name == senderUsername)
                        {
                            // Создаем и добавляем сообщение
                            string messageText = parts[5];
                            string messageTime = parts[3];
                            
                            if (parts[2] == "Text")
                            {
                                Message message = new Message(contact.Name, messageText, messageTime);
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    contact.Messages.Add(message);
                                }));
                            }
                            else if (parts[2] == "File")
                            {
                                Message message = new Message(
                                    contact.Name, 
                                    messageText, 
                                    messageTime, 
                                    MessageType.File);
                                    
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    contact.Messages.Add(message);
                                }));
                            }
                            break;
                        }
                    }
                }
            }
        }

        private async Task<bool> CheckUserOnlineAsync(IPAddress ip, int port)
        {
            try
            {
                DateTime startTime = DateTime.Now;
                Log.Save($"[CheckUserOnline] ⏱️ СТАРТ ПРОВЕРКИ ОНЛАЙН СТАТУСА: {startTime:HH:mm:ss.fff}");
                
                // Проверяем онлайн только через публичный адрес сервера
                string userToCheck = Contact?.Username ?? Contact?.Name ?? "unknown";
                Log.Save($"[CheckUserOnline] 👤 Проверяем пользователя: {userToCheck}");
                Log.Save($"[CheckUserOnline] 📋 Contact.Username: {Contact?.Username ?? "null"}");
                Log.Save($"[CheckUserOnline] 📋 Contact.Name: {Contact?.Name ?? "null"}");
                
                string url = $"{serverAdress}/users/online?user={userToCheck}";
                Log.Save($"[CheckUserOnline] 🌐 URL для проверки: {url}");
                Log.Save($"[CheckUserOnline] 🔗 Адрес сервера: {serverAdress}");
                
                using (var client = new HttpClient())
                {
                    DateTime clientCreateTime = DateTime.Now;
                    TimeSpan clientCreateDuration = clientCreateTime - startTime;
                    Log.Save($"[CheckUserOnline] 🔧 HttpClient создан за {clientCreateDuration.TotalMilliseconds}ms");
                    
                    client.Timeout = TimeSpan.FromMilliseconds(2000);
                    Log.Save($"[CheckUserOnline] ⏰ Таймаут установлен: 2000ms");
                    
                    try
                    {
                        DateTime requestStartTime = DateTime.Now;
                        Log.Save($"[CheckUserOnline] 🚀 Отправляем GET запрос в {requestStartTime:HH:mm:ss.fff}");
                        
                        HttpResponseMessage response = await client.GetAsync(url);
                        
                        DateTime requestEndTime = DateTime.Now;
                        TimeSpan requestDuration = requestEndTime - requestStartTime;
                        Log.Save($"[CheckUserOnline] ⏱️ GET запрос завершен за {requestDuration.TotalMilliseconds}ms");
                        
                        Log.Save($"[CheckUserOnline] 📊 Статус ответа: {(int)response.StatusCode} {response.StatusCode}");
                        Log.Save($"[CheckUserOnline] 🏷️ Reason phrase: {response.ReasonPhrase ?? "пусто"}");
                        
                        // Логируем заголовки ответа
                        Log.Save($"[CheckUserOnline] 📋 Заголовки ответа:");
                        foreach (var header in response.Headers)
                        {
                            Log.Save($"[CheckUserOnline]   {header.Key}: {string.Join(", ", header.Value)}");
                        }
                        
                        if (response.IsSuccessStatusCode)
                        {
                            DateTime readStartTime = DateTime.Now;
                            string responseText = await response.Content.ReadAsStringAsync();
                            DateTime readEndTime = DateTime.Now;
                            TimeSpan readDuration = readEndTime - readStartTime;
                            
                            Log.Save($"[CheckUserOnline] 📖 Чтение ответа завершено за {readDuration.TotalMilliseconds}ms");
                            Log.Save($"[CheckUserOnline] 📄 Текст ответа: '{responseText}'");
                            Log.Save($"[CheckUserOnline] 📏 Длина ответа: {responseText.Length} символов");
                            
                            DateTime parseStartTime = DateTime.Now;
                            bool isOnline = bool.TryParse(responseText, out bool result) && result;
                            DateTime parseEndTime = DateTime.Now;
                            TimeSpan parseDuration = parseEndTime - parseStartTime;
                            
                            Log.Save($"[CheckUserOnline] 🔢 Парсинг bool завершен за {parseDuration.TotalMilliseconds}ms");
                            Log.Save($"[CheckUserOnline] ✅ РЕЗУЛЬТАТ: Пользователь {userToCheck} онлайн: {isOnline}");
                            
                            DateTime totalEndTime = DateTime.Now;
                            TimeSpan totalDuration = totalEndTime - startTime;
                            Log.Save($"[CheckUserOnline] ⏱️ ОБЩЕЕ ВРЕМЯ ПРОВЕРКИ: {totalDuration.TotalMilliseconds}ms");
                            
                            return isOnline;
                        }
                        else
                        {
                            Log.Save($"[CheckUserOnline] ❌ Неуспешный статус код: {response.StatusCode}");
                            
                            // Попытаемся прочитать тело ошибки
                            try
                            {
                                string errorContent = await response.Content.ReadAsStringAsync();
                                Log.Save($"[CheckUserOnline] 📄 Содержимое ошибки: {errorContent}");
                            }
                            catch (Exception readEx)
                            {
                                Log.Save($"[CheckUserOnline] ❌ Не удалось прочитать тело ошибки: {readEx.Message}");
                            }
                        }
                    }
                    catch (TaskCanceledException tcEx)
                    {
                        DateTime timeoutTime = DateTime.Now;
                        TimeSpan timeoutDuration = timeoutTime - startTime;
                        Log.Save($"[CheckUserOnline] ⏰ ТАЙМАУТ через {timeoutDuration.TotalMilliseconds}ms");
                        Log.Save($"[CheckUserOnline] ❌ TaskCanceledException: {tcEx.Message}");
                        
                        if (tcEx.CancellationToken.IsCancellationRequested)
                        {
                            Log.Save($"[CheckUserOnline] 🚫 Запрос был отменен (таймаут)");
                        }
                    }
                    catch (HttpRequestException httpEx)
                    {
                        DateTime httpErrorTime = DateTime.Now;
                        TimeSpan httpErrorDuration = httpErrorTime - startTime;
                        Log.Save($"[CheckUserOnline] 🌐 HTTP ОШИБКА через {httpErrorDuration.TotalMilliseconds}ms");
                        Log.Save($"[CheckUserOnline] ❌ HttpRequestException: {httpEx.Message}");
                        
                        if (httpEx.InnerException != null)
                        {
                            Log.Save($"[CheckUserOnline] 🔗 Inner exception: {httpEx.InnerException.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        DateTime generalErrorTime = DateTime.Now;
                        TimeSpan generalErrorDuration = generalErrorTime - startTime;
                        Log.Save($"[CheckUserOnline] ❌ ОБЩАЯ ОШИБКА через {generalErrorDuration.TotalMilliseconds}ms");
                        Log.Save($"[CheckUserOnline] 📄 Тип исключения: {ex.GetType().Name}");
                        Log.Save($"[CheckUserOnline] 📄 Сообщение: {ex.Message}");
                        Log.Save($"[CheckUserOnline] 📚 Stack trace: {ex.StackTrace}");
                    }
                }
                
                Log.Save($"[CheckUserOnline] ❌ ВОЗВРАЩАЕМ FALSE (по умолчанию)");
                return false;
            }
            catch (Exception ex) 
            { 
                DateTime criticalErrorTime = DateTime.Now;
                Log.Save($"[CheckUserOnline] 💥 КРИТИЧЕСКАЯ ОШИБКА в {criticalErrorTime:HH:mm:ss.fff}");
                Log.Save($"[CheckUserOnline] 📄 Тип: {ex.GetType().Name}");
                Log.Save($"[CheckUserOnline] 📄 Сообщение: {ex.Message}");
                Log.Save($"[CheckUserOnline] 📚 Stack trace: {ex.StackTrace}");
                return false; 
            }
        }

        private async Task SendMessageToUserAsync(Message message)
        {
            try
            {
                DateTime startTime = DateTime.Now;
                Log.Save($"[SendMessageToUser] ⏱️ СТАРТ ОТПРАВКИ СООБЩЕНИЯ: {startTime:HH:mm:ss.fff}");
                Log.Save($"[SendMessageToUser] 👤 Отправитель: {User.Login}");
                Log.Save($"[SendMessageToUser] 📝 Текст сообщения: '{message.Text}'");
                Log.Save($"[SendMessageToUser] 🏷️ Тип сообщения: {message.MessageType}");
                Log.Save($"[SendMessageToUser] ⏰ Время сообщения: {message.Time}");
                
                // Проверяем, что у контакта есть корректные данные
                if (Contact == null)
                {
                    Log.Save($"[SendMessageToUser] ❌ КРИТИЧЕСКАЯ ОШИБКА: Contact is null");
                    MessageBox.Show("Ошибка: не выбран получатель сообщения");
                    return;
                }

                Log.Save($"[SendMessageToUser] 👥 Получатель: {Contact.Name}");
                Log.Save($"[SendMessageToUser] 🆔 Username получателя: {Contact.Username ?? "не указан"}");
                Log.Save($"[SendMessageToUser] ✅ Валидация контакта прошла успешно");

                // Все исходящие сообщения сохраняются со статусом Sent
                message.Status = MessageStatus.Sent;
                Log.Save($"[SendMessageToUser] 🏷️ Устанавливаем статус сообщения: {message.Status}");

                DateTime saveStartTime = DateTime.Now;
                Log.Save($"[SendMessageToUser] 💾 Начинаем сохранение на сервере в {saveStartTime:HH:mm:ss.fff}...");
                
                // Сохраняем сообщение на сервере СРАЗУ - это важно для общей истории
                await SaveMessageToServer(message);
                
                DateTime saveEndTime = DateTime.Now;
                TimeSpan saveDuration = saveEndTime - saveStartTime;
                Log.Save($"[SendMessageToUser] ✅ Сохранение завершено за {saveDuration.TotalMilliseconds}ms");

                // Формируем данные сообщения для отправки через сервер
                string toUserValue = !string.IsNullOrEmpty(Contact.Username) ? Contact.Username : Contact.Name;
                
                var messageData = new
                {
                    fromUser = User.Login,
                    toUser = toUserValue,
                    message = message.Text,
                    timestamp = message.Time,
                    messageType = message.MessageType.ToString(),
                    status = message.Status.ToString()
                };

                Log.Save($"[SendMessageToUser] 📋 Формируем данные для отправки:");
                Log.Save($"[SendMessageToUser]   fromUser: {messageData.fromUser}");
                Log.Save($"[SendMessageToUser]   toUser: {messageData.toUser}");
                Log.Save($"[SendMessageToUser]   message: {messageData.message}");
                Log.Save($"[SendMessageToUser]   timestamp: {messageData.timestamp}");
                Log.Save($"[SendMessageToUser]   messageType: {messageData.messageType}");
                Log.Save($"[SendMessageToUser]   status: {messageData.status}");

                DateTime serializeStartTime = DateTime.Now;
                string json = System.Text.Json.JsonSerializer.Serialize(messageData);
                DateTime serializeEndTime = DateTime.Now;
                TimeSpan serializeDuration = serializeEndTime - serializeStartTime;
                
                Log.Save($"[SendMessageToUser] 📄 JSON сериализация завершена за {serializeDuration.TotalMilliseconds}ms");
                Log.Save($"[SendMessageToUser] 📄 JSON содержимое: {json}");

                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
                Log.Save($"[SendMessageToUser] 📦 StringContent создан с encoding: UTF8, content-type: application/json");

                // Отправляем сообщение через публичный адрес сервера (localtunnel)
                string endpointUrl = $"{serverAdress}/messages/send";
                Log.Save($"[SendMessageToUser] 🌐 Целевой URL: {endpointUrl}");
                Log.Save($"[SendMessageToUser] 🔗 Используемый адрес сервера: {serverAdress}");
                
                try
                {
                    DateTime httpStartTime = DateTime.Now;
                    Log.Save($"[HTTP] 🚀 НАЧИНАЕМ HTTP POST запрос в {httpStartTime:HH:mm:ss.fff}");
                    Log.Save($"[HTTP] 📍 URL: {endpointUrl}");
                    Log.Save($"[HTTP] 📄 Content-Type: application/json");
                    Log.Save($"[HTTP] 📏 Размер тела запроса: {json.Length} символов");
                    
                    using (HttpResponseMessage response = await httpClient.PostAsync(endpointUrl, content))
                    {
                        DateTime httpEndTime = DateTime.Now;
                        TimeSpan httpDuration = httpEndTime - httpStartTime;
                        
                        Log.Save($"[HTTP] ⏱️ HTTP запрос завершен за {httpDuration.TotalMilliseconds}ms");
                        Log.Save($"[HTTP] 📊 Статус ответа: {(int)response.StatusCode} {response.StatusCode}");
                        Log.Save($"[HTTP] 🏷️ Reason phrase: {response.ReasonPhrase ?? "пусто"}");
                        
                        // Читаем содержимое ответа
                        string responseContent = "";
                        try
                        {
                            DateTime readStartTime = DateTime.Now;
                            responseContent = await response.Content.ReadAsStringAsync();
                            DateTime readEndTime = DateTime.Now;
                            TimeSpan readDuration = readEndTime - readStartTime;
                            
                            Log.Save($"[HTTP] 📖 Чтение ответа завершено за {readDuration.TotalMilliseconds}ms");
                            Log.Save($"[HTTP] 📄 Содержимое ответа: {responseContent}");
                            Log.Save($"[HTTP] 📏 Размер ответа: {responseContent.Length} символов");
                        }
                        catch (Exception readEx)
                        {
                            Log.Save($"[HTTP] ❌ Ошибка чтения ответа: {readEx.Message}");
                        }
                        
                        // Проверяем заголовки ответа
                        Log.Save($"[HTTP] 📋 Заголовки ответа:");
                        foreach (var header in response.Headers)
                        {
                            Log.Save($"[HTTP]   {header.Key}: {string.Join(", ", header.Value)}");
                        }
                        
                        if (response.IsSuccessStatusCode)
                        {
                            Log.Save($"[HTTP] ✅ УСПЕХ: Сообщение успешно отправлено пользователю {Contact.Name}");
                        }
                        else
                        {
                            Log.Save($"[HTTP] ❌ ОШИБКА: Не удалось доставить сообщение {Contact.Name}");
                            Log.Save($"[HTTP] 🔢 Код ошибки: {(int)response.StatusCode} {response.StatusCode}");
                            Log.Save($"[HTTP] 📝 Описание: {response.ReasonPhrase}");
                            Log.Save($"[HTTP] 📄 Тело ответа с ошибкой: {responseContent}");
                        }
                    }
                }
                catch (Exception httpEx)
                {
                    Log.Save($"[HTTP] Ошибка отправки: {httpEx.Message}");
                }
                
                // Обновляем интерфейс
                UpdateChatDisplay();
            }
            catch (Exception ex)
            {
                Log.Save($"[SendMessageToUser] Error: {ex.Message}");
            }
        }

        private void SaveMessageToFile(string ContactName, string messageData, bool IsMe = true)
        {
            try
            {
                string[] MessegeData = messageData.Split('▫');
                string MessegeDataInFile = $"{MessegeData[0]}▫{MessegeData[1]}▫{ContactName}▫{MessegeData[2]}▫{MessegeData[3]}▫{MessegeData[4]}▫{MessegeData[5]}";

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

                using (HttpResponseMessage response = await httpClient.PostAsync($"{serverAdress}/messages/save", content))
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

            Message Message = new Message(User.Name, message, DateTime.Now.ToString("hh:mm"), messageType);
            Contact.Messages.Add(Message);
            
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
            
            // Сохраняем старое имя до изменения
            string oldName = Contact.Name;
            
            // Используем специальное окно для редактирования имени
            EditContactNameWindow editWindow = new EditContactNameWindow(Contact);

            if (editWindow.ShowDialog() == true)
            {
                // Изменяем имя контакта на новое
                Contact.ChangeName(editWindow.NewName);
                
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsRunning = false;
            
            if (httpListener != null && httpListener.IsListening)
            {
                httpListener.Stop();
            }
            
            // Останавливаем localtunnel процессы при закрытии
            StopLocaltunnel();
            
            Process.GetCurrentProcess().Kill();
        }

        private void StopLocaltunnel()
        {
            try
            {
                // Останавливаем таймер мониторинга
                if (tunnelCheckTimer != null)
                {
                    tunnelCheckTimer.Stop();
                    tunnelCheckTimer.Dispose();
                }

                // Останавливаем основной процесс localtunnel
                if (localtunnelProcess != null && !localtunnelProcess.HasExited)
                {
                    try
                    {
                        localtunnelProcess.Kill();
                        localtunnelProcess.WaitForExit(2000);
                        Log.Save($"[StopLocaltunnel] Основной процесс localtunnel остановлен");
                    }
                    catch (Exception ex)
                    {
                        Log.Save($"[StopLocaltunnel] Ошибка остановки основного процесса: {ex.Message}");
                    }
                }

                // Находим и останавливаем все процессы localtunnel
                Process[] ltProcesses = Process.GetProcessesByName("lt");
                foreach (Process proc in ltProcesses)
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(2000);
                        Log.Save($"[StopLocaltunnel] Остановлен процесс localtunnel (PID: {proc.Id})");
                    }
                    catch (Exception ex)
                    {
                        Log.Save($"[StopLocaltunnel] Ошибка остановки процесса {proc.Id}: {ex.Message}");
                    }
                }
                
                // Также попробуем остановить процессы node.js (localtunnel работает на Node.js)
                Process[] nodeProcesses = Process.GetProcessesByName("node");
                foreach (Process proc in nodeProcesses)
                {
                    try
                    {
                        // Проверяем, что это процесс localtunnel
                        if (proc.MainModule?.FileName?.Contains("localtunnel") == true || 
                            proc.StartInfo.Arguments?.Contains("localtunnel") == true)
                        {
                            proc.Kill();
                            proc.WaitForExit(2000);
                            Log.Save($"[StopLocaltunnel] Остановлен процесс node localtunnel (PID: {proc.Id})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Save($"[StopLocaltunnel] Ошибка остановки node процесса {proc.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[StopLocaltunnel] Error: {ex.Message}");
            }
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

            using var response = await httpClient.PostAsync($"{serverAdress}/upload", multipar);
            var ResponseText = await response.Content.ReadAsStringAsync();
            this.Dispatcher.Invoke(new Action(() => { SendMessage(Path.GetFileName(filePath), MessageType.File, $"{serverAdress}/upload/{Path.GetFileName(filePath)}"); }));
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
                var fileUrl = $"{serverAdress}/upload/{fileName}";

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

        private async Task LoadMessageHistoryAsync()
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
                
                using (HttpResponseMessage response = await httpClient.GetAsync($"{serverAdress}/messages/{User.Login}"))
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
                                await RestoreMessageFromServer(msgData);
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
                this.Dispatcher.BeginInvoke(new Action(async () => {
                    RestoreLastSelectedContact();
                }));
            }
            catch (Exception ex)
            {
                Log.Save($"[LoadMessageHistory] Error: {ex.Message}");
            }
        }

        private Task RestoreMessageFromServer(Dictionary<string, object> messageData)
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
        }

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

        /// <summary>
        /// Определяет адрес сервера динамически
        /// </summary>
        private static string GetServerAddress()
        {
            try
            {
                Log.Save($"[GetServerAddress] 🔍 Поиск правильного адреса сервера...");
                
                // Список возможных путей к файлу с URL туннеля
                List<string> possiblePaths = new List<string>
                {
                    // В папке сервера (откуда запускается туннель)
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TebegramServer", "TebegramServer", "tunnel_url.txt"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tunnel_url.txt"),
                    
                    // В системных папках
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tebegram", "tunnel_url.txt"),
                    Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? "", "tebegram_tunnel_url.txt"),
                    
                    // В рабочей папке
                    "tunnel_url.txt"
                };

                Log.Save($"[GetServerAddress] 📁 Проверяем {possiblePaths.Count} возможных местоположений файла URL...");

                foreach (string path in possiblePaths)
                {
                    try
                    {
                        string fullPath = Path.GetFullPath(path);
                        Log.Save($"[GetServerAddress] 🔍 Проверяем: {fullPath}");
                        
                        if (File.Exists(fullPath))
                        {
                            string tunnelUrl = File.ReadAllText(fullPath).Trim();
                            if (!string.IsNullOrEmpty(tunnelUrl) && tunnelUrl.StartsWith("http"))
                            {
                                // Приводим к HTTP если пришел HTTPS
                                if (tunnelUrl.StartsWith("https://"))
                                {
                                    tunnelUrl = tunnelUrl.Replace("https://", "http://");
                                    Log.Save($"[GetServerAddress] 🔄 Преобразован HTTPS -> HTTP: {tunnelUrl}");
                                }
                                
                                Log.Save($"[GetServerAddress] ✅ Найден действующий URL туннеля: {tunnelUrl}");
                                return tunnelUrl;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Save($"[GetServerAddress] ⚠️ Ошибка при проверке пути {path}: {ex.Message}");
                    }
                }

                // Если файл не найден, пробуем стандартные адреса
                List<string> fallbackUrls = new List<string>
                {
                    "http://tebegrammmm.loca.lt",
                    "http://tebegram-server.loca.lt", 
                    "http://tebegram-chat.loca.lt",
                    "http://localhost:5000" // Локальный сервер как запасной вариант
                };

                Log.Save($"[GetServerAddress] 🔄 Тестируем запасные адреса...");

                foreach (string url in fallbackUrls)
                {
                    try
                    {
                        Log.Save($"[GetServerAddress] 🌐 Тестируем доступность: {url}");
                        
                        using (var client = new HttpClient())
                        {
                            client.Timeout = TimeSpan.FromSeconds(5);
                            var response = client.GetAsync($"{url}/health").Result;
                            
                            if (response.IsSuccessStatusCode)
                            {
                                Log.Save($"[GetServerAddress] ✅ Сервер доступен по адресу: {url}");
                                return url;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Save($"[GetServerAddress] ❌ {url} недоступен: {ex.Message}");
                    }
                }

                // Если ничего не найдено, возвращаем дефолтный
                string defaultUrl = "http://tebegrammmm.loca.lt";
                Log.Save($"[GetServerAddress] ⚠️ Все попытки неудачны, используем дефолтный: {defaultUrl}");
                return defaultUrl;
            }
            catch (Exception ex)
            {
                Log.Save($"[GetServerAddress] ❌ Критическая ошибка: {ex.Message}");
                return "http://tebegrammmm.loca.lt"; // Дефолтный адрес
            }
        }

        /// <summary>
        /// Уведомляет сервер об открытии чата с пользователем
        /// </summary>
        private async Task NotifyServerOpenChat(string contactName)
        {
            try
            {
                Log.Save($"[NotifyServerOpenChat] 📢 Уведомляем сервер об открытии чата с {contactName}");
                
                var data = new
                {
                    user = User.Login,
                    chatWith = contactName,
                    timestamp = DateTime.Now.ToString("o")
                };

                string json = System.Text.Json.JsonSerializer.Serialize(data);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await httpClient.PostAsync($"{serverAdress}/users/open-chat", content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        Log.Save($"[NotifyServerOpenChat] ✅ Сервер уведомлен об открытии чата с {contactName}");
                    }
                    else
                    {
                        Log.Save($"[NotifyServerOpenChat] ⚠️ Не удалось уведомить сервер: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[NotifyServerOpenChat] ❌ Ошибка уведомления: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновляет время последней активности пользователя на сервере
        /// </summary>
        private async Task UpdateUserLastActivity()
        {
            try
            {
                Log.Save($"[UpdateUserLastActivity] ⏰ Обновляем время последней активности для {User.Login}");
                
                var data = new
                {
                    user = User.Login,
                    lastActivity = DateTime.Now.ToString("o"),
                    action = "message_check"
                };

                string json = System.Text.Json.JsonSerializer.Serialize(data);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await httpClient.PostAsync($"{serverAdress}/users/update-activity", content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        Log.Save($"[UpdateUserLastActivity] ✅ Время активности обновлено");
                    }
                    else
                    {
                        Log.Save($"[UpdateUserLastActivity] ⚠️ Не удалось обновить активность: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[UpdateUserLastActivity] ❌ Ошибка обновления активности: {ex.Message}");
            }
        }
    }
}
