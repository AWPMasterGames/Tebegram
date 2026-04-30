using System.Security.Cryptography;
using System.Text;

namespace TebegramServer.Tools
{
    public class TokenGenerator
    {
        public TokenGenerator() { }

        public string GetToken(string label)
        {
            // 32 криптографически случайных байта (256 бит)
            var randomBytes = RandomNumberGenerator.GetBytes(32);

            // SHA256 от случайных байт + метки для привязки к контексту
            var combined = randomBytes.Concat(Encoding.UTF8.GetBytes(label)).ToArray();
            var hash = SHA256.HashData(combined);

            return Convert.ToBase64String(hash)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
