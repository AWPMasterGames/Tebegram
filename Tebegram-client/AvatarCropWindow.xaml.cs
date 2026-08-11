using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tebegrammmm
{
    /// <summary>
    /// Окно обрезки аватара (как в Telegram): предпросмотр в круге,
    /// перетаскивание фото и масштаб. По «Поставить» отдаёт обрезанный квадрат (PNG).
    /// </summary>
    public partial class AvatarCropWindow : Window
    {
        private const int CropSize = 300;    // размер области обрезки (px в разметке)
        private const int OutputSize = 1024; // размер итогового аватара (как в вебе)

        private bool _dragging;
        private Point _dragStart;
        private double _startX, _startY;

        /// <summary>Путь к сохранённому обрезанному PNG (после успешного применения).</summary>
        public string CroppedPngPath { get; private set; }

        public AvatarCropWindow(string imagePath)
        {
            InitializeComponent();

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
            // WPF игнорирует EXIF-ориентацию - фото с телефона грузилось повёрнутым,
            // и аватар после обрезки оказывался «на боку»/«вверх ногами»
            bmp.Rotation = ReadExifRotation(imagePath);
            bmp.EndInit();
            bmp.Freeze();
            CropImage.Source = bmp;
        }

        /// <summary>Читает EXIF Orientation (JPEG с телефона) и переводит в Rotation для WPF.</summary>
        private static Rotation ReadExifRotation(string imagePath)
        {
            try
            {
                using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                if (frame.Metadata is BitmapMetadata meta &&
                    meta.ContainsQuery("System.Photo.Orientation") &&
                    meta.GetQuery("System.Photo.Orientation") is ushort orientation)
                {
                    return orientation switch
                    {
                        3 => Rotation.Rotate180,
                        6 => Rotation.Rotate90,
                        8 => Rotation.Rotate270,
                        _ => Rotation.Rotate0,
                    };
                }
            }
            catch
            {
                // метаданных нет (PNG/BMP) или не читаются - без поворота
            }
            return Rotation.Rotate0;
        }

        // ── Перетаскивание фото ──────────────────────────────────────────────
        private void CropArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragging = true;
            _dragStart = e.GetPosition(CropArea);
            _startX = ImgTranslate.X;
            _startY = ImgTranslate.Y;
            CropArea.CaptureMouse();
        }

        private void CropArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point p = e.GetPosition(CropArea);
            ImgTranslate.X = _startX + (p.X - _dragStart.X);
            ImgTranslate.Y = _startY + (p.Y - _dragStart.Y);
        }

        private void CropArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;
            CropArea.ReleaseMouseCapture();
        }

        // ── Масштаб колесом и ползунком ──────────────────────────────────────
        private void CropArea_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double step = e.Delta > 0 ? 0.15 : -0.15;
            ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + step));
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ImgScale == null) return;
            ImgScale.ScaleX = e.NewValue;
            ImgScale.ScaleY = e.NewValue;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Тащим окно только за фон (не за область обрезки)
            if (e.ChangedButton == MouseButton.Left && !CropArea.IsMouseOver)
                this.DragMove();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Рендерим область обрезки БЕЗ затемнения и кольца
                MaskOverlay.Visibility = Visibility.Collapsed;
                RingOverlay.Visibility = Visibility.Collapsed;
                CropArea.UpdateLayout();

                // Рендерим через VisualBrush сразу в 512px - раньше рендерили 300px
                // и растягивали (аватар терял в качестве)
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                    dc.DrawRectangle(new VisualBrush(CropArea), null, new Rect(0, 0, OutputSize, OutputSize));

                var rtb = new RenderTargetBitmap(OutputSize, OutputSize, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);

                MaskOverlay.Visibility = Visibility.Visible;
                RingOverlay.Visibility = Visibility.Visible;

                string path = Path.Combine(Path.GetTempPath(), $"tbg_avatar_{Guid.NewGuid():N}.png");
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    encoder.Save(fs);
                }

                CroppedPngPath = path;
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось обрезать изображение: {ex.Message}", "Ошибка");
            }
        }
    }
}
