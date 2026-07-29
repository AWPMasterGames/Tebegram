using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Размеры и положение окон приложения — в одном месте.
    ///
    /// Как считается размер. Раньше просмотрщик брал размер прямо от разрешения
    /// (0.92 рабочей области) и подгонялся под каждое фото: на 4K открывался почти
    /// во весь экран, на ноутбуке — как придётся. Чисто фиксированный размер тоже
    /// плох: на маленьком ноутбуке 1000x680 занимает 92% высоты (снова «во весь
    /// экран»), а на 4K@100% — жалкие 26% ширины.
    ///
    /// Поэтому здесь ГИБРИД: у каждого окна есть базовый комфортный размер, но он
    /// зажат в вилку долей рабочей области (<see cref="MinFraction"/>..<see cref="MaxFraction"/>).
    /// На обычных мониторах работает базовый размер, на маленьких окно ужимается,
    /// на больших — растёт вместе с экраном, никогда не занимая его целиком.
    ///
    /// Все размеры — в единицах WPF (DIP), то есть с уже учтённым масштабированием
    /// Windows: при 150% «1000» превращается в 1500 физических пикселей.
    /// </summary>
    public static class UiSizes
    {
        // ── Базовые размеры окон ────────────────────────────────────────────
        // Главное окно мессенджера
        public const double MessengerWidth  = 900;
        public const double MessengerHeight = 620;
        public const double MessengerMinWidth  = 600;
        public const double MessengerMinHeight = 350;

        // Просмотрщик фото и видео — одинаковый для обоих
        public const double ViewerWidth  = 1000;
        public const double ViewerHeight = 680;
        public const double ViewerMinWidth  = 480;
        public const double ViewerMinHeight = 360;

        // Вспомогательные окна: высоту подбирает содержимое (SizeToContent),
        // фиксируется только ширина
        public const double DialogWidth   = 380;
        public const double FormWidth     = 400;
        public const double WideFormWidth = 460;

        // ── Вилка размеров относительно рабочей области ─────────────────────
        /// <summary>Ниже этой доли экрана окно не опускается (иначе теряется на 4K).</summary>
        private const double MinFraction = 0.55;
        /// <summary>Выше этой доли не поднимается (иначе «во весь экран»).</summary>
        private const double MaxFraction = 0.80;

        /// <summary>
        /// Размер окна для заданной рабочей области. Вынесен отдельно и без
        /// зависимостей от WPF-окна, чтобы поведение можно было проверить
        /// расчётом для любых разрешений.
        /// </summary>
        public static Size Resolve(double baseWidth, double baseHeight, Size workArea)
        {
            return new Size(
                Clamp(baseWidth,  workArea.Width),
                Clamp(baseHeight, workArea.Height));

            static double Clamp(double value, double available)
            {
                double min = available * MinFraction;
                double max = available * MaxFraction;
                // На совсем узких экранах вилка может «схлопнуться» — max главнее:
                // окно никогда не должно быть больше экрана
                if (min > max) return max;
                return Math.Min(Math.Max(value, min), max);
            }
        }

        /// <summary>
        /// Ставит окну размер по правилам выше и центрирует его на том мониторе,
        /// где находится окно-владелец (или главное окно). Центрируем вручную:
        /// WindowStartupLocation=CenterScreen срабатывает в момент показа, а размер
        /// нередко выставляется позже — и окно уезжает от центра.
        /// </summary>
        public static void ApplyAndCenter(Window window, double baseWidth, double baseHeight)
        {
            Rect work = WorkAreaFor(window);
            Size size = Resolve(baseWidth, baseHeight, new Size(work.Width, work.Height));

            window.Width  = size.Width;
            window.Height = size.Height;
            window.Left   = work.Left + (work.Width  - size.Width)  / 2;
            window.Top    = work.Top  + (work.Height - size.Height) / 2;
        }

        /// <summary>
        /// Ставит окно по центру монитора, на котором оно сейчас находится, не
        /// трогая его размер. Нужно, чтобы вернуть модальное окно (настройки) в
        /// центр экрана по клику по чату.
        /// </summary>
        public static void CenterOnScreen(Window window)
        {
            if (window == null) return;
            Rect work = WorkAreaFor(window);
            double w = double.IsNaN(window.Width) ? window.ActualWidth : window.Width;
            double h = double.IsNaN(window.Height) ? window.ActualHeight : window.Height;
            window.Left = work.Left + (work.Width - w) / 2;
            window.Top = work.Top + (work.Height - h) / 2;
        }

        /// <summary>
        /// Рабочая область монитора, на котором открыто окно (без панели задач).
        /// При одном мониторе это то же, что SystemParameters.WorkArea, но при
        /// нескольких окно больше не улетает на основной экран.
        /// </summary>
        public static Rect WorkAreaFor(Window window) => MonitorRect(window, workArea: true);

        /// <summary>
        /// ПОЛНЫЕ границы монитора, включая область панели задач — для режима
        /// «во весь экран» в просмотрщике.
        /// </summary>
        public static Rect MonitorBoundsFor(Window window) => MonitorRect(window, workArea: false);

        private static Rect MonitorRect(Window window, bool workArea)
        {
            Rect fallback = workArea
                ? SystemParameters.WorkArea
                : new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            try
            {
                // Ориентируемся на само окно, если оно уже создано, иначе на владельца
                Window anchor = window;
                IntPtr handle = anchor != null ? new WindowInteropHelper(anchor).Handle : IntPtr.Zero;
                if (handle == IntPtr.Zero)
                {
                    anchor = window?.Owner ?? Application.Current?.MainWindow;
                    handle = anchor != null ? new WindowInteropHelper(anchor).Handle : IntPtr.Zero;
                }
                if (handle == IntPtr.Zero) return fallback;

                IntPtr monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
                    return fallback;

                RECT r = workArea ? info.rcWork : info.rcMonitor;

                // Windows отдаёт физические пиксели — переводим в единицы WPF,
                // иначе при масштабировании 125/150% окно оказалось бы больше экрана
                var source = PresentationSource.FromVisual(anchor);
                double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
                if (scaleX <= 0) scaleX = 1.0;
                if (scaleY <= 0) scaleY = 1.0;

                return new Rect(r.Left / scaleX, r.Top / scaleY,
                                (r.Right - r.Left) / scaleX, (r.Bottom - r.Top) / scaleY);
            }
            catch (Exception ex)
            {
                // Любая неожиданность с WinAPI не должна мешать открыть окно
                Log.Save($"[UiSizes.MonitorRect] {ex.GetType().Name}: {ex.Message}");
                return fallback;
            }
        }

        // ── WinAPI: рабочая область конкретного монитора ────────────────────
        private const int MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    }
}
