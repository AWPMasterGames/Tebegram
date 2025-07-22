namespace TebegramServer.Data
{
    public static class ServerData
    {
        private static string _ServerAdress = "http://localhost:5005";

        public static string ServerAdress {  get { return _ServerAdress; } }
    }
}
