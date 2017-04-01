using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

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


    }
}
