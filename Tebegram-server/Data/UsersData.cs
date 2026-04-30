using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using TebegramServer.Data.Entities;

namespace TebegramServer.Data
{
    public static class UsersData
    {
        static ObservableCollection<User> Users = new ObservableCollection<User>();
        public static int UsersCount { get { return Users.Count; } }

        private static IDbContextFactory<AppDbContext>? _dbFactory;

        public static bool IsExistUser(string login) =>
            Users.Any(u => u.Login == login);

        public static User? Authorize(string login, string password) =>
            Users.FirstOrDefault(u => u.Authorize(login, password));

        public static User? FindUserById(int id) =>
            Users.FirstOrDefault(u => u.Id == id);

        public static User? FindUserByLogin(string login) =>
            Users.FirstOrDefault(u => u.Login == login);

        public static User? FindUserByUsername(string username) =>
            Users.FirstOrDefault(u => u.Username == username);

        // Вызывается один раз при старте сервера
        public static void Initialize(IDbContextFactory<AppDbContext> factory)
        {
            _dbFactory = factory;
            LoadFromDb();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Данные пользователей загружены из БД. Пользователей: {Users.Count}");
        }

        private static void LoadFromDb()
        {
            using var db = _dbFactory!.CreateDbContext();

            var userEntities = db.Users
                .Include(u => u.Contacts)
                    .ThenInclude(c => c.ContactUser)
                .ToList();

            foreach (var ue in userEntities)
            {
                var contacts = new ObservableCollection<Contact>();
                foreach (var ce in ue.Contacts)
                {
                    contacts.Add(new Contact(
                        ce.ContactUserId,
                        ce.ContactUser.Username,
                        ce.DisplayName ?? ce.ContactUser.Name
                    ));
                }

                var folder = new ChatFolder("Все чаты", contacts, "", false);
                Users.Add(new User(
                    ue.Id, ue.Login, ue.Password, ue.Name, ue.Username,
                    new ObservableCollection<ChatFolder> { folder },
                    ue.Avatar ?? ""
                ));
            }
        }

        // Добавляет пользователя в память после того, как он уже сохранён в БД
        public static void AddToMemory(User user)
        {
            if (!Users.Any(u => u.Login == user.Login))
            {
                Users.Add(user);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Пользователь {user.Login} добавлен (id={user.Id})");
            }
        }

        // Добавляет контакт в БД и в память
        public static void AddContact(int ownerId, Contact contact, AppDbContext db)
        {
            var owner = FindUserById(ownerId);
            if (owner == null) return;

            // Уже есть в памяти — не дублируем
            if (owner.FindContactByUsername(contact.Username) != null) return;

            var contactUser = FindUserByUsername(contact.Username);
            if (contactUser == null) return;

            // Проверяем, нет ли уже в БД (на случай рассинхрона)
            bool alreadyInDb = db.Contacts.Any(c => c.OwnerId == ownerId && c.ContactUserId == contactUser.Id);
            if (!alreadyInDb)
            {
                db.Contacts.Add(new ContactEntity
                {
                    OwnerId = ownerId,
                    ContactUserId = contactUser.Id,
                    DisplayName = contact.Name != contactUser.Name ? contact.Name : null
                });
                db.SaveChanges();
            }

            owner.ChatsFolders[0].Contacts.Add(contact);
        }

        // Удаляет контакт из БД и из памяти
        public static void RemoveContact(int ownerId, string contactUsername, AppDbContext db)
        {
            var owner = FindUserById(ownerId);
            var contactUser = FindUserByUsername(contactUsername);
            if (owner == null || contactUser == null) return;

            var entity = db.Contacts.FirstOrDefault(c => c.OwnerId == ownerId && c.ContactUserId == contactUser.Id);
            if (entity != null)
            {
                db.Contacts.Remove(entity);
                db.SaveChanges();
            }

            var contact = owner.FindContactByUsername(contactUsername);
            if (contact != null) owner.RemoveContact(contact);
        }

        // Переименовывает контакт в БД и в памяти
        public static void UpdateContactName(int ownerId, string contactUsername, string newName, AppDbContext db)
        {
            var owner = FindUserById(ownerId);
            var contactUser = FindUserByUsername(contactUsername);
            if (owner == null || contactUser == null) return;

            var entity = db.Contacts.FirstOrDefault(c => c.OwnerId == ownerId && c.ContactUserId == contactUser.Id);
            if (entity != null)
            {
                entity.DisplayName = newName;
                db.SaveChanges();
            }

            owner.FindContactByUsername(contactUsername)?.ChangeName(newName);
        }

        // Обновляет аватар в БД и в памяти
        public static void UpdateAvatar(int userId, string avatarFileName, AppDbContext db)
        {
            var entity = db.Users.Find(userId);
            if (entity != null)
            {
                entity.Avatar = avatarFileName;
                db.SaveChanges();
            }

            var user = FindUserById(userId);
            if (user != null) user.Avatar = avatarFileName;
        }
    }
}
