using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Tebegrammmm
{
    public static class UsersData
    {
        static private ObservableCollection<User> Users = new ObservableCollection<User>()
        {
            new User(1,"aa", "123", "Вася жопкин бамбук", "127.0.0.1", 4004,
                new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact(IPAddress.Parse( "127.0.0.1"),4005,"Убека"),
                            new Contact(IPAddress.Parse( "127.0.0.1"),5005,"Masya")
                        },"💬",false)
                }),


            new User(2,"aa1", "1234", "убека", "127.0.0.1", 4005,
                 new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact(IPAddress.Parse( "127.0.0.1"),4004,"Вася жопкин бамбук"),
                            new Contact(IPAddress.Parse( "127.0.0.1"),5005,"Masya")
                        },"💬",false)
                 }),
             new User(3,"masya", "123", "Мася", "127.0.0.1", 4005,
                 new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact(IPAddress.Parse( "127.0.0.1"),4004,"Вася жопкин бамбук"),
                            new Contact(IPAddress.Parse( "127.0.0.1"),4005,"Убебка")
                        },"💬",false)
                 })
        };

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

        public static ObservableCollection<User> GetUsers()
        {
            return Users;
        }

        public static User FindUser(string userLogin)
        {
            foreach (var user in Users)
            {
                if (userLogin == user.Login) return user;
            }
            return null;
        }
    }
}
