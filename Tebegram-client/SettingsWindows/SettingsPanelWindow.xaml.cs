using NAudio.CoreAudioApi;
using System.IO;
using System.Threading;
using System.Windows;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    public partial class SettingsPanelWindow : Window
    {
        public SettingsPanelWindow()
        {
            InitializeComponent();
            TBUsername.Text = UserData.User.Username;
            UserInfo.DataContext = UserData.User;
            CheckInputDevices();
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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
                File.Create("userDevice.data").Close();
            File.WriteAllText("userDevice.data", $"{UserData.User.SelectedDeviceNum}");
        }
    }
}
