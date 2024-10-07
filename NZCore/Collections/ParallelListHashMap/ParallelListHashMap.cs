// <copyright project="NZCore" file="ParallelListHashMap.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore
{
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct ParallelListHashMap<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeParallelListHashMap<TKey, TValue>* _unsafeParallelListHashMap;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelListHashMap<TKey, TValue>>();
#endif

        public ParallelListHashMap(AllocatorManager.AllocatorHandle allocator)
            : this(1, allocator)
        {
        }

        public ParallelListHashMap(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(initialCapacity, ref temp);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        private void Initialize<TAllocator>(int initialCapacity, ref TAllocator allocator) where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.ToAllocator);

            if (UnsafeUtility.IsNativeContainerType<TKey>() || UnsafeUtility.IsNativeContainerType<TValue>())
                AtomicSafetyHandle.SetNestedContainer(m_Safety, true);

            CollectionHelper.SetStaticSafetyId<ParallelListHashMap<TKey, TValue>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif

            _unsafeParallelListHashMap = UnsafeParallelListHashMap<TKey, TValue>.Create(initialCapacity, ref allocator);
        }

        public bool ContainsKey(TKey key)
        {
            return _unsafeParallelListHashMap->TryPeekFirstRefValue(key);
        }

        public void SetArrays(UnsafeParallelList<TKey> keyArray, UnsafeParallelList<TValue> valueArray)
        {
            _unsafeParallelListHashMap->SetArrays(keyArray, valueArray);
        }

        // public void PrintValues()
        // {
        //     //Debug.Log($"PrintValues with length {allocatedIndexLength}");
        //     for (int i = 0; i < allocatedIndexLength; i++)
        //     {
        //         var key = (*(TKey*)(Keys + i * sizeof(TKey)));
        //         Debug.Log($"Key: {key}");
        //     }
        //     for (int i = 0; i < allocatedIndexLength; i++)
        //     {
        //         var value = (*(TValue*)(Values + i * sizeof(TValue)));
        //         Debug.Log($"value: {value}");
        //     }
        //     for (int i = 0; i < allocatedIndexLength; i++)
        //     {
        //         var nextValue = (*(int*)(next + i * sizeof(int)));
        //         Debug.Log($"nextValue: {nextValue}");
        //     }
        //     for (int i = 0; i < (bucketCapacityMask + 1); i++)
        //     {
        //         var bucketValue = (*(int*)(buckets + i * sizeof(int)));
        //         Debug.Log($"bucketValue: {bucketValue}");
        //     }
        // }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif

            UnsafeParallelListHashMap<TKey, TValue>.Destroy(_unsafeParallelListHashMap);
        }

        public UnsafeParallelListHashMapEnumerator<TKey, TValue> GetValuesForKey(TKey key)
        {
            return new UnsafeParallelListHashMapEnumerator<TKey, TValue>
            {
                Map = _unsafeParallelListHashMap,
                Key = key,
                IsFirst = true
            };
        }
    }
}