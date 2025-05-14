using System.Collections.ObjectModel;
using TebegramServer.Classes;
namespace TebegramServer.Data
{
    public static class UsersData
    {
        static ObservableCollection<User> Users;

        public static ObservableCollection<User> GetUsers()
        {
            if (Users == null)
            {
                Users = new ObservableCollection<User>()
                {
                    new User("aa","123"),
                    new User("aa1","1234"),
                    new User("masya","123")
                };
            }
            return Users;
        }

        public static void AddUser(User user)
        {
            Users.Add(user);
        }
        public static void RemoveUser(User user)
        {
            Users.Remove(user);
        }

        public static User Authorize(string login, string password)
        {
            for (int i = 0; i < Users.Count; i++)
            {
                if (Users[i].Login == login)
                {
                    if (Users[i].Authorize(login, password))
                    {
                        return Users[i];
                    }
                }
            }
            return null;
        }

        public static User FindUser(string userLogin)
        {
            foreach (var user in Users)
            {
                if(userLogin == user.Login) return user;
            }
            return null;
        }
    }

}
