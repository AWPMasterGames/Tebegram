using System;

namespace Tebegrammmm.Data
{
    public static class ServerData
    {
        private static string _ServerAdress = "https://1nnf1f30-5000.euw.devtunnels.ms";
        public static string ServerAdress {  get { return _ServerAdress; } }

        private static System.Net.WebClient wc = new System.Net.WebClient();
        public static void GetServerAdress()
        {
            string _ServerAdress = wc.DownloadString("https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Tebegrammmm/Adress.txt");
        }
    }
}
