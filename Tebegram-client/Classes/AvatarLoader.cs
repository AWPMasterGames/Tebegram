using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Tebegrammmm.Data;

namespace Tebegrammmm.Classes
{
    public static class AvatarLoader
    {
        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });

        public static async Task<BitmapImage> LoadAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                byte[] bytes;
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    string cachePath = AppPaths.GetAvatarCachePath(url);
                    if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
                    {
                        bytes = await Task.Run(() => File.ReadAllBytes(cachePath));
                    }
                    else
                    {
                        bytes = await _http.GetByteArrayAsync(url);
                        _ = Task.Run(() => { try { File.WriteAllBytes(cachePath, bytes); } catch { } });
                    }
                }
                else
                {
                    bytes = await Task.Run(() => File.ReadAllBytes(url));
                }

                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }
    }
}
