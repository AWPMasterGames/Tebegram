using NAudio.Wave;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Устройства записи в нумерации WaveInEvent (MME) - той же, которой пользуется
    /// VoiceRoom (waveIn.DeviceNumber). Раньше настройки нумеровали микрофоны по
    /// MMDeviceEnumerator (WASAPI): порядок устройств там ДРУГОЙ, поэтому индекс,
    /// выбранный в настройках, в звонке мог указывать на другой микрофон.
    /// </summary>
    public static class AudioDevices
    {
        /// <summary>Имена устройств записи по порядку WaveInEvent.DeviceNumber.</summary>
        public static string[] GetInputNames()
        {
            var names = new string[WaveInEvent.DeviceCount];
            for (int i = 0; i < names.Length; i++)
                names[i] = WaveInEvent.GetCapabilities(i).ProductName;
            return names;
        }

        /// <summary>
        /// Возвращает индекс устройства по сохранённому имени либо -1, если
        /// устройство не найдено.
        ///
        /// Значения, сохранённые текущей версией, содержат ProductName и находятся
        /// точным совпадением. Сохранённые ранее содержат DeviceFriendlyName из
        /// WASAPI, то есть имя адаптера, тогда как ProductName в MME обрезан до
        /// 31 символа и выглядит как «Микрофон (Адаптер…». Проверка через Contains
        /// в этом случае не срабатывает.
        ///
        /// Поэтому выбирается устройство с наибольшим общим фрагментом имени.
        /// Порог в 11 символов отсекает совпадение по строке «Микрофон (», длина
        /// которой равна 10 символам.
        /// </summary>
        public static int FindByName(string saved)
        {
            if (string.IsNullOrWhiteSpace(saved)) return -1;
            string[] names = GetInputNames();
            for (int i = 0; i < names.Length; i++)
                if (names[i] == saved) return i;

            int best = -1, bestLen = 10;
            for (int i = 0; i < names.Length; i++)
            {
                int len = LongestCommonSubstring(names[i], saved);
                if (len > bestLen) { bestLen = len; best = i; }
            }
            return best;
        }

        private static int LongestCommonSubstring(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;
            // Классическая ДП-таблица по одной строке; имена короткие (до ~64 символов)
            var prev = new int[b.Length + 1];
            int max = 0;
            for (int i = 1; i <= a.Length; i++)
            {
                var cur = new int[b.Length + 1];
                for (int j = 1; j <= b.Length; j++)
                {
                    if (a[i - 1] == b[j - 1])
                    {
                        cur[j] = prev[j - 1] + 1;
                        if (cur[j] > max) max = cur[j];
                    }
                }
                prev = cur;
            }
            return max;
        }
    }
}
