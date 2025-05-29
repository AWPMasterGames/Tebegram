using System.Windows;
using System.Windows.Input;
using System.Net.Http;
using System;
using Tebegrammmm.Classes;



namespace Tebegrammmm
{
    public partial class MainWindow : Window
    {
        static HttpClient httpClient = new HttpClient();
        string serverAdress = "https://localhost:7034";
        public MainWindow()
        {
            InitializeComponent();
            TBUserLogin.Focus();
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

                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{serverAdress}/login/{TBUserLogin.Text}-{PBUserPassord.Password}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();
                if (content == "Succes")
                {
                    MessengerWindow mw = new MessengerWindow(UsersData.FindUser(TBUserLogin.Text));
                    mw.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show($"{content}");
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Save($"[Authorization] Error: {ex.Message}");
                MessageBox.Show("Ошибка при попытке авторизации\nПодробнее от ошибке можно узнать в краш логах");
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