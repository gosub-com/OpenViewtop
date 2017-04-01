﻿using System;
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
    public class HttpWriter : Stream
    {
        Stream mStream;
        long mLength;
        long mPosition;

        internal HttpWriter(Stream stream)
        {
            mStream = stream;
        }


        public override long Length => mLength;
        public override long Position { get => mPosition; set => throw new NotImplementedException(); }
        public override bool CanRead => false;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override void SetLength(long value) { throw new NotImplementedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotImplementedException(); }
        public override bool CanTimeout => true;
        public override int ReadTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override int WriteTimeout { get => mStream.WriteTimeout; set => mStream.WriteTimeout = value; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        public override int ReadByte() => throw new NotImplementedException();

        // Do not allow Close or Dispose (the web server manages the streams)
        public override void Flush() => mStream.Flush();
        public override void Close() => mStream.Flush();
        protected override void Dispose(bool disposing) => mStream.Flush();

        /// <summary>
        /// Server only
        /// </summary>
        internal long PositionInternal { get => mPosition; set => mPosition = value; }
        internal long LengthInternal { get => mLength; set => mLength = value; }


        public override void Write(byte[] buffer, int offset, int count)
        {
            mPosition += count;
            mStream.Write(buffer, offset, count);
            if (mPosition > mLength)
                throw new HttpException(500, "Request handler wrote too many bytes");
        }

        public override void WriteByte(byte value)
        {
            mPosition++;
            mStream.WriteByte(value);
            if (mPosition > mLength)
                throw new HttpException(500, "Request handler wrote too many bytes");
        }


    }
}
