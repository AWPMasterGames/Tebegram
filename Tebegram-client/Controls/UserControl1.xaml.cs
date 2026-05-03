using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Tebegrammmm.Data;

namespace Tebegrammmm.Controls
{
    public partial class UserControl1 : UserControl
    {
        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });

        public static readonly DependencyProperty AvatarProperty = DependencyProperty.Register(
            nameof(Avatar),
            typeof(string),
            typeof(UserControl1),
            new PropertyMetadata(null, OnAvatarChanged));

        public static readonly DependencyProperty LoadedImageProperty = DependencyProperty.Register(
            nameof(LoadedImage),
            typeof(BitmapImage),
            typeof(UserControl1),
            new PropertyMetadata(null));

        public string Avatar
        {
            get => (string)GetValue(AvatarProperty);
            set => SetValue(AvatarProperty, value);
        }

        public BitmapImage LoadedImage
        {
            get => (BitmapImage)GetValue(LoadedImageProperty);
            set => SetValue(LoadedImageProperty, value);
        }

        public UserControl1()
        {
            InitializeComponent();
        }

        private static void OnAvatarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UserControl1 control && e.NewValue is string url && !string.IsNullOrWhiteSpace(url))
                control.LoadImageAsync(url);
        }

        private async void LoadImageAsync(string url)
        {
            try
            {
                byte[] bytes;

                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    string cachePath = AppPaths.GetAvatarCachePath(url);

                    if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
                    {
                        // Берём из кэша — мгновенно, без сети
                        bytes = File.ReadAllBytes(cachePath);
                    }
                    else
                    {
                        // Скачиваем и сохраняем в кэш
                        bytes = await _http.GetByteArrayAsync(url);
                        _ = System.Threading.Tasks.Task.Run(() =>
                        {
                            try { File.WriteAllBytes(cachePath, bytes); }
                            catch { }
                        });
                    }
                }
                else
                {
                    // Локальный файл (временный показ до ответа сервера)
                    bytes = File.ReadAllBytes(url);
                }

                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                LoadedImage = bmp;
            }
            catch { }
        }
    }
}
