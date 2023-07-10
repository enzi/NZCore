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
    public unsafe struct ArrayHashMap<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeArrayHashMap<TKey, TValue>* _unsafeArrayHashMap;
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ArrayHashMap<TKey, TValue>>();
#endif

        public int Length => _unsafeArrayHashMap->Length;

        public ArrayHashMap(int keyOffset, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(keyOffset, ref temp);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        private void Initialize<TAllocator>(int keyOffset, ref TAllocator allocator) where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.ToAllocator);

            if (UnsafeUtility.IsNativeContainerType<TKey>() || UnsafeUtility.IsNativeContainerType<TValue>())
                AtomicSafetyHandle.SetNestedContainer(m_Safety, true);

            CollectionHelper.SetStaticSafetyId<ArrayHashMap<TKey, TValue>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
            
            _unsafeArrayHashMap = UnsafeArrayHashMap<TKey, TValue>.Create(keyOffset, ref allocator);
            
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key)
        {
            return _unsafeArrayHashMap->TryPeekFirstRefValue(key);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetArrays(ref NativeList<TValue> list)
        {
            var array = list.AsArray();
            _unsafeArrayHashMap->SetArrays(ref array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetArrays(ref NativeArray<TValue> valueArray)
        {
            _unsafeArrayHashMap->SetArrays(ref valueArray);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CalculateBuckets()
        {
            _unsafeArrayHashMap->CalculateBuckets();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TKey* GetKeyArrayPtr()
        {
            return _unsafeArrayHashMap->GetKeyArrayPtr();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            
            UnsafeArrayHashMap<TKey,TValue>.Destroy(_unsafeArrayHashMap);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayHashMapEnumerator<TKey, TValue> GetValuesForKey(TKey key)
        {
            return new ArrayHashMapEnumerator<TKey, TValue>
            {
                Map = _unsafeArrayHashMap, 
                Key = key, 
                IsFirst = true
            };
        }
        
        // helper jobs
        
        public JobHandle ScheduleCalculateBuckets(ref NativeList<TValue> values, JobHandle dependency)
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
            [ReadOnly] public NativeList<TValue> Values;

            public ArrayHashMap<TKey, TValue> Hashmap;

            public void Execute()
            {
                Hashmap.SetArrays(ref Values);
                Hashmap.CalculateBuckets();
            }
        }
        
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public struct SetArraysJob : IJob
        {
            [ReadOnly] 
            public NativeArray<TValue> Values;
            public ArrayHashMap<TKey, TValue> Hashmap;

            public void Execute()
            {
                Hashmap.SetArrays(ref Values);
            }
        }
    }
}