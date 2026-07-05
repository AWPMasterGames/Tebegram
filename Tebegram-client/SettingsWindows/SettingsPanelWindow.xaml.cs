using Microsoft.Win32;
using NAudio.CoreAudioApi;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Windows;
using Tebegrammmm.Classes;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    public partial class SettingsPanelWindow : Window
    {
        static HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });

        public SettingsPanelWindow()
        {
            InitializeComponent();
            TBUsername.Text = UserData.User.Username;
            TBVersion.Text = $"Tebegram {UpdateChecker.CurrentVersion}";
            UserInfo.DataContext = UserData.User;
            CheckInputDevices();
        }

        private async void ChangeAvatar_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Выбери новый аватар"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                using var multipart = new MultipartFormDataContent();
                var fileContent = new StreamContent(File.OpenRead(dlg.FileName));
                string mime = MIME.GetMimeType(Path.GetExtension(dlg.FileName));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(mime == "application/octet-stream" ? "image/png" : mime);
                multipart.Add(fileContent, "file", Path.GetFileName(dlg.FileName));

                using var response = await httpClient.PostAsync($"{ServerData.ServerAdress}/avatars/{UserData.User.Id}", multipart);
                string stored = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(stored))
                {
                    UserData.User.Avatar = $"{ServerData.ServerAdress}/avatars/{stored}";
                    // Перепривязываем DataContext, чтобы аватар обновился на экране
                    UserInfo.DataContext = null;
                    UserInfo.DataContext = UserData.User;
                    Log.Save($"[Settings] Аватар обновлён: {stored}");
                }
                else
                {
                    MessageBox.Show("Не удалось загрузить аватар. Проверь соединение с сервером.");
                }
            }
            catch (System.Exception ex)
            {
                Log.Save($"[Settings.ChangeAvatar] {ex.Message}");
                MessageBox.Show("Не удалось загрузить аватар. Проверь соединение с сервером.");
            }
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            await UpdateChecker.CheckAsync(notifyIfLatest: true);
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

        private void CheckInputDevices()
        {
            MMDeviceCollection DeviceCollector = (new MMDeviceEnumerator()).EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            InputDeviceCB.ItemsSource = DeviceCollector;
            if (UserData.User.SelectedDeviceName != null)
            {
                foreach (MMDevice device in InputDeviceCB.Items)
                {
                    if (device.DeviceFriendlyName == UserData.User.SelectedDeviceName)
                    {
                        InputDeviceCB.SelectedItem = device;
                    }
                }
            }
            else
            {
                InputDeviceCB.SelectedIndex = 0;
            }
        }

        private void InputDeviceCB_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (InputDeviceCB.SelectedItem is not MMDevice device) return;
            UserData.User.SelectedDeviceNum = InputDeviceCB.SelectedIndex;
            UserData.User.SelectedDeviceName = device.DeviceFriendlyName;
            try
            {
                // Настройки пишем в AppData — в Program Files запись запрещена
                AppPaths.EnsureDir();
                File.WriteAllText(AppPaths.DeviceDataFile, $"{UserData.User.SelectedDeviceName}");
            }
            catch (System.Exception ex)
            {
                Tebegrammmm.Classes.Log.Save($"[Settings] Не удалось сохранить устройство записи: {ex.Message}");
            }
        }
    }
}
