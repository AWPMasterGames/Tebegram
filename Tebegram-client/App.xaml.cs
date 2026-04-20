using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Tebegrammmm.Classes;

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
