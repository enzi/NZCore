﻿// <copyright project="NZCore" file="DynamicMultiHashMap.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore.Core.Iterators
{
    public interface IDynamicMultiHashMap<TKey, TValue> : IDynamicHashMapBase<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
    }

    public unsafe struct DynamicMultiHashMap<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        private DynamicBuffer<byte> data;

        internal DynamicMultiHashMap(DynamicBuffer<byte> buffer)
        {
            CheckSize(buffer);

            this.data = buffer;

            // First time, need to setup
            if (buffer.Length == 0)
            {
                this.Allocate();
            }
        }

        /// <summary> Reports whether container is empty. </summary>
        /// <value>True if this container empty.</value>
        public bool IsEmpty => !this.IsCreated || DynamicHashMapData.IsEmpty(this.BufferReadOnly);

        public bool IsCreated => this.data.IsCreated;

        /// <summary> The number of items that can fit in the container. </summary>
        /// <value>The number of items that the container can hold before it resizes its internal storage.</value>
        /// <remarks>Capacity specifies the number of items the container can currently hold. You can change Capacity
        /// to fit more or fewer items. Changing Capacity creates a new array of the specified size, copies the
        /// old array to the new one, and then deallocates the original array memory.</remarks>
        public int Capacity
        {
            get => this.data.AsDataReadOnly<TKey, TValue>()->KeyCapacity;
            set => DynamicHashMapData.ReallocateHashMap<TKey, TValue>(this.data, value, UnsafeParallelHashMapData.GetBucketSize(value), out _);
        }

        internal DynamicHashMapData* Buffer => this.data.AsData<TKey, TValue>();

        internal DynamicHashMapData* BufferReadOnly => this.data.AsDataReadOnly<TKey, TValue>();

        /// <summary> The current number of items in the container. </summary>
        /// <returns>The item count.</returns>
        public int Count() => DynamicHashMapData.GetCount(this.BufferReadOnly);

        /// <summary>
        /// Clears the container.
        /// </summary>
        /// <remarks>Containers capacity remains unchanged.</remarks>
        public void Clear()
        {
            DynamicHashMapBase<TKey, TValue>.Clear(this.data);
        }

        /// <summary> Add an element with the specified key and value into the container. </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="item">The value of the element to add.</param>
        public void Add(TKey key, TValue item)
        {
            DynamicHashMapBase<TKey, TValue>.TryAdd(this.data, key, item, true);
        }

        /// <summary>
        /// Removes the element with the specified key from the container.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>Returns true if the key was removed from the container, otherwise returns false indicating key wasn't in the container.</returns>
        public bool Remove(TKey key)
        {
            return DynamicHashMapBase<TKey, TValue>.Remove(this.data, key, true) != 0;
        }

        public void Remove<TValueEq>(TKey key, TValueEq value)
            where TValueEq : unmanaged, IEquatable<TValueEq>
        {
            DynamicHashMapBase<TKey, TValue>.RemoveKeyValue(this.Buffer, key, value);
        }

        /// <summary> Gets the value associated with the specified key. </summary>
        /// <param name="key"> The key of the value to get. </param>
        /// <param name="item"> If key is found item parameter will contain value. </param>
        /// <returns> Returns true if key is found, otherwise returns false. </returns>
        public bool TryGetValue(TKey key, out TValue* item)
        {
            return DynamicHashMapBase<TKey, TValue>.TryGetFirstValueAtomic(this.BufferReadOnly, key, out item, out _);
        }

        /// <summary> Retrieve iterator for the first value for the key. </summary>
        /// <param name="key">The key.</param>
        /// <param name="item">Output value.</param>
        /// <param name="it">Iterator.</param>
        /// <returns>Returns true if the container contains the key.</returns>
        public bool TryGetFirstValue(TKey key, out TValue* item, out NativeParallelMultiHashMapIterator<TKey> it)
        {
            return DynamicHashMapBase<TKey, TValue>.TryGetFirstValueAtomic(this.BufferReadOnly, key, out item, out it);
        }

        /// <summary>
        /// Retrieve iterator to the next value for the key.
        /// </summary>
        /// <param name="item">Output value.</param>
        /// <param name="it">Iterator.</param>
        /// <returns>Returns true if next value for the key is found.</returns>
        public bool TryGetNextValue(out TValue* item, ref NativeParallelMultiHashMapIterator<TKey> it)
        {
            return DynamicHashMapBase<TKey, TValue>.TryGetNextValueAtomic(this.BufferReadOnly, out item, ref it);
        }

        /// <summary> Determines whether an key is in the container. </summary>
        /// <param name="key"> The key to locate in the container. </param>
        /// <returns> Returns true if the container contains the key. </returns>
        public bool ContainsKey(TKey key)
        {
            return DynamicHashMapBase<TKey, TValue>.TryGetFirstValueAtomic(this.BufferReadOnly, key, out _, out _);
        }

        /// <summary> Count number of values for specified key. </summary>
        /// <param name="key">The key to locate in the container.</param>
        /// <returns> The count. </returns>
        public int CountValuesForKey(TKey key)
        {
            if (!this.TryGetFirstValue(key, out _, out var iterator))
            {
                return 0;
            }

            var count = 1;
            while (this.TryGetNextValue(out _, ref iterator))
            {
                count++;
            }

            return count;
        }

        /// <summary> Returns array populated with keys. </summary>
        /// <remarks>Number of returned keys will match number of values in the container. If key contains multiple values it will appear number of times
        /// how many values are associated to the same key. If only unique key values desired use GetUniqueKeyArray instead.</remarks>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns>Array of keys.</returns>
        public NativeArray<TKey> GetKeyArray(Allocator allocator)
        {
            var result = new NativeArray<TKey>(this.Count(), allocator, NativeArrayOptions.UninitializedMemory);
            DynamicHashMapData.GetKeyArray(this.BufferReadOnly, result);
            return result;
        }

        /// <summary>
        /// Returns array populated with values.
        /// </summary>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns>Array of values.</returns>
        public NativeArray<TValue> GetValueArray(Allocator allocator)
        {
            var result = new NativeArray<TValue>(this.Count(), allocator, NativeArrayOptions.UninitializedMemory);
            DynamicHashMapData.GetValueArray(this.BufferReadOnly, result);
            return result;
        }

        /// <summary>
        /// Returns arrays populated with keys and values.
        /// </summary>
        /// <remarks>If key contains multiple values, returned key array will contain multiple identical keys.</remarks>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns>Array of keys-values.</returns>
        public NativeKeyValueArrays<TKey, TValue> GetKeyValueArrays(Allocator allocator)
        {
            var result = new NativeKeyValueArrays<TKey, TValue>(this.Count(), allocator, NativeArrayOptions.UninitializedMemory);
            DynamicHashMapData.GetKeyValueArrays(this.BufferReadOnly, result);
            return result;
        }
        
        public Enumerator GetEnumerator(TKey key)
        {
            return new Enumerator()
            {
                isFirst = 1, 
                key = key,
                hashmap = BufferReadOnly
            };
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckSize(DynamicBuffer<byte> buffer)
        {
            if (buffer.Length != 0 && buffer.Length < UnsafeUtility.SizeOf<DynamicHashMapData>())
            {
                throw new InvalidOperationException($"Buffer has data but is too small to be a header.");
            }
        }

        private void Allocate()
        {
            CollectionHelper.CheckIsUnmanaged<TKey>();
            CollectionHelper.CheckIsUnmanaged<TValue>();

            DynamicHashMapData.AllocateHashMap<TKey, TValue>(this.data, 0, 0, out _);
            this.Clear();
        }
        
        [NativeContainer]
        [NativeContainerIsReadOnly]
        public struct Enumerator
        {
            internal DynamicHashMapData* hashmap;
            internal TKey key;
            internal byte isFirst;
            
            private TValue* value;
            private NativeParallelMultiHashMapIterator<TKey> iterator;

            public void Dispose() { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (isFirst == 1)
                {
                    isFirst = 0;
                    return !DynamicHashMapData.IsEmpty(hashmap) && DynamicHashMapBase<TKey, TValue>.TryGetFirstValueAtomic(hashmap, key, out value, out iterator);
                }

                return DynamicHashMapBase<TKey, TValue>.TryGetNextValueAtomic(hashmap, out value, ref iterator);
            }

            //public ref TKey CurrentKey => ref UnsafeUtility.AsRef<TKey>(key);
            public ref TValue CurrentValue => ref UnsafeUtility.AsRef<TValue>(value);
        }
    }
}
