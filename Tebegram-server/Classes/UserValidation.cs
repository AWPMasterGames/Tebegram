using System.Linq;

// Общий для сервера и win-клиента файл: клиент подключает его ссылкой
// (см. Tebegrammmm.csproj), поэтому правила физически одни и те же и не могут
// разъехаться. Веб-клиент повторяет их на JS - при правке менять и там.
namespace Tebegram.Shared
{
    /// <summary>
    /// Проверка полей пользователя при регистрации.
    ///
    /// Адрес регистрации имеет вид /register/{логин}-{пароль}-{ник}-{имя}, то есть
    /// разделителем полей служит дефис. Дефис внутри значения сдвигает разбор и
    /// создаёт испорченную учётную запись. Случай из рабочей базы: при вводе имени
    /// «top-9» вместо набора (top9, 1234, top9, top-9) сервер получил логин
    /// «top9-1234», пароль «top9», ник «top» и имя «9».
    ///
    /// По той же причине запрещены символы протокола (▫ разделяет поля, ❂ разделяет
    /// сообщения, &amp; разделяет контакты) и служебные символы URL (/ \ # ? % +):
    /// значения подставляются непосредственно в путь запроса.
    ///
    /// Мера временная. Перевод регистрации и входа на POST с телом запроса, как уже
    /// сделано в /Contact, снимет ограничения на дефис и пробелы в имени.
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
        /// надёжнее чёрного - заведомо исключает и невидимые, и будущие спецсимволы.
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
        /// цифры, пробелы, точку и подчёркивание. Дефис пока запрещён - он
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
        /// в порядке. Ник проверяется по правилам логина - он тоже попадает в URL.
        /// </summary>
        public static string? CheckRegistration(string login, string password, string username, string name)
        {
            return CheckLogin(login, "Логин")
                ?? CheckPassword(password)
                ?? CheckLogin(username, "Имя пользователя")
                ?? CheckName(name);
        }

        // ── Текст сообщения ──────────────────────────────────────────────────
        /// <summary>Предел длины одного сообщения. Веб повторяет: MAX_MESSAGE_LENGTH в app.js.</summary>
        public const int MessageMaxLength = 512;

        /// <summary>
        /// Приводит текст сообщения к безопасному виду.
        ///
        /// Эмодзи и символы любых алфавитов сохраняются: ограничивать набор знаков
        /// в мессенджере незачем. Удаляется только то, что нарушает передачу.
        ///
        /// Разделители протокола ▫ и ❂. Символ ▫ при разборе компенсировался
        /// склейкой хвоста, а ❂ разрывал строку на два сообщения, и вторая половина
        /// приходила испорченной.
        ///
        /// Управляющие символы, кроме перевода строки. Невидимы в интерфейсе, но
        /// попадают в файлы истории и делают их нечитаемыми.
        ///
        /// Длина обрезается по <see cref="MessageMaxLength"/>: поле ввода ограничено,
        /// но вставка из буфера может пройти мимо него.
        ///
        /// Возвращает null, если после очистки не осталось содержимого.
        /// </summary>
        public static string? SanitizeMessage(string? text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '▫' || c == '❂') { sb.Append(' '); continue; }   // разделители протокола
                if (c == '\n' || c == '\r' || c == '\t') { sb.Append(c); continue; }
                if (char.IsControl(c)) continue;                          // прочее невидимое - прочь
                sb.Append(c);
            }

            string result = sb.ToString().Trim();
            if (result.Length > MessageMaxLength) result = result.Substring(0, MessageMaxLength);
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }

        // ── Свободный текст: имя контакта, название группы, название папки ───
        /// <summary>
        /// Проверяет произвольную подпись, попадающую в протокол: имя контакта при
        /// переименовании, название группы или папки.
        ///
        /// В отличие от имени при регистрации, дефис здесь разрешён: эти значения
        /// передаются телом запроса или отдельным параметром, а не частью адреса.
        /// Запрещены только разделители протокола и невидимые символы. Эмодзи
        /// допустимы, поскольку в разборе не участвуют.
        /// </summary>
        public static string? CheckLabel(string? value, string fieldName, int maxLength = 60)
        {
            if (string.IsNullOrWhiteSpace(value))
                return $"{fieldName} не может быть пустым";
            if (value.Length > maxLength)
                return $"{fieldName} должно быть не длиннее {maxLength} символов";
            if (value.Any(char.IsControl))
                return $"{fieldName} содержит недопустимые символы";

            foreach (char c in value)
                if (c == '▫' || c == '❂' || c == '&')
                    return $"{fieldName} не может содержать символ «{c}»";
            return null;
        }

        /// <summary>
        /// Определяет, содержит ли ответ сервера сообщение об ошибке вместо данных
        /// пользователя.
        ///
        /// Проверка нужна потому, что ошибки регистрации и входа возвращаются с
        /// кодом HTTP 200 и обычным текстом в теле. Клиент Windows ранее считал
        /// успехом любой ответ 200 и сообщал о создании учётной записи даже при
        /// занятом логине. Веб-клиент повторяет этот список в isErrorResponse,
        /// файл docs/app.js.
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
