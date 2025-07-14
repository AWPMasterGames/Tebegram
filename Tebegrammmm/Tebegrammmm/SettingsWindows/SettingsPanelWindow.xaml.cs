using System.Windows;

namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для SettingsPanelWindow.xaml
    /// </summary>
    public partial class SettingsPanelWindow : Window
    {
        User User;
        public SettingsPanelWindow(User user)
        {
            InitializeComponent();
            User = user;
            TBLogin.Text = User.Login;          // Устанавливаем логин
            // Показываем localhost вместо 127.0.0.1 для лучшего UX
            string displayAddress = User.IpAddress.ToString() == "127.0.0.1" ? "localhost" : User.IpAddress.ToString();
            TBIPAdress.Text = displayAddress;
            TBPort.Text = User.Port.ToString();
        }
    }
}
