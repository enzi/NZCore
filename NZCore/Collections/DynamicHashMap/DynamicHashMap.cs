// <copyright project="NZCore" file="DynamicHashMap.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using static NZCore.DynamicHashMapUtility;
using Assert = UnityEngine.Assertions.Assert;

namespace NZCore
{
    /// <summary>
    /// Marker interface for DynamicHashMap buffer elements.
    /// Implement this on your buffer element struct to use it with DynamicHashMap.
    /// </summary>
    public interface IDynamicHashMap : IBufferElementData { }

    public interface IDynamicHashMap<TKey, TValue> : IDynamicHashMap
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged { }

    /// <summary>
    /// High-performance hashmap backed by a DynamicBuffer using open addressing with linear probing.
    ///
    /// TBuffer allows multiple hashmaps per entity by defining different buffer element types:
    /// <code><![CDATA[
    /// public struct InventoryHashMapBuffer : IDynamicHashMap { public byte Value; }
    /// public struct SkillHashMapBuffer : IDynamicHashMap { public byte Value; }
    ///
    /// var inventoryMap = new DynamicHashMap<InventoryHashMapBuffer, int, Item>(inventoryBuffer);
    /// var skillMap = new DynamicHashMap<SkillHashMapBuffer, int, Skill>(skillBuffer);
    /// ]]></code>
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
        private readonly DynamicBuffer<TBuffer> _buffer;
        private readonly bool _isReadOnly;

        /// <summary>
        /// Creates a DynamicHashMap wrapper around an existing DynamicBuffer.
        /// Auto-initializes with default capacity if buffer is empty.
        /// </summary>
        public DynamicHashMap(DynamicBuffer<TBuffer> buffer, bool isReadOnly = false)
        {
            if (buffer.Length == 0)
            {
                Initialize(buffer, 16);
            }

            _buffer = buffer;
            _isReadOnly = isReadOnly;
        }

        // growing moves the buffer, so each call takes the pointer fresh
        private byte* Ptr => _isReadOnly ? (byte*)_buffer.GetUnsafeReadOnlyPtr() : (byte*)_buffer.GetUnsafePtr();

        private int BufferLength => _buffer.Length * UnsafeUtility.SizeOf<TBuffer>();

        private static byte* GetMetadata(byte* buffer) => buffer + sizeof(Header);

        private static Entry* GetEntries(byte* buffer) => (Entry*)(buffer + ((Header*)buffer)->EntriesOffset);

        /// <summary>
        /// Number of elements in the hashmap.
        /// </summary>
        public int Count => BufferLength >= sizeof(Header) ? ((Header*)Ptr)->Count : 0;

        /// <summary>
        /// Capacity of the hashmap (number of slots).
        /// </summary>
        public int Capacity => BufferLength >= sizeof(Header) ? ((Header*)Ptr)->Capacity : 0;

        public static int EntrySize => sizeof(Entry);

        public static int ValueOffset
        {
            get
            {
                Entry entry = default;
                return (int)((byte*)&entry.Value - (byte*)&entry);
            }
        }

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
            Assert.IsTrue(BufferLength >= sizeof(Header), "DynamicHashMap must be initialized before calling Clear()");

