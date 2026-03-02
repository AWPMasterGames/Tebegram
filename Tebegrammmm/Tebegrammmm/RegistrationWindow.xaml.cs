using System;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using Tebegrammmm.Classes;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    public partial class RegistrationWindow : Window
    {
        static HttpClient httpClient;

        static RegistrationWindow()
        {
            // Игнорируем ошибки SSL сертификата для localhost (только для разработки!)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            httpClient = new HttpClient(handler);
        }

        public RegistrationWindow()
        {
            InitializeComponent();
            TBUserName.Focus();
        }

        private async void Registration()
        {
            if (string.IsNullOrWhiteSpace(TBUserName.Text))
            {
                MessageBox.Show("Enter your name");
                return;
            }

            if (string.IsNullOrWhiteSpace(TBUserLogin.Text))
            {
                MessageBox.Show("Enter login");
                return;
            }

            if (string.IsNullOrWhiteSpace(PBUserPassword.Password))
            {
                MessageBox.Show("Enter password");
                return;
            }

            if (PBUserPassword.Password != PBUserPasswordConfirm.Password)
            {
                MessageBox.Show("Passwords do not match");
                return;
            }

            if (PBUserPassword.Password.Length < 4)
            {
                MessageBox.Show("Password must be at least 4 characters");
                return;
            }
            //Валидация логина
            if (TBUserLogin.Text.Contains("-") || TBUserLogin.Text.Contains("▫") || TBUserLogin.Text.Contains(" "))
            {
                MessageBox.Show("Username cannot contain spaces, dashes or special characters");
                return;
            }

            if (TBUserLogin.Text.Length < 3)
            {
                MessageBox.Show("Username must be at least 3 characters");
                return;
            }

            try
            {
                string username = TBUserLogin.Text.Trim();
                string password = PBUserPassword.Password;
                string name = TBUserName.Text.Trim();

                string url = $"{ServerData.ServerAdress}/register/{username}-{password}-{username}-{name}";

                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                using HttpResponseMessage response = await httpClient.SendAsync(request);

                string content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Registration successful! You can now log in.");
                    Log.Save($"[Registration] User registered successfully: {TBUserLogin.Text}");
                    
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    if (content.Contains("already exists") || content.Contains("уже существует"))
                    {
                        MessageBox.Show("User with this username already exists");
                    }
                    else if (content.Contains("должны быть заполнены"))
                    {
                        MessageBox.Show("All fields must be filled");
                    }
                    else
                    {
                        MessageBox.Show($"Registration error: {content}");
                    }
                    Log.Save($"[Registration] Registration error: {content}");
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Save($"[Registration] Error: {ex.Message}");
                MessageBox.Show("Registration error\nCheck server connection\nSee crash logs for details");
            }
            catch (Exception ex)
            {
                Log.Save($"[Registration] Unexpected error: {ex.Message}");
                MessageBox.Show("Unexpected error occurred\nSee crash logs for details");
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender == TBUserName)
                {
                    TBUserLogin.Focus();
                }
                else if (sender == TBUserLogin)
                {
                    PBUserPassword.Focus();
                }
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

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            Registration();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                Log.Save($"[RegistrationWindow] Error opening MainWindow: {ex.Message}");
                MessageBox.Show($"Error opening login window: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
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
