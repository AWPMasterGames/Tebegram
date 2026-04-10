using System.Windows;
using System.Windows.Input;

namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для AddContact.xaml
    /// </summary>
    public partial class AddContact : Window
    {
        Contact Contact;
        public AddContact(Contact contact)
        {
            InitializeComponent();
            this.Title = "Добавить контакт";
            TBTitle.Text = "Добавить контакт";
            Contact = contact;
            TBName.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
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
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ChangeContact();
        }

        private void TBUsername_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == Key.Enter) ChangeContact();
        }
    }
}
