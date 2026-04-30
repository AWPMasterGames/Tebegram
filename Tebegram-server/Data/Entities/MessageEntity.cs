using System.ComponentModel.DataAnnotations;

namespace TebegramServer.Data.Entities
{
    public class MessageEntity
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string FromUsername { get; set; } = "";

        [Required, MaxLength(100)]
        public string ToUsername { get; set; } = "";

        // Text, Image, File
        [Required, MaxLength(20)]
        public string Type { get; set; } = "Text";

        public string? Content { get; set; }

        // URL файла/изображения на сервере (null для текстовых сообщений)
        public string? ServerAddress { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
