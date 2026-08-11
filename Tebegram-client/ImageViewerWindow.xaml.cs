using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Tebegrammmm.Classes;

namespace Tebegrammmm
{
    public partial class ImageViewerWindow : Window
    {
        // Сертификат сервера самоподписанный - как и в остальных клиентских запросах
        private static readonly HttpClient _http = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });

        // Хром: тень 8px + отступы ScrollViewer 12px с каждой стороны
        private const double ShadowMargin     = 8;
        private const double ScrollSideMargin = 12;
        private const double TitleBarH        = 36;
        private const double HorizChrome = (ShadowMargin + ScrollSideMargin) * 2; // 40
        private const double VertChrome  = TitleBarH + ScrollSideMargin + ShadowMargin * 2; // 68
        // Панель управления видео (слайдер + кнопка) с отступами - резервируем под неё высоту,
        // иначе она перекрывает нижнюю часть кадра
        private const double VideoBarH = 92;

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

        // ── Видео ────────────────────────────────────────────────────────────────
        // Тот же просмотрщик показывает и видео: снизу кнопка паузы и полоса перемотки.
        private readonly bool _isVideo;
        private DispatcherTimer _videoTimer;   // тикает, пока идёт воспроизведение
        private bool _videoSliderDragging;     // пользователь тащит ползунок - не перебиваем его позицией
        private bool _videoPlaying;

        // Автоскрытие панели управления: секунда без движения мыши - и она уходит
        private DispatcherTimer _barHideTimer;
        private bool _barVisible = true;

        // Полноэкранный режим (кнопка справа снизу): запоминаем, куда вернуться
        private bool _isFullscreen;
        private Rect _preFullscreenBounds;

        // Ролик играли потоком с сервера - после закрытия окна докачаем его в кэш
        private bool _cacheAfterClose;

        /// <summary>Расширения, которые открываем как видео (их играет MediaElement/WMP).</summary>
        public static bool IsVideoFile(string name)
        {
            string ext = Path.GetExtension(name ?? "").ToLowerInvariant();
            return ext == ".mp4" || ext == ".webm" || ext == ".mov"
                || ext == ".avi" || ext == ".mkv" || ext == ".wmv" || ext == ".m4v";
        }

        public ImageViewerWindow(string imageUrl, string fileName)
        {
            InitializeComponent();
            ImageUrl = imageUrl;
            TitleText.Text = fileName;
            Title = fileName;
            _isVideo = IsVideoFile(fileName);

            // Один и тот же размер для фото и для видео, на любом мониторе,
            // и строго по центру экрана (см. UiSizes)
            UiSizes.ApplyAndCenter(this, UiSizes.ViewerWidth, UiSizes.ViewerHeight);

            _zoomBadgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _zoomBadgeTimer.Tick += (_, _) =>
            {
                _zoomBadgeTimer.Stop();
                ZoomBadge.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(300)));
            };

            if (_isVideo)
                Loaded += (_, _) => StartVideo(imageUrl);
            else
                Loaded += async (_, _) => await LoadImageAsync(imageUrl);
        }

        // ── Видео: запуск, перемотка, пауза ──────────────────────────────────────

        /// <summary>
        /// Запускает ролик. Если он уже в кэше - играем ФАЙЛ С ДИСКА: старт без
        /// ожидания и перемотка мгновенная. Если нет - играем потоком с сервера,
        /// как раньше, а копию в кэш докачиваем после закрытия окна (см. Window_Closed),
        /// чтобы загрузка не отбирала канал у самого воспроизведения.
        /// </summary>
        private void StartVideo(string url)
        {
            try
            {
                if (Tebegrammmm.Data.MediaCache.TryGetLocalPath(url, out string localPath))
                    url = localPath;
                else
                    _cacheAfterClose = true;

                MainVideo.Visibility = Visibility.Visible;
                VideoBar.Visibility = Visibility.Visible;
                // Полосы прокрутки для видео не нужны (кадр всегда вписан), но сам
                // ScrollViewer оставляем видимым - внутри него живёт LoadingText,
                // который показывает «Загрузка…» и ошибку кодека
                ImageScrollViewer.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                ImageScrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                LoadingText.Text = "Загрузка видео...";

                SetFullscreenIcon();
                // Панель видна сразу после открытия, дальше живёт по движению мыши
                ShowVideoBar();

                // Полоса перемотки обновляется 4 раза в секунду - этого хватает
                // и не грузит UI лишними перерисовками
                _videoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                _videoTimer.Tick += (_, _) => UpdateVideoProgress();
                _videoTimer.Start();

                MainVideo.Source = new Uri(url);
                MainVideo.Play();
                SetPlaying(true);
            }
            catch (Exception ex)
            {
                LoadingText.Text = $"Не удалось открыть видео:\n{ex.Message}";
                LoadingText.Visibility = Visibility.Visible;
            }
        }

        private void Video_MediaOpened(object sender, RoutedEventArgs e)
        {
            LoadingText.Visibility = Visibility.Collapsed;

            // Размер окна под пропорции ролика не подгоняется: он задан константами
            // UiSizes, как и для фотографий, поэтому просмотрщик всегда открывается
            // одинаково. Кадр вписывается в доступную область сам, Stretch=Uniform.
            // Здесь остаётся выставить длину ролика на шкале перемотки.
            if (MainVideo.NaturalDuration.HasTimeSpan)
                VideoSlider.Maximum = MainVideo.NaturalDuration.TimeSpan.TotalSeconds;

            UpdateVideoProgress();
        }

        /// <summary>Клик по кадру - пауза/продолжить (окно двигается за верхнюю панель).</summary>
        private void Video_Click(object sender, MouseButtonEventArgs e) => TogglePlayPause();

        // ── Панель управления: показ по движению мыши, скрытие через секунду ──

        /// <summary>Показать панель и завести таймер скрытия заново.</summary>
        private void ShowVideoBar()
        {
            if (!_isVideo) return;

            if (!_barVisible)
            {
                _barVisible = true;
                VideoBar.IsHitTestVisible = true;
                VideoBar.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120)));
            }

            _barHideTimer ??= CreateBarHideTimer();
            _barHideTimer.Stop();
            _barHideTimer.Start();
        }

        private DispatcherTimer CreateBarHideTimer()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();

                // Прячем независимо от того, где стоит курсор - важно именно
                // ОТСУТСТВИЕ ДВИЖЕНИЯ (любое движение вернёт панель мгновенно).
                // Единственное исключение - пока тянут ползунок перемотки:
                // выдёргивать его из-под пальца нельзя
                if (_videoSliderDragging) { timer.Start(); return; }

                _barVisible = false;
                VideoBar.IsHitTestVisible = false;   // невидимая панель не должна ловить клики
                VideoBar.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(250)));
            };
            return timer;
        }

        // ── Полноэкранный режим ──────────────────────────────────────────────

        private void FullscreenBtn_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

        /// <summary>
        /// Во весь экран и обратно. Запоминаем прежние границы окна, чтобы повторное
        /// нажатие вернуло ровно тот размер и положение, что были до переключения.
        /// Занимаем ВЕСЬ монитор (а не рабочую область) - панель задач тоже скрывается.
        /// </summary>
        private void ToggleFullscreen()
        {
            if (_isFullscreen)
            {
                Left   = _preFullscreenBounds.Left;
                Top    = _preFullscreenBounds.Top;
                Width  = _preFullscreenBounds.Width;
                Height = _preFullscreenBounds.Height;
                OuterGrid.Margin = new Thickness(8);   // возвращаем поля с тенью
                _isFullscreen = false;
            }
            else
            {
                _preFullscreenBounds = new Rect(Left, Top, Width, Height);

                Rect screen = UiSizes.MonitorBoundsFor(this);
                Left   = screen.Left;
                Top    = screen.Top;
                Width  = screen.Width;
                Height = screen.Height;
                OuterGrid.Margin = new Thickness(0);   // без полей - кадр во весь экран
                _isFullscreen = true;
            }

            SetFullscreenIcon();
            ShowVideoBar();   // после переключения панель снова на виду
        }

        private void SetFullscreenIcon()
        {
            if (FullscreenIcon == null) return;
            FullscreenIcon.Data = (System.Windows.Media.Geometry)FindResource(
                _isFullscreen ? "IconFullscreenExit" : "IconFullscreen");
        }

        private void Video_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // Нет кодека (частый случай для mkv/avi) - предлагаем открыть системным плеером
            Log.Save($"[ImageViewer.Video] {e.ErrorException?.Message}");
            VideoBar.Visibility = Visibility.Collapsed;
            MainVideo.Visibility = Visibility.Collapsed;
            LoadingText.Visibility = Visibility.Visible;
            LoadingText.Text = "Не удалось воспроизвести это видео - \nв системе нет подходящего кодека.\n" +
                               "Сохрани файл и открой его своим плеером.";
            _videoTimer?.Stop();
        }

        private void Video_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Возвращаемся в начало и показываем «play» - можно пересмотреть
            MainVideo.Pause();
            MainVideo.Position = TimeSpan.Zero;
            SetPlaying(false);
            UpdateVideoProgress();
        }

        private void UpdateVideoProgress()
        {
            if (!_isVideo) return;

            TimeSpan pos = MainVideo.Position;
            TimeSpan dur = MainVideo.NaturalDuration.HasTimeSpan
                ? MainVideo.NaturalDuration.TimeSpan
                : TimeSpan.Zero;

            // Пока тащат ползунок - позицию не перезаписываем, иначе он «убегает» из-под курсора
            if (!_videoSliderDragging)
            {
                _videoSliderSyncing = true;
                VideoSlider.Value = pos.TotalSeconds;
                _videoSliderSyncing = false;
            }

            VideoTimeText.Text = $"{Format(pos)} / {Format(dur)}";

            static string Format(TimeSpan t) =>
                t.Hours > 0 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }

        private bool _videoSliderSyncing; // отличает программное обновление от пользовательского

        private void SetPlaying(bool playing)
        {
            _videoPlaying = playing;
            PlayPauseIcon.Data = (System.Windows.Media.Geometry)FindResource(playing ? "IconPause" : "IconPlay");
        }

        private void TogglePlayPause()
        {
            if (!_isVideo) return;

            if (_videoPlaying) MainVideo.Pause();
            else MainVideo.Play();
            SetPlaying(!_videoPlaying);
        }

        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e) => TogglePlayPause();

        private void VideoSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
            => _videoSliderDragging = true;

        private void VideoSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _videoSliderDragging = false;
            MainVideo.Position = TimeSpan.FromSeconds(VideoSlider.Value);
            UpdateVideoProgress();
        }

        private void VideoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isVideo || _videoSliderSyncing) return;

            // Клик по дорожке (IsMoveToPointEnabled) приходит сюда без Drag-событий - 
            // перематываем сразу; во время перетаскивания даём «живую» перемотку
            MainVideo.Position = TimeSpan.FromSeconds(e.NewValue);
            if (_videoSliderDragging) UpdateVideoProgress();
        }

        // ── Загрузка ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Показывает фото в полном размере. Байты берутся через MediaCache: если
        /// снимок уже смотрели (или он просто был виден в чате), он лежит на диске
        /// и открывается мгновенно; при промахе скачивается и остаётся в кэше.
        /// </summary>
        private async Task LoadImageAsync(string url)
        {
            try
            {
                var bytes = await Tebegrammmm.Data.MediaCache.GetBytesAsync(url)
                            ?? await _http.GetByteArrayAsync(url); // кэш не смог - пробуем напрямую
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

        /// <summary>
        /// Вписывает снимок в окно. Размер самого окна БОЛЬШЕ не зависит от размера
        /// фото и от разрешения монитора - он задан константами (UiSizes) в
        /// конструкторе, поэтому просмотрщик всегда открывается одинаково.
        /// </summary>
        private void FitToScreen(BitmapImage bitmap)
        {
            // Область просмотра могла ещё не перемериться - вписываем после раскладки
            Dispatcher.BeginInvoke(new Action(() => FitToViewport(resetZoom: true)),
                DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Вписывает фото в ФАКТИЧЕСКУЮ область просмотра, чтобы прокрутки не было
        /// вовсе. Раньше зум считался по «прикидке» из констант хрома, а они не
        /// учитывали, например, полосы прокрутки - фото оказывалось на десяток
        /// пикселей больше области, появлялся скролл и снимок можно было таскать.
        /// </summary>
        private void FitToViewport(bool resetZoom)
        {
            if (MainImage.Source is not BitmapSource bitmap) return;

            double areaW = ImageScrollViewer.ViewportWidth;
            double areaH = ImageScrollViewer.ViewportHeight;
            if (areaW < 1 || areaH < 1) return; // окно ещё не разложено

            double imgW = bitmap.Width, imgH = bitmap.Height;
            if (imgW < 1 || imgH < 1) return;

            bool wasFitted = Math.Abs(_zoom - _fitZoom) < 0.001; // пользователь не менял масштаб

            // Без ограничения «не больше 1.0»: снимок должен занимать всю доступную
            // область, даже если он мельче окна
            _fitZoom = Math.Min(areaW / imgW, areaH / imgH);
            _minZoom = _fitZoom * 0.5;
            _maxZoom = _fitZoom * 10.0;

            if (!resetZoom && !wasFitted) return;

            SetZoom(_fitZoom);

            // Доводка. Область просмотра сама зависит от полос прокрутки: пока они
            // видны, ViewportWidth/Height меньше на их толщину, и посчитанный по
            // ним масштаб оставлял снимок на несколько пикселей больше области - 
            // фото опять можно было тянуть. Один проход после раскладки убирает
            // остаток: ужимаем ровно во столько, во сколько содержимое вылезло.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (Math.Abs(_zoom - _fitZoom) > 0.0001) return; // масштаб уже сменили вручную

                if (ImageScrollViewer.ScrollableWidth < 0.5 && ImageScrollViewer.ScrollableHeight < 0.5)
                    return; // всё вписано

                double kx = ImageScrollViewer.ViewportWidth  / Math.Max(1.0, ImageScrollViewer.ExtentWidth);
                double ky = ImageScrollViewer.ViewportHeight / Math.Max(1.0, ImageScrollViewer.ExtentHeight);
                double k = Math.Min(kx, ky);
                if (k <= 0 || k >= 1) return;

                _fitZoom *= k;
                _minZoom = _fitZoom * 0.5;
                _maxZoom = _fitZoom * 10.0;
                SetZoom(_fitZoom);
            }), DispatcherPriority.Loaded);
        }

        // Пользователь потянул за край окна - перевписываем фото под новый размер.
        // ВАЖНО: считаем не сразу, а после раскладки. В момент SizeChanged
        // ViewportWidth/Height у ScrollViewer ещё СТАРЫЕ, и масштаб получался
        // рассчитанным под прежний размер окна: после «поиграть с размерами и
        // вернуться на 100%» фото снова оказывалось больше области и прокручивалось.
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isVideo) return;   // видео вписывает сам Stretch=Uniform
            Dispatcher.BeginInvoke(new Action(() => FitToViewport(resetZoom: false)),
                DispatcherPriority.Loaded);
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
            // У видео масштабирования нет: кадр всегда вписан в окно целиком
            if (_isVideo) return;

            bool ctrl  = Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl);
            bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (ctrl)
            {
                // Ctrl+колесо - зум с относительным шагом и точками привязки
                double step = _fitZoom * 0.15;
                SetZoom(SnapZoom(_zoom + (e.Delta > 0 ? step : -step)));
                e.Handled = true;
            }
            else if (shift)
            {
                // Shift+колесо - горизонтальный скролл
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
            // Видео живёт вне ScrollViewer (вписывается целиком, без зума и панорамирования),
            // поэтому сюда попадают только клики по фото
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
                // Фото вписано в окно или отдалено - тянуть за окно
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

        private Point _lastMousePos = new(double.NaN, double.NaN);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point pos = e.GetPosition(this);
            SetTitleBarVisible(pos.Y < 50);

            // Панель возвращает только РЕАЛЬНОЕ движение курсора. WPF шлёт MouseMove
            // и когда картинка под курсором просто перерисовалась (а видео
            // перерисовывается каждый кадр) - такие события сбрасывали таймер,
            // и панель не скрывалась никогда.
            if (!double.IsNaN(_lastMousePos.X) &&
                Math.Abs(pos.X - _lastMousePos.X) < 1 && Math.Abs(pos.Y - _lastMousePos.Y) < 1)
                return;

            _lastMousePos = pos;
            ShowVideoBar();
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
                // сдвинется - иначе DragMove не получает корректный anchor.
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

        // ── Изменение размера окна ручками по краям ──────────────────────────
        // MinWidth/MinHeight не дают схлопнуть окно; фото после ресайза
        // перевписывается само (Window_SizeChanged → FitToViewport).

        private void ResizeRight_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
            => Width = Math.Max(MinWidth, Width + e.HorizontalChange);

        private void ResizeBottom_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
            => Height = Math.Max(MinHeight, Height + e.VerticalChange);

        private void ResizeCorner_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Width  = Math.Max(MinWidth,  Width  + e.HorizontalChange);
            Height = Math.Max(MinHeight, Height + e.VerticalChange);
        }

        // Пробел - привычная пауза/продолжить, как в любом плеере
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Esc: сначала выходим из полного экрана, и только потом закрываем окно
            if (e.Key == Key.Escape)
            {
                if (_isFullscreen) { ToggleFullscreen(); e.Handled = true; return; }
                Close();
                return;
            }

            if (!_isVideo) return;

            if (e.Key == Key.Space)
            {
                TogglePlayPause();
                ShowVideoBar();
                e.Handled = true;
            }
            else if (e.Key == Key.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Без этого звук ролика продолжал играть после закрытия окна
            _videoTimer?.Stop();
            _videoTimer = null;
            try
            {
                MainVideo.Stop();
                MainVideo.Close();
                MainVideo.Source = null;
            }
            catch (Exception ex)
            {
                Log.Save($"[ImageViewer.Closed] {ex.Message}");
            }

            // Ролик смотрели потоком - теперь тихо забираем копию в кэш, чтобы
            // в следующий раз он открылся сразу и без сети
            if (_cacheAfterClose) Tebegrammmm.Data.MediaCache.Prefetch(ImageUrl);

            base.OnClosed(e);
        }
    }
}
