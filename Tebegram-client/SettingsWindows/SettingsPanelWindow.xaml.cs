using Microsoft.Win32;
using NAudio.CoreAudioApi;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Tebegrammmm.Classes;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    public partial class SettingsPanelWindow : Window
    {
        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });
        public SettingsPanelWindow()
        {
            InitializeComponent();
            DataContext = UserData.User;
            TBUsername.Text = UserData.User.Username;

            ThemeToggle.Checked -= ThemeToggle_Changed;
            ThemeToggle.Unchecked -= ThemeToggle_Changed;
            ThemeToggle.IsChecked = ThemeManager.IsDark;
            ThemeLabel.Text = ThemeManager.IsDark ? "Тёмная тема" : "Светлая тема";
            ThemeToggle.Checked += ThemeToggle_Changed;
            ThemeToggle.Unchecked += ThemeToggle_Changed;

            CheckInputDevices();

            _ = LoadAvatarAsync(UserData.User.Avatar);
            UserData.User.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(UserData.User.Avatar))
                    _ = LoadAvatarAsync(UserData.User.Avatar);
            };
        }

        private async Task LoadAvatarAsync(string url)
        {
            var bmp = await AvatarLoader.LoadAsync(url);
            if (bmp != null) AvatarImageBrush.ImageSource = bmp;
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
            // MMDeviceEnumerator — COM STA-объект, должен вызываться на UI-потоке
            try
            {
                var devices = new MMDeviceEnumerator()
                    .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                InputDeviceCB.ItemsSource = devices;
                int idx = UserData.User.SelectedDeviceNum;
                InputDeviceCB.SelectedIndex = idx < devices.Count ? idx : 0;
            }
            catch { }
        }

        private void InputDeviceCB_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UserData.User.SelectedDeviceNum = InputDeviceCB.SelectedIndex;
            AppPaths.EnsureDir();
            File.WriteAllText(AppPaths.DeviceDataFile, $"{UserData.User.SelectedDeviceNum}");
        }

        private void ThemeToggle_Changed(object sender, RoutedEventArgs e)
        {
            bool isDark = ThemeToggle.IsChecked == true;
            ThemeLabel.Text = isDark ? "Тёмная тема" : "Светлая тема";
            ThemeManager.Apply(isDark);
            AppPaths.EnsureDir();
            File.WriteAllText(AppPaths.ThemeDataFile, isDark.ToString());
        }

        private async void EditAvatarBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp",
                Title = "Выберите аватарку"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                // Показываем выбранный файл мгновенно пока идёт загрузка
                UserData.User.Avatar = dlg.FileName;

                using var form = new MultipartFormDataContent();
                await using var stream = File.OpenRead(dlg.FileName);
                form.Add(new StreamContent(stream), "file", Path.GetFileName(dlg.FileName));

                using var response = await _http.PostAsync(
                    $"{ServerData.ServerAdress}/avatars/{UserData.User.Id}", form);
                string newFileName = (await response.Content.ReadAsStringAsync()).Trim();

                if (!string.IsNullOrEmpty(newFileName))
                {
                    // Строим постоянный серверный URL для аватарки
                    string serverUrl = $"{ServerData.ServerAdress}/avatars/{newFileName}";

                    // Сразу прописываем файл в дисковый кэш — следующий запуск загрузит без сети
                    try
                    {
                        byte[] imageBytes = await System.Threading.Tasks.Task.Run(
                            () => File.ReadAllBytes(dlg.FileName));
                        string cachePath = AppPaths.GetAvatarCachePath(serverUrl);
                        await System.Threading.Tasks.Task.Run(
                            () => File.WriteAllBytes(cachePath, imageBytes));
                    }
                    catch { }

                    UserData.User.Avatar = serverUrl;
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[SettingsPanelWindow.EditAvatarBtn_Click] {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
