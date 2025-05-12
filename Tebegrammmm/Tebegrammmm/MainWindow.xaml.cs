using System.Windows;
using System.Windows.Input;
namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            TBUserLogin.Focus();
        }

        private void Authorization()
        {
            User user = UsersData.Authorize(TBUserLogin.Text, TBUserPassord.Password);
            if (user != null)
            {
                MessengerWindow mw = new MessengerWindow(user);
                mw.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Неверный логин или пароль");
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
    }
}
