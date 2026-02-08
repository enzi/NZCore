// <copyright project="NZCore" file="DynamicHashMap.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace NZCore
{
    /// <summary>
    /// Marker interface for DynamicHashMap buffer elements.
    /// Implement this on your buffer element struct to use it with DynamicHashMap.
    /// </summary>
    public interface IDynamicHashMap : IBufferElementData { }

    /// <summary>
    /// High-performance hashmap backed by a DynamicBuffer using open addressing with linear probing.
    ///
    /// TBuffer allows multiple hashmaps per entity by defining different buffer element types:
    /// <code>
    /// public struct InventoryHashMapBuffer : IDynamicHashMap { public byte Value; }
    /// public struct SkillHashMapBuffer : IDynamicHashMap { public byte Value; }
    ///
    /// var inventoryMap = new DynamicHashMap&lt;InventoryHashMapBuffer, int, Item&gt;(inventoryBuffer);
    /// var skillMap = new DynamicHashMap&lt;SkillHashMapBuffer, int, Skill&gt;(skillBuffer);
    /// </code>
    ///
    /// Memory layout in buffer:
    /// [Header (16 bytes)] [Metadata array (1 byte per slot)] [Padding to align] [Entries array (Key+Value per slot)]
    ///
    /// Metadata byte: 0 = empty, 0x7F = deleted, 0x80+ = occupied with h2 hash bits
    /// </summary>
    public readonly unsafe struct DynamicHashMap<TBuffer, TKey, TValue>
        where TBuffer : unmanaged, IDynamicHashMap
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        private const byte Empty = 0;
        private const byte Occupied = 0x80; // High bit set = occupied
        private const byte Deleted = 0x7F;  // Special marker for tombstone

        [NativeDisableUnsafePtrRestriction]
        private readonly byte* _buffer;
        private readonly int _bufferLength;

        /// <summary>
        /// Creates a DynamicHashMap wrapper around an existing DynamicBuffer.
        /// Auto-initializes with default capacity if buffer is empty.
        /// </summary>
        public DynamicHashMap(DynamicBuffer<TBuffer> buffer)
        {
            if (buffer.Length == 0)
                Initialize(buffer, 16);

            _buffer = (byte*)buffer.GetUnsafePtr();
            _bufferLength = buffer.Length * UnsafeUtility.SizeOf<TBuffer>();
        }

        /// <summary>
        /// Creates a DynamicHashMap from a raw pointer (for use in jobs).
        /// </summary>
        public DynamicHashMap(byte* buffer, int length)
        {
            _buffer = buffer;
            _bufferLength = length;
        }

        private ref Header GetHeader()
        {
            return ref UnsafeUtility.AsRef<Header>(_buffer);
        }

        private Header* GetHeaderPtr()
        {
            return (Header*)_buffer;
        }

        private byte* GetMetadata()
        {
            return _buffer + sizeof(Header);
        }

        private Entry* GetEntries()
        {
            ref var header = ref GetHeader();
            return (Entry*)(_buffer + header.EntriesOffset);
        }

        /// <summary>
        /// Number of elements in the hashmap.
        /// </summary>
        public int Count => _bufferLength >= sizeof(Header) ? GetHeader().Count : 0;

        /// <summary>
        /// Capacity of the hashmap (number of slots).
        /// </summary>
        public int Capacity => _bufferLength >= sizeof(Header) ? GetHeader().Capacity : 0;

        /// <summary>
        /// Initializes the hashmap with specified capacity. Must be called before use.
        /// </summary>
        public static void Initialize(DynamicBuffer<TBuffer> buffer, int capacity)
        {
            capacity = math.max(16, math.ceilpow2(capacity));

            var entriesOffset = sizeof(Header) + capacity; // Header + metadata
            entriesOffset = AlignUp(entriesOffset, 8); // Align entries to 8 bytes

            var totalSize = entriesOffset + capacity * sizeof(Entry);
            var bufferElementSize = UnsafeUtility.SizeOf<TBuffer>();
            var elementsNeeded = (totalSize + bufferElementSize - 1) / bufferElementSize;

            buffer.ResizeUninitialized(elementsNeeded);

            var ptr = (byte*)buffer.GetUnsafePtr();

            // Initialize header
            var header = (Header*)ptr;
            header->Count = 0;
            header->Capacity = capacity;
            header->CapacityMask = capacity - 1;
            header->EntriesOffset = entriesOffset;

            // Clear metadata to empty
            UnsafeUtility.MemSet(ptr + sizeof(Header), Empty, capacity);
        }

        /// <summary>
        /// Clears all entries from the hashmap.
        /// </summary>
        public void Clear()
        {
            Assert.IsTrue(_bufferLength >= sizeof(Header), "DynamicHashMap must be initialized before calling Clear()");

            ref var header = ref GetHeader();
            header.Count = 0;
            UnsafeUtility.MemSet(GetMetadata(), Empty, header.Capacity);
        }

        /// <summary>
        /// Tries to add a key-value pair. Returns false if key already exists.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, TValue value)
        {
            var header = GetHeaderPtr();
            var metadata = GetMetadata();
            var entries = GetEntries();

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            var firstDeleted = -1;

            for (int i = 0; i < header->Capacity; i++)
            {
                var meta = metadata[index];

                if (meta == Empty)
                {
                    // Found empty slot
                    var insertIndex = firstDeleted >= 0 ? (uint)firstDeleted : index;
                    metadata[insertIndex] = (byte)(Occupied | h2);
                    entries[insertIndex].Key = key;
                    entries[insertIndex].Value = value;
                    header->Count++;
                    return true;
                }

                if (meta == Deleted)
                {
                    if (firstDeleted < 0)
                        firstDeleted = (int)index;
                }
                else if ((meta & 0x7F) == h2)
                {
                    // Potential match, check key
                    if (entries[index].Key.Equals(key))
                        return false; // Key exists
                }

                index = (index + 1) & (uint)header->CapacityMask;
            }

            // Table is full (shouldn't happen with proper load factor management)
            return false;
        }

        /// <summary>
        /// Adds or updates a key-value pair.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrSet(TKey key, TValue value)
        {
            var header = GetHeaderPtr();
            var metadata = GetMetadata();
            var entries = GetEntries();

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            var firstDeleted = -1;

            for (int i = 0; i < header->Capacity; i++)
            {
                var meta = metadata[index];

                if (meta == Empty)
                {
                    var insertIndex = firstDeleted >= 0 ? (uint)firstDeleted : index;
                    metadata[insertIndex] = (byte)(Occupied | h2);
                    entries[insertIndex].Key = key;
                    entries[insertIndex].Value = value;
                    header->Count++;
                    return;
                }

                if (meta == Deleted)
                {
                    if (firstDeleted < 0)
                        firstDeleted = (int)index;
                }
                else if ((meta & 0x7F) == h2)
                {
                    if (entries[index].Key.Equals(key))
                    {
                        entries[index].Value = value;
                        return;
                    }
                }

                index = (index + 1) & (uint)header->CapacityMask;
            }

            // Insert at first deleted if we found one
            if (firstDeleted >= 0)
            {
                metadata[firstDeleted] = (byte)(Occupied | h2);
                entries[firstDeleted].Key = key;
                entries[firstDeleted].Value = value;
                header->Count++;
            }
        }

        /// <summary>
        /// Tries to get a value by key.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            var header = GetHeaderPtr();
            var metadata = GetMetadata();
            var entries = GetEntries();

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            for (int i = 0; i < header->Capacity; i++)
            {
                var meta = metadata[index];

                if (meta == Empty)
                {
                    value = default;
                    return false;
                }

                if (meta != Deleted && (meta & 0x7F) == h2)
                {
                    if (entries[index].Key.Equals(key))
                    {
                        value = entries[index].Value;
                        return true;
                    }
                }

                index = (index + 1) & (uint)header->CapacityMask;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Gets a reference to a value by key. Throws if not found.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetValueRef(TKey key)
        {
            var header = GetHeaderPtr();
            var metadata = GetMetadata();
            var entries = GetEntries();

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            for (int i = 0; i < header->Capacity; i++)
            {
                var meta = metadata[index];

                if (meta == Empty)
                    break;

                if (meta != Deleted && (meta & 0x7F) == h2)
                {
                    if (entries[index].Key.Equals(key))
                        return ref entries[index].Value;
                }

                index = (index + 1) & (uint)header->CapacityMask;
            }

            throw new InvalidOperationException($"Key not found in DynamicHashMap");
        }

        /// <summary>
        /// Tries to get a pointer to a value by key.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetRefValue(TKey key, out TValue* value)
        {
            var header = GetHeaderPtr();
            var metadata = GetMetadata();
            var entries = GetEntries();

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            for (int i = 0; i < header->Capacity; i++)
            {
                var meta = metadata[index];

                if (meta == Empty)
                {
                    value = null;
                    return false;
                }

                if (meta != Deleted && (meta & 0x7F) == h2)
                {
                    if (entries[index].Key.Equals(key))
                    {
                        value = &entries[index].Value;
                        return true;
                    }
                }

                index = (index + 1) & (uint)header->CapacityMask;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Checks if a key exists.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key)
        {
            var header = GetHeaderPtr();
            var metadata = GetMetadata();
            var entries = GetEntries();

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            for (int i = 0; i < header->Capacity; i++)
            {
                var meta = metadata[index];

                if (meta == Empty)
                    return false;

                if (meta != Deleted && (meta & 0x7F) == h2)
                {
                    if (entries[index].Key.Equals(key))
                        return true;
                }

                index = (index + 1) & (uint)header->CapacityMask;
            }

            return false;
        }

        /// <summary>
        /// Removes a key-value pair.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TKey key)
        {
            var header = GetHeaderPtr();
            var metadata = GetMetadata();
            var entries = GetEntries();

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            for (int i = 0; i < header->Capacity; i++)
            {
                var meta = metadata[index];

                if (meta == Empty)
                    return false;

                if (meta != Deleted && (meta & 0x7F) == h2)
                {
                    if (entries[index].Key.Equals(key))
                    {
                        metadata[index] = Deleted;
                        header->Count--;
                        return true;
                    }
                }

                index = (index + 1) & (uint)header->CapacityMask;
            }

            return false;
        }

        /// <summary>
        /// Gets or sets a value by key. Throws on get if not found.
        /// </summary>
        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (TryGetValue(key, out var value))
                    return value;
                throw new InvalidOperationException($"Key not found in DynamicHashMap");
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => AddOrSet(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetH2(uint hash)
        {
            // Use upper bits for h2 to get different bits than bucket index
            return (byte)((hash >> 25) & 0x7F);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AlignUp(int value, int alignment)
        {
            return (value + alignment - 1) & ~(alignment - 1);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the hashmap.
        /// </summary>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(_buffer);
        }

        public struct Enumerator
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly byte* _buffer;
            private readonly byte* _metadata;
            private readonly Entry* _entries;
            private readonly int _capacity;
            private int _index;

            internal Enumerator(byte* buffer)
            {
                _buffer = buffer;
                var header = (Header*)buffer;
                _metadata = buffer + sizeof(Header);
                _entries = (Entry*)(buffer + header->EntriesOffset);
                _capacity = header->Capacity;
                _index = -1;
            }

            public bool MoveNext()
            {
                while (++_index < _capacity)
                {
                    var meta = _metadata[_index];
                    if (meta != Empty && meta != Deleted)
                        return true;
                }
                return false;
            }

            public KeyValue Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new KeyValue(_entries[_index].Key, _entries[_index].Value);
            }

            public void Reset()
            {
                _index = -1;
            }
        }

        public readonly struct KeyValue
        {
            public readonly TKey Key;
            public readonly TValue Value;

            public KeyValue(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }

            public void Deconstruct(out TKey key, out TValue value)
            {
                key = Key;
                value = Value;
            }
        }

        private struct Header
        {
            public int Count;
            public int Capacity;
            public int CapacityMask;
            public int EntriesOffset;
        }

        private struct Entry
        {
            public TKey Key;
            public TValue Value;
        }
    }

    public static class DynamicHashMapExtensions
    {
        public static DynamicHashMap<TBuffer, TKey, TValue> AsHashMap<TBuffer, TKey, TValue>(this DynamicBuffer<TBuffer> buffer)
            where TBuffer : unmanaged, IDynamicHashMap
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return new DynamicHashMap<TBuffer, TKey, TValue>(buffer);
        }
    }
}