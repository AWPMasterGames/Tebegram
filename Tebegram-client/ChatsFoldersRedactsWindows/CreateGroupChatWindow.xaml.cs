using System.Collections.ObjectModel;
using System.Linq;
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

        private void CreateBtn_Click(object sender, RoutedEventArgs e)
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

            // TODO(Максим): здесь вызвать создание группы на сервере —
            // GET {ServerData.ServerAdress}/Chat/Create/{UserData.User.Id}-{string.Join('▫', SelectedUsernames)}
            // — и обработать ответ (или дождаться WS «addChat▫$▫…»).
            // Название группы (GroupName) сервер пока не принимает — в его CreateChat
            // имя собирается из имён участников; поле уже есть в UI на вырост.

            DialogResult = true;
            Close();
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
