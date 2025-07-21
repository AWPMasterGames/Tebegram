using System.Windows;

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
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (TBName.Text.Trim().Length < 1)
            {
                MessageBox.Show("Укажите Имя");
                return;
            }
            if (TBUsername.Text.Trim().Length < 1)
            {
                MessageBox.Show("Укажите имя пользователя");
                return;
            }
            Contact.ChangeName(TBUsername.Text.Trim());
            Contact.ChangeUsername(TBUsername.Text.Trim());
                
            this.DialogResult = true;
            this.Close();
        }
    }
}
