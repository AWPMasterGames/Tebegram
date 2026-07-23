using System.Linq;

// Общий для сервера и win-клиента файл: клиент подключает его ссылкой
// (см. Tebegrammmm.csproj), поэтому правила физически одни и те же и не могут
// разъехаться. Веб-клиент повторяет их на JS — при правке менять и там.
namespace Tebegram.Shared
{
    /// <summary>
    /// Проверка полей пользователя при регистрации.
    ///
    /// Зачем: адрес регистрации выглядит как
    ///     /register/{логин}-{пароль}-{ник}-{имя}
    /// то есть разделителем служит ДЕФИС. Любой дефис внутри поля сдвигает разбор
    /// и создаёт мусорный аккаунт. Реальный случай из базы: пользователь ввёл имя
    /// «top-9», и вместо (top9, 1234, top9, top-9) сервер получил логин «top9-1234»,
    /// пароль «top9», ник «top» и имя «9».
    ///
    /// Кроме дефиса опасны символы протокола (▫ разделяет поля, ❂ — сообщения,
    /// & — контакты внутри поля) и служебные символы URL (/ \ # ? % +), потому что
    /// поля подставляются прямо в путь запроса.
    ///
    /// ВАЖНО: это временная защита. Правильное решение — перевести регистрацию и
    /// вход на POST с телом (как сделано в /Contact), тогда ограничения на дефис
    /// и пробелы в ИМЕНИ можно будет снять.
    /// </summary>
    public static class UserValidation
    {
        // Символы, ломающие разбор запроса или протокол. Проверяются во всех полях.
        private static readonly char[] Forbidden =
            { '-', '▫', '❂', '&', '/', '\\', '#', '?', '%', '+', ':', '=' };

        public const int LoginMinLength = 3;
        public const int LoginMaxLength = 32;
        public const int PasswordMinLength = 4;
        public const int PasswordMaxLength = 64;
        public const int NameMaxLength = 48;

        /// <summary>
        /// Логин и ник: только латиница, цифры, точка и подчёркивание. Белый список
        /// надёжнее чёрного — заведомо исключает и невидимые, и будущие спецсимволы.
        /// </summary>
        public static string? CheckLogin(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return $"{fieldName} не может быть пустым";
            if (value.Length < LoginMinLength)
                return $"{fieldName} должен быть не короче {LoginMinLength} символов";
            if (value.Length > LoginMaxLength)
                return $"{fieldName} должен быть не длиннее {LoginMaxLength} символов";

            foreach (char c in value)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                       || (c >= '0' && c <= '9') || c == '_' || c == '.';
                if (!ok)
                    return $"{fieldName} может содержать только латинские буквы, цифры, точку и подчёркивание";
            }
            return null;
        }

        /// <summary>Пароль: без пробелов и символов, ломающих адрес запроса.</summary>
        public static string? CheckPassword(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Пароль не может быть пустым";
            if (value.Length < PasswordMinLength)
                return $"Пароль должен быть не короче {PasswordMinLength} символов";
            if (value.Length > PasswordMaxLength)
                return $"Пароль должен быть не длиннее {PasswordMaxLength} символов";
            if (value.Any(char.IsWhiteSpace))
                return "Пароль не может содержать пробелы";
            if (value.Any(char.IsControl))
                return "Пароль содержит недопустимые символы";

            foreach (char c in value)
                if (Forbidden.Contains(c))
                    return $"Пароль не может содержать символ «{c}»";
            return null;
        }

        /// <summary>
        /// Отображаемое имя: разрешаем буквы любого алфавита (нужна кириллица),
        /// цифры, пробелы, точку и подчёркивание. Дефис пока запрещён — он
        /// разделитель в адресе регистрации.
        /// </summary>
        public static string? CheckName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Имя не может быть пустым";
            if (value.Length > NameMaxLength)
                return $"Имя должно быть не длиннее {NameMaxLength} символов";
            if (value.Any(char.IsControl))
                return "Имя содержит недопустимые символы";

            foreach (char c in value)
            {
                if (Forbidden.Contains(c))
                    return $"Имя не может содержать символ «{c}»";
                bool ok = char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '.';
                if (!ok)
                    return $"Имя не может содержать символ «{c}»";
            }
            return null;
        }

        /// <summary>
        /// Полная проверка регистрации. Возвращает текст ошибки или null, если всё
        /// в порядке. Ник проверяется по правилам логина — он тоже попадает в URL.
        /// </summary>
        public static string? CheckRegistration(string login, string password, string username, string name)
        {
            return CheckLogin(login, "Логин")
                ?? CheckPassword(password)
                ?? CheckLogin(username, "Имя пользователя")
                ?? CheckName(name);
        }

        /// <summary>
        /// Ответ сервера — это сообщение об ошибке, а не данные пользователя?
        ///
        /// Нужно потому, что сервер отдаёт ошибки регистрации и входа с кодом
        /// HTTP 200 и обычным текстом в теле. Win-клиент раньше считал успехом
        /// ЛЮБОЙ ответ 200 и показывал «Аккаунт создан» даже когда логин занят.
        /// Веб-клиент повторяет этот список в isErrorResponse (docs/app.js).
        /// </summary>
        public static bool IsErrorResponse(string? response)
        {
            if (string.IsNullOrWhiteSpace(response)) return true;

            string[] markers =
            {
                "Ошибка",
                "Все поля",
                "Пользователь с таким логином уже существует",
                "Пользователь с таким именем уже существует",
                "Пользователь с таким логином не существует",
                "Неверный пароль",
            };
            foreach (string marker in markers)
                if (response.StartsWith(marker)) return true;
            return false;
        }
    }
}
