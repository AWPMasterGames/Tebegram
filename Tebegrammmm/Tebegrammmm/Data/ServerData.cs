using System;
using System.Net.Http;

namespace Tebegrammmm.Data
{
    public static class ServerData
    {
        private static string _ServerAdress = "https://1nnf1f30-5000.euw.devtunnels.ms";
        public static string ServerAdress { get { return _ServerAdress; } }
        private static HttpClient _httpClient;

        private static System.Net.WebClient _wc = new System.Net.WebClient();
        public static async void CheckAdressValid()
        {
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/");
                using HttpResponseMessage response = await _httpClient.SendAsync(request);
            }
            catch
            {
                _ServerAdress = "https://localhost:5000";
            }
        }
        public static void GetServerAdress()
        {
            try
            {
                string _ServerAdress = _wc.DownloadString("https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Tebegrammmm/Adress.txt");
            }
            catch
            {
                _ServerAdress = "https://localhost:5000";
            }
        }
    }
}
