﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SslStreamPerf
{
    // CONSIDER: Make read buffer size larger.

    internal abstract class BaseHandler : IDisposable
    {
//        private const int ReadBufferSize = 16 * 1024;
        private const int ReadBufferSize = 4 * 1024;

        protected readonly Stream _stream;

        private readonly byte[] _readBuffer;
        private int _readOffset;
        private int _readCount;
        private int _messageByteCount;

        public BaseHandler(Stream stream)
        {
            _stream = stream;

            _readBuffer = new byte[ReadBufferSize];
            _readOffset = 0;
            _readCount = 0;
        }

        protected byte[] CreateMessageBuffer(int messageSize)
        {
            // Create zero-terminated message of the specified length
            var buffer = new byte[messageSize];
            Array.Fill(buffer, (byte)0xFF);
            buffer[messageSize - 1] = 0;
            return buffer;
        }

        private bool TryReadMessage()
        {
            if (_readCount == 0)
            {
                return false;
            }

            int index = Array.IndexOf<byte>(_readBuffer, 0, _readOffset, _readCount);
            if (index < 0)
            {
                // Consume all remaining bytes
                _readOffset = 0;
                _readCount = 0;

                _messageByteCount += _readCount;
                return false;
            }

            // Consume bytes for this message (including trailing 0)
            _readOffset += index + 1;
            _readCount -= index + 1;

            _messageByteCount += index + 1;
            return true;
        }

        // Returns when a full message has been read, and returns the message size.
        protected async Task<int> ReceiveMessage()
        {
            _messageByteCount = 0;
            while (!TryReadMessage())
            {
                // Get more data
                _readOffset = 0;
                _readCount = await _stream.ReadAsync(_readBuffer, 0, ReadBufferSize);
                if (_readCount == 0)
                {
                    Trace("Connection closed by client");
                    return 0;   // EOF
                }

                Trace($"Read complete, bytesRead = {_readCount}");
            }

            return _messageByteCount;
        }

        public abstract Task Run();

        public void Dispose()
        {
            _stream.Dispose();
        }

        [Conditional("PERFTRACE")]
        protected void Trace(string s)
        {
            Console.WriteLine(s);
        }
    }
}
