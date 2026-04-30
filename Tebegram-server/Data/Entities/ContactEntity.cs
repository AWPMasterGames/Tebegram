using System.ComponentModel.DataAnnotations;

namespace TebegramServer.Data.Entities
{
    // Контакт: пользователь OwnerId добавил пользователя ContactUserId в свой список
    public class ContactEntity
    {
        public int Id { get; set; }

        public int OwnerId { get; set; }
        public UserEntity Owner { get; set; } = null!;

        public int ContactUserId { get; set; }
        public UserEntity ContactUser { get; set; } = null!;

        // Кастомное имя, которое владелец задал этому контакту
        [MaxLength(100)]
        public string? DisplayName { get; set; }
    }
}
