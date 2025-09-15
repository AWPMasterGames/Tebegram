using System;
using System.Net.Http;
using System.Windows;

namespace Tebegrammmm.Data
{
    public static class ServerData
    {
        private static string _ServerAdress = "https://1nnf1f30-5000.euw.devtunnels.ms";
        public static string ServerAdress { get { return _ServerAdress; } }
        private static HttpClient _httpClient = new HttpClient();

        private static System.Net.WebClient _wc = new System.Net.WebClient();
        public static async void CheckAdressValid()
        {
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerAdress}/");
                using HttpResponseMessage response = await _httpClient.SendAsync(request);
                MessageBox.Show(response.Content.ReadAsStringAsync().Result);
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
                //CheckAdressValid();
            }
            catch
            {
                _ServerAdress = "https://localhost:5000";
            }
        }
    }
}
