using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using Tebegrammmm.Classes;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    public enum Mode
    {
        AcceptCall,
        ActiveCall
    }
    public partial class VoiceRoom : Window
    {
        private static VoiceRoom _instance;

        static HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });
        private string Token { get; set; }
        private Contact Contact { get; set; }
        private bool IsMicrophoneOn { get; set; }

        private DispatcherTimer _callTimer;

        // Сторож входящего звонка: пока трубку не взяли, WebSocket не открыт (см. Init),
        // поэтому служебное "CloseConnection" до нас дойти не может. Если звонивший
        // отменил вызов, окно висело бы бесконечно - поэтому опрашиваем свой CallToken.
        private DispatcherTimer _incomingWatchdog;
        private bool _watchdogBusy;
        private TimeSpan _callDuration;

        /// <summary>
        /// Вызывается по завершении звонка у ЗВОНИВШЕГО (contact, длительность).
        /// MessengerWindow подписывается и пишет в чат «📞 Аудиозвонок (м:сс)».
        /// Только звонивший - чтобы сообщение не дублировалось с двух сторон.
        /// </summary>
        public static Action<Contact, TimeSpan> CallEnded;

        private bool _isCaller;

        public VoiceRoom(Mode mode, Contact contact, string token)
        {
            InitializeComponent();

            // Если окно уже открыто - вывести его на передний план
            if (_instance != null)
            {
                _instance.Activate();
                if (_instance.WindowState == WindowState.Minimized)
                    _instance.WindowState = WindowState.Normal;
                this.Loaded += (_, __) => this.Close();
                return;
            }

            _instance = this;
            Contact = contact;
            Token = token;
            _isCaller = mode == Mode.ActiveCall;
            this.DataContext = contact;

            switch (mode)
            {
                case Mode.AcceptCall:
                    DefoultVoiceRoom.Visibility = Visibility.Visible;
                    ActiveVoiceRoom.Visibility = Visibility.Collapsed;
                    // Входящий звонок: звука пока нет, поэтому вся сигнализация - 
                    // визуальная. Окно выносим на передний план по центру монитора.
                    Loaded += (_, __) => { AnnounceIncomingCall(); StartIncomingWatchdog(); };
                    break;
                case Mode.ActiveCall:
                    Init();
                    DefoultVoiceRoom.Visibility = Visibility.Collapsed;
                    ActiveVoiceRoom.Visibility = Visibility.Visible;
                    break;
            }
        }

        // ── Сигнализация о входящем звонке ───────────────────────────────────

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_ALL = 3;      // мигать и заголовком, и кнопкой в панели задач
        private const uint FLASHW_TIMERNOFG = 12; // мигать, пока окно не станет активным

        /// <summary>
        /// Выводит окно входящего звонка по центру монитора, поверх прочих окон и
        /// с миганием кнопки в панели задач.
        ///
        /// Центрирование выполняется здесь, а не через WindowStartupLocation:
        /// у окна задано SizeToContent="Height", поэтому высота становится известна
        /// только после показа и штатное центрирование даёт смещение.
        ///
        /// Свойство Topmost необходимо, поскольку Windows не позволяет фоновому
        /// приложению перехватить фокус: вызов Activate приводит лишь к миганию
        /// кнопки в панели задач. После принятия звонка свойство снимается в
        /// AnimateToActive, чтобы окно разговора не перекрывало остальные.
        /// </summary>
        private void AnnounceIncomingCall()
        {
            try
            {
                Classes.UiSizes.CenterOnScreen(this);

                Topmost = true;
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                Activate();

                var info = new FLASHWINFO
                {
                    hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle,
                    dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                    uCount = uint.MaxValue,
                    dwTimeout = 0
                };
                info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(info);
                FlashWindowEx(ref info);
            }
            catch (Exception ex)
            {
                // Не смогли привлечь внимание - не повод ронять звонок
                Log.Save($"[VoiceRoom.AnnounceIncomingCall] {ex.GetType().Name}: {ex.Message}");
            }
        }

        Thread SendVoiceThread;
        Thread ReceiveVoiceThread;

        private ClientWebSocket ws;
        private WaveInEvent waveIn;
        // Создаётся в Init вместе с проверкой наличия устройства: раньше объект жил в поле
        // и waveOut.Init/Play падали на машине без колонок/наушников, роняя приложение
        private WaveOutEvent waveOut;

        byte[] buffer;
        BufferedWaveProvider waveProvider;

        /// <summary>
        /// Запуск звонка. Обёртка над InitAsync: это async void (обработчик кнопки),
        /// поэтому НИ ОДНО исключение не должно из него вылететь - необработанное
        /// исключение в async void убивает всё приложение. Так и падало в аудитории
        /// при принятии звонка на машинах без микрофона/наушников.
        /// </summary>
        private async void Init()
        {
            try
            {
                await InitAsync();
            }
            catch (Exception ex)
            {
                Log.Save($"[VoiceRoom.Init] {ex.GetType().Name}: {ex.Message}");
                TbgDialogWindow.Show("Не удалось начать звонок. Проверь подключение к серверу и звуковые устройства.", "Звонок");
                try { Close(); } catch { }
            }
        }

        private async Task InitAsync()
        {
            ws = new ClientWebSocket();

            // Приёмный буфер нужен всегда - в него пишет ReceiveVoice, даже если
            // воспроизводить нечем (тогда звук просто отбрасывается)
            waveProvider = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
            {
                BufferDuration = TimeSpan.FromSeconds(3),
                DiscardOnBufferOverflow = true
            };
            buffer = new byte[8192];

            SetupMicrophone();
            SetupSpeaker();

            // Совсем без звука звонок бессмысленен - честно говорим об этом и выходим
            if (waveIn == null && waveOut == null)
            {
                TbgDialogWindow.Show("Не найдено ни микрофона, ни устройства воспроизведения. " +
                                     "Подключи наушники или гарнитуру и попробуй снова.", "Звонок");
                Close();
                return;
            }

            string wsAddress = ServerData.ServerAdress.Replace("https:", "wss:").Replace("http:", "ws:");
            await ws.ConnectAsync(new Uri($"{wsAddress}/Voice/ws?userId={UserData.User.Id}&roomToken={Token}"),
                CancellationToken.None);

            IsMicrophoneOn = waveIn != null;
            // Таймер НЕ стартует здесь: подключился только наш сокет, собеседник
            // мог ещё не взять трубку. До его прихода показываем «Соединяем…»,
            // отсчёт начнётся по событию CallConnected от сервера.
            CallTimeText.Text = "Соединяем…";
            StartSVT();
            StartRVT();
        }

        /// <summary>
        /// Микрофон: устройств может не быть вовсе, а сохранённый индекс - указывать
        /// на отключённую гарнитуру (после переподключения нумерация WaveIn меняется).
        /// Без микрофона звонок продолжается в режиме «только слушать».
        /// </summary>
        private void SetupMicrophone()
        {
            try
            {
                if (WaveInEvent.DeviceCount == 0)
                {
                    Log.Save("[VoiceRoom] Микрофон не найден - звонок только на приём");
                    TbgDialogWindow.Show("Микрофон не найден - собеседник тебя не услышит. " +
                                         "Слышать его ты сможешь.", "Звонок");
                    return;
                }

                // Сначала ищем сохранённое устройство ПО ИМЕНИ (индекс мог сдвинуться),
                // и только если не нашли - берём первое доступное
                int deviceNumber = Classes.AudioDevices.FindByName(UserData.User.SelectedDeviceName);
                if (deviceNumber < 0 || deviceNumber >= WaveInEvent.DeviceCount)
                    deviceNumber = 0;

                waveIn = new WaveInEvent { DeviceNumber = deviceNumber };
                // Общий формат для всех платформ (ПК/веб/Android): PCM 16 бит, 48 кГц, моно.
                // Это родная частота браузера и Android - звук совместим между устройствами.
                waveIn.WaveFormat = new WaveFormat(48000, 16, 1);
                waveIn.BufferMilliseconds = 20; // короткие пакеты - меньше задержка

                // Шумоподавление + нормализация исходящего звука (как в веб-клиенте)
                var dsp = new VoiceDsp(48000);
                waveIn.DataAvailable += async (s, e) =>
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        try
                        {
                            dsp.Process(e.Buffer, e.BytesRecorded);
                            await ws.SendAsync(new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded), WebSocketMessageType.Binary, true, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            Log.Save($"[VoiceRoom.Send] {ex.Message}");
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                // Устройство занято другим приложением или отвалилось между проверкой и открытием
                Log.Save($"[VoiceRoom.SetupMicrophone] {ex.GetType().Name}: {ex.Message}");
                waveIn = null;
                TbgDialogWindow.Show("Не удалось включить микрофон - возможно, он занят другим приложением. " +
                                     "Звонок продолжится без него.", "Звонок");
            }
        }

        /// <summary>
        /// Воспроизведение. Без устройства вывода звонок продолжается «только на передачу»,
        /// раньше waveOut.Init на такой машине ронял приложение.
        /// </summary>
        private void SetupSpeaker()
        {
            try
            {
                if (WaveOut.DeviceCount == 0)
                {
                    Log.Save("[VoiceRoom] Устройство воспроизведения не найдено - звонок только на передачу");
                    TbgDialogWindow.Show("Устройство воспроизведения не найдено - ты не услышишь собеседника. " +
                                         "Он тебя услышит.", "Звонок");
                    return;
                }

                waveOut = new WaveOutEvent();
                waveOut.Init(waveProvider);
            }
            catch (Exception ex)
            {
                Log.Save($"[VoiceRoom.SetupSpeaker] {ex.GetType().Name}: {ex.Message}");
                try { waveOut?.Dispose(); } catch { }
                waveOut = null;
                TbgDialogWindow.Show("Не удалось включить воспроизведение звука. " +
                                     "Звонок продолжится без него.", "Звонок");
            }
        }

        private void StartCallTimer()
        {
            if (_callTimer != null) return; // сервер мог прислать CallConnected повторно

            _callDuration = TimeSpan.Zero;
            CallTimeText.Text = "00:00";

            _callTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _callTimer.Tick += (s, e) =>
            {
                _callDuration = _callDuration.Add(TimeSpan.FromSeconds(1));
                CallTimeText.Text = _callDuration.Hours > 0
                    ? _callDuration.ToString(@"h\:mm\:ss")
                    : _callDuration.ToString(@"mm\:ss");
            };
            _callTimer.Start();
        }

        /// <summary>
        /// Пока показывается входящий звонок, узнать об отмене можно только опросом:
        /// сервер обнуляет CallToken обеих сторон (ClearCallTokens) при завершении звонка.
        /// Пропал токен - звонивший отменил вызов, закрываем окно.
        /// </summary>
        private void StartIncomingWatchdog()
        {
            if (_incomingWatchdog != null) return;
            _incomingWatchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _incomingWatchdog.Tick += async (s, e) => await CheckIncomingStillAliveAsync();
            _incomingWatchdog.Start();
        }

        private void StopIncomingWatchdog()
        {
            _incomingWatchdog?.Stop();
            _incomingWatchdog = null;
        }

        private async Task CheckIncomingStillAliveAsync()
        {
            // Не наслаиваем запросы и не трогаем уже завершённый звонок
            if (_watchdogBusy || _callEnded || _incomingWatchdog == null) return;
            _watchdogBusy = true;
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/Voice/GetCallToken/{UserData.User.Id}?platform=win");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return; // сервер моргнул - окно не трогаем
                string content = await response.Content.ReadAsStringAsync();

                if (content == "NotFound" || !content.Contains(Token))
                {
                    StopIncomingWatchdog();
                    try { this.Close(); } catch { }
                }
            }
            catch (Exception ex)
            {
                // Сервер недоступен - это не повод сбрасывать звонок, просто ждём следующей попытки
                Log.Save($"[VoiceRoom.Watchdog] {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _watchdogBusy = false;
            }
        }

        private void StopCallTimer()
        {
            _callTimer?.Stop();
            _callTimer = null;
            _callDuration = TimeSpan.Zero;
            CallTimeText.Text = "00:00";
        }


        private volatile bool _callEnded = false;

        private void StartSVT()
        {
            if (waveIn == null) return; // без микрофона переключать нечего

            SendVoiceThread = new Thread(() =>
            {
                bool isOn = false;
                while (!_callEnded)
                {
                    if (IsMicrophoneOn != isOn)
                    {
                        // Устройство могут выдернуть прямо во время разговора - исключение
                        // в фоновом потоке роняет приложение целиком, поэтому глушим здесь
                        try
                        {
                            if (IsMicrophoneOn) waveIn.StartRecording();
                            else waveIn.StopRecording();
                        }
                        catch (Exception ex)
                        {
                            Log.Save($"[VoiceRoom.SVT] {ex.GetType().Name}: {ex.Message}");
                        }
                        isOn = IsMicrophoneOn;
                    }
                    // Раньше цикл крутился без задержки и съедал целое ядро процессора,
                    // а MessageBox при каждом переключении блокировал поток
                    Thread.Sleep(50);
                }
            }) { IsBackground = true };

            SendVoiceThread.Start();
        }

        private void StartRVT()
        {
            ReceiveVoiceThread = new Thread(() =>
            {
                // waveOut == null - устройства вывода нет, слушаем сокет вхолостую
                // (иначе звонок оборвался бы у обеих сторон)
                try { waveOut?.Play(); }
                catch (Exception ex) { Log.Save($"[VoiceRoom.RVT] {ex.GetType().Name}: {ex.Message}"); }
                ReceiveVoice();
            }) { IsBackground = true };
            ReceiveVoiceThread.Start();
        }

        private async void ReceiveVoice()
        {
            try
            {
                await ReceiveVoiceLoop();
            }
            catch (Exception ex)
            {
                // async void: без этого catch любая ошибка приёма убивала приложение
                Log.Save($"[VoiceRoom.ReceiveVoice] {ex.GetType().Name}: {ex.Message}");
            }
        }

        private async Task ReceiveVoiceLoop()
        {
            while (ws.State == WebSocketState.Open && !_callEnded)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // Обрыв связи - выходим из цикла, не роняя приложение
                    Log.Save($"[VoiceRoom.ReceiveVoice] {ex.Message}");
                    break;
                }
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    //Console.WriteLine(buffer[0]);
                    waveProvider.AddSamples(buffer, 0, result.Count);
                }
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    // Микрофон СОБЕСЕДНИКА: значок рядом с его аватаром
                    if (message.StartsWith("MIC:"))
                    {
                        bool peerMuted = message == "MIC:0";
                        Dispatcher.Invoke(new Action(() => SetPeerMicMuted(peerMuted)));
                        continue;
                    }

                    switch (message)
                    {
                        // В комнате стало двое - разговор состоялся, включаем отсчёт
                        case "CallConnected":
                            Dispatcher.Invoke(new Action(() =>
                            {
                                StartCallTimer();
                                // Заодно сообщаем собеседнику своё состояние микрофона:
                                // он мог подключиться позже, чем мы его выключили
                                _ = SendMicStateAsync();
                            }));
                            break;

                        case "CloseConnection":
                            Dispatcher.Invoke(new Action(() =>
                            {
                                this.Close();
                            }));
                            break;
                    }
                }
            }
        }

        private void AnimateToActive()
        {
            // Звонок приняли - окно больше не должно висеть поверх всех остальных
            Topmost = false;

            var easeIn  = new CubicEase { EasingMode = EasingMode.EaseIn };
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var exitDuration  = new Duration(TimeSpan.FromMilliseconds(220));
            var enterDuration = new Duration(TimeSpan.FromMilliseconds(320));

            // --- Выход: кнопки DefaultVoiceRoom уезжают вниз + весь блок гаснет ---
            DefaultButtonsPanelTransform.Y = 0;
            DefaultButtonsPanel.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, exitDuration));
            DefaultButtonsPanelTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, 60, exitDuration) { EasingFunction = easeIn });

            var fadeOutRoom = new DoubleAnimation(1, 0, exitDuration);
            fadeOutRoom.Completed += (s, e) =>
            {
                DefoultVoiceRoom.Visibility = Visibility.Collapsed;
                DefoultVoiceRoom.Opacity    = 1; // сброс на случай повторного показа

                // Сброс панельного трансформа (используется в AnimateClose)
                ButtonsPanelTransform.Y = 0;

                // --- Вход: ActiveVoiceRoom появляется ---
                ActiveVoiceRoom.Opacity = 0;
                ActiveVoiceRoom.Visibility = Visibility.Visible;
                ActiveVoiceRoom.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, enterDuration));

                // --- Поочерёдное выплывание каждой кнопки снизу ---
                var buttons = new[] { BtnScreenShare, BtnCamera, BtnHangup, BtnMic, BtnAddUser };
                for (int i = 0; i < buttons.Length; i++)
                {
                    var btn   = buttons[i];
                    var delay = TimeSpan.FromMilliseconds(i * 50);
                    var tf    = new TranslateTransform { Y = 60 };
                    btn.RenderTransform = tf;
                    btn.Opacity = 0;

                    tf.BeginAnimation(TranslateTransform.YProperty,
                        new DoubleAnimation(60, 0, enterDuration)
                        {
                            EasingFunction = easeOut,
                            BeginTime      = delay
                        });
                    btn.BeginAnimation(OpacityProperty,
                        new DoubleAnimation(0, 1, enterDuration) { BeginTime = delay });
                }
            };
            DefoultVoiceRoom.BeginAnimation(OpacityProperty, fadeOutRoom);
        }

        private void AnimateClose()
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var duration = new Duration(TimeSpan.FromMilliseconds(250));

            var slideDown = new DoubleAnimation(0, 70, duration) { EasingFunction = ease };
            ButtonsPanelTransform.BeginAnimation(TranslateTransform.YProperty, slideDown);

            ActiveButtonsPanel.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, duration));

            var fadeOut = new DoubleAnimation(1, 0, duration);
            fadeOut.Completed += (s, e) => this.Close();
            ActiveVoiceRoom.BeginAnimation(OpacityProperty, fadeOut);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (_instance == this) _instance = null;
            base.OnClosed(e);
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            AnimateClose();
        }

        private void Button_Click_Accept(object sender, RoutedEventArgs e)
        {
            // Трубку взяли: дальше о завершении узнаём по "CloseConnection" из WebSocket
            StopIncomingWatchdog();
            Init();
            AnimateToActive();
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Дубликат окна (когда звонок уже открыт) не должен сбрасывать состояние реального звонка
            if (_instance != null && _instance != this) return;

            _callEnded = true;
            // Запоминаем длительность ДО сброса таймера - для сообщения в чат
            TimeSpan callLength = _callDuration;
            StopCallTimer();
            StopIncomingWatchdog();
            try
            {
                waveIn?.StopRecording();
                waveOut?.Stop();
                // Освобождаем устройства: без Dispose микрофон оставался занятым нашим
                // процессом, и следующий звонок (или другое приложение) его не получал
                waveIn?.Dispose();
                waveOut?.Dispose();
                waveIn = null;
                waveOut = null;
            }
            catch (Exception ex)
            {
                Log.Save($"[VoiceRoom.Closing] {ex.Message}");
            }
            try
            {
                if (ws != null && ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.Save($"[VoiceRoom.Closing] {ex.Message}");
            }
            try
            {
                // Сообщаем серверу о завершении звонка при ЛЮБОМ закрытии окна (крестик,
                // завершение вызова), а не только по кнопке «Отклонить» - иначе токены звонка
                // зависают на сервере: у собеседника бесконечно всплывает входящий звонок,
                // а у звонившего падает опрос GetCallToken. Повторный вызов безвреден.
                using HttpRequestMessage declineRequest = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/Voice/DeclineCall/{UserData.User.Id}-{Token}");
                using HttpResponseMessage declineResponse = await httpClient.SendAsync(declineRequest);
            }
            catch (Exception ex)
            {
                Log.Save($"[VoiceRoom.Closing] DeclineCall: {ex.Message}");
            }
            UserData.User.InCall = false;

            // Сообщение о звонке в чат пишет только ЗВОНИВШИЙ (без дублей)
            if (_isCaller)
            {
                try { CallEnded?.Invoke(Contact, callLength); }
                catch (Exception ex) { Log.Save($"[VoiceRoom.CallEnded] {ex.Message}"); }
            }
        }

        private async void Button_Click_Decline(object sender, RoutedEventArgs e)
        {
            // async void: недоступный сервер (обрыв связи, туннель выключен) кидал здесь
            // HttpRequestException - и приложение падало прямо при отклонении звонка.
            // Окно закрываем в любом случае: Window_Closing сам повторит DeclineCall
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/Voice/DeclineCall/{UserData.User.Id}-{Token}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Log.Save($"[VoiceRoom.Decline] {ex.GetType().Name}: {ex.Message}");
            }

            try { this.Close(); } catch { }
        }

        private void Button_Click_OffOnMicrofon(object sender, RoutedEventArgs e)
        {
            IsMicrophoneOn = !IsMicrophoneOn;
            AnimateOwnMicToggle(muting: !IsMicrophoneOn);
            _ = SendMicStateAsync();
        }

        /// <summary>
        /// Сообщает собеседнику состояние СВОЕГО микрофона (MIC:1 / MIC:0).
        /// Сервер перешлёт это остальным в комнате, и у них обновится значок
        /// рядом с нашим аватаром.
        /// </summary>
        private async Task SendMicStateAsync()
        {
            try
            {
                if (ws == null || ws.State != WebSocketState.Open) return;
                byte[] payload = Encoding.UTF8.GetBytes(IsMicrophoneOn ? "MIC:1" : "MIC:0");
                await ws.SendAsync(new ArraySegment<byte>(payload),
                    WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.Save($"[VoiceRoom.SendMicState] {ex.GetType().Name}: {ex.Message}");
            }
        }

        // ── Значки микрофона ─────────────────────────────────────────────────
        // Значков два, и отражают они разные состояния. Нижняя кнопка BtnMic
        // управляет собственным микрофоном. Отметка у аватара MicButtonBorder
        // показывает микрофон собеседника и отвечает на вопрос, слышит ли он нас.
        //
        // Ранее одно нажатие меняло оба значка, поэтому отметка повторяла
        // собственное состояние и о собеседнике ничего не сообщала.

        /// <summary>Свой микрофон: нижняя кнопка.</summary>
        private void AnimateOwnMicToggle(bool muting)
            => ApplyMicVisual(muting, MicSlashLine, MicIconPath, b => BtnMic.Background = b);

        /// <summary>Микрофон собеседника: бейдж рядом с его аватаром.</summary>
        private void SetPeerMicMuted(bool muted)
            => ApplyMicVisual(muted, MicAvatarSlashLine, MicAvatarPath, b => MicButtonBorder.Background = b);

        // Фон передаётся сеттером: снизу это Button, у аватара - Border, общего
        // предка со свойством Background у них нет
        private void ApplyMicVisual(bool muted, Line slash, Path icon, Action<Brush> setBackground)
        {
            const double SlashLength = 26.0;
            var duration = new Duration(TimeSpan.FromMilliseconds(220));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Черта: рисуем при мьюте (offset 26→0), стираем при включении (0→26)
            slash.BeginAnimation(Shape.StrokeDashOffsetProperty,
                new DoubleAnimation(muted ? SlashLength : 0.0, muted ? 0.0 : SlashLength, duration)
                { EasingFunction = ease });

            // Фон: серый ↔ мягко-красный
            setBackground(muted
                ? (Brush)FindResource("Light.DangerMutedBrush")
                : (Brush)FindResource("Light.BgElevatedBrush"));

            // Цвет иконки и черты: обычный ↔ красный
            var iconColor = muted
                ? (Brush)FindResource("Light.DangerBrush")
                : (Brush)FindResource("Light.TextPrimaryBrush");
            icon.Fill = iconColor;
            slash.Stroke = iconColor;
        }
    }
}
