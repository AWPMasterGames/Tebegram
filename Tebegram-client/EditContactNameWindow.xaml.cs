using System.Windows;
using System.Windows.Input;

namespace Tebegrammmm
{
    public partial class EditContactNameWindow : Window
    {
        public Contact Contact { get; private set; }
        public string NewName { get; private set; }

        /// <summary>Пользователь нажал «Удалить контакт».</summary>
        public bool DeleteRequested { get; private set; }

        public EditContactNameWindow(Contact contact)
        {
            InitializeComponent();
            Contact = contact;
            TBName.Text = contact.Name;
            TBName.Focus();
            TBName.SelectAll();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void TBName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Save();
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"Удалить контакт «{Contact.Name}» и всю переписку с ним?",
                "Удаление контакта",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            DeleteRequested = true;
            this.DialogResult = true;
            this.Close();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(TBName.Text))
            {
                MessageBox.Show("Пожалуйста, введите имя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            NewName = TBName.Text.Trim();
            this.DialogResult = true;
            this.Close();
        }
    }
}
