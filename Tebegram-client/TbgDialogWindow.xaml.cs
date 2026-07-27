using System;
using System.Windows;
using System.Windows.Input;

namespace Tebegrammmm
{
    /// <summary>
    /// Диалог в стиле приложения вместо системного MessageBox:
    /// TbgDialogWindow.Show(...) — сообщение с кнопкой «ОК»,
    /// TbgDialogWindow.Confirm(...) — вопрос «Да/Нет», возвращает bool,
    /// TbgDialogWindow.Prompt(...) — ввод строки (адрес сервера), возвращает текст или null.
    /// </summary>
    public partial class TbgDialogWindow : Window
    {
        private TbgDialogWindow(string title, string message, string primaryText, string secondaryText,
                                bool isPrompt = false, string inputDefault = null)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            PrimaryBtn.Content = primaryText;

            if (string.IsNullOrEmpty(secondaryText))
            {
                SecondaryBtn.Visibility = Visibility.Collapsed;
                PrimaryBtn.Margin = new Thickness(0);
            }
            else
            {
                SecondaryBtn.Content = secondaryText;
            }

            if (isPrompt)
            {
                InputBorder.Visibility = Visibility.Visible;
                InputBox.Text = inputDefault ?? string.Empty;
                // Фокус и выделение всего текста — сразу можно печатать/заменять
                Loaded += (_, __) => { InputBox.Focus(); InputBox.SelectAll(); };
            }
        }

        public static void Show(string message, string title = "Tebegram")
        {
            OnUI(() =>
            {
                var dlg = new TbgDialogWindow(title, message, "ОК", null);
                TrySetOwner(dlg);
                dlg.ShowDialog();
                return true;
            });
        }

        public static bool Confirm(string message, string title, string yesText = "Да", string noText = "Нет")
        {
            return OnUI(() =>
            {
                var dlg = new TbgDialogWindow(title, message, yesText, noText);
                TrySetOwner(dlg);
                return dlg.ShowDialog() == true;
            });
        }

        /// <summary>
        /// Запрашивает строку в стиле приложения. Возвращает введённый текст
        /// (обрезанный), либо null — если пользователь отменил или оставил пусто.
        /// </summary>
        public static string Prompt(string message, string title, string defaultValue = null,
                                    string okText = "Сохранить", string cancelText = "Отмена")
        {
            string result = null;
            OnUI(() =>
            {
                var dlg = new TbgDialogWindow(title, message, okText, cancelText,
                                              isPrompt: true, inputDefault: defaultValue);
                TrySetOwner(dlg);
                if (dlg.ShowDialog() == true)
                {
                    string text = dlg.InputBox.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(text)) result = text;
                }
                return true;
            });
            return result;
        }

        private static void TrySetOwner(TbgDialogWindow dlg)
        {
            try
            {
                Window active = Application.Current?.MainWindow;
                foreach (Window w in Application.Current.Windows)
                    if (w.IsActive) { active = w; break; }

                if (active != null && active.IsVisible && active != dlg)
                {
                    dlg.Owner = active;
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
            }
            catch
            {
                // без владельца — просто по центру экрана
            }
        }

        // Диалог можно вызывать и не из UI-потока (проверка обновлений идёт в фоне)
        private static bool OnUI(Func<bool> action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return false;
            return dispatcher.CheckAccess() ? action() : dispatcher.Invoke(action);
        }

        private void PrimaryBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void SecondaryBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
