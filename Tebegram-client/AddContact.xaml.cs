using System.Windows;
using System.Windows.Input;

namespace Tebegrammmm
{
    public partial class AddContact : Window
    {
        Contact Contact;

        public AddContact(Contact contact)
        {
            InitializeComponent();
            Contact = contact;
            TBName.Focus();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void TBName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                TBUsername.Focus();
        }

        private void TBUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ChangeContact();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeContact();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ChangeContact()
        {
            if (TBUsername.Text.Trim().Length < 1)
            {
                MessageBox.Show("Укажите имя пользователя");
                return;
            }
            Contact.ChangeName(TBName.Text.Trim());
            Contact.ChangeUsername(TBUsername.Text.Trim());

            this.DialogResult = true;
            this.Close();
        }
    }
}
