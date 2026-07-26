// <copyright project="NZCore" file="MixedEventBus.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
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
            public int PayloadOffset;
            public int PayloadLength;
        }
        
        [NativeDisableUnsafePtrRestriction] private UnsafeList<MixedEventHeader>* _headers;
        [NativeDisableUnsafePtrRestriction] private UnsafeList<byte>* _payloads;

        private AllocatorManager.AllocatorHandle _allocator;

        /// <summary>Allocates the bus.</summary>
        public static MixedEventBus<T> Create(int initialTypeCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            var bus = default(MixedEventBus<T>);
            bus._allocator = allocator;
            bus._headers = UnsafeList<MixedEventHeader>.Create(16, allocator, NativeArrayOptions.UninitializedMemory);
            bus._payloads = UnsafeList<byte>.Create(initialTypeCapacity, allocator, NativeArrayOptions.UninitializedMemory);

            return bus;
        }

        /// <summary>Disposes all allocations.</summary>
        public void Dispose()
        {
            _headers->Dispose();
            Memory.Unmanaged.Free(_headers, _allocator);
            _headers = null;

            _payloads->Dispose();
            Memory.Unmanaged.Free(_payloads, _allocator);
            _payloads = null;
        }

        /// <summary>Total queued events for <typeparamref name="T"/>.</summary>
        public int Count(int type)
        {
            return _headers->m_length;
        }

        /// <summary>Clears all events</summary>
        public void Clear()
        {
            _headers->Clear();
            _payloads->Clear();
        }
        
        /// <summary>Clears all queued events for <typeparamref name="T"/>.</summary>
        public void ClearBus<TData>() 
            where TData : unmanaged
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
                var payloadOffset = _payloads->m_length;

                var newPayloadLength = payloadOffset + payloadSize;
                if (newPayloadLength > _payloads->Capacity)
                {
                    _payloads->Resize(newPayloadLength);
                }
                else
                {
                    _payloads->m_length = newPayloadLength;
                }

                UnsafeUtility.MemCpy(_payloads->Ptr + payloadOffset, evtPtr, payloadSize);

                _headers->Add(new MixedEventHeader
                {
                    Type = UnsafeUtility.EnumToInt(type),
                    PayloadOffset = payloadOffset,
                    PayloadLength = payloadSize
                });
            }
        }

        /// <summary>Returns a sequential reader so all events can be processed linearly.</summary>
        public MixedBusReader AsReader()
        {
            return new MixedBusReader(_headers->Ptr, _headers->m_length, _payloads->Ptr);
        }


        /// <summary>
        /// A linear reader to read all stored events in the MixedBus
        /// </summary>
        public struct MixedBusReader
        {
            [NativeDisableUnsafePtrRestriction] private readonly MixedEventHeader* _end;
            [NativeDisableUnsafePtrRestriction] private MixedEventHeader* _nextHeader;
            [NativeDisableUnsafePtrRestriction] private readonly byte* _payloadBase;
            [NativeDisableUnsafePtrRestriction] private byte* _payload;

            internal MixedBusReader(void* headers, int headerCount, byte* payloadBase)
            {
                _nextHeader = (MixedEventHeader*)headers;
                _end = _nextHeader + headerCount;
                _payloadBase = payloadBase;
                _payload = null;
            }

            public bool CanRead => _nextHeader < _end;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext(out T eventType)
            {
                if (Hint.Unlikely(_nextHeader >= _end))
                {
                    eventType = default;
                    return false;
                }

                var header = _nextHeader;
                var typeInt = header->Type;
                eventType = UnsafeUtility.As<int, T>(ref typeInt);

                _payload = _payloadBase + header->PayloadOffset;
                _nextHeader++;

                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref TData GetPayload<TData>() where TData : unmanaged
            {
                return ref *(TData*)_payload;
            }
        }
    }
}
