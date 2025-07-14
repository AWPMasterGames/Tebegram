using System.Windows;
using System.Windows.Input;
using System.Net.Http;
using System;
using System.Collections.ObjectModel;
using System.Net;
using Tebegrammmm.Classes;



namespace Tebegrammmm
{
    public partial class MainWindow : Window
    {
        static HttpClient httpClient = new HttpClient();
        string serverAdress = "http://localhost:5000";
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
                    var userJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
                    
                    // Создаем пользователя на основе данных с сервера
                    string userIpAddress = userJson.GetProperty("IpAddress").GetString() ?? "127.0.0.1";
                    // Преобразуем localhost в IP для внутреннего использования
                    if (userIpAddress.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                    {
                        userIpAddress = "127.0.0.1";
                    }
                    
                    User user = new User(
                        userJson.GetProperty("Id").GetInt32(),
                        userJson.GetProperty("Login").GetString() ?? "",
                        PBUserPassord.Password, // Пароль не передается с сервера по безопасности
                        userJson.GetProperty("Name").GetString() ?? "",
                        userIpAddress,
                        userJson.GetProperty("Port").GetInt32(),
                        new ObservableCollection<ChatFolder>()
                    );
                    
                    // Добавляем папки чатов из данных сервера
                    if (userJson.TryGetProperty("ChatsFolders", out var foldersJson))
                    {
                        foreach (var folderJson in foldersJson.EnumerateArray())
                        {
                            var contacts = new ObservableCollection<Contact>();
                            
                            if (folderJson.TryGetProperty("Contacts", out var contactsJson))
                            {
                                foreach (var contactJson in contactsJson.EnumerateArray())
                                {
                                    contacts.Add(new Contact(
                                        IPAddress.Parse(contactJson.GetProperty("IPAddress").GetString() ?? "127.0.0.1"),
                                        contactJson.GetProperty("Port").GetInt32(),
                                        contactJson.GetProperty("Name").GetString() ?? ""
                                    ));
                                }
                            }
                            
                            user.ChatsFolders.Add(new ChatFolder(
                                folderJson.GetProperty("FolderName").GetString() ?? "Все чаты",
                                contacts,
                                folderJson.GetProperty("Icon").GetString() ?? "💬",
                                folderJson.GetProperty("IsCanRedact").GetBoolean()
                            ));
                        }
                    }
                    
                    MessengerWindow mw = new MessengerWindow(user);
                    mw.Show();
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