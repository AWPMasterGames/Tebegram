using Microsoft.Win32;
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
            UpdateCacheLabel();

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

        // ── Кэш медиа ────────────────────────────────────────────────────────

        /// <summary>Показывает, сколько занимают скачанные фото и видео.</summary>
        private void UpdateCacheLabel()
        {
            var (files, bytes) = MediaCache.Stats();
            TBCacheSize.Text = files == 0
                ? "Кэш медиа: пусто"
                : $"Кэш медиа: {FormatSize(bytes)} · файлов: {files}";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024 / 1024:0.0} ГБ";
            if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024:0.0} МБ";
            return $"{bytes / 1024.0:0} КБ";
        }

        /// <summary>
        /// Очистка кэша. Файлы никуда не денутся — они лежат на сервере и
        /// скачаются заново при следующем открытии чата.
        /// </summary>
        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            MediaCache.Clear();
            UpdateCacheLabel();
            Log.Save("[Settings] Кэш медиа очищен");
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
            // Список в нумерации WaveInEvent — той же, что использует VoiceRoom
            // (waveIn.DeviceNumber). Раньше список брался из MMDeviceEnumerator
            // (WASAPI), а там ДРУГОЙ порядок устройств — выбранный индекс в звонке
            // мог указывать на другой микрофон («используется не тот микро»)
            InputDeviceCB.ItemsSource = Classes.AudioDevices.GetInputNames();
            int saved = Classes.AudioDevices.FindByName(UserData.User.SelectedDeviceName);
            InputDeviceCB.SelectedIndex = saved >= 0 ? saved
                : (InputDeviceCB.Items.Count > 0 ? 0 : -1);
        }

        private void InputDeviceCB_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (InputDeviceCB.SelectedIndex < 0) return;
            UserData.User.SelectedDeviceNum = InputDeviceCB.SelectedIndex;
            UserData.User.SelectedDeviceName = InputDeviceCB.SelectedItem as string;
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
