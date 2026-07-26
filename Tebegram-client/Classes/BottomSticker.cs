using System.Windows.Controls;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Держит список прижатым к низу, пока пользователь сам не ушёл вверх.
    ///
    /// Зачем: пузырь с фото или видео растёт УЖЕ ПОСЛЕ появления — сначала это
    /// пустое место, и только потом картинка в полный размер. Прокрутка к
    /// последнему сообщению происходит раньше, а когда картинки догружаются,
    /// высота содержимого увеличивается. Смещение прокрутки при этом остаётся
    /// прежним, поэтому чат, стоявший внизу, уползает вверх — ровно на высоту
    /// догрузившегося фото. Со стороны выглядит как «прокрутило к последнему
    /// загруженному файлу».
    ///
    /// Отдельным классом, а не парой строк в окне: логика неочевидная и ломается
    /// незаметно, а так её можно прогнать на стенде с настоящим ScrollViewer.
    /// </summary>
    public sealed class BottomSticker
    {
        /// <summary>Погрешность в пикселях: на дробном масштабе экрана точного равенства не бывает.</summary>
        private const double Epsilon = 2.0;

        /// <summary>Пользователь сейчас у самого низа? (тогда список за ним и следует)</summary>
        public bool AtBottom { get; private set; } = true;

        /// <summary>
        /// Обработка прокрутки. extentHeightChange = 0 означает, что высота
        /// содержимого не менялась — значит прокручивал сам пользователь, и надо
        /// запомнить его выбор. Иначе содержимое выросло (или уменьшилось), и если
        /// пользователь стоял внизу — возвращаем его туда же.
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
