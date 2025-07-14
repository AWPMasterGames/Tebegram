using System;
using System.Net;
using System.Windows;
using Tebegrammmm.Classes;

namespace Tebegrammmm.ChatsFoldersRedactsWindows
{
    public partial class AddContact : Window
    {
        Contact Contact { get; set; }   

        public AddContact(Contact contact, string title)
        {
            InitializeComponent();
            Contact = contact;
        }

        private void Button_Click_Save(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TB_Name.Text) || string.IsNullOrWhiteSpace(TB_IP.Text) || string.IsNullOrWhiteSpace(TB_Port.Text))
            {
                MessageBox.Show("Заполните все поля");
                return;
            }

            Contact.ChangeName(TB_Name.Text);
            Contact.IPAddress = IPAddress.Parse(TB_IP.Text);
            Contact.Port = Convert.ToInt32(TB_Port.Text);

            DialogResult = true;
        }

        private void Button_Click_Cancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}