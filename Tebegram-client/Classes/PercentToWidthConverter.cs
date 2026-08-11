using System;
using System.Globalization;
using System.Windows.Data;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Процент (0..100) → ширина в пикселях: доля от полной ширины дорожки,
    /// переданной параметром. Нужен для полоски загрузки файла в пузыре - у
    /// вложенного Border нет собственной ширины, а ProgressBar со своим шаблоном
    /// сюда тянуть тяжелее, чем один конвертер.
    /// </summary>
    public sealed class PercentToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = value is int i ? i : System.Convert.ToDouble(value, culture);
            double track = 0;
            if (parameter != null) double.TryParse(parameter.ToString(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out track);

            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            return track * percent / 100.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
