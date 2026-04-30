using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tebegrammmm.Classes;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    public partial class MainWindow : Window
    {
        static HttpClient httpClient;


        static MainWindow()
        {
            // Игнорируем ошибки SSL сертификата для localhost (только для разработки!)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            httpClient = new HttpClient(handler);
        }

        public MainWindow()
        {
            ServerData.GetServerAdress();
            InitializeComponent();
            StartConnectingPulse();
            _ = TrackConnectionAsync();
            TBUserLogin.Focus();
            if (File.Exists(AppPaths.UserDataFile))
            {
                string[] data;
                if ((data = File.ReadAllText(AppPaths.UserDataFile).Split('▫')).Length == 2)
                {
                    TBUserLogin.Text = data[0];
                    PBUserPassord.Password = data[1];
                    LoginButton.Focus();
                    // Автоавторизация — после полного показа окна, без блокировки UI.
                    this.Loaded += MainWindow_AutoAuth;
                }
            }
        }

        private void MainWindow_AutoAuth(object sender, RoutedEventArgs e)
        {
            this.Loaded -= MainWindow_AutoAuth;
            this.Dispatcher.BeginInvoke(new Action(Authorization),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private async void Authorization()
        {
            if (string.IsNullOrWhiteSpace(PBUserPassord.Password) || string.IsNullOrWhiteSpace(TBUserLogin.Text))
            {
                MessageBox.Show("Заполните все поля");
                return;
            }
            SetLoginLoading(true);
            try
            {
                if (!ServerData.IsConnected)
                    await ServerData.RetryAsync();
                else
                    await ServerData.Ready;
                string loginEnc = Uri.EscapeDataString(TBUserLogin.Text);
                string passEnc = Uri.EscapeDataString(PBUserPassord.Password);
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/login/{loginEnc}-{passEnc}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(content))
                {
                    Log.Save($"[Authorization] Empty response: status={(int)response.StatusCode}, addr={ServerData.ServerAdress}");
                    MessageBox.Show($"Сервер не вернул ответ (статус {(int)response.StatusCode}).\nАдрес: {ServerData.ServerAdress}\nПроверьте, что сервер запущен.");
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Log.Save($"[Authorization] Error response: status={(int)response.StatusCode}, body={content}");
                    MessageBox.Show($"Ошибка сервера ({(int)response.StatusCode}):\n{content}");
                    return;
                }

                if (content.StartsWith("Пользователь с таким логином не существует") ||
                    content.StartsWith("Неверный пароль") ||
                    content.StartsWith("Ошибка"))
                {
                    MessageBox.Show($"Ошибка авторизации: {content}");
                    return;
                }

                try
                {
                    Log.Save($"[Authorization] Server response received, length={content.Length}");
                    string[] userData = content.Split('▫');
                    Log.Save($"[Authorization] Parsed userData parts: {userData.Length}");
                    User user = new User(int.Parse(userData[0]), userData[1],
                        PBUserPassord.Password,
                        userData[2],
                        userData[3],
                        new ObservableCollection<ChatFolder>{
                            new ChatFolder(userData[5], new ObservableCollection<Contact> {}, userData[6], bool.Parse(userData[7]))},
                        $"{ServerData.ServerAdress}/{userData[4]}"
                    );

                    for (int i = 9; i < userData.Length - 1; i++)
                    {
                        string[] ContactData = userData[i].Split('&');
                        user.ChatsFolders[0].AddContact(new Contact(int.Parse(ContactData[0]), ContactData[1], ContactData[2]));
                    }
                    UserData.User = user;
                    Log.Save($"[Authorization] User object created, opening MessengerWindow");
                    MessengerWindow mw = new MessengerWindow();
                    this.Hide();
                    mw.Show();
                    AppPaths.EnsureDir();
                    File.WriteAllText(AppPaths.UserDataFile, $"{TBUserLogin.Text}▫{PBUserPassord.Password}");
                    this.Close();
                }
                catch (System.Text.Json.JsonException)
                {
                    MessageBox.Show("Ошибка обработки данных сервера");
                }
                catch (Exception ex)
                {
                    Log.Save($"[Authorization] Exception while handling server response: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                    MessageBox.Show($"Ошибка при входе: {ex.Message}\nПодробности в CrashLogs.");
                }
            }
            catch (HttpRequestException ex)
            {
                string inner = ex.InnerException?.Message ?? "нет";
                Log.Save($"[Authorization] Error: {ex.Message} | Inner: {inner} | Addr: {ServerData.ServerAdress}");
                MessageBox.Show($"Ошибка при попытке авторизации\nАдрес: {ServerData.ServerAdress}\n{ex.Message}\n{inner}");
            }
            finally
            {
                SetLoginLoading(false);
            }
        }

        private async void Registration()
        {
            if (string.IsNullOrWhiteSpace(TBUserName.Text))
            {
                MessageBox.Show("Введите имя");
                return;
            }
            if (string.IsNullOrWhiteSpace(TBUserNameLogin.Text))
            {
                MessageBox.Show("Введите логин");
                return;
            }
            if (string.IsNullOrWhiteSpace(PBUserPassword.Password))
            {
                MessageBox.Show("Введите пароль");
                return;
            }
            if (PBUserPassword.Password != PBUserPasswordConfirm.Password)
            {
                MessageBox.Show("Пароли не совпадают");
                return;
            }
            if (PBUserPassword.Password.Length < 4)
            {
                MessageBox.Show("Пароль должен быть не менее 4 символов");
                return;
            }
            if (TBUserNameLogin.Text.Contains("-") || TBUserNameLogin.Text.Contains("▫") || TBUserNameLogin.Text.Contains(" "))
            {
                MessageBox.Show("Логин не может содержать пробелы, дефисы или спецсимволы");
                return;
            }
            if (TBUserNameLogin.Text.Length < 3)
            {
                MessageBox.Show("Логин должен быть не менее 3 символов");
                return;
            }

            SetRegLoading(true);
            try
            {
                if (!ServerData.IsConnected)
                    await ServerData.RetryAsync();
                else
                    await ServerData.Ready;
                string username = TBUserNameLogin.Text.Trim();
                string password = PBUserPassword.Password;
                string name = TBUserName.Text.Trim();

                string url = $"{ServerData.ServerAdress}/register/{Uri.EscapeDataString(username)}-{Uri.EscapeDataString(password)}-{Uri.EscapeDataString(username)}-{Uri.EscapeDataString(name)}";

                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Log.Save($"[Registration] User registered successfully: {username}");

                    // Останавливаем состояние загрузки перед анимацией баннера
                    SetRegLoading(false);

                    // Плавно скрываем форму
                    RegFormPanel.BeginAnimation(UIElement.OpacityProperty,
                        new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(200))));

                    // Показываем баннер успеха
                    RegSuccessBanner.Visibility = Visibility.Visible;
                    var bannerFadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(350)))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    RegSuccessBanner.BeginAnimation(UIElement.OpacityProperty, bannerFadeIn);

                    // Ждём, затем переходим на экран входа
                    await Task.Delay(1800);

                    RegSuccessBanner.Visibility = Visibility.Collapsed;
                    RegSuccessBanner.BeginAnimation(UIElement.OpacityProperty, null);
                    RegFormPanel.BeginAnimation(UIElement.OpacityProperty, null);
                    RegFormPanel.Opacity = 0;

                    LoginGrid.Visibility = Visibility.Visible;
                    RegistrationGrid.Visibility = Visibility.Collapsed;
                    TBUserLogin.Text = username;
                    PBUserPassord.Password = "";
                    TBLoginPassShow.Text = "";
                    TBUserLogin.Focus();
                    AnimateFormEntrance(LoginFormPanel);
                }
                else
                {
                    if (content.Contains("already exists") || content.Contains("уже существует"))
                        MessageBox.Show("Пользователь с таким логином уже существует");
                    else if (content.Contains("должны быть заполнены"))
                        MessageBox.Show("Все поля должны быть заполнены");
                    else
                        MessageBox.Show($"Ошибка регистрации: {content}");
                    Log.Save($"[Registration] Registration error: {content}");
                }
            }
            catch (HttpRequestException ex)
            {
                string inner = ex.InnerException?.Message ?? "нет";
                Log.Save($"[Registration] Error: {ex.Message} | Inner: {inner} | Addr: {ServerData.ServerAdress}");
                MessageBox.Show($"Ошибка регистрации\nАдрес: {ServerData.ServerAdress}\n{ex.Message}\n{inner}");
            }
            catch (Exception ex)
            {
                Log.Save($"[Registration] Unexpected error: {ex.Message}");
                MessageBox.Show("Произошла неожиданная ошибка\nСмотрите краш-логи");
            }
            finally
            {
                SetRegLoading(false);
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (LoginGrid.Visibility == Visibility.Visible)
                {
                    if (PBUserPassord.Visibility == Visibility.Visible)
                        PBUserPassord.Focus();
                    else
                        TBLoginPassShow.Focus();
                }
                else
                {
                    if (sender == TBUserName) TBUserNameLogin.Focus();
                    else if (sender == TBUserNameLogin)
                    {
                        if (PBUserPassword.Visibility == Visibility.Visible)
                            PBUserPassword.Focus();
                        else
                            TBRegPassShow.Focus();
                    }
                }
            }
        }

        private void TBUserPassord_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Authorization();
            }
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender == PBUserPassword)
                {
                    PBUserPasswordConfirm.Focus();
                }
                else if (sender == PBUserPasswordConfirm)
                {
                    Registration();
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //if (RegistrationGrid.Visibility == Visibility.Visible)
            //{
            //    RegistrationGrid.Visibility == Visibility.Hidden
            //    LoginGrid.Visibility = Visibility.Visible;
            //}
            Authorization();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            LoginGrid.Visibility = Visibility.Collapsed;
            RegistrationGrid.Visibility = Visibility.Visible;
            TBUserName.Focus();
            AnimateFormEntrance(RegFormPanel);
        }

        private void DoRegisterButton_Click(object sender, RoutedEventArgs e)
        {
            Registration();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            LoginGrid.Visibility = Visibility.Visible;
            RegistrationGrid.Visibility = Visibility.Collapsed;
            PBUserPassord.Password = "";
            TBLoginPassShow.Text = "";
            TBUserLogin.Focus();
            AnimateFormEntrance(LoginFormPanel);
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // ── Статус подключения ───────────────────────────────────────────────

        private DoubleAnimation _pulseAnim;

        private void StartConnectingPulse()
        {
            _pulseAnim = new DoubleAnimation(1.0, 0.25, new Duration(TimeSpan.FromMilliseconds(700)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            ConnStatusDot.BeginAnimation(UIElement.OpacityProperty, _pulseAnim);
            ConnStatusDotReg.BeginAnimation(UIElement.OpacityProperty, _pulseAnim);
        }

        private async Task TrackConnectionAsync()
        {
            await ServerData.Ready;
            Dispatcher.Invoke(() => ApplyConnectionStatus(ServerData.IsConnected));
        }

        private void ApplyConnectionStatus(bool connected)
        {
            // Останавливаем пульс
            ConnStatusDot.BeginAnimation(UIElement.OpacityProperty, null);
            ConnStatusDotReg.BeginAnimation(UIElement.OpacityProperty, null);
            ConnStatusDot.Opacity = 1;
            ConnStatusDotReg.Opacity = 1;

            var dotColor  = connected
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // зелёный
                : new SolidColorBrush(Color.FromRgb(244, 67, 54));   // красный
            string label  = connected ? "Подключен." : "Нет соединения";

            ConnStatusDot.Fill    = dotColor;
            ConnStatusDotReg.Fill = dotColor;
            ConnStatusText.Text    = label;
            ConnStatusTextReg.Text = label;

            // Лёгкий fade текста
            var fade = new DoubleAnimation(0.4, 1.0, new Duration(TimeSpan.FromMilliseconds(400)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ConnStatusText.BeginAnimation(UIElement.OpacityProperty, fade);
            ConnStatusTextReg.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        // ── Состояние загрузки — логин ───────────────────────────────────────
        private void SetLoginLoading(bool loading)
        {
            LoginButton.Content   = loading ? "Входим…" : "Войти";
            LoginButton.IsEnabled = !loading;
            RegisterButton.IsEnabled = !loading;
            TBUserLogin.IsEnabled     = !loading;
            PBUserPassord.IsEnabled   = !loading;
            TBLoginPassShow.IsEnabled = !loading;
            LoginEyeBtn.IsEnabled     = !loading;

            if (loading)
            {
                var pulse = new DoubleAnimation(1.0, 0.5, new Duration(TimeSpan.FromMilliseconds(650)))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                LoginButton.BeginAnimation(UIElement.OpacityProperty, pulse);
            }
            else
            {
                LoginButton.BeginAnimation(UIElement.OpacityProperty, null);
                LoginButton.Opacity = 1.0;
            }
        }

        // ── Состояние загрузки — регистрация ─────────────────────────────────
        private void SetRegLoading(bool loading)
        {
            DoRegisterButton.Content   = loading ? "Создаём аккаунт…" : "Зарегистрироваться";
            DoRegisterButton.IsEnabled = !loading;
            BackButton.IsEnabled           = !loading;
            TBUserName.IsEnabled           = !loading;
            TBUserNameLogin.IsEnabled      = !loading;
            PBUserPassword.IsEnabled       = !loading;
            TBRegPassShow.IsEnabled        = !loading;
            RegEyeBtn.IsEnabled            = !loading;
            PBUserPasswordConfirm.IsEnabled   = !loading;
            TBRegConfirmPassShow.IsEnabled    = !loading;
            RegConfirmEyeBtn.IsEnabled        = !loading;

            if (loading)
            {
                var pulse = new DoubleAnimation(1.0, 0.5, new Duration(TimeSpan.FromMilliseconds(650)))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                DoRegisterButton.BeginAnimation(UIElement.OpacityProperty, pulse);
            }
            else
            {
                DoRegisterButton.BeginAnimation(UIElement.OpacityProperty, null);
                DoRegisterButton.Opacity = 1.0;
            }
        }

        // ── Анимация появления формы (fade + slide up) ──────────────────────
        private void AnimateFormEntrance(System.Windows.Controls.StackPanel panel)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = new Duration(TimeSpan.FromMilliseconds(380));

            ((TranslateTransform)panel.RenderTransform).Y = 12;
            panel.Opacity = 0;

            var opAnim = new DoubleAnimation(0, 1, dur) { EasingFunction = ease };
            var trAnim = new DoubleAnimation(12, 0, dur) { EasingFunction = ease };

            panel.BeginAnimation(UIElement.OpacityProperty, opAnim);
            ((TranslateTransform)panel.RenderTransform).BeginAnimation(TranslateTransform.YProperty, trAnim);
        }

        // ── Анимация появления элемента (fade in) ───────────────────────────
        private void AnimateFadeIn(UIElement element)
        {
            var anim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(150)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        // ── Кнопка показа пароля — логин ─────────────────────────────────────
        private void LoginEyeBtn_Click(object sender, RoutedEventArgs e)
        {
            bool isShowing = TBLoginPassShow.Visibility == Visibility.Visible;
            if (isShowing)
            {
                PBUserPassord.Password = TBLoginPassShow.Text;
                PBUserPassord.Visibility = Visibility.Visible;
                AnimateFadeIn(PBUserPassord);
                TBLoginPassShow.Visibility = Visibility.Collapsed;
                LoginEyePath.Data = (Geometry)FindResource("IconEye");
            }
            else
            {
                TBLoginPassShow.Text = PBUserPassord.Password;
                TBLoginPassShow.Visibility = Visibility.Visible;
                AnimateFadeIn(TBLoginPassShow);
                PBUserPassord.Visibility = Visibility.Collapsed;
                LoginEyePath.Data = (Geometry)FindResource("IconEyeOff");
            }
        }

        private void TBLoginPassShow_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => PBUserPassord.Password = TBLoginPassShow.Text;

        private void TBLoginPassShow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Authorization();
        }

        // ── Общее состояние видимости паролей на экране регистрации ─────────
        private bool _regPasswordVisible = false;

        // Один обработчик на обе кнопки-глаза в форме регистрации:
        // переключает видимость СРАЗУ для обоих полей (пароль + подтверждение).
        private void RegEyeBtn_Click(object sender, RoutedEventArgs e) => ToggleRegPassword();
        private void RegConfirmEyeBtn_Click(object sender, RoutedEventArgs e) => ToggleRegPassword();

        private void ToggleRegPassword()
        {
            _regPasswordVisible = !_regPasswordVisible;

            if (_regPasswordVisible)
            {
                // Показать оба пароля
                TBRegPassShow.Text = PBUserPassword.Password;
                TBRegPassShow.Visibility = Visibility.Visible;
                AnimateFadeIn(TBRegPassShow);
                PBUserPassword.Visibility = Visibility.Collapsed;

                TBRegConfirmPassShow.Text = PBUserPasswordConfirm.Password;
                TBRegConfirmPassShow.Visibility = Visibility.Visible;
                AnimateFadeIn(TBRegConfirmPassShow);
                PBUserPasswordConfirm.Visibility = Visibility.Collapsed;

                RegEyePath.Data = (Geometry)FindResource("IconEyeOff");
                RegConfirmEyePath.Data = (Geometry)FindResource("IconEyeOff");
            }
            else
            {
                // Скрыть оба пароля
                PBUserPassword.Password = TBRegPassShow.Text;
                PBUserPassword.Visibility = Visibility.Visible;
                AnimateFadeIn(PBUserPassword);
                TBRegPassShow.Visibility = Visibility.Collapsed;

                PBUserPasswordConfirm.Password = TBRegConfirmPassShow.Text;
                PBUserPasswordConfirm.Visibility = Visibility.Visible;
                AnimateFadeIn(PBUserPasswordConfirm);
                TBRegConfirmPassShow.Visibility = Visibility.Collapsed;

                RegEyePath.Data = (Geometry)FindResource("IconEye");
                RegConfirmEyePath.Data = (Geometry)FindResource("IconEye");
            }
        }

        private void TBRegPassShow_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => PBUserPassword.Password = TBRegPassShow.Text;

        private void TBRegPassShow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (PBUserPasswordConfirm.Visibility == Visibility.Visible)
                    PBUserPasswordConfirm.Focus();
                else
                    TBRegConfirmPassShow.Focus();
            }
        }

        private void TBRegConfirmPassShow_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => PBUserPasswordConfirm.Password = TBRegConfirmPassShow.Text;

        // ── Появление/скрытие кнопок-глаз в зависимости от наличия текста ──
        private void PBUserPassord_PasswordChanged(object sender, RoutedEventArgs e)
            => FadeEyeButton(LoginEyeBtn, PBUserPassord.Password.Length > 0);

        private void PBUserPassword_PasswordChanged(object sender, RoutedEventArgs e)
            => FadeEyeButton(RegEyeBtn, PBUserPassword.Password.Length > 0);

        private void PBUserPasswordConfirm_PasswordChanged(object sender, RoutedEventArgs e)
            => FadeEyeButton(RegConfirmEyeBtn, PBUserPasswordConfirm.Password.Length > 0);

        private void FadeEyeButton(System.Windows.Controls.Button btn, bool show)
        {
            // Кликабельность переключаем мгновенно, чтобы во время fade-out
            // нельзя было случайно попасть по полупрозрачной кнопке.
            btn.IsHitTestVisible = show;

            var anim = new DoubleAnimation(show ? 1.0 : 0.0,
                                           new Duration(TimeSpan.FromMilliseconds(150)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            btn.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void TBRegConfirmPassShow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Registration();
        }
    }
}
