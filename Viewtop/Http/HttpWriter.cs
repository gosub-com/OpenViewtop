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
    public class HttpWriter
    {
        Stream mStream;
        bool mSync;
        CancellationToken mCancellationToken;
        long mLength;
        long mPosition;
        byte[] mHeaderBytes;

        internal HttpWriter(Stream stream, CancellationToken cancellationToken, bool sync)
        {
            mStream = stream;
            mCancellationToken = cancellationToken;
            mSync = sync;
        }

        public CancellationToken CancellationToken { get => mCancellationToken; }
        public long Length => mLength;
        public long Position => mPosition;
        public int WriteTimeout { get => mStream.WriteTimeout; set => mStream.WriteTimeout = value; }
        
        /// <summary>
        /// Server only
        /// </summary>
        internal long PositionInternal { get => mPosition; set => mPosition = value; }
        internal long LengthInternal { get => mLength; set => mLength = value; }
        internal void SetHeaderInternal(byte[] headerBytes)
        {
            if (mHeaderBytes != null)
                throw new HttpException(500, "Header bytes already set");
            mHeaderBytes = headerBytes;
        }
        internal async Task FlushHeaderInternal()
        {
            if (mHeaderBytes != null)
                await WriteAsync(null, 0, 0);
        }

        public async Task WriteAsync(byte[] buffer, int offset, int count)
        {
            // Send Header
            if (mHeaderBytes != null)
            {
                if (mSync)
                    mStream.Write(mHeaderBytes, 0, mHeaderBytes.Length);
                else
                    await mStream.WriteAsync(mHeaderBytes, 0, mHeaderBytes.Length, mCancellationToken);
                mHeaderBytes = null;
            }
            if (count == 0)
                return;

            // Send data
            mPosition += count;
            if (mPosition > mLength)
                throw new HttpException(500, "Request handler wrote too many bytes");
            if (mSync)
                mStream.Write(buffer, offset, count);
            else
                await mStream.WriteAsync(buffer, offset, count, mCancellationToken);
        }

        public async Task WriteAsync(Stream stream)
        {
            byte[] buffer = new byte[8192];
            int length;
            if (mSync)
            {
                while ((length = stream.Read(buffer, 0, buffer.Length)) != 0)
                    await WriteAsync(buffer, 0, length);
            }
            else
            {
                while ((length = await stream.ReadAsync(buffer, 0, buffer.Length, mCancellationToken)) != 0)
                    await WriteAsync(buffer, 0, length);
            }
        }

        public async Task FlushAsync()
        {
            if (mSync)
                mStream.Flush();
            else
                await mStream.FlushAsync();
        }
    }
}
