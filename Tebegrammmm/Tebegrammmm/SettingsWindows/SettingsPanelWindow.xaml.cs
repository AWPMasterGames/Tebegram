using System.Windows;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для SettingsPanelWindow.xaml
    /// </summary>
    public partial class SettingsPanelWindow : Window
    {
        public SettingsPanelWindow()
        {
            InitializeComponent();
            //TBLogin.Text = User.Login;          // Устанавливаем логин
            // Показываем localhost вместо 127.0.0.1 для лучшего UX
            string displayAddress = UserData.User.Username;
            TBUsername.Text = displayAddress;


            UserInfo.DataContext = UserData.User;

        }

        private void Button_Exit_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
