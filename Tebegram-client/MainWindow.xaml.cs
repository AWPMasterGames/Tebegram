using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Input;
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

        Thread AutoAuthThread;

        public MainWindow()
        {
            ServerData.GetServerAdress();
            InitializeComponent();
            TBUserLogin.Focus();
            if (File.Exists("user.data"))
            {
                string[] data;
                if ((data = File.ReadAllText("user.data").Split('▫')).Length == 2)
                {
                    TBUserLogin.Text = data[0];
                    PBUserPassord.Password = data[1];
                    LoginButton.Focus();
                    AutoAuthThread = new Thread(() => { AutoAuth(); });
                    AutoAuthThread.Start();
                }
            }
        }

        private async void AutoAuth()
        {
            Thread.Sleep(100);
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                Authorization();
            }));
        }

        private async void Authorization()
        {
            if (string.IsNullOrWhiteSpace(PBUserPassord.Password) || string.IsNullOrWhiteSpace(TBUserLogin.Text))
            {
                MessageBox.Show("Заполните все поля");
                return;
            }
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/login/{TBUserLogin.Text}-{PBUserPassord.Password}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();

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
                    if (!File.Exists("user.data"))
                    {
                        File.Create("user.data").Close();
                    }
                    File.WriteAllText("user.data", $"{TBUserLogin.Text}▫{PBUserPassord.Password}");
                    this.Close();
                }
                catch (System.Text.Json.JsonException)
                {
                    MessageBox.Show("Ошибка обработки данных сервера");
                    return;
                }
                catch (Exception ex)
                {
                    Log.Save($"[Authorization] Exception while handling server response: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                    MessageBox.Show($"Ошибка при входе: {ex.Message}\nПодробности в CrashLogs.");
                    return;
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Save($"[Authorization] Error: {ex.Message}");
                MessageBox.Show("Ошибка при попытке авторизации\nПодробнее от ошибке можно узнать в краш логах");
                return;
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

            try
            {
                string username = TBUserNameLogin.Text.Trim();
                string password = PBUserPassword.Password;
                string name = TBUserName.Text.Trim();

                string url = $"{ServerData.ServerAdress}/register/{username}-{password}-{username}-{name}";

                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Регистрация успешна! Теперь можно войти.");
                    Log.Save($"[Registration] User registered successfully: {username}");

                    // Переключаемся на экран логина
                    LoginGrid.Visibility = Visibility.Visible;
                    RegistrationGrid.Visibility = Visibility.Hidden;
                    TBUserLogin.Text = username;
                    TBUserLogin.Focus();
                }
                else
                {
                    if (content.Contains("already exists") || content.Contains("уже существует"))
                    {
                        MessageBox.Show("Пользователь с таким логином уже существует");
                    }
                    else if (content.Contains("должны быть заполнены"))
                    {
                        MessageBox.Show("Все поля должны быть заполнены");
                    }
                    else
                    {
                        MessageBox.Show($"Ошибка регистрации: {content}");
                    }
                    Log.Save($"[Registration] Registration error: {content}");
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Save($"[Registration] Error: {ex.Message}");
                MessageBox.Show("Ошибка регистрации\nПроверьте подключение к серверу\nСмотрите краш-логи");
            }
            catch (Exception ex)
            {
                Log.Save($"[Registration] Unexpected error: {ex.Message}");
                MessageBox.Show("Произошла неожиданная ошибка\nСмотрите краш-логи");
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (LoginGrid.Visibility == Visibility.Visible)
                {
                    PBUserPassord.Focus();
                }
                else
                {
                    if (sender == TBUserName) TBUserNameLogin.Focus();
                    else if (sender == TBUserNameLogin) PBUserPassword.Focus();
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
            // Переключаемся на экран регистрации
            LoginGrid.Visibility = Visibility.Hidden;
            this.Width = 400;
            this.Height = 500;
            RegistrationGrid.Visibility = Visibility.Visible;
            TBUserName.Focus();

        }

        private void DoRegisterButton_Click(object sender, RoutedEventArgs e)
        {
            Registration();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Возвращаемся на экран логина
            LoginGrid.Visibility = Visibility.Visible;
            this.Width = 400;
            this.Height = 400;
            RegistrationGrid.Visibility = Visibility.Hidden;
            TBUserLogin.Focus();
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
    }
}
