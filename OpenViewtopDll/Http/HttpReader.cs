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
    public class HttpReader
    {
        const int HTTP_MAX_HEADER_LENGTH = 16000;

        Stream mStream;
        CancellationToken mCancellationToken;
        bool mSync;
        long mLength;
        long mPosition;

        int mBufferIndex;
        int mBufferLength;
        byte[] mBuffer;

        internal HttpReader(Stream stream, CancellationToken cancellationToken, bool sync)
        {
            mStream = stream;
            mCancellationToken = cancellationToken;
            mSync = sync;
        }

        public CancellationToken CancellationToken { get => mCancellationToken; }
        public long Length => mLength;
        public long Position => mPosition;
        public int ReadTimeout { get => mStream.ReadTimeout; set => mStream.ReadTimeout = value; }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            // Limit number of bytes to read
            count = (int)Math.Min(mLength - mPosition, count);

            // Read data from internal buffer
            int length;
            if (mBufferIndex != mBufferLength)
            {
                length = Math.Min(count, mBufferLength - mBufferIndex);
                Array.Copy(mBuffer, mBufferIndex, buffer, offset, length);
                mBufferIndex += length;
                mPosition += length;
                return length;
            }
            // Pass request to underlying stream
            if (mSync)
                length = mStream.Read(buffer, offset, count);
            else
                length = await mStream.ReadAsync(buffer, offset, count, mCancellationToken);

            mPosition += length;
            return length;
        }

        /// <summary>
        /// Fill a buffer with the requested number of bytes, do not return 
        /// until they are all there or a timeout exception is thrown
        /// </summary>
        public async Task<int> ReadAllAsync(ArraySegment<byte> buffer)
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

        /// <summary>
        /// Server only
        /// </summary>
        internal long PositionInternal { get => mPosition; set => mPosition = value; }
        internal long LengthInternal { get => mLength; set => mLength = value; }

        /// <summary>
        /// Called by the server to read the HTTP header into an internal buffer.
        /// Returns an empty buffer if the connection is closed.
        /// Throws an exception if there is an error.
        /// </summary>
        internal async Task<ArraySegment<byte>> ReadHttpHeaderAsyncInternal()
        {
            // Create or shift buffer
            if (mBuffer == null)
                mBuffer = new byte[HTTP_MAX_HEADER_LENGTH];
            if (mBufferIndex != mBufferLength)
                Array.Copy(mBuffer, mBufferIndex, mBuffer, 0, mBufferLength - mBufferIndex);
            mBufferLength = mBufferLength - mBufferIndex;
            mBufferIndex = 0;

            // Wait for HTTP header
            int headerEndIndex = 0;
            while (!FindEndOfHttpHeaderIndex(ref headerEndIndex))
            {
                int maxCount = mBuffer.Length - mBufferLength;
                if (maxCount <= 0)
                    throw new HttpException(400, "HTTP header is too long");

                int length;
                if (mSync)
                    length = mStream.Read(mBuffer, mBufferLength, maxCount);
                else
                    length = await mStream.ReadAsync(mBuffer, mBufferLength, maxCount, mCancellationToken);
                mBufferLength += length;

                if (length == 0)
                {
                    if (mBufferIndex != 0)
                        throw new HttpException(400, "Connection closed after reading partial HTTP header");
                    return new ArraySegment<byte>();
                }
            }

            // Consume the header and return the buffer
            mBufferIndex = headerEndIndex;
            return new ArraySegment<byte>(mBuffer, 0, mBufferIndex);
        }

        bool FindEndOfHttpHeaderIndex(ref int index)
        {
            int i = index;
            while (i < mBufferLength - 3)
            {
                if (mBuffer[i] == '\r' && mBuffer[i+1] == '\n' && mBuffer[i+2] == '\r' && mBuffer[i+3] == '\n')
                {
                    index = i + 4;
                    return true;
                }
                i++;
            }
            index = i;
            return false;
        }


    }
}
