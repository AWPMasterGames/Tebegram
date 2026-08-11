using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Tebegrammmm
{
    public static class ThemeManager
    {
        public static bool IsDark { get; private set; } = false;

        // Реестр изменяемых кистей темы. Создаются один раз (при первом Apply на старте),
        // регистрируются в ресурсах приложения и перекрывают замороженные кисти из XAML.
        // Все окна, открытые после старта, берут именно эти экземпляры → смена .Color
        // применяется вживую.
        private static readonly Dictionary<string, SolidColorBrush> _live = new();

        // Уточнённая палитра «индиго + графит»: мягкие нейтрали, один акцент #5B6BF5,
        // меньше видимых рамок. Значения - источник истины для рантайма (см. Apply).
        private static readonly Dictionary<string, Color> LightColors = new()
        {
            // Светлая тема заметно СЕРЕЕ и темнее: раньше слепила почти белым.
            // Оттенок сведён ближе к нейтральному серому (меньше голубизны),
            // фон чата ощутимо темнее - на нём отчётливо видны точки узора
            // (см. Light.ChatDotBrush).
            ["Light.BgDeepBrush"]        = Color.FromRgb(0xCE, 0xD1, 0xD9), // фон приложения / область чата
            ["Light.BgPrimaryBrush"]     = Color.FromRgb(0xE9, 0xEB, 0xF0), // карточки/панели (не чисто-белые)
            ["Light.BgSurfaceBrush"]     = Color.FromRgb(0xDD, 0xE0, 0xE7), // список чатов, шапки
            ["Light.BgElevatedBrush"]    = Color.FromRgb(0xD3, 0xD7, 0xE0), // ховер
            ["Light.BgInputBrush"]       = Color.FromRgb(0xDD, 0xE0, 0xE7),
            ["Light.AccentPrimaryBrush"] = Color.FromRgb(0x5B, 0x6B, 0xF5),
            ["Light.AccentHoverBrush"]   = Color.FromRgb(0x6B, 0x7B, 0xFF),
            ["Light.AccentPressedBrush"] = Color.FromRgb(0x4A, 0x5A, 0xE0),
            ["Light.AccentMutedBrush"]   = Color.FromRgb(0xE4, 0xE8, 0xFF), // мягкий индиго-тинт (выделение)
            ["Light.AccentTextBrush"]    = Color.FromRgb(0x4E, 0x5E, 0xE6),
            ["Light.DangerBrush"]        = Color.FromRgb(0xE5, 0x48, 0x4D),
            ["Light.DangerHoverBrush"]   = Color.FromRgb(0xF0, 0x55, 0x60),
            ["Light.DangerMutedBrush"]   = Color.FromRgb(0xFD, 0xEC, 0xEC),
            ["Light.SuccessBrush"]       = Color.FromRgb(0x30, 0xA4, 0x6C),
            ["Light.WarningBrush"]       = Color.FromRgb(0xD9, 0x82, 0x0A),
            ["Light.TextPrimaryBrush"]   = Color.FromRgb(0x14, 0x16, 0x1C),
            ["Light.TextSecondaryBrush"] = Color.FromRgb(0x5B, 0x61, 0x72),
            ["Light.TextMutedBrush"]     = Color.FromRgb(0x99, 0xA0, 0xB0),
            ["Light.TextDisabledBrush"]  = Color.FromRgb(0xC6, 0xCB, 0xD6),
            ["Light.TextInverseBrush"]   = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["Light.BorderSubtleBrush"]  = Color.FromRgb(0xDC, 0xE0, 0xEA), // почти невидимая
            ["Light.BorderDefaultBrush"] = Color.FromRgb(0xCF, 0xD4, 0xE0), // мягкая
            ["Light.BorderStrongBrush"]  = Color.FromRgb(0xBD, 0xC4, 0xD2),
            ["Light.BorderFocusBrush"]   = Color.FromRgb(0x5B, 0x6B, 0xF5),
            ["Light.MsgOutgoingBrush"]   = Color.FromRgb(0x5B, 0x6B, 0xF5),
            ["Light.MsgIncomingBrush"]   = Color.FromRgb(0xFF, 0xFF, 0xFF), // белые пузыри ярко читаются на сером фоне
            ["Light.MsgFailedBrush"]     = Color.FromRgb(0xFD, 0xEC, 0xEC),
            ["Light.MsgPendingBrush"]    = Color.FromRgb(0xEE, 0xF1, 0xFA),
            // Плашка вложения-файла ВНУТРИ входящего пузыря: должна отличаться от
            // фона пузыря, чтобы было видно, куда жать. На белом пузыре - светло-серая.
            ["Light.MsgFileChipBrush"]   = Color.FromRgb(0xE7, 0xEA, 0xF1),
            // Точки узора на фоне чата: на светлой теме - заметные серо-синие
            // (раньше узор был общий и на светлом фоне пропадал)
            ["Light.ChatDotBrush"]       = Color.FromArgb(0x55, 0x4E, 0x5C, 0x78),
        };

        private static readonly Dictionary<string, Color> DarkColors = new()
        {
            ["Light.BgDeepBrush"]        = Color.FromRgb(0x0F, 0x11, 0x17), // фон приложения
            ["Light.BgPrimaryBrush"]     = Color.FromRgb(0x17, 0x1A, 0x22), // панели
            ["Light.BgSurfaceBrush"]     = Color.FromRgb(0x1C, 0x20, 0x29), // список чатов, шапки
            ["Light.BgElevatedBrush"]    = Color.FromRgb(0x23, 0x28, 0x34), // ховер
            ["Light.BgInputBrush"]       = Color.FromRgb(0x19, 0x1D, 0x26),
            ["Light.AccentPrimaryBrush"] = Color.FromRgb(0x61, 0x72, 0xF6),
            ["Light.AccentHoverBrush"]   = Color.FromRgb(0x74, 0x82, 0xFF),
            ["Light.AccentPressedBrush"] = Color.FromRgb(0x4E, 0x5F, 0xE0),
            ["Light.AccentMutedBrush"]   = Color.FromRgb(0x23, 0x2A, 0x52),
            ["Light.AccentTextBrush"]    = Color.FromRgb(0x9A, 0xA6, 0xFF),
            ["Light.DangerBrush"]        = Color.FromRgb(0xF0, 0x57, 0x5C),
            ["Light.DangerHoverBrush"]   = Color.FromRgb(0xFF, 0x6A, 0x6E),
            ["Light.DangerMutedBrush"]   = Color.FromRgb(0x3A, 0x1A, 0x1A),
            ["Light.SuccessBrush"]       = Color.FromRgb(0x3D, 0xD6, 0x8C),
            ["Light.WarningBrush"]       = Color.FromRgb(0xE8, 0xA1, 0x3A),
            ["Light.TextPrimaryBrush"]   = Color.FromRgb(0xE9, 0xEC, 0xF2),
            ["Light.TextSecondaryBrush"] = Color.FromRgb(0x99, 0xA0, 0xB0),
            ["Light.TextMutedBrush"]     = Color.FromRgb(0x62, 0x6A, 0x7C),
            ["Light.TextDisabledBrush"]  = Color.FromRgb(0x3E, 0x44, 0x53),
            ["Light.TextInverseBrush"]   = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["Light.BorderSubtleBrush"]  = Color.FromRgb(0x1C, 0x21, 0x2B),
            ["Light.BorderDefaultBrush"] = Color.FromRgb(0x2A, 0x30, 0x3C),
            ["Light.BorderStrongBrush"]  = Color.FromRgb(0x3A, 0x41, 0x4F),
            ["Light.BorderFocusBrush"]   = Color.FromRgb(0x61, 0x72, 0xF6),
            ["Light.MsgOutgoingBrush"]   = Color.FromRgb(0x2E, 0x3A, 0x66),
            ["Light.MsgIncomingBrush"]   = Color.FromRgb(0x23, 0x28, 0x34),
            ["Light.MsgFailedBrush"]     = Color.FromRgb(0x3A, 0x1A, 0x1A),
            ["Light.MsgPendingBrush"]    = Color.FromRgb(0x1C, 0x21, 0x2B),
            // Плашка файла внутри входящего пузыря заметно светлее самого пузыря
            // (#232834): раньше она была цветом BgElevated = того же #232834, и на
            // тёмной теме кликабельная область сливалась с телом сообщения
            ["Light.MsgFileChipBrush"]   = Color.FromRgb(0x33, 0x3B, 0x4B),
            // Точки узора в тёмной теме оставляем как были - они и так видны
            ["Light.ChatDotBrush"]       = Color.FromArgb(0x12, 0x8A, 0x93, 0xA6),
        };

        public static void Apply(bool isDark, bool animate = true)
        {
            IsDark = isDark;
            var target = isDark ? DarkColors : LightColors;

            // Заменяем кисти темы на новые. Элементы XAML ссылаются на них через
            // DynamicResource, поэтому подхватывают замену вживую (мгновенно, без рестарта).
            // Параметр animate оставлен для совместимости вызовов; плавный переход не нужен - 
            // DynamicResource не позволяет анимировать общий экземпляр (WPF замораживает кисти).
            foreach (var (key, toColor) in target)
            {
                _live[key] = new SolidColorBrush(toColor);
                Application.Current.Resources[key] = _live[key];
            }

            ApplyChatPattern(isDark);
        }

        /// <summary>
        /// Собирает точечный узор фона чата под текущую тему и помещает готовую
        /// кисть в ресурсы приложения.
        ///
        /// Ранее цвет точки задавался через DynamicResource внутри DrawingBrush, а
        /// сама кисть подключалась как StaticResource. WPF замораживает такой
        /// Freezable, поэтому цвет фиксировался на значении времени загрузки и на
        /// светлой теме точки становились неразличимы.
        ///
        /// Теперь кисть строится целиком в коде с уже вычисленным цветом. Окно
        /// ссылается на неё через DynamicResource ChatPatternBrush, поэтому смена
        /// темы обновляет узор без перезапуска.
        /// </summary>
        private static void ApplyChatPattern(bool isDark)
        {
            // Те же значения, что и для Light.ChatDotBrush: тёмная - деликатная,
            // светлая - заметная серо-синяя (фон при этом НЕ меняем)
            Color dot = isDark
                ? Color.FromArgb(0x12, 0x8A, 0x93, 0xA6)
                : Color.FromArgb(0x55, 0x4E, 0x5C, 0x78);

            var drawing = new GeometryDrawing(
                new SolidColorBrush(dot), null,
                new EllipseGeometry(new Point(13, 13), 1.3, 1.3));

            var pattern = new DrawingBrush(drawing)
            {
                Stretch = Stretch.None,
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 26, 26),
                ViewportUnits = BrushMappingMode.Absolute
            };
            pattern.Freeze(); // кисть неизменна в пределах темы - можно заморозить для скорости

            Application.Current.Resources["ChatPatternBrush"] = pattern;
        }
    }
}
