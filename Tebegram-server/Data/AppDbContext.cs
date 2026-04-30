using Microsoft.EntityFrameworkCore;
using TebegramServer.Data.Entities;

namespace TebegramServer.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<UserEntity> Users { get; set; }
        public DbSet<MessageEntity> Messages { get; set; }
        public DbSet<ContactEntity> Contacts { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ID пользователя назначается вручную, не БД
            modelBuilder.Entity<UserEntity>()
                .Property(u => u.Id)
                .ValueGeneratedNever();

            // Уникальные логин и username
            modelBuilder.Entity<UserEntity>()
                .HasIndex(u => u.Login).IsUnique();

            modelBuilder.Entity<UserEntity>()
                .HasIndex(u => u.Username).IsUnique();

            // Контакт: один пользователь (Owner) добавил другого (ContactUser)
            modelBuilder.Entity<ContactEntity>()
                .HasOne(c => c.Owner)
                .WithMany(u => u.Contacts)
                .HasForeignKey(c => c.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContactEntity>()
                .HasOne(c => c.ContactUser)
                .WithMany()
                .HasForeignKey(c => c.ContactUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Один пользователь не может добавить одного и того же контакта дважды
            modelBuilder.Entity<ContactEntity>()
                .HasIndex(c => new { c.OwnerId, c.ContactUserId }).IsUnique();

            // Индекс для быстрой выборки переписки между двумя пользователями
            modelBuilder.Entity<MessageEntity>()
                .HasIndex(m => new { m.FromUsername, m.ToUsername, m.SentAt });
        }
    }
}
