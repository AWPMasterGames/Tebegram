using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Tebegrammmm
{
    public partial class ImageViewerWindow : Window
    {
        private static readonly HttpClient _http = new();

        public string ImageUrl { get; }

        public ImageViewerWindow(string imageUrl, string fileName)
        {
            InitializeComponent();
            ImageUrl = imageUrl;
            TitleText.Text = fileName;
            Title = fileName;

            Width = SystemParameters.PrimaryScreenWidth * 0.6;
            Height = SystemParameters.PrimaryScreenHeight * 0.75;

            Loaded += async (_, _) => await LoadImageAsync(imageUrl);
        }

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
            }
            catch (Exception ex)
            {
                LoadingText.Text = $"Ошибка загрузки:\n{ex.Message}";
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void CloseBtn_Click(object sender, RoutedEventArgs e) =>
            Close();
    }
}
