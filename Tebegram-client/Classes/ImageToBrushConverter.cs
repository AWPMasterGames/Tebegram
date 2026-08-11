using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Преобразует URL-строку или готовый ImageSource в ImageBrush для заливки
    /// Ellipse и Border.
    ///
    /// Прежний вариант помещал привязку внутрь кисти:
    ///     &lt;Ellipse.Fill&gt;&lt;ImageBrush ImageSource="{Binding Avatar}"/&gt;
    /// ImageBrush наследует Freezable, а не FrameworkElement, поэтому не участвует
    /// в наследовании DataContext и не видит NameScope. Привязка не разрешалась, и
    /// окно выдавало ошибку поиска управляющего FrameworkElement, а при пустом
    /// аватаре дополнительно ошибку преобразования null в ImageSource.
    ///
    /// Теперь привязка стоит на самом элементе, то есть на FrameworkElement, а
    /// кисть собирает конвертер. Пустое значение он возвращает как null.
    ///
    /// ConverterParameter задаёт режим растяжения: "Uniform" вписывает изображение
    /// целиком и применяется к превью файлов, "UniformToFill" заполняет круг
    /// аватара и используется по умолчанию.
    /// </summary>
    public class ImageToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ImageSource source = value as ImageSource;

            if (source == null && value is string url && !string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(url, UriKind.RelativeOrAbsolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    source = bitmap;
                }
                catch (Exception ex)
                {
                    // Битый URL - рисуем «пустой» аватар вместо падения привязки
                    Log.Save($"[ImageToBrush] {ex.GetType().Name}: {ex.Message} ({url})");
                    return null;
                }
            }

            if (source == null) return null;

            Stretch stretch = (parameter as string) == "Uniform" ? Stretch.Uniform : Stretch.UniformToFill;
            var brush = new ImageBrush(source) { Stretch = stretch };

            // Заморозка удешевляет отрисовку списков, НО картинка по http грузится
            // асинхронно: пока идёт загрузка, Freeze() кидает InvalidOperationException
            // («не удается заморозить этот объект Freezable») - и это исключение летит
            // прямо из привязки. Поэтому замораживаем только когда WPF это разрешает.
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
