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
        private Stream stream;
        private byte[] buffer;
        public long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        public StreamBinaryWriter(string fileName, int bufferSize = 65536)
        {
            stream = File.Open(fileName, FileMode.Create, FileAccess.Write);
            buffer = new byte[bufferSize];
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        public void WriteBytes(void* data, int bytes)
        {
            int remaining = bytes;
            int bufferSize = buffer.Length;

            fixed (byte* fixedBuffer = buffer)
            {
                while (remaining != 0)
                {
                    int bytesToWrite = Math.Min(remaining, bufferSize);
                    UnsafeUtility.MemCpy(fixedBuffer, data, bytesToWrite);
                    stream.Write(buffer, 0, bytesToWrite);
                    data = (byte*) data + bytesToWrite;
                    remaining -= bytesToWrite;
                }
            }
        }

        public long Length => stream.Length;
    }
}