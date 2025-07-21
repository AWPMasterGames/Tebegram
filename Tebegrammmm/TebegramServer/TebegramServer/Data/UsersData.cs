using System.Collections.ObjectModel;

namespace TebegramServer.Data
{
    public static class UsersData
    {
        static ObservableCollection<User> Users = new ObservableCollection<User>()
                {
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
                };

        public static bool IsExistUser(string login)
        {
            return Users.Any(user => user.Login == login);
        }

        public static User? Authorize(string login, string password)
        {
            return Users.FirstOrDefault(user => user.Authorize(login,password));
        }
        public static User? FindUserById(int id)
        {
            return Users.FirstOrDefault(user => user.Id == id);
        }
        public static User? FindUserByLogin(string login)
        {
            return Users.FirstOrDefault(user => user.Login == login);
        }
        public static User? FindUserByUsername(string username)
        {
            return Users.FirstOrDefault(user => user.Username == username);
        }

        public static void AddUser(User user)
        {
            Users.Add(user);
        }
    }
}
