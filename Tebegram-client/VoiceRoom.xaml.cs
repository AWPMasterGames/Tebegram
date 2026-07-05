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
        private TimeSpan _callDuration;

        public VoiceRoom(Mode mode, Contact contact, string token)
        {
            InitializeComponent();

            // Если окно уже открыто — вывести его на передний план
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
            this.DataContext = contact;

            switch (mode)
            {
                case Mode.AcceptCall:
                    DefoultVoiceRoom.Visibility = Visibility.Visible;
                    ActiveVoiceRoom.Visibility = Visibility.Collapsed;
                    break;
                case Mode.ActiveCall:
                    Init();
                    DefoultVoiceRoom.Visibility = Visibility.Collapsed;
                    ActiveVoiceRoom.Visibility = Visibility.Visible;
                    break;
            }
        }

        Thread SendVoiceThread;
        Thread ReceiveVoiceThread;

        private ClientWebSocket ws;
        private WaveInEvent waveIn;
        private WaveOutEvent waveOut = new WaveOutEvent();

        byte[] buffer;
        BufferedWaveProvider waveProvider;

        private async void Init()
        {

            ws = new ClientWebSocket();
            waveIn = new WaveInEvent();
            waveIn.DeviceNumber = UserData.User.SelectedDeviceNum;
            // Общий формат для всех платформ (ПК/веб/Android): PCM 16 бит, 48 кГц, моно.
            // Это родная частота браузера и Android — звук совместим между устройствами.
            waveIn.WaveFormat = new WaveFormat(48000, 16, 1);
            waveIn.BufferMilliseconds = 20; // короткие пакеты — меньше задержка

            waveProvider = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
            {
                BufferDuration = TimeSpan.FromSeconds(3),
                DiscardOnBufferOverflow = true
            };
            buffer = new byte[8192];

            waveOut.Init(waveProvider);

            waveIn.DataAvailable += async (s, e) =>
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.SendAsync(new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded), WebSocketMessageType.Binary, true, CancellationToken.None);
                        //Console.WriteLine($"Send {} Bytes");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }

                    //Thread.Sleep(200);
                    //Console.WriteLine(e.Buffer[0]);
                }
            };

            string wsAddress = ServerData.ServerAdress.Replace("https:", "wss:").Replace("http:", "ws:");
            await ws.ConnectAsync(new Uri($"{wsAddress}/Voice/ws?userId={UserData.User.Id}&roomToken={Token}"),
                CancellationToken.None);

            IsMicrophoneOn = true;
            StartCallTimer();
            StartSVT();
            StartRVT();
        }

        private void StartCallTimer()
        {
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
            SendVoiceThread = new Thread(() =>
            {
                bool isOn = false;
                while (!_callEnded)
                {
                    if (IsMicrophoneOn != isOn)
                    {
                        if (IsMicrophoneOn) waveIn.StartRecording();
                        else waveIn.StopRecording();
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
                waveOut.Play();
                ReceiveVoice();
            }) { IsBackground = true };
            ReceiveVoiceThread.Start();
        }

        private async void ReceiveVoice()
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
                    // Обрыв связи — выходим из цикла, не роняя приложение
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
                    switch (message)
                    {
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
            Init();
            AnimateToActive();
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Дубликат окна (когда звонок уже открыт) не должен сбрасывать состояние реального звонка
            if (_instance != null && _instance != this) return;

            _callEnded = true;
            StopCallTimer();
            try
            {
                waveIn?.StopRecording();
                waveOut?.Stop();
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
            UserData.User.InCall = false;
        }

        private async void Button_Click_Decline(object sender, RoutedEventArgs e)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/Voice/DeclineCall/{UserData.User.Id}-{Token}");
            using HttpResponseMessage response = await httpClient.SendAsync(request);
            string Content = await response.Content.ReadAsStringAsync();

            this.Close();
        }

        private void Button_Click_OffOnMicrofon(object sender, RoutedEventArgs e)
        {
            IsMicrophoneOn = !IsMicrophoneOn;
            AnimateMicToggle(muting: !IsMicrophoneOn);
        }

        private void AnimateMicToggle(bool muting)
        {
            const double SlashLength = 26.0;
            var duration = new Duration(TimeSpan.FromMilliseconds(220));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Черта: рисуем при мьюте (offset 26→0), стираем при разблокировке (0→26)
            var fromOffset = muting ? SlashLength : 0.0;
            var toOffset   = muting ? 0.0 : SlashLength;

            MicSlashLine.BeginAnimation(Shape.StrokeDashOffsetProperty,
                new DoubleAnimation(fromOffset, toOffset, duration) { EasingFunction = ease });
            MicAvatarSlashLine.BeginAnimation(Shape.StrokeDashOffsetProperty,
                new DoubleAnimation(fromOffset, toOffset, duration) { EasingFunction = ease });

            // Фон кнопки и бейджа: серый ↔ мягко-красный
            var mutedBg  = (Brush)FindResource("Light.DangerMutedBrush");
            var normalBg = (Brush)FindResource("Light.BgElevatedBrush");
            BtnMic.Background          = muting ? mutedBg : normalBg;
            MicButtonBorder.Background = muting ? mutedBg : normalBg;

            // Цвет иконки и черты: обычный ↔ красный
            var iconColor = muting
                ? (Brush)FindResource("Light.DangerBrush")
                : (Brush)FindResource("Light.TextPrimaryBrush");
            MicIconPath.Fill          = iconColor;
            MicAvatarPath.Fill        = iconColor;
            MicSlashLine.Stroke       = iconColor;
            MicAvatarSlashLine.Stroke = iconColor;
        }
    }
}
