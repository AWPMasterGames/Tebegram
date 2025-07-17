using System.Collections.ObjectModel;
using TebegramServer.Classes;
namespace TebegramServer.Data
{
    public static class UsersData
    {
        static ObservableCollection<User> Users = new ObservableCollection<User>()
                {
<<<<<<< Updated upstream
                    new User("aa","123"),
                    new User("aa1","1234"),
                    new User("masya","123")
=======
            new User(1,"aa", "123", "Вася жопкин бамбук", "vasya",
                new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact("ybeka","Убека"),
                            new Contact("masya","Masya")
                        },"💬",false)
                }),


            new User(2,"aa1", "123", "убека", "ybeka",
                 new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact("vasya","Вася жопкин бамбук"),
                            new Contact("masya","Masya")
                        },"💬",false)
                 }),
             new User(3,"masya", "123", "Мася", "masya",
                 new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact("vasya","Вася жопкин бамбук"),
                            new Contact("ybeka","Убебка")
                        },"💬",false)
                 }),
             new User(4, "admin", "123", "Админ", "admin_228",
                 new ObservableCollection<ChatFolder>
                 {
                     new ChatFolder("Все чаты",
                         new ObservableCollection<Contact>
                         {
                             new Contact("vasya", "Вася"),
                             new Contact("ybeka", "убека")
                         }, "💬", false)
                 })
>>>>>>> Stashed changes
                };

        public static ObservableCollection<User> GetUsers()
        {
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
                if (userLogin == user.Login) return user;
            }
            return null;
        }

        public static bool IsExistUser(string login)
        {
            foreach (var user in Users)
            {
                if (login == user.Login) return true;
            }
            return false;
        }
    }

}
