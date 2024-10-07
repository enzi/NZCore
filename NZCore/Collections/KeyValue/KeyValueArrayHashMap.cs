// <copyright project="NZCore" file="KeyValueArrayHashMap.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace NZCore
{
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct KeyValueArrayHashMap<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeKeyValueArrayHashMap<TKey, TValue>* _unsafeKeyValueArrayHashMap;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<KeyValueArrayHashMap<TKey, TValue>>();
#endif

        public KeyValueArrayHashMap(int initialCapacity, int keyOffset, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(initialCapacity, keyOffset, ref temp);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        private void Initialize<TAllocator>(int initialCapacity, int keyOffset, ref TAllocator allocator) where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.ToAllocator);

            if (UnsafeUtility.IsNativeContainerType<TKey>() || UnsafeUtility.IsNativeContainerType<TValue>())
                AtomicSafetyHandle.SetNestedContainer(m_Safety, true);

            CollectionHelper.SetStaticSafetyId<KeyValueArrayHashMap<TKey, TValue>>(ref m_Safety, ref staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif

            _unsafeKeyValueArrayHashMap = UnsafeKeyValueArrayHashMap<TKey, TValue>.Create(initialCapacity, keyOffset, ref allocator);
        }

        public bool ContainsKey(TKey key)
        {
            return _unsafeKeyValueArrayHashMap->TryPeekFirstRefValue(key);
        }

        public void SetArrays(NativeArray<TKey> keysArray, NativeArray<TValue> valueArray)
        {
            _unsafeKeyValueArrayHashMap->SetArrays(keysArray, valueArray);
        }

        public void CalculateBuckets()
        {
            _unsafeKeyValueArrayHashMap->CalculateBuckets();
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

            UnsafeKeyValueArrayHashMap<TKey, TValue>.Destroy(_unsafeKeyValueArrayHashMap);
        }

        public KeyValueArrayHashMapEnumerator<TKey, TValue> GetValuesForKey(TKey key)
        {
            return new KeyValueArrayHashMapEnumerator<TKey, TValue>
            {
                Map = _unsafeKeyValueArrayHashMap,
                Key = key,
                IsFirst = true
            };
        }

        // helper jobs

        public JobHandle ScheduleCalculateBuckets(NativeArray<TKey> keysArray, NativeArray<TValue> valuesArray, JobHandle dependency)
        {
            return new CalculateBucketsJob()
            {
                Hashmap = this,
                Keys = keysArray,
                Values = valuesArray
            }.Schedule(dependency);
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public struct CalculateBucketsJob : IJob
        {
            [ReadOnly] public NativeArray<TKey> Keys;
            [ReadOnly] public NativeArray<TValue> Values;

            public KeyValueArrayHashMap<TKey, TValue> Hashmap;

            public void Execute()
            {
                Hashmap.SetArrays(Keys, Values);
                Hashmap.CalculateBuckets();
            }
        }
    }
}