            var buffer = Ptr;
            var header = (Header*)buffer;
            header->Count = 0;
            UnsafeUtility.MemSet(GetMetadata(buffer), Empty, header->Capacity);
        }

        /// <summary>
        /// Tries to add a key-value pair. Returns false if key already exists.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, TValue value)
        {
            var buffer = Ptr;
            var hash = (uint)key.GetHashCode();

            if (Find(buffer, key, hash, out var insertIndex) >= 0)
            {
                return false;
            }

            Insert(buffer, key, value, hash, insertIndex);
            return true;
        }

        /// <summary>
        /// Adds or updates a key-value pair.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrSet(TKey key, TValue value)
        {
            var buffer = Ptr;
            var hash = (uint)key.GetHashCode();
            var index = Find(buffer, key, hash, out var insertIndex);

            if (index >= 0)
            {
                GetEntries(buffer)[index].Value = value;
                return;
            }

            Insert(buffer, key, value, hash, insertIndex);
        }

        // the slot of the key or -1, insertIndex is the first empty or deleted slot on its probe
        private static int Find(byte* buffer, TKey key, uint hash, out int insertIndex)
        {
            var header = (Header*)buffer;
            var metadata = GetMetadata(buffer);
            var entries = GetEntries(buffer);

            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;
            insertIndex = -1;

            for (var i = 0; i < header->Capacity; i++)
            {
                var meta = metadata[index];

                if (meta == Empty)
                {
                    if (insertIndex < 0)
                    {
                        insertIndex = (int)index;
                    }

                    return -1;
                }

                if (meta == Deleted)
                {
                    if (insertIndex < 0)
                    {
                        insertIndex = (int)index;
                    }
                }
                else if ((meta & 0x7F) == h2 && entries[index].Key.Equals(key))
                {
                    return (int)index;
                }

                index = (index + 1) & (uint)header->CapacityMask;
            }

            return -1;
        }

        private void Insert(byte* buffer, TKey key, TValue value, uint hash, int insertIndex)
        {
            var header = (Header*)buffer;

            // below 3/4 load the probe always finds a free slot and stays short
            if ((header->Count + 1) * 4 > header->Capacity * 3)
            {
                buffer = Grow(header->Capacity * 2);
                header = (Header*)buffer;
                Find(buffer, key, hash, out insertIndex);
            }

            GetMetadata(buffer)[insertIndex] = (byte)(Occupied | GetH2(hash));
            GetEntries(buffer)[insertIndex] = new Entry { Key = key, Value = value };
            header->Count++;
        }

        // rehashes the live entries into a buffer with the new capacity, deleted slots don't carry over
        private byte* Grow(int capacity)
        {
            var buffer = Ptr;
            var header = (Header*)buffer;
            var metadata = GetMetadata(buffer);
            var entries = GetEntries(buffer);

            var count = header->Count;
            var live = (Entry*)UnsafeUtility.Malloc(count * sizeof(Entry), UnsafeUtility.AlignOf<Entry>(), Allocator.Temp);
            var liveCount = 0;

            for (var i = 0; i < header->Capacity; i++)
            {
                if (metadata[i] != Empty && metadata[i] != Deleted)
                {
                    live[liveCount++] = entries[i];
                }
            }

            Initialize(_buffer, capacity);

            buffer = Ptr;
            header = (Header*)buffer;
            metadata = GetMetadata(buffer);
            entries = GetEntries(buffer);

            for (var i = 0; i < liveCount; i++)
            {
                var hash = (uint)live[i].Key.GetHashCode();
                var index = hash & (uint)header->CapacityMask;

                while (metadata[index] != Empty)
                {
                    index = (index + 1) & (uint)header->CapacityMask;
                }

                metadata[index] = (byte)(Occupied | GetH2(hash));
                entries[index] = live[i];
            }

            header->Count = liveCount;
            UnsafeUtility.Free(live, Allocator.Temp);
            return buffer;
        }

        /// <summary>
        /// Tries to get a value by key.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            var buffer = Ptr;
            var header = (Header*)buffer;
            var metadata = GetMetadata(buffer);
            var entries = GetEntries(buffer);

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            for (var i = 0; i < header->Capacity; i++)
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
            var buffer = Ptr;
            var header = (Header*)buffer;
            var metadata = GetMetadata(buffer);
            var entries = GetEntries(buffer);

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            for (var i = 0; i < header->Capacity; i++)
            {
                var meta = metadata[index];

                if (meta == Empty)
                {
                    break;
                }

                if (meta != Deleted && (meta & 0x7F) == h2)
                {
                    if (entries[index].Key.Equals(key))
                    {
                        return ref entries[index].Value;
                    }
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
            var buffer = Ptr;
            var header = (Header*)buffer;
            var metadata = GetMetadata(buffer);
            var entries = GetEntries(buffer);

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            for (var i = 0; i < header->Capacity; i++)
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
            var buffer = Ptr;
            var header = (Header*)buffer;
            var metadata = GetMetadata(buffer);
            var entries = GetEntries(buffer);

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            for (var i = 0; i < header->Capacity; i++)
            {
                var meta = metadata[index];

                if (meta == Empty)
                {
                    return false;
                }

                if (meta != Deleted && (meta & 0x7F) == h2)
                {
                    if (entries[index].Key.Equals(key))
                    {
                        return true;
                    }
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
            var buffer = Ptr;
            var header = (Header*)buffer;
            var metadata = GetMetadata(buffer);
            var entries = GetEntries(buffer);

            var hash = (uint)key.GetHashCode();
            var h2 = GetH2(hash);
            var index = hash & (uint)header->CapacityMask;

            for (var i = 0; i < header->Capacity; i++)
            {
                var meta = metadata[index];

                if (meta == Empty)
                {
                    return false;
                }

                if (meta != Deleted && (meta & 0x7F) == h2)
                {
                    if (entries[index].Key.Equals(key))
                    {
                        header->Count--;
                        RemoveAt(header, metadata, entries, index);
                        return true;
                    }
                }

                index = (index + 1) & (uint)header->CapacityMask;
            }

            return false;
        }

        // shifts the following entries of the cluster back into the hole, a probe stops at the first empty slot and would miss them otherwise
        private static void RemoveAt(Header* header, byte* metadata, Entry* entries, uint hole)
        {
            var mask = (uint)header->CapacityMask;
            var next = (hole + 1) & mask;

            for (var i = 1; i < header->Capacity; i++)
            {
                var meta = metadata[next];

                if (meta == Empty)
                {
                    metadata[hole] = Empty;
                    return;
                }

                if (meta != Deleted)
                {
                    var home = (uint)entries[next].Key.GetHashCode() & mask;

                    // the hole lies on the probe from its home slot
                    if (((next - home) & mask) >= ((next - hole) & mask))
                    {
                        metadata[hole] = meta;
                        entries[hole] = entries[next];
                        hole = next;
                    }
                }

                next = (next + 1) & mask;
            }

            // no empty slot left, only a buffer from before the shift can be that full of deleted slots
            metadata[hole] = Deleted;
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
                {
                    return value;
                }

                throw new InvalidOperationException($"Key not found in DynamicHashMap");
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => AddOrSet(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AlignUp(int value, int alignment) => (value + alignment - 1) & ~(alignment - 1);

        /// <summary>
        /// Returns an enumerator that iterates through the hashmap.
        /// </summary>
        public Enumerator GetEnumerator() => new(Ptr);

        public struct Enumerator
        {
            private readonly byte* _metadata;
            private readonly Entry* _entries;
            private readonly int _capacity;
            private int _index;

            internal Enumerator(byte* buffer)
            {
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
                    {
                        return true;
                    }
                }

                return false;
            }

            public KeyValue Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new(_entries[_index].Key, _entries[_index].Value);
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

        private struct Entry
        {
            public TKey Key;
            public TValue Value;
        }
    }

    public interface IEntityRemap
    {
        bool TryGetNewEntity(Entity oldEntity, out Entity newEntity);
    }

    public static unsafe class DynamicHashMapUtility
    {
        internal const byte Empty = 0;
        internal const byte Occupied = 0x80; // High bit set = occupied
        internal const byte Deleted = 0x7F; // Special marker for tombstone

        internal struct Header
        {
            public int Count;
            public int Capacity;
            public int CapacityMask;
            public int EntriesOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte GetH2(uint hash) =>
            // Use upper bits for h2 to get different bits than bucket index
            (byte)((hash >> 25) & 0x7F);

        public static void RemapEntities<TRemap>(byte* buffer, int length, int entrySize, bool entityKey, int* valueEntityOffsets, int valueEntityOffsetCount,
            ref TRemap remap)
            where TRemap : struct, IEntityRemap
        {
            if (length < sizeof(Header) || ((Header*)buffer)->Count == 0)
            {
                return;
            }

            var header = (Header*)buffer;
            var metadata = buffer + sizeof(Header);
            var entries = buffer + header->EntriesOffset;
            var kept = entityKey ? (byte*)UnsafeUtility.Malloc((long)header->Count * entrySize, 8, Allocator.Temp) : null;
            var keptCount = 0;

            for (var i = 0; i < header->Capacity; i++)
            {
                if (metadata[i] == Empty || metadata[i] == Deleted)
                {
                    continue;
                }

                var entry = entries + i * entrySize;

                for (var j = 0; j < valueEntityOffsetCount; j++)
                {
                    ref var valueEntity = ref *(Entity*)(entry + valueEntityOffsets[j]);
                    valueEntity = remap.TryGetNewEntity(valueEntity, out var newValueEntity) ? newValueEntity : Entity.Null;
                }

                if (entityKey && remap.TryGetNewEntity(*(Entity*)entry, out var newKey))
                {
                    *(Entity*)entry = newKey;
                    UnsafeUtility.MemCpy(kept + keptCount * entrySize, entry, entrySize);
                    keptCount++;
                }
            }

            if (!entityKey)
            {
                return;
            }

            var mask = (uint)header->CapacityMask;
            UnsafeUtility.MemSet(metadata, Empty, header->Capacity);

            for (var i = 0; i < keptCount; i++)
            {
                var entry = kept + i * entrySize;
                var hash = (uint)((Entity*)entry)->GetHashCode();
                var index = hash & mask;

                while (metadata[index] != Empty)
                {
                    index = (index + 1) & mask;
                }

                metadata[index] = (byte)(Occupied | GetH2(hash));
                UnsafeUtility.MemCpy(entries + index * entrySize, entry, entrySize);
            }

            header->Count = keptCount;
        }
    }

    public static class DynamicHashMapExtensions
    {
        public static DynamicHashMap<TBuffer, TKey, TValue> AsHashMap<TBuffer, TKey, TValue>(this DynamicBuffer<TBuffer> buffer, bool isReadOnly = false)
            where TBuffer : unmanaged, IDynamicHashMap where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged =>
            new(buffer, isReadOnly);
    }
}