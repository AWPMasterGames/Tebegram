using System;
using System.Windows;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Единые размеры окон приложения.
    ///
    /// Зачем: раньше просмотрщик считал свой размер от разрешения монитора
    /// (0.92 рабочей области), поэтому на 4K-экране открывался почти во весь
    /// экран, а на маленьком — крошечным. Теперь у каждого окна ФИКСИРОВАННЫЙ
    /// размер в независимых от устройства единицах: на любом мониторе окно
    /// выглядит одинаково. Рабочая область учитывается только как потолок —
    /// чтобы окно не оказалось больше экрана на маленьких разрешениях.
    /// </summary>
    public static class UiSizes
    {
        // Главное окно мессенджера
        public const double MessengerWidth  = 900;
        public const double MessengerHeight = 620;
        public const double MessengerMinWidth  = 600;
        public const double MessengerMinHeight = 350;

        // Просмотрщик фото и видео — одинаковый размер для обоих
        public const double ViewerWidth  = 1000;
        public const double ViewerHeight = 680;
        public const double ViewerMinWidth  = 480;
        public const double ViewerMinHeight = 360;

        // Вспомогательные окна (настройки, папки, группы, диалоги) — их высота
        // подбирается содержимым (SizeToContent), фиксируется только ширина
        public const double DialogWidth       = 380;
        public const double FormWidth         = 400;
        public const double WideFormWidth     = 460;

        /// <summary>Доля рабочей области, которую окно не должно превышать.</summary>
        private const double MaxWorkAreaFraction = 0.92;

        /// <summary>
        /// Ставит окну размер из констант, ужимая его под рабочую область экрана
        /// (на маленьких мониторах), и ставит по центру этой области.
        /// Центрируем вручную: WindowStartupLocation=CenterScreen срабатывает
        /// в момент показа, а если размер меняется позже — окно уезжает от центра.
        /// </summary>
        public static void ApplyAndCenter(Window window, double width, double height)
        {
            Rect work = SystemParameters.WorkArea;

            double w = Math.Min(width,  work.Width  * MaxWorkAreaFraction);
            double h = Math.Min(height, work.Height * MaxWorkAreaFraction);

            window.Width  = w;
            window.Height = h;
            window.Left   = work.Left + (work.Width  - w) / 2;
            window.Top    = work.Top  + (work.Height - h) / 2;
        }
    }
}
