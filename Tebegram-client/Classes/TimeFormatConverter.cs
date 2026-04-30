using System;
using System.Globalization;
using System.Windows.Data;

namespace Tebegrammmm.Classes
{
    public class TimeFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string timeStr || string.IsNullOrEmpty(timeStr))
                return value;

            if (DateTime.TryParse(timeStr, out var dt))
                return dt.ToString("HH:mm");

            // Fallback: вытаскиваем HH:mm из любой строки с двоеточием
            foreach (var part in timeStr.Split(' '))
                if (part.Contains(':'))
                    return part.Length > 5 ? part[..5] : part;

            return timeStr;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
