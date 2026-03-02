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
        static HttpClient httpClient = new HttpClient();
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
                // Проверяем, не является ли ответ ошибкой
                if (content.StartsWith("Пользователь с таким логином не существует") ||
                    content.StartsWith("Неверный пароль") ||
                    content.StartsWith("Ошибка"))
                {
                    MessageBox.Show($"Ошибка авторизации: {content}");
                    return;
                }

                try
                {
                    // Пытаемся распарсить JSON с данными пользователя
                    string[] userData = content.Split('▫');

                    // Создаем пользователя на основе данных с сервера

                    User user = new User(int.Parse(userData[0]), userData[1],
                        PBUserPassord.Password, // Пароль не передается с сервера по безопасности
                        userData[2],
                        userData[3],
                        new ObservableCollection<ChatFolder>{
                            new ChatFolder(userData[5], new ObservableCollection<Contact> {},userData[6],bool.Parse(userData[7]))},
                        $"{ServerData.ServerAdress}/{userData[4]}"

                    );

                    for (int i = 9; i < userData.Length - 1; i++)
                    {
                        string[] ContactData = userData[i].Split('&');
                        user.ChatsFolders[0].AddContact(new Contact(int.Parse(ContactData[0]), ContactData[1], ContactData[2]));
                    }

                    MessengerWindow mw = new MessengerWindow(user);
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
            }
            catch (HttpRequestException ex)
            {
                Log.Save($"[Authorization] Error: {ex.Message}");
                MessageBox.Show("Ошибка при попытке авторизации\nПодробнее от ошибке можно узнать в краш логах");
                return; // Добавляем return, чтобы прекратить выполнение
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PBUserPassord.Focus();
            }
        }

        private void TBUserPassord_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Authorization();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Authorization();
        }
        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            RegistrationWindow registrationWindow = new RegistrationWindow();
            registrationWindow.Show();
            this.Close();
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