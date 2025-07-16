using System;
using System.Windows;
using System.Net;

namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для AddContact.xaml
    /// </summary>
    public partial class AddContact : Window
    {
        Contact Contact;
        
        public AddContact(Contact contact, string title)
        {
            InitializeComponent();
            this.Title = title;
            TBTitle.Text = title;
            Contact = contact;
            
            if (contact.Name != string.Empty)
            {
                TBName.Text = contact.Name;
            }
            if (contact.Username != null)
            {
                TBIPAdress.Text = contact.Username;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (TBName.Text.Length < 1)
            {
                MessageBox.Show("Укажите Имя");
                return;
            }
            
            Contact.ChangeName(TBName.Text);

            this.DialogResult = true;
            this.Close();
        }
    }
}
