using System.Windows;

namespace Tebegrammmm
{
    public partial class EditContactNameWindow : Window
    {
        public Contact Contact { get; private set; }
        public string NewName { get; private set; }

        public EditContactNameWindow(Contact contact)
        {
            InitializeComponent();
            Contact = contact;
            TBName.Text = contact.Name;
            TBName.Focus();
            TBName.SelectAll();
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
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

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}