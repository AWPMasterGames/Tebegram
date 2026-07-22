using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Значение (URL-строка или готовый ImageSource) → ImageBrush для заливки Ellipse/Border.
    ///
    /// Зачем: раньше в XAML писали
    ///     &lt;Ellipse&gt;&lt;Ellipse.Fill&gt;&lt;ImageBrush ImageSource="{Binding Avatar}"/&gt;…
    /// то есть Binding сидел ВНУТРИ ImageBrush. ImageBrush — Freezable, а не
    /// FrameworkElement: он не участвует в наследовании DataContext и не видит
    /// NameScope, поэтому окно сыпало ошибками привязки
    ///   «Не удается найти управляющий FrameworkElement… для целевого элемента»
    /// (особенно с ElementName, как в UserControl1), а при пустом аватаре — ещё и
    ///   «ImageSourceConverter cannot convert from (null)».
    /// Теперь Binding стоит на самом элементе (Ellipse.Fill / Border.Background),
    /// то есть на FrameworkElement, а кисть собирает этот конвертер. Он же гасит
    /// null: возвращает null вместо попытки сконвертировать пустоту в ImageSource.
    ///
    /// ConverterParameter — режим растяжения: "Uniform" (вписать целиком, для
    /// превью файлов) или по умолчанию "UniformToFill" (заполнить круг аватара).
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
                    // Битый URL — рисуем «пустой» аватар вместо падения привязки
                    Log.Save($"[ImageToBrush] {ex.GetType().Name}: {ex.Message} ({url})");
                    return null;
                }
            }

            if (source == null) return null;

            Stretch stretch = (parameter as string) == "Uniform" ? Stretch.Uniform : Stretch.UniformToFill;
            var brush = new ImageBrush(source) { Stretch = stretch };

            // Заморозка удешевляет отрисовку списков, НО картинка по http грузится
            // асинхронно: пока идёт загрузка, Freeze() кидает InvalidOperationException
            // («не удается заморозить этот объект Freezable») — и это исключение летит
            // прямо из привязки. Поэтому замораживаем только когда WPF это разрешает.
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
