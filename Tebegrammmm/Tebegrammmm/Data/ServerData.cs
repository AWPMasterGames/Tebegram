using System;
using System.Net.Http;
using System.Windows;

namespace Tebegrammmm.Data
{
    public static class ServerData
    {
        private static string _ServerAdress;
        public static string ServerAdress { get { return _ServerAdress; } }

        private static System.Net.WebClient _wc = new System.Net.WebClient();
        public static async void CheckAdressValid()
        {
            string a = "?";
            try
            {
                a = _wc.DownloadString($"{ServerAdress}/Test");
                if (a == "HI!") return;
            }
            catch
            {
                _ServerAdress = "https://localhost:5000";
            }
            finally {
                //MessageBox.Show(a + ServerAdress);
            }
        }
        public static void GetServerAdress()
        {
            try
            {
                _ServerAdress = _wc.DownloadString("https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Tebegrammmm/Adress.txt").Split("\n")[0].Trim();
                CheckAdressValid();
            }
            catch
            {
                _ServerAdress = "https://localhost:5000";
            }
        }
    }
}
