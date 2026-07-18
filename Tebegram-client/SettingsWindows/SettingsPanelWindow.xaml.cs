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
            TBLogin.Text = UserData.User.Login; // раньше {Binding Login} не работал — TBLogin вне UserInfo
            TBVersion.Text = $"Tebegram {UpdateChecker.CurrentVersion}";
            UserInfo.DataContext = UserData.User;
            CheckInputDevices();

            // Текущий выбор сервера (DrunkMan / Adress.txt)
            UpdateServerChoiceLabel();

            // Список тем — тот же ComboBox, что и выбор микрофона.
            // Текущую тему выставляем без вызова обработчика (иначе лишняя запись файла)
            ThemeCB.SelectionChanged -= ThemeCB_SelectionChanged;
            ThemeCB.ItemsSource = new[] { "Светлая тема", "Тёмная тема" };
            ThemeCB.SelectedIndex = ThemeManager.IsDark ? 1 : 0;
            ThemeCB.SelectionChanged += ThemeCB_SelectionChanged;
        }

        private void UpdateServerChoiceLabel()
        {
            TBServerChoice.Text = ServerData.ServerChoice == "drunkman"
                ? "Туннель DrunkMan"
                : "Adress.txt (авто)";
        }

        /// <summary>Переключение сервера: туннель DrunkMan ↔ обычная цепочка Adress.txt.</summary>
        private void ToggleServer_Click(object sender, RoutedEventArgs e)
        {
            ServerData.SetServerChoice(ServerData.ServerChoice == "drunkman" ? "auto" : "drunkman");
            UpdateServerChoiceLabel();
            Log.Save($"[Settings] Сервер переключён: {ServerData.ServerChoice} → {ServerData.ServerAdress}");
        }

        private void ThemeCB_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Индекс 1 = «Тёмная тема» (порядок элементов задан в конструкторе)
            bool isDark = ThemeCB.SelectedIndex == 1;
            ThemeManager.Apply(isDark);
            try
            {
                AppPaths.EnsureDir();
                File.WriteAllText(AppPaths.ThemeDataFile, isDark.ToString());
            }
            catch (System.Exception ex)
            {
                Tebegrammmm.Classes.Log.Save($"[Settings] Не удалось сохранить тему: {ex.Message}");
            }
        }

        private async void ChangeAvatar_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Выбери новый аватар"
            };
            if (dlg.ShowDialog() != true) return;

            // Окно предпросмотра/обрезки — выбираем участок фото под круглый аватар
            var cropper = new AvatarCropWindow(dlg.FileName) { Owner = this };
            if (cropper.ShowDialog() != true || string.IsNullOrEmpty(cropper.CroppedPngPath))
                return;

            string cropped = cropper.CroppedPngPath;
            try
            {
                using var multipart = new MultipartFormDataContent();
                var fileContent = new StreamContent(File.OpenRead(cropped));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                // Уникальное имя, чтобы кэш аватара обновился
                string uploadName = $"avatar_{UserData.User.Id}_{System.DateTime.Now:HHmmss}.png";
                multipart.Add(fileContent, "file", uploadName);

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
            finally
            {
                try { File.Delete(cropped); } catch { }
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
