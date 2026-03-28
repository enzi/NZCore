// <copyright project="NZCore" file="BinaryWriter.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using BinaryWriter = Unity.Entities.Serialization.BinaryWriter;

namespace NZCore
{
    public unsafe class StreamBinaryWriter : BinaryWriter
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer;

        public long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        public StreamBinaryWriter(string fileName, int bufferSize = 65536)
        {
            _stream = File.Open(fileName, FileMode.Create, FileAccess.Write);
            _buffer = new byte[bufferSize];
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        public void WriteBytes(void* data, int bytes)
        {
            var remaining = bytes;
            var bufferSize = _buffer.Length;

            fixed (byte* fixedBuffer = _buffer)
            {
                while (remaining != 0)
                {
                    var bytesToWrite = Math.Min(remaining, bufferSize);
                    UnsafeUtility.MemCpy(fixedBuffer, data, bytesToWrite);
                    _stream.Write(_buffer, 0, bytesToWrite);
                    data = (byte*)data + bytesToWrite;
                    remaining -= bytesToWrite;
                }
            }
        }

        public long Length => _stream.Length;
    }
}