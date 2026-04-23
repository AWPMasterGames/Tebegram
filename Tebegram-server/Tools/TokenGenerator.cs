using System.Security.Cryptography;

namespace TebegramServer.Tools
{
    public class TokenGenerator
    {
        public TokenGenerator() { }
        public string GetToken(string text)
        {
            var salt = Guid.NewGuid().ToString().Replace("-", "").ToLower();
            var base64Salt = ToBase64String(salt);
            var hashedSalt = MD5.HashData(Convert.FromBase64String(base64Salt));
            var secret = $"{base64Salt}.{text}.{hashedSalt}";
            var token = ToBase64String(secret);

            return token;
        }

        private string ToBase64String(string salt)
        {
            using var stream = new MemoryStream();
            using TextWriter writer = new StreamWriter(stream);

            writer.Write(salt);
            writer.Flush();
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            using BinaryReader reader = new BinaryReader(stream);
            return Convert.ToBase64String(reader.ReadBytes(salt.Length * 2));
        }
    }
}
