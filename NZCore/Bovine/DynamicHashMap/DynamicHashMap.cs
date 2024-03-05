﻿// <copyright file="DynamicHashMap.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore.Core.Iterators
{
    using CollectionHelper = Unity.Collections.CollectionHelper;

    //[DebuggerDisplay("Count = {Count()}, Capacity = {Capacity}, IsCreated = {IsCreated}, IsEmpty = {IsEmpty}")]
    //[DebuggerTypeProxy(typeof(DynamicHashMapDebuggerTypeProxy<,>))]
    public unsafe struct DynamicHashMap<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        private DynamicBuffer<byte> data;

        internal DynamicHashMap(DynamicBuffer<byte> buffer)
        {
            CheckSize(buffer);

            this.data = buffer;

            // First time, need to setup
            if (buffer.Length == 0)
            {
                this.Allocate();
            }
        }

        /// <summary> Gets a value indicating whether container is empty. </summary>
        /// <value>True if this container empty.</value>
        public bool IsEmpty => !this.IsCreated || DynamicHashMapData.IsEmpty(this.BufferReadOnly);

        public bool IsCreated => this.data.IsCreated;

        /// <summary> Gets or sets the number of items that can fit in the container. </summary>
        /// <value>The number of items that the container can hold before it resizes its internal storage.</value>
        /// <remarks>Capacity specifies the number of items the container can currently hold. You can change Capacity
        /// to fit more or fewer items. Changing Capacity creates a new array of the specified size, copies the
        /// old array to the new one, and then deallocates the original array memory.</remarks>
        public int Capacity
        {
            get => this.data.AsDataReadOnly<TKey, TValue>()->KeyCapacity;
            set => DynamicHashMapData.ReallocateHashMap<TKey, TValue>(this.data, value, UnsafeParallelHashMapData.GetBucketSize(value), out _);
        }

        private DynamicHashMapData* BufferReadOnly => this.data.AsDataReadOnly<TKey, TValue>();

        /// <summary> Gets and sets values by key. </summary>
        /// <remarks> Getting a key that is not present will throw. Setting a key that is not already present will add the key. </remarks>
        /// <param name="key"> The key to look up. </param>
        /// <value> The value associated with the key. </value>
        /// <exception cref="ArgumentException"> For getting, thrown if the key was not present. </exception>
        public TValue this[TKey key]
        {
            get
            {
                if (this.TryGetValue(key, out var res))
                {
                    return *res;
                }

                ThrowKeyNotPresent(key);

                return default;
            }

            set
            {
                if (DynamicHashMapBase<TKey, TValue>.TryGetFirstValueAtomic(this.BufferReadOnly, key, out var item, out var iterator))
                {
                    DynamicHashMapBase<TKey, TValue>.SetValue(this.data, ref iterator, ref value);
                }
                else
                {
                    DynamicHashMapBase<TKey, TValue>.TryAdd(this.data, key, value, false);
                }
            }
        }

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

        /// <summary>
        /// Try adding an element with the specified key and value into the container. If the key already exist, the value won't be updated.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="item">The value of the element to add.</param>
        /// <returns>Returns true if value is added into the container, otherwise returns false.</returns>
        public bool TryAdd(TKey key, TValue item)
        {
            return DynamicHashMapBase<TKey, TValue>.TryAdd(this.data, key, item, false);
        }

        /// <summary>
        /// Add an element with the specified key and value into the container.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="item">The value of the element to add.</param>
        public void Add(TKey key, TValue item)
        {
            this.TryAdd(key, item);
        }

        /// <summary>
        /// Removes the element with the specified key from the container.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>Returns true if the key was removed from the container, otherwise returns false indicating key wasn't in the container.</returns>
        public bool Remove(TKey key)
        {
            return DynamicHashMapBase<TKey, TValue>.Remove(this.data, key, false) != 0;
        }

        /// <summary> Gets the value associated with the specified key. </summary>
        /// <param name="key"> The key of the value to get. </param>
        /// <param name="item"> If key is found item parameter will contain value. </param>
        /// <returns> Returns true if key is found, otherwise returns false. </returns>
        public bool TryGetValue(TKey key, out TValue* item)
        {
            return DynamicHashMapBase<TKey, TValue>.TryGetFirstValueAtomic(this.BufferReadOnly, key, out item, out _);
        }

        /// <summary> Determines whether an key is in the container. </summary>
        /// <param name="key"> The key to locate in the container. </param>
        /// <returns> Returns true if the container contains the key. </returns>
        public bool ContainsKey(TKey key)
        {
            return DynamicHashMapBase<TKey, TValue>.TryGetFirstValueAtomic(this.BufferReadOnly, key, out _, out _);
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
        
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
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

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void ThrowKeyNotPresent(TKey key)
        {
            throw new ArgumentException($"Key: {key} is not present in the NativeHashMap.");
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
            private readonly DynamicHashMapData* data;
            private readonly int capacityMask;
            private readonly byte* keys;
            private readonly byte* values;
            private readonly int* bucketArray;
            private readonly int* bucketNext;
            
            private byte* key;
            private byte* value;

            private int currentIndex;
            private int currentBucketIndex;

            public Enumerator(DynamicHashMap<TKey, TValue> hashMap)
            {
                data = hashMap.BufferReadOnly;
                keys = DynamicHashMapData.GetKeys(data);
                values = DynamicHashMapData.GetValues(data);
                bucketArray = (int*) DynamicHashMapData.GetBuckets(data);
                bucketNext = (int*) DynamicHashMapData.GetNexts(data);

                currentBucketIndex = -1;
                currentIndex = 0;
                capacityMask = data->BucketCapacityMask;

                key = null;
                value = null;
            }

            public void Dispose() { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (currentBucketIndex == -1)
                {
                    do
                    {
                        if (currentIndex <= capacityMask)
                        {
                            currentBucketIndex = bucketArray[currentIndex];
                            currentIndex++;
                        }
                        else
                        {
                            return false;
                        }
                    } while (currentBucketIndex == -1);
                }
                
                // if (currentBucketIndex == -1) // not needed (?)
                //     return false;

                key = keys + currentBucketIndex * sizeof(TKey);
                value = values + currentBucketIndex * sizeof(TValue);
                currentBucketIndex = bucketNext[currentBucketIndex];
                return true;
            }

            public ref TKey CurrentKey => ref UnsafeUtility.AsRef<TKey>(key);
            public ref TValue CurrentValue => ref UnsafeUtility.AsRef<TValue>(value);
        }
    }
    
    internal sealed class DynamicHashMapDebuggerTypeProxy<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
#if !NET_DOTS
        DynamicHashMap<TKey, TValue> m_Target;

        public DynamicHashMapDebuggerTypeProxy(DynamicHashMap<TKey, TValue> target)
        {
            m_Target = target;
        }

        public List<Pair<TKey, TValue>> Items
        {
            get
            {
                var result = new List<Pair<TKey, TValue>>();
                using (var kva = m_Target.GetKeyValueArrays(Allocator.Temp))
                {
                    for (var i = 0; i < kva.Length; ++i)
                    {
                        result.Add(new Pair<TKey, TValue>(kva.Keys[i], kva.Values[i]));
                    }
                }
                return result;
            }
        }
#endif
    }
}
