using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace Gosub.Http
{
    /// <summary>
    /// An HTTP stream reader.  Do not dispose (the web server owns and re-uses these)
    /// </summary>
    public class HttpReader : Stream
    {
        Stream mStream;
        long mLength;
        long mPosition;

        internal HttpReader(Stream stream)
        {
            mStream = stream;
        }

        public override long Length => mLength;
        public override long Position { get => mPosition; set => throw new NotImplementedException(); }
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override void SetLength(long value) { throw new NotImplementedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotImplementedException(); }
        public override bool CanTimeout => true;
        public override int ReadTimeout { get => mStream.ReadTimeout; set => mStream.ReadTimeout = value; }
        public override int WriteTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        public override void WriteByte(byte value) => throw new NotImplementedException();
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        // Do not allow Close or Dispose (the web server manages the streams)
        public override void Flush() => mStream.Flush();
        public override void Close() => mStream.Flush();
        protected override void Dispose(bool disposing) => mStream.Flush();

        /// <summary>
        /// Server only
        /// </summary>
        internal long PositionInternal { get => mPosition; set => mPosition = value; }
        internal long LengthInternal { get => mLength; set => mLength = value; }
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            // Limit number of bytes to read
            count = (int)Math.Min(mLength - mPosition, count);
            if (count <= 0)
                return 0;
            var length = mStream.Read(buffer, offset, count);
            mPosition += length;
            return length;
        }

        public override int ReadByte()
        {
            if (mPosition >= mLength)
                return -1;
            int b = mStream.ReadByte();
            if (b >= 0)
                mPosition++;
            return b;
        }

        public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Limit number of bytes to read
            count = (int)Math.Min(mLength - mPosition, count);
            int length = await mStream.ReadAsync(buffer, offset, count, cancellationToken);
            mPosition += length;
            return length;
        }

        /// <summary>
        /// Fill a buffer with the requested number of bytes, do not return 
        /// until they are all there or a timeout exception is thrown
        /// </summary>
        public async Task<int> ReadAllAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < buffer.Count)
            {
                var length = await ReadAsync(buffer.Array, buffer.Offset + offset, buffer.Count - offset);
                if (length == 0)
                    throw new HttpException(400, "Unexpected end of stream");
                offset += length;
            }
            return offset;
        }


    }
}
