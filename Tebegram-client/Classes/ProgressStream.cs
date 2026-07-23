using System;
using System.IO;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Обёртка над потоком файла, считающая прочитанные байты. HttpClient при
    /// отправке multipart читает файл именно этим потоком, так что «прочитано» =
    /// «отправлено» (с точностью до буфера сокета) — этого хватает для полоски
    /// загрузки в пузыре сообщения.
    ///
    /// Свой класс, потому что StreamContent прогресс не отдаёт, а тянуть ради
    /// одной полоски внешнюю библиотеку незачем.
    /// </summary>
    public sealed class ProgressStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _total;
        private readonly Action<long, long> _onProgress; // (передано, всего)
        private long _read;

        public ProgressStream(Stream inner, long total, Action<long, long> onProgress)
        {
            _inner = inner;
            _total = total;
            _onProgress = onProgress;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = _inner.Read(buffer, offset, count);
            if (n > 0)
            {
                _read += n;
                _onProgress?.Invoke(_read, _total);
            }
            return n;
        }

        // Остальное — прозрачная передача во вложенный поток
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
