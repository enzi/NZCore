// <copyright project="NZCore" file="MixedEventBus.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    /// <summary>
    /// Event bus that stores events in a linear UnsafeList
    /// </summary>
    public unsafe struct MixedEventBus<T> : IDisposable
        where T : unmanaged, Enum
    {
        private struct MixedEventHeader
        {
            public int Type;
            public int PayloadLength;
        }
        
        [NativeDisableUnsafePtrRestriction] private UnsafeList<byte>* _bus;

        private AllocatorManager.AllocatorHandle _allocator;

        /// <summary>Allocates the bus.</summary>
        public static MixedEventBus<T> Create(int initialTypeCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            var bus = default(MixedEventBus<T>);
            bus._allocator = allocator;
            bus._bus = UnsafeList<byte>.Create(initialTypeCapacity, allocator, NativeArrayOptions.UninitializedMemory);

            return bus;
        }

        /// <summary>Disposes all allocations.</summary>
        public void Dispose()
        {
            _bus->Dispose();
            Memory.Unmanaged.Free(_bus, _allocator);
            _bus = null;
        }

        /// <summary>Total queued events for <typeparamref name="T"/>.</summary>
        public int Count(int type)
        {
            int count = 0;

            var reader = new NodeReader(_bus->Ptr, _bus->m_length);

            while (reader.CanRead)
            {
                ref var header = ref reader.ReadFromNode<MixedEventHeader>();
                reader.AddOffset(header.PayloadLength);

                count++;
            }

            return count;
        }

        /// <summary>Clears all events</summary>
        public void Clear()
        {
            _bus->Clear();
        }
        
        /// <summary>Clears all queued events for <typeparamref name="T"/>.</summary>
        public void ClearBus<T>() where T : unmanaged
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Single-threaded write
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TData>(T type, in TData evt) 
            where TData : unmanaged
        {
            fixed (void* evtPtr = &evt)
            {
                var payloadSize = UnsafeUtility.SizeOf<TData>();
                var header = new MixedEventHeader()
                {
                    Type = UnsafeUtility.EnumToInt(type),
                    PayloadLength = payloadSize
                };
                AppendBytes(_bus, &header, UnsafeUtility.SizeOf<MixedEventHeader>()); 
                AppendBytes(_bus, evtPtr, payloadSize);
            }
        }

        /// <summary>Returns a sequential reader so all events can be processed linearly.</summary>
        public MixedBusReader AsReader()
        {
            return new MixedBusReader(_bus->Ptr, _bus->m_length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendBytes(UnsafeList<byte>* list, void* src, int size)
        {
            var oldLen = list->m_length;
            var newLen = oldLen + size;
            if (newLen > list->Capacity)
            {
                list->Resize(newLen);
            }
            else
            {
                list->m_length = newLen;
            }

            UnsafeUtility.MemCpy(list->Ptr + oldLen, src, size);
        }

        /// <summary>
        /// A linear reader to read all stored events in the MixedBus
        /// </summary>
        public struct MixedBusReader
        {
            [NativeDisableUnsafePtrRestriction] private readonly byte* _ptr;
            private readonly int _bufferLength;
            private int _offset;
            private int _payloadOffset;

            internal MixedBusReader(byte* ptr, int bufferLength)
            {
                _ptr = ptr;
                _bufferLength = bufferLength;
                _offset = 0;
                _payloadOffset = 0;
            }

            public bool CanRead => _offset < _bufferLength;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext(out T eventType)
            {
                if (_offset >= _bufferLength)
                {
                    eventType = default;
                    return false;
                }

                ref var header = ref UnsafeUtility.AsRef<MixedEventHeader>(_ptr + _offset);
                _offset += UnsafeUtility.SizeOf<MixedEventHeader>();

                var typeInt = header.Type;
                eventType = UnsafeUtility.As<int, T>(ref typeInt);
                _payloadOffset = _offset;
                _offset += header.PayloadLength;

                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref TData GetPayload<TData>() where TData : unmanaged
            {
                return ref UnsafeUtility.AsRef<TData>(_ptr + _payloadOffset);
            }
        }
    }
}