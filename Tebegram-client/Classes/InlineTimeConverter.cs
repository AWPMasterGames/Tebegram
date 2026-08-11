using System;
using System.Globalization;
using System.Windows.Data;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Видимость блока времени: у короткого однострочного текста время рисуется
    /// справа в той же строке, у длинных/многострочных сообщений и файлов - снизу.
    /// Вход - объект Message; parameter="Invert" - видимость «нижнего» блока времени.
    /// </summary>
    public class InlineTimeConverter : IValueConverter
    {
        // Максимальная длина текста, при которой время ставится в ту же строку.
        // Подобрано под пузырь MaxWidth=400 и шрифт 14px.
        private const int MaxInlineLength = 36;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool inline = false;
            if (value is Message m && m.MessageType == MessageType.Text)
            {
                string text = m.Text ?? string.Empty;
                inline = text.Length <= MaxInlineLength && !text.Contains('\n');
            }

            if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
                inline = !inline;

            return inline ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
