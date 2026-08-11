using System.Windows.Controls;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Удерживает список прижатым к нижней границе, пока пользователь не прокрутил
    /// его вверх самостоятельно.
    ///
    /// Пузырь с фотографией или видео увеличивается уже после появления: сначала
    /// это пустая область, изображение приходит позже. Прокрутка к последнему
    /// сообщению выполняется раньше загрузки, а смещение прокрутки при росте
    /// содержимого не меняется. В результате список уезжает вверх ровно на высоту
    /// загрузившегося изображения, и выглядит это как прокрутка к произвольному
    /// сообщению.
    ///
    /// Вынесено в отдельный класс, поскольку поведение неочевидно и нарушается
    /// незаметно. Отдельный класс проверяется на стенде с реальным ScrollViewer.
    /// </summary>
    public sealed class BottomSticker
    {
        /// <summary>Погрешность в пикселях: на дробном масштабе экрана точного равенства не бывает.</summary>
        private const double Epsilon = 2.0;

        /// <summary>Пользователь сейчас у самого низа? (тогда список за ним и следует)</summary>
        public bool AtBottom { get; private set; } = true;

        /// <summary>
        /// Обработка прокрутки. extentHeightChange = 0 означает, что высота
        /// содержимого не менялась - значит прокручивал сам пользователь, и надо
        /// запомнить его выбор. Иначе содержимое выросло (или уменьшилось), и если
        /// пользователь стоял внизу - возвращаем его туда же.
        /// </summary>
        public void Handle(ScrollViewer scroll, double extentHeightChange)
        {
            if (scroll == null) return;

            if (extentHeightChange == 0)
                AtBottom = scroll.VerticalOffset >= scroll.ScrollableHeight - Epsilon;
            else if (AtBottom)
                scroll.ScrollToEnd();
        }

        /// <summary>Прижать к низу принудительно (открытие чата, своё новое сообщение).</summary>
        public void Stick(ScrollViewer scroll)
        {
            AtBottom = true;
            scroll?.ScrollToEnd();
        }
    }
}
