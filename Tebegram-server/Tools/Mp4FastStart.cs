using System;
using System.Collections.Generic;
using System.IO;

namespace TebegramServer.Tools
{
    /// <summary>
    /// Переносит блок метаданных MP4 в начало файла (режим, известный как faststart).
    ///
    /// Зачем. Файл MP4 состоит из боксов. Данные лежат в mdat, описание дорожек и
    /// таблицы смещений - в moov. Кодировщики часто дописывают moov в конец, потому
    /// что до окончания записи размеры неизвестны. Для локального файла порядок
    /// безразличен, а при воспроизведении по сети критичен: без moov плеер не знает
    /// ни длительности, ни где искать кадры.
    ///
    /// Пока сервер отдаёт файл сам, плеер добирает хвост запросом Range и обходится.
    /// Но туннель, через который сервер выставлен наружу, заголовок Range не передаёт
    /// и отвечает кодом 200 вместо 206, да ещё и без Content-Length. Safari на iPhone
    /// в таких условиях сдаётся и показывает значок «воспроизведение недоступно»,
    /// тогда как настольный браузер дочитывает файл целиком и всё же играет.
    /// Отсюда и расхождение: на компьютере ролик открывался, на телефоне нет.
    ///
    /// После переноса moov в начало метаданные приходят первыми же килобайтами
    /// обычного потока, и запросы Range перестают быть нужны вовсе.
    ///
    /// Перестановка сдвигает mdat вперёд ровно на размер moov, поэтому все смещения
    /// чанков внутри таблиц stco (32 бита) и co64 (64 бита) увеличиваются на ту же
    /// величину. Сами данные дорожек не пересчитываются и не перекодируются: файл
    /// остаётся тем же, меняется только порядок блоков.
    ///
    /// При любом сомнении файл остаётся нетронутым: испорченный ролик хуже, чем
    /// ролик, который не открывается на телефоне.
    /// </summary>
    public static class Mp4FastStart
    {
        /// <summary>Файл больше этого размера не обрабатываем: moov читается в память целиком.</summary>
        private const long MaxFileBytes = 256L * 1024 * 1024;

        /// <summary>
        /// Приводит файл к faststart, если это MP4 с moov в конце.
        /// Возвращает true, только если файл был реально переписан.
        /// </summary>
        public static bool TryApply(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length < 16 || info.Length > MaxFileBytes) return false;

                List<Box> boxes;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    if (!TryReadTopLevel(fs, info.Length, out boxes)) return false;
                }

                int moovIndex = boxes.FindIndex(b => b.Type == "moov");
                int mdatIndex = boxes.FindIndex(b => b.Type == "mdat");

                // Нет одного из блоков или moov уже впереди - делать нечего
                if (moovIndex < 0 || mdatIndex < 0 || moovIndex < mdatIndex) return false;

