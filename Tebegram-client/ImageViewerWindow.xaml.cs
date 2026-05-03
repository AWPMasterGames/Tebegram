using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Tebegrammmm
{
    public partial class ImageViewerWindow : Window
    {
        private static readonly HttpClient _http = new();

        // Хром: тень 8px + отступы ScrollViewer 12px с каждой стороны
        private const double ShadowMargin     = 8;
        private const double ScrollSideMargin = 12;
        private const double TitleBarH        = 36;
        private const double HorizChrome = (ShadowMargin + ScrollSideMargin) * 2; // 40
        private const double VertChrome  = TitleBarH + ScrollSideMargin + ShadowMargin * 2; // 68

        // _minZoom и _maxZoom вычисляются относительно _fitZoom в FitToScreen.
        // 100% = изображение полностью вписано в окно (_fitZoom).
        // 50%  = половина от вписанного размера; 1000% = в 10 раз больше вписанного.
        private double _minZoom = 0.01; // заменяется в FitToScreen
        private double _maxZoom = 10.0; // заменяется в FitToScreen

        private double _zoom    = 1.0;
        private double _fitZoom = 1.0;
        private bool   _titleBarVisible = true;

        // Таймер скрытия бейджа зума через 1с после последнего изменения
        private readonly DispatcherTimer _zoomBadgeTimer;

        // Состояние пользовательского «развернуть»
        private bool _isMaximized;
        private Rect _restoreBounds;

        // Состояние перетаскивания (панорамирование) изображения
        private bool   _isPanning;
        private Point  _panStart;
        private double _scrollStartH;
        private double _scrollStartV;

        // Ожидание начала перетаскивания с заголовка в развёрнутом состоянии
        private bool _titleDragPending;

        public string ImageUrl { get; }

        public ImageViewerWindow(string imageUrl, string fileName)
        {
            InitializeComponent();
            ImageUrl = imageUrl;
            TitleText.Text = fileName;
            Title = fileName;

            _zoomBadgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _zoomBadgeTimer.Tick += (_, _) =>
            {
                _zoomBadgeTimer.Stop();
                ZoomBadge.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(300)));
            };

            Loaded += async (_, _) => await LoadImageAsync(imageUrl);
        }

        // ── Загрузка ─────────────────────────────────────────────────────────────

        private async Task LoadImageAsync(string url)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                var bitmap = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                MainImage.Source = bitmap;
                MainImage.Visibility = Visibility.Visible;
                LoadingText.Visibility = Visibility.Collapsed;

                FitToScreen(bitmap);
            }
            catch (Exception ex)
            {
                LoadingText.Text = $"Ошибка загрузки:\n{ex.Message}";
            }
        }

        // ── Масштабирование и размер окна ────────────────────────────────────────

        // Подбирает начальный зум и размер окна под реальный размер фото.
        private void FitToScreen(BitmapImage bitmap)
        {
            var workArea = SystemParameters.WorkArea;
            double maxW = workArea.Width  * 0.92;
            double maxH = workArea.Height * 0.92;

            double imgW = bitmap.Width;
            double imgH = bitmap.Height;

            _fitZoom = Math.Min(1.0, Math.Min(
                (maxW - HorizChrome) / imgW,
                (maxH - VertChrome)  / imgH));

            _minZoom = _fitZoom * 0.5;   // 50% от «вписать»
            _maxZoom = _fitZoom * 10.0;  // 1000% от «вписать»

            SetZoom(_fitZoom);

            Width  = Math.Clamp(imgW * _zoom + HorizChrome, 420, maxW);
            Height = Math.Clamp(imgH * _zoom + VertChrome,  320, maxH);
        }

        private void SetZoom(double zoom)
        {
            _zoom = Math.Clamp(zoom, _minZoom, _maxZoom);
            ImageScale.ScaleX = _zoom;
            ImageScale.ScaleY = _zoom;

            // Курсор: рука (панорамирование) или стрелка (перемещение окна)
            bool zoomed = _zoom > _fitZoom + 0.001;
            ImageScrollViewer.Cursor = zoomed ? Cursors.Hand : Cursors.Arrow;

            // Бейдж: 100% = изображение полностью вписано в окно (_fitZoom)
            ZoomBadgeText.Text = $"{_zoom / _fitZoom:P0}";
            ZoomBadge.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120)));
            _zoomBadgeTimer.Stop();
            _zoomBadgeTimer.Start();
        }

        // ── Колесо мыши ──────────────────────────────────────────────────────────

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            bool ctrl  = Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl);
            bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (ctrl)
            {
                // Ctrl+колесо — зум с относительным шагом и точками привязки
                double step = _fitZoom * 0.15;
                SetZoom(SnapZoom(_zoom + (e.Delta > 0 ? step : -step)));
                e.Handled = true;
            }
            else if (shift)
            {
                // Shift+колесо — горизонтальный скролл
                ImageScrollViewer.ScrollToHorizontalOffset(
                    ImageScrollViewer.HorizontalOffset - e.Delta / 3.0);
                e.Handled = true;
            }
        }

        // Притягивает зум к опорным точкам 50 / 80 / 100% (±9% от _fitZoom)
        private double SnapZoom(double newZoom)
        {
            double[] snaps     = { _fitZoom * 0.5, _fitZoom * 0.8, _fitZoom };
            double   snapRange = _fitZoom * 0.09;
            foreach (var snap in snaps)
                if (Math.Abs(newZoom - snap) < snapRange)
                    return snap;
            return newZoom;
        }

        // ── Панорамирование / перетаскивание окна ────────────────────────────────

        private void Image_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            bool canPan = ImageScrollViewer.ScrollableWidth > 1.0
                       || ImageScrollViewer.ScrollableHeight > 1.0;

            if (canPan)
            {
                _isPanning    = true;
                _panStart     = e.GetPosition(this);
                _scrollStartH = ImageScrollViewer.HorizontalOffset;
                _scrollStartV = ImageScrollViewer.VerticalOffset;
                ImageScrollViewer.CaptureMouse();
                ImageScrollViewer.Cursor = Cursors.SizeAll;
                e.Handled = true;
            }
            else
            {
                // Фото вписано в окно или отдалено — тянуть за окно
                DragMove();
            }
        }

        private void Image_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning || e.LeftButton != MouseButtonState.Pressed) return;

            var pos = e.GetPosition(this);
            ImageScrollViewer.ScrollToHorizontalOffset(_scrollStartH - (pos.X - _panStart.X));
            ImageScrollViewer.ScrollToVerticalOffset  (_scrollStartV - (pos.Y - _panStart.Y));
            e.Handled = true;
        }

        private void Image_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning) return;

            _isPanning = false;
            ImageScrollViewer.ReleaseMouseCapture();
            bool zoomed = _zoom > _fitZoom + 0.05;
            ImageScrollViewer.Cursor = zoomed ? Cursors.Hand : Cursors.Arrow;
            e.Handled = true;
        }

        // ── Заголовок: fade по позиции курсора ───────────────────────────────────

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SetTitleBarVisible(e.GetPosition(this).Y < 50);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            SetTitleBarVisible(false);
        }

        private void SetTitleBarVisible(bool visible)
        {
            if (visible == _titleBarVisible) return;
            _titleBarVisible = visible;
            TitleBarRow.BeginAnimation(OpacityProperty,
                new DoubleAnimation(visible ? 1.0 : 0.0, TimeSpan.FromMilliseconds(200)));
        }

        // ── Кнопки заголовка ─────────────────────────────────────────────────────

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            if (_isMaximized)
            {
                // Восстановление делаем не сразу, а когда мышь действительно
                // сдвинется — иначе DragMove не получает корректный anchor.
                _titleDragPending = true;
                TitleBarRow.CaptureMouse();
                e.Handled = true;
                return;
            }

            DragMove();
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_titleDragPending || e.LeftButton != MouseButtonState.Pressed) return;

            TitleBarRow.ReleaseMouseCapture();
            _titleDragPending = false;

            // Позиция курсора на экране в момент первого движения
            var mouseScreen = PointToScreen(e.GetPosition(this));
            // Относительная X-позиция курсора в окне (0…1), чтобы после восстановления
            // окно «держалось» под пальцем естественно
            double relX = Math.Clamp(e.GetPosition(this).X / ActualWidth, 0.05, 0.95);

            Width        = _restoreBounds.Width;
            Height       = _restoreBounds.Height;
            Left         = mouseScreen.X - _restoreBounds.Width * relX;
            Top          = mouseScreen.Y - TitleBarH / 2.0;
            _isMaximized = false;
            MaximizeBtn.Content = "□";
            OuterGrid.Margin    = new Thickness(8);

            DragMove();
        }

        private void TitleBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_titleDragPending) return;
            _titleDragPending = false;
            TitleBarRow.ReleaseMouseCapture();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isMaximized)
            {
                // Восстановить
                Left   = _restoreBounds.Left;
                Top    = _restoreBounds.Top;
                Width  = _restoreBounds.Width;
                Height = _restoreBounds.Height;
                _isMaximized = false;
                MaximizeBtn.Content = "□";
                OuterGrid.Margin = new Thickness(8);
            }
            else
            {
                // Развернуть на рабочую область (без перекрытия панели задач)
                _restoreBounds = new Rect(Left, Top, Width, Height);
                var wa = SystemParameters.WorkArea;
                Left   = wa.Left;
                Top    = wa.Top;
                Width  = wa.Width;
                Height = wa.Height;
                _isMaximized = true;
                MaximizeBtn.Content = "❐";
                OuterGrid.Margin = new Thickness(0);
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) =>
            Close();
    }
}
