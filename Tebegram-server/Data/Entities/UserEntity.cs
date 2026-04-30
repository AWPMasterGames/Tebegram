using System.ComponentModel.DataAnnotations;

namespace TebegramServer.Data.Entities
{
    public class UserEntity
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Login { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";

        [Required, MaxLength(100)]
        public string Name { get; set; } = "";

        [Required, MaxLength(100)]
        public string Username { get; set; } = "";

        public string? Avatar { get; set; }

        public ICollection<ContactEntity> Contacts { get; set; } = new List<ContactEntity>();
    }
}
