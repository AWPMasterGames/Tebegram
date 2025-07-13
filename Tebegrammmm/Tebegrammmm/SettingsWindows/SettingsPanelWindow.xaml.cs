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
            TBIPAdress.Text = User.IpAddress.ToString();
            TBPort.Text = User.Port.ToString();
        }
    }
}
