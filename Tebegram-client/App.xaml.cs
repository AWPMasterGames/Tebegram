using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Tebegrammmm.Classes;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Применяем тему до показа окон.
            // Вызываем ВСЕГДА (даже для светлой) - так кисти темы заменяются на
            // изменяемые (не замороженные) экземпляры ДО загрузки окон, и последующее
            // переключение применяется вживую ко всем уже открытым окнам.
            try
            {
                AppPaths.EnsureDir();
                bool savedDark = false;
                if (File.Exists(AppPaths.ThemeDataFile))
                    bool.TryParse(File.ReadAllText(AppPaths.ThemeDataFile), out savedDark);
                ThemeManager.Apply(savedDark, animate: false);
            }
            catch (Exception ex)
            {
                Log.Save($"[App] Не удалось применить тему: {ex.Message}");
            }

            //ошибки в UI-потоке
            DispatcherUnhandledException += (s, args) =>
            {
                Log.Save($"[UnhandledDispatcher] {args.Exception.GetType().Name}: {args.Exception.Message}\n{args.Exception.StackTrace}");
                MessageBox.Show($"Необработанная ошибка:\n{args.Exception.Message}\n\nПодробности в CrashLogs.", "Ошибка");
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    Log.Save($"[UnhandledDomain] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                Log.Save($"[UnobservedTask] {args.Exception.GetType().Name}: {args.Exception.Message}\n{args.Exception.StackTrace}");
                args.SetObserved();
            };
        }
    }
}
