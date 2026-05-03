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

        private static readonly Dictionary<string, Color> LightColors = new()
        {
            ["Light.BgDeepBrush"]        = Color.FromRgb(0xE8, 0xEC, 0xF5),
            ["Light.BgPrimaryBrush"]     = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["Light.BgSurfaceBrush"]     = Color.FromRgb(0xF2, 0xF4, 0xFA),
            ["Light.BgElevatedBrush"]    = Color.FromRgb(0xEA, 0xEC, 0xF5),
            ["Light.BgInputBrush"]       = Color.FromRgb(0xF0, 0xF2, 0xF8),
            ["Light.AccentPrimaryBrush"] = Color.FromRgb(0x4A, 0x5A, 0xE8),
            ["Light.AccentHoverBrush"]   = Color.FromRgb(0x5B, 0x6B, 0xF5),
            ["Light.AccentPressedBrush"] = Color.FromRgb(0x3A, 0x4A, 0xD0),
            ["Light.AccentMutedBrush"]   = Color.FromRgb(0xDC, 0xE0, 0xFF),
            ["Light.AccentTextBrush"]    = Color.FromRgb(0x3A, 0x4A, 0xD0),
            ["Light.DangerBrush"]        = Color.FromRgb(0xD9, 0x30, 0x25),
            ["Light.DangerHoverBrush"]   = Color.FromRgb(0xE8, 0x40, 0x30),
            ["Light.DangerMutedBrush"]   = Color.FromRgb(0xFD, 0xEC, 0xEA),
            ["Light.SuccessBrush"]       = Color.FromRgb(0x1E, 0x8A, 0x5E),
            ["Light.WarningBrush"]       = Color.FromRgb(0xC9, 0x7A, 0x00),
            ["Light.TextPrimaryBrush"]   = Color.FromRgb(0x0D, 0x11, 0x17),
            ["Light.TextSecondaryBrush"] = Color.FromRgb(0x50, 0x58, 0x70),
            ["Light.TextMutedBrush"]     = Color.FromRgb(0x90, 0x9A, 0xAF),
            ["Light.TextDisabledBrush"]  = Color.FromRgb(0xC4, 0xC8, 0xD8),
            ["Light.TextInverseBrush"]   = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["Light.BorderSubtleBrush"]  = Color.FromRgb(0xEA, 0xEC, 0xF5),
            ["Light.BorderDefaultBrush"] = Color.FromRgb(0xD4, 0xD8, 0xE8),
            ["Light.BorderStrongBrush"]  = Color.FromRgb(0xB0, 0xB8, 0xD0),
            ["Light.BorderFocusBrush"]   = Color.FromRgb(0x4A, 0x5A, 0xE8),
            ["Light.MsgOutgoingBrush"]   = Color.FromRgb(0x4A, 0x5A, 0xE8),
            ["Light.MsgIncomingBrush"]   = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["Light.MsgFailedBrush"]     = Color.FromRgb(0xFD, 0xEC, 0xEA),
            ["Light.MsgPendingBrush"]    = Color.FromRgb(0xEE, 0xF0, 0xFA),
        };

        private static readonly Dictionary<string, Color> DarkColors = new()
        {
            ["Light.BgDeepBrush"]        = Color.FromRgb(0x0D, 0x11, 0x17),
            ["Light.BgPrimaryBrush"]     = Color.FromRgb(0x16, 0x1B, 0x27),
            ["Light.BgSurfaceBrush"]     = Color.FromRgb(0x1E, 0x24, 0x38),
            ["Light.BgElevatedBrush"]    = Color.FromRgb(0x26, 0x2D, 0x47),
            ["Light.BgInputBrush"]       = Color.FromRgb(0x1A, 0x1F, 0x33),
            ["Light.AccentPrimaryBrush"] = Color.FromRgb(0x5B, 0x6B, 0xF5),
            ["Light.AccentHoverBrush"]   = Color.FromRgb(0x6B, 0x7B, 0xFF),
            ["Light.AccentPressedBrush"] = Color.FromRgb(0x4A, 0x5A, 0xE0),
            ["Light.AccentMutedBrush"]   = Color.FromRgb(0x1E, 0x25, 0x60),
            ["Light.AccentTextBrush"]    = Color.FromRgb(0x8B, 0x9B, 0xFF),
            ["Light.DangerBrush"]        = Color.FromRgb(0xF0, 0x4F, 0x4F),
            ["Light.DangerHoverBrush"]   = Color.FromRgb(0xFF, 0x60, 0x60),
            ["Light.DangerMutedBrush"]   = Color.FromRgb(0x3A, 0x15, 0x15),
            ["Light.SuccessBrush"]       = Color.FromRgb(0x3E, 0xCF, 0x8E),
            ["Light.WarningBrush"]       = Color.FromRgb(0xF5, 0xA6, 0x23),
            ["Light.TextPrimaryBrush"]   = Color.FromRgb(0xE8, 0xEA, 0xED),
            ["Light.TextSecondaryBrush"] = Color.FromRgb(0x8B, 0x91, 0xA8),
            ["Light.TextMutedBrush"]     = Color.FromRgb(0x4A, 0x50, 0x68),
            ["Light.TextDisabledBrush"]  = Color.FromRgb(0x36, 0x3B, 0x52),
            ["Light.TextInverseBrush"]   = Color.FromRgb(0x0D, 0x11, 0x17),
            ["Light.BorderSubtleBrush"]  = Color.FromRgb(0x1E, 0x24, 0x38),
            ["Light.BorderDefaultBrush"] = Color.FromRgb(0x2E, 0x35, 0x50),
            ["Light.BorderStrongBrush"]  = Color.FromRgb(0x3E, 0x48, 0x68),
            ["Light.BorderFocusBrush"]   = Color.FromRgb(0x5B, 0x6B, 0xF5),
            ["Light.MsgOutgoingBrush"]   = Color.FromRgb(0x2D, 0x3F, 0x9E),
            ["Light.MsgIncomingBrush"]   = Color.FromRgb(0x26, 0x2D, 0x47),
            ["Light.MsgFailedBrush"]     = Color.FromRgb(0x3A, 0x15, 0x15),
            ["Light.MsgPendingBrush"]    = Color.FromRgb(0x1E, 0x24, 0x38),
        };

        public static void Apply(bool isDark, bool animate = true)
        {
            IsDark = isDark;
            var target = isDark ? DarkColors : LightColors;
            var duration = new Duration(TimeSpan.FromMilliseconds(animate ? 350 : 0));
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

            foreach (var (key, toColor) in target)
            {
                var existing = Application.Current.Resources[key] as SolidColorBrush;

                if (existing != null && !existing.IsFrozen)
                {
                    if (animate)
                    {
                        var anim = new ColorAnimation(toColor, duration) { EasingFunction = easing };
                        existing.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                    }
                    else
                    {
                        existing.Color = toColor;
                    }
                }
                else
                {
                    // Кисть заморожена (BAML-оптимизация или ControlTemplate).
                    // Создаём новую с нужным цветом ДО регистрации — после добавления
                    // в ResourceDictionary WPF может снова заморозить объект.
                    Application.Current.Resources[key] = new SolidColorBrush(toColor);
                }
            }
        }
    }
}
