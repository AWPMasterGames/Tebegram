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
            new User(1,"aa", "123", "Вася жопкин бамбук", "vasya",
                new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact("ybeka","Убека"),
                            new Contact("masya","Masya")
                        },"💬",false)
                }),


            new User(2,"aa1", "1234", "убека", "ybeka",
                 new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact("vasya","Вася жопкин бамбук"),
                            new Contact("vasya","Masya")
                        },"💬",false)
                 }),
             new User(3,"masya", "123", "Мася", "masya",
                 new ObservableCollection<ChatFolder>{
                    new ChatFolder("Все чаты",
                        new ObservableCollection<Contact> {
                            new Contact("vasya","Вася жопкин бамбук"),
                            new Contact("ybeka","Убебка")
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
