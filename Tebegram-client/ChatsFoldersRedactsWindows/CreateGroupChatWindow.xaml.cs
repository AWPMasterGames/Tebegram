using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Windows;
using Tebegrammmm.Data;

namespace Tebegrammmm.ChatsFoldersRedactsWindows
{
    /// <summary>
    /// Поверхностный UI создания ГРУППОВОГО чата (стилистика — как у создания папки):
    /// название + перекидывание контактов между «все» и «в группе».
    ///
    /// ЛОГИКИ СОЗДАНИЯ ЗДЕСЬ НЕТ — её пишет Максим. Окно только собирает данные:
    /// после ShowDialog() == true доступны GroupName и SelectedUsernames.
    /// Серверная часть уже готова: GET /Chat/Create/{userId}-{usernames},
    /// где usernames — логины через ▫ (три и больше участников => группа,
    /// владелец — создатель). Ответ: id&name&isGroup&avatar&ownerId.
    /// Клиент также уже понимает WS-команду «addChat▫$▫…» (см. HandleAddChat).
    /// </summary>
    public partial class CreateGroupChatWindow : Window
    {
        static HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });
        /// <summary>Название группы (после успешного закрытия окна).</summary>
        public string GroupName { get; private set; } = string.Empty;

        /// <summary>Логины выбранных участников (без самого себя — сервер добавит создателя).</summary>
        public string[] SelectedUsernames { get; private set; } = System.Array.Empty<string>();

        private readonly ObservableCollection<Contact> _allContacts = new();
        private readonly ObservableCollection<Contact> _members = new();

        public CreateGroupChatWindow()
        {
            InitializeComponent();

            // Все контакты, кроме «Избранного» (чат с собой в группу не зовём)
            foreach (Contact contact in UserData.User.Contacts)
            {
                if (!contact.IsFavorites) _allContacts.Add(contact);
            }

            LBMyContacts.ItemsSource = _allContacts;
            LBGroupMembers.ItemsSource = _members;
        }

        // Клик слева — контакт уходит в группу
        private void LBMyContacts_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LBMyContacts.SelectedItem is not Contact contact) return;
            _allContacts.Remove(contact);
            _members.Add(contact);
            LBMyContacts.SelectedItem = null;
        }

        // Клик справа — контакт возвращается в общий список
        private void LBGroupMembers_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LBGroupMembers.SelectedItem is not Contact contact) return;
            _members.Remove(contact);
            _allContacts.Add(contact);
            LBGroupMembers.SelectedItem = null;
        }

        private async void CreateBtn_Click(object sender, RoutedEventArgs e)
        {
            string name = TBoxGroupName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowValidation("Введи название группы");
                return;
            }
            // Группа = минимум 3 участника вместе с создателем, значит выбрать нужно хотя бы двоих
            if (_members.Count < 2)
            {
                ShowValidation("Выбери хотя бы двух участников");
                return;
            }

            GroupName = name;
            SelectedUsernames = _members.Select(m => m.Username).ToArray();

            // Окно закрываем ТОЛЬКО после успешного ответа: раньше запрос уходил
            // «в никуда» (async void без обработки), окно закрывалось сразу, и при
            // ошибке пользователь ничего не узнавал
            CreateBtn.IsEnabled = false;
            bool ok = await SendCreateGroupRequestAsync();
            CreateBtn.IsEnabled = true;
            if (!ok) return;

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Создаёт группу на сервере. Название передаём отдельным параметром запроса
        /// (?name=…), а не в пути: в пути разделителем служит дефис, и название с
        /// дефисом сдвинуло бы разбор — так уже рождались мусорные аккаунты.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> SendCreateGroupRequestAsync()
        {
            try
            {
                string members = string.Join('▫', SelectedUsernames);
                string url = $"{ServerData.ServerAdress}/Chat/Create/{UserData.User.Id}-" +
                             $"{Uri.EscapeDataString(members)}?name={Uri.EscapeDataString(GroupName)}";

                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    ShowValidation(string.IsNullOrWhiteSpace(content)
                        ? "Сервер не смог создать группу" : content);
                    Classes.Log.Save($"[CreateGroup] Отказ сервера {(int)response.StatusCode}: {content}");
                    return false;
                }

                Classes.Log.Save($"[CreateGroup] Группа создана: {content}");
                return true;
            }
            catch (Exception ex)
            {
                // async void без try/catch ронял приложение при недоступном сервере
                Classes.Log.Save($"[CreateGroup] {ex.GetType().Name}: {ex.Message}");
                ShowValidation("Нет связи с сервером — группа не создана");
                return false;
            }
        }

        private void ShowValidation(string text)
        {
            TBValidation.Text = text;
            TBValidation.Visibility = Visibility.Visible;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }
    }
}