                Box moov = boxes[moovIndex];
                // moov в памяти: таблицы смещений правятся на месте
                byte[] moovData = new byte[moov.Size];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(moov.Offset, SeekOrigin.Begin);
                    if (!ReadExactly(fs, moovData, moovData.Length)) return false;
                }

                // Новый порядок: всё, что было до mdat, затем moov, затем остальное.
                // mdat уезжает вперёд ровно на размер moov.
                long delta = moov.Size;
                if (!TryPatchChunkOffsets(moovData, delta)) return false;

                string temp = path + ".faststart";
                using (var src = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var dst = new FileStream(temp, FileMode.Create, FileAccess.Write))
                {
                    // Боксы до mdat, кроме самого moov
                    for (int i = 0; i < mdatIndex; i++)
                    {
                        if (i == moovIndex) continue;
                        CopyRange(src, dst, boxes[i].Offset, boxes[i].Size);
                    }

                    dst.Write(moovData, 0, moovData.Length);

                    // Всё от mdat и дальше, кроме moov, в исходном порядке
                    for (int i = mdatIndex; i < boxes.Count; i++)
                    {
                        if (i == moovIndex) continue;
                        CopyRange(src, dst, boxes[i].Offset, boxes[i].Size);
                    }
                }

                // Размер обязан совпасть: мы только переставили блоки
                if (new FileInfo(temp).Length != info.Length)
                {
                    File.Delete(temp);
                    return false;
                }

                File.Move(temp, path, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FastStart] {Path.GetFileName(path)}: {ex.GetType().Name} {ex.Message}");
                return false;
            }
        }

        private readonly struct Box
        {
            public Box(string type, long offset, long size) { Type = type; Offset = offset; Size = size; }
            public string Type { get; }
            public long Offset { get; }
            public long Size { get; }
        }

        /// <summary>Разбирает боксы верхнего уровня. false, если структура не похожа на MP4.</summary>
        private static bool TryReadTopLevel(Stream fs, long length, out List<Box> boxes)
        {
            boxes = new List<Box>();
            long pos = 0;
            byte[] header = new byte[16];

            while (pos + 8 <= length)
            {
                fs.Seek(pos, SeekOrigin.Begin);
                if (!ReadExactly(fs, header, 8)) return false;

                long size = ReadUInt32(header, 0);
                string type = System.Text.Encoding.ASCII.GetString(header, 4, 4);

                if (size == 1)
                {
                    // Расширенный размер: 64 бита сразу за заголовком
                    if (!ReadExactly(fs, header, 8)) return false;
                    size = (long)ReadUInt64(header, 0);
                }
                else if (size == 0)
                {
                    size = length - pos;   // последний бокс до конца файла
                }

                if (size < 8 || pos + size > length) return false;
                foreach (char c in type)
                    if (c < 0x20 || c > 0x7E) return false;   // мусор вместо типа

                boxes.Add(new Box(type, pos, size));
                pos += size;
            }

            // Первым боксом обязан быть ftyp, иначе это не MP4
            return boxes.Count >= 2 && boxes[0].Type == "ftyp" && pos == length;
        }

        /// <summary>
        /// Увеличивает смещения чанков внутри moov на delta.
        /// Боксы stco и co64 ищутся обходом дерева, а не поиском по сырым байтам:
        /// четыре байта «stco» вполне могут встретиться внутри данных.
        /// </summary>
        private static bool TryPatchChunkOffsets(byte[] moov, long delta)
        {
            bool anyTable = false;
            if (!Walk(moov, 8, moov.Length, ref anyTable, delta)) return false;
            // Файл без таблиц смещений переставлять незачем и небезопасно
            return anyTable;
        }

        private static bool Walk(byte[] buf, int start, int end, ref bool anyTable, long delta)
        {
            int pos = start;
            while (pos + 8 <= end)
            {
                long size = ReadUInt32(buf, pos);
                string type = System.Text.Encoding.ASCII.GetString(buf, pos + 4, 4);
                int headerSize = 8;

                if (size == 1)
                {
                    if (pos + 16 > end) return false;
                    size = (long)ReadUInt64(buf, pos + 8);
                    headerSize = 16;
                }
                else if (size == 0)
                {
                    size = end - pos;
                }

                if (size < headerSize || pos + size > end) return false;

                switch (type)
                {
                    // Контейнеры: спускаемся внутрь
                    case "trak":
                    case "mdia":
                    case "minf":
                    case "stbl":
                    case "edts":
                    case "udta":
                        if (!Walk(buf, pos + headerSize, pos + (int)size, ref anyTable, delta)) return false;
                        break;

                    case "stco":
                    {
                        int p = pos + headerSize + 4;             // версия и флаги
                        if (p + 4 > end) return false;
                        long count = ReadUInt32(buf, p);
                        p += 4;
                        if (p + count * 4 > end) return false;
                        for (long i = 0; i < count; i++, p += 4)
                        {
                            long v = ReadUInt32(buf, p) + delta;
                            // Переполнение 32 бит: такой файл правится только через co64
                            if (v > uint.MaxValue) return false;
                            WriteUInt32(buf, p, (uint)v);
                        }
                        anyTable = true;
                        break;
                    }

                    case "co64":
                    {
                        int p = pos + headerSize + 4;
                        if (p + 4 > end) return false;
                        long count = ReadUInt32(buf, p);
                        p += 4;
                        if (p + count * 8 > end) return false;
                        for (long i = 0; i < count; i++, p += 8)
                            WriteUInt64(buf, p, ReadUInt64(buf, p) + (ulong)delta);
                        anyTable = true;
                        break;
                    }
                }

                pos += (int)size;
            }
            return true;
        }

        private static void CopyRange(Stream src, Stream dst, long offset, long count)
        {
            src.Seek(offset, SeekOrigin.Begin);
            byte[] buffer = new byte[81920];
            long left = count;
            while (left > 0)
            {
                int want = (int)Math.Min(buffer.Length, left);
                int read = src.Read(buffer, 0, want);
                if (read <= 0) throw new IOException("Файл кончился раньше, чем описано в боксе");
                dst.Write(buffer, 0, read);
                left -= read;
            }
        }

        private static bool ReadExactly(Stream s, byte[] buffer, int count)
        {
            int done = 0;
            while (done < count)
            {
                int read = s.Read(buffer, done, count - done);
                if (read <= 0) return false;
                done += read;
            }
            return true;
        }

        // Числа в MP4 хранятся старшим байтом вперёд
        private static uint ReadUInt32(byte[] b, int i) =>
            (uint)((b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3]);

        private static ulong ReadUInt64(byte[] b, int i) =>
            ((ulong)ReadUInt32(b, i) << 32) | ReadUInt32(b, i + 4);

        private static void WriteUInt32(byte[] b, int i, uint v)
        {
            b[i] = (byte)(v >> 24); b[i + 1] = (byte)(v >> 16);
            b[i + 2] = (byte)(v >> 8); b[i + 3] = (byte)v;
        }

        private static void WriteUInt64(byte[] b, int i, ulong v)
        {
            WriteUInt32(b, i, (uint)(v >> 32));
            WriteUInt32(b, i + 4, (uint)v);
        }
    }
}
