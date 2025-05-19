using System.Windows;
using System.Windows.Input;
using System.Net.Http;



namespace Tebegrammmm
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            TBUserLogin.Focus();
        }

        private void Authorization()
        {
            string login = TBUserLogin.Text.Trim();
            string password = TBUserPassord.Password.Trim();

            User user = UsersData.Authorize(login, password);
            if (user != null)
            {
                MessengerWindow mw = new MessengerWindow(user);
                mw.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Неверный логин или пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                TBUserPassord.Clear();
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TBUserPassord.Focus();
            }
        }

        private void TBUserPassord_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Authorization();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Authorization();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}