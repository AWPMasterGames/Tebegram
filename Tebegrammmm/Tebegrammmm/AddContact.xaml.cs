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
            if (contact.IPAddress != null)
            {
                TBIPAdress.Text = contact.IPAddress.ToString();
            }
            TBPort.Text = contact.Port.ToString();
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
            
            if (Convert.ToInt32(TBPort.Text) < 1500)
            {
                MessageBox.Show("Минимальное значение для порта 1500");
                return;
            }
            
            try
            {
                Contact.ChangeIpAdress(IPAddress.Parse(TBIPAdress.Text));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
            
            Contact.ChangeName(TBName.Text);
            Contact.ChangePort(Convert.ToInt32(TBPort.Text));

            this.DialogResult = true;
            this.Close();
        }
    }
}
