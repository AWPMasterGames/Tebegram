using System.Collections.ObjectModel;
using System.Net;
using TebegramServer.Classes;
using System.Linq;

namespace TebegramServer.Data
{
    public static class UsersData
    {
        static ObservableCollection<User> Users = new ObservableCollection<User>()
                {
                    new User(1,"aa", "123", "Вася", "localhost", 8001,
                new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact(IPAddress.Parse( "127.0.0.1"),8002,"убека", "aa1"),
                            new Contact(IPAddress.Parse( "127.0.0.1"),8003,"Админ", "Я"),
                            new Contact(IPAddress.Parse( "127.0.0.1"),8004,"Мася", "masya")
                        },"💬",false)
                }),


            new User(2,"aa1", "1234", "убека", "localhost", 8002,
                 new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact(IPAddress.Parse( "127.0.0.1"),8001,"Вася", "aa"),
                            new Contact(IPAddress.Parse( "127.0.0.1"),8003,"Админ", "Я"),
                            new Contact(IPAddress.Parse( "127.0.0.1"),8004,"Мася", "masya")
                        },"💬",false)
                 }),
            new User(3,"Я", "1", "Админ", "localhost", 8003,
                 new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact(IPAddress.Parse( "127.0.0.1"),8001,"Вася", "aa"),
                            new Contact(IPAddress.Parse( "127.0.0.1"),8002,"убека", "aa1"),
                            new Contact(IPAddress.Parse( "127.0.0.1"),8004,"Мася", "masya")
                        },"💬",false)
                 }),
             new User(4, "masya", "123", "Мася", "localhost", 8004,
                 new ObservableCollection<ChatFolder>
                 {
                     new ChatFolder("Все чаты",
                         new ObservableCollection<Contact>
                         {
                             new Contact(IPAddress.Parse("127.0.0.1"), 8001, "Вася", "aa"),
                             new Contact(IPAddress.Parse("127.0.0.1"), 8002, "убека", "aa1"),
                             new Contact(IPAddress.Parse("127.0.0.1"), 8003, "Админ", "Я")
                         }, "💬", false)
                 })
                };

        public static bool IsExistUser(string login)
        {
            return Users.Any(user => user.Login == login);
        }

        public static User? Authorize(string login, string password)
        {
            return Users.FirstOrDefault(user => user.Login == login && user.Password == password);
        }

        public static User? FindUser(string login)
        {
            return Users.FirstOrDefault(user => user.Login == login);
        }

        public static void AddUser(User user)
        {
            Users.Add(user);
        }
    }
}
