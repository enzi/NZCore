// <copyright project="NZCore" file="KeyDynamicValueArrayHashMap.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
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
    public unsafe struct KeyDynamicValueArrayHashMap<TKey, TKeyInterpreter> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TKeyInterpreter : unmanaged, IKeyInterpreter<TKey>
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeKeyDynamicValueArrayHashMap<TKey, TKeyInterpreter>* _unsafeKeyValueArrayHashMap;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<KeyValueArrayHashMap<TKey, TKeyInterpreter>>();
#endif

        public KeyDynamicValueArrayHashMap(int initialCapacity, int keyOffset, AllocatorManager.AllocatorHandle allocator)
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

            if (UnsafeUtility.IsNativeContainerType<TKey>())
                AtomicSafetyHandle.SetNestedContainer(m_Safety, true);

            CollectionHelper.SetStaticSafetyId<KeyValueArrayHashMap<TKey, TKeyInterpreter>>(ref m_Safety, ref staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif

            _unsafeKeyValueArrayHashMap = UnsafeKeyDynamicValueArrayHashMap<TKey, TKeyInterpreter>.Create(initialCapacity, ref allocator);
        }

        public bool ContainsKey(TKey key)
        {
            return _unsafeKeyValueArrayHashMap->TryPeekFirstRefValue(key);
        }

        public void SetArrays(NativeArray<TKey> keysArray, NativeArray<byte> valueArray)
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

            UnsafeKeyDynamicValueArrayHashMap<TKey, TKeyInterpreter>.Destroy(_unsafeKeyValueArrayHashMap);
        }

        public KeyDynamicValueArrayHashMapEnumerator<TKey, TKeyInterpreter> GetValuesForKey(TKey key)
        {
            return new KeyDynamicValueArrayHashMapEnumerator<TKey, TKeyInterpreter>
            {
                Map = _unsafeKeyValueArrayHashMap,
                Key = key,
                IsFirst = true
            };
        }

        // helper jobs

        public JobHandle ScheduleCalculateBuckets(NativeArray<TKey> keysArray, NativeArray<byte> valuesArray, JobHandle dependency)
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
            [ReadOnly] public NativeArray<byte> Values;

            public KeyDynamicValueArrayHashMap<TKey, TKeyInterpreter> Hashmap;

            public void Execute()
            {
                Hashmap.SetArrays(Keys, Values);
                Hashmap.CalculateBuckets();
            }
        }
    }
}