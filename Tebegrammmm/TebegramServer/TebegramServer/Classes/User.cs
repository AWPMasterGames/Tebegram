namespace TebegramServer.Classes
{
    public class User
    {
        private string _Login;
        private string _Password;

        public string Login { get { return _Login; } }
        public string Password { get { return _Password; } }
        public User(string login, string password)
        {
            _Login = login;
            _Password = password;
        }

        public bool Authorize(string login, string password)
        {
            if (login == _Login & password == _Password) return true;
            return false;
        }
    }
}
