// <copyright project="NZCore" file="ParallelListHashMapValueOnly.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
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
    public unsafe struct ParallelListHashMapValueOnly<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeParallelListHashMapValueOnly<TKey, TValue>* _unsafeParallelListHashMap;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelListHashMapValueOnly<TKey, TValue>>();
#endif

        public ParallelListHashMapValueOnly(int initialCapacity, int keyOffset, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(initialCapacity, keyOffset, ref temp);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal void Initialize<TAllocator>(int initialCapacity, int keyOffset, ref TAllocator allocator) where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.ToAllocator);

            if (UnsafeUtility.IsNativeContainerType<TKey>() || UnsafeUtility.IsNativeContainerType<TValue>())
                AtomicSafetyHandle.SetNestedContainer(m_Safety, true);

            CollectionHelper.SetStaticSafetyId<ParallelListHashMap<TKey, TValue>>(ref m_Safety, ref staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif

            _unsafeParallelListHashMap = UnsafeParallelListHashMapValueOnly<TKey, TValue>.Create(initialCapacity, keyOffset, ref allocator);
        }

        public bool ContainsKey(TKey key)
        {
            return _unsafeParallelListHashMap->TryPeekFirstRefValue(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetArrays(ParallelList<TValue> valueArray)
        {
            _unsafeParallelListHashMap->SetArrays(*valueArray._unsafeParallelList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBuckets()
        {
            _unsafeParallelListHashMap->CalculateBuckets();
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

            UnsafeParallelListHashMapValueOnly<TKey, TValue>.Destroy(_unsafeParallelListHashMap);
        }

        public UnsafeParallelListHashMapValueOnlyEnumerator<TKey, TValue> GetValuesForKey(TKey key)
        {
            return new UnsafeParallelListHashMapValueOnlyEnumerator<TKey, TValue>
            {
                Map = _unsafeParallelListHashMap,
                Key = key,
                IsFirst = true
            };
        }

        // helper jobs

        public JobHandle ScheduleCalculateBuckets(ParallelList<TValue> values, JobHandle dependency)
        {
            return new CalculateBucketsJob()
            {
                Hashmap = this,
                Values = values
            }.Schedule(dependency);
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public struct CalculateBucketsJob : IJob
        {
            [ReadOnly] public ParallelList<TValue> Values;

            public ParallelListHashMapValueOnly<TKey, TValue> Hashmap;

            public void Execute()
            {
                Hashmap.SetArrays(Values);
                Hashmap.CalculateBuckets();
            }
        }
    }
}