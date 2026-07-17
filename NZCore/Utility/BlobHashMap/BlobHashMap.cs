// <copyright project="NZCore" file="BlobHashMap.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace NZCore
{
    /// <summary>
    /// Immutable hashmap stored in a blob asset using open addressing with linear probing.
    ///
    /// Build inside a BlobBuilder:
    /// <code><![CDATA[
    /// ref var root = ref builder.ConstructRoot<MyBlob>();
    /// var mapBuilder = builder.AllocateHashMap(ref root.Map, itemCount);
    /// mapBuilder.Add(key, value);
    /// ]]></code>
    ///
    /// Metadata byte: 0 = empty, 0x80+ = occupied with h2 hash bits (no tombstones, blob is immutable)
    /// </summary>
    [MayOnlyLiveInBlobStorage]
    public unsafe struct BlobHashMap<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        internal BlobArray<byte> Metadata;
        internal BlobArray<Entry> Entries;
        internal int CountValue;

        /// <summary>
        /// Number of elements in the hashmap.
        /// </summary>
        public int Count => CountValue;

        /// <summary>
        /// Capacity of the hashmap (number of slots).
        /// </summary>
        public int Capacity => Metadata.Length;

        /// <summary>
        /// Tries to get a pointer to a value by key.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out Ptr<TValue> value)
        {
            var capacity = Metadata.Length;
            var mask = (uint)(capacity - 1);

            var hash = (uint)key.GetHashCode();
            var h2 = BlobHashMapHelper.GetH2(hash);
            var index = hash & mask;

            for (var i = 0; i < capacity; i++)
            {
                var meta = Metadata[(int)index];

                if (meta == BlobHashMapHelper.Empty)
                {
                    value = default;
                    return false;
                }

                if ((meta & 0x7F) == h2)
                {
                    ref var entry = ref Entries[(int)index];
                    if (entry.Key.Equals(key))
                    {
                        value = new Ptr<TValue>(ref entry.Value);
                        return true;
                    }
                }

                index = (index + 1) & mask;
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
            var capacity = Metadata.Length;
            var mask = (uint)(capacity - 1);

            var hash = (uint)key.GetHashCode();
            var h2 = BlobHashMapHelper.GetH2(hash);
            var index = hash & mask;

            for (var i = 0; i < capacity; i++)
            {
                var meta = Metadata[(int)index];

                if (meta == BlobHashMapHelper.Empty)
                {
                    break;
                }

                if ((meta & 0x7F) == h2)
                {
                    ref var entry = ref Entries[(int)index];
                    if (entry.Key.Equals(key))
                    {
                        return ref entry.Value;
                    }
                }

                index = (index + 1) & mask;
            }

            throw new InvalidOperationException("Key not found in BlobHashMap");
        }

        /// <summary>
        /// Checks if a key exists.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key)
        {
            return TryGetValue(key, out _);
        }

        /// <summary>
        /// Gets a value by key. Throws if not found.
        /// </summary>
        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value.Ref;
                }

                throw new InvalidOperationException("Key not found in BlobHashMap");
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the hashmap.
        /// </summary>
        public Enumerator GetEnumerator() => new(ref this);

        public struct Enumerator
        {
            private readonly byte* _metadata;
            private readonly Entry* _entries;
            private readonly int _capacity;
            private int _index;

            internal Enumerator(ref BlobHashMap<TKey, TValue> map)
            {
                _metadata = (byte*)map.Metadata.GetUnsafePtr();
                _entries = (Entry*)map.Entries.GetUnsafePtr();
                _capacity = map.Metadata.Length;
                _index = -1;
            }

            public bool MoveNext()
            {
                while (++_index < _capacity)
                {
                    if (_metadata[_index] != BlobHashMapHelper.Empty)
                    {
                        return true;
                    }
                }

                return false;
            }

            public KeyValue Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new(_entries + _index);
            }

            public void Reset()
            {
                _index = -1;
            }
        }

        public readonly struct KeyValue
        {
            private readonly Entry* _entry;

            internal KeyValue(Entry* entry)
            {
                _entry = entry;
            }

            public ref TKey Key => ref _entry->Key;
            public ref TValue Value => ref _entry->Value;

            public void Deconstruct(out TKey key, out TValue value)
            {
                key = _entry->Key;
                value = _entry->Value;
            }
        }

        public struct Entry
        {
            public TKey Key;
            public TValue Value;
        }
    }

    /// <summary>
    /// Write-side view of a BlobHashMap during blob construction.
    /// Valid only while the owning BlobBuilder is alive.
    /// </summary>
    public readonly unsafe struct BlobBuilderHashMap<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        private readonly byte* _metadata;
        private readonly BlobHashMap<TKey, TValue>.Entry* _entries;
        private readonly int* _count;
        private readonly int _capacity;

        internal BlobBuilderHashMap(byte* metadata, BlobHashMap<TKey, TValue>.Entry* entries, int* count, int capacity)
        {
            _metadata = metadata;
            _entries = entries;
            _count = count;
            _capacity = capacity;
        }

        public int Count => *_count;
        public int Capacity => _capacity;

        /// <summary>
        /// Adds a key-value pair. Throws if the key already exists or the map is full.
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            if (!TryAdd(key, value))
            {
                throw new ArgumentException("Key already exists in BlobBuilderHashMap or the map is full");
            }
        }

        /// <summary>
        /// Tries to add a key-value pair. Returns false if the key already exists or the map is full.
        /// </summary>
        public bool TryAdd(TKey key, TValue value)
        {
            var mask = (uint)(_capacity - 1);

            var hash = (uint)key.GetHashCode();
            var h2 = BlobHashMapHelper.GetH2(hash);
            var index = hash & mask;

            for (var i = 0; i < _capacity; i++)
            {
                var meta = _metadata[index];

                if (meta == BlobHashMapHelper.Empty)
                {
                    _metadata[index] = (byte)(BlobHashMapHelper.Occupied | h2);
                    _entries[index].Key = key;
                    _entries[index].Value = value;
                    (*_count)++;
                    return true;
                }

                if ((meta & 0x7F) == h2 && _entries[index].Key.Equals(key))
                {
                    return false;
                }

                index = (index + 1) & mask;
            }

            return false;
        }
    }

    public static class BlobHashMapExtensions
    {
        /// <summary>
        /// Allocates a BlobHashMap sized for itemCapacity items (max ~75% load factor)
        /// and returns a builder to add key-value pairs with.
        /// </summary>
        public static unsafe BlobBuilderHashMap<TKey, TValue> AllocateHashMap<TKey, TValue>(this ref BlobBuilder builder, ref BlobHashMap<TKey, TValue> map, int itemCapacity)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var capacity = math.ceilpow2(math.max(4, itemCapacity * 4 / 3));

            var metadata = builder.Allocate(ref map.Metadata, capacity);
            var entries = builder.Allocate(ref map.Entries, capacity);
            map.CountValue = 0;

            var metadataPtr = (byte*)metadata.GetUnsafePtr();
            UnsafeUtility.MemSet(metadataPtr, BlobHashMapHelper.Empty, capacity);

            return new BlobBuilderHashMap<TKey, TValue>(
                metadataPtr,
                (BlobHashMap<TKey, TValue>.Entry*)entries.GetUnsafePtr(),
                (int*)UnsafeUtility.AddressOf(ref map.CountValue),
                capacity);
        }
    }

    internal static class BlobHashMapHelper
    {
        internal const byte Empty = 0;
        internal const byte Occupied = 0x80; // High bit set = occupied

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte GetH2(uint hash) =>
            // Use upper bits for h2 to get different bits than bucket index
            (byte)((hash >> 25) & 0x7F);
    }
}