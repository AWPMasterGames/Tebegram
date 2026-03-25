using NAudio.CoreAudioApi;
using System.IO;
using System.Threading;
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

            CheckInputDevices();
        }

        private void Button_Exit_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private async void CheckInputDevices()
        {
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            InputDeviceCB.ItemsSource = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            InputDeviceCB.SelectedIndex = UserData.User.SelectedDeviceNum == null ? 0 : UserData.User.SelectedDeviceNum;
            Thread.Sleep(100);
        }

        private void InputDeviceCB_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UserData.User.SelectedDeviceNum = InputDeviceCB.SelectedIndex;
            if (!File.Exists("userDevice.data"))
            {
                File.Create("userDevice.data").Close();
            }
            File.WriteAllText("userDevice.data", $"{UserData.User.SelectedDeviceNum}");
        }
    }
}
