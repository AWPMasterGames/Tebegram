using System;
using System.Globalization;
using System.Windows.Data;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Превращает ключ группы «yyyyMMdd» в подпись даты как в Telegram
    /// (Сегодня / Вчера / 4 июля / 4 июля 2025). Используется в заголовке группы сообщений.
    /// </summary>
    public class DateKeyToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => Message.DateLabel(value as string ?? "");

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
