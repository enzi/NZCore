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
        internal AtomicSafetyHandle m_Safety;
        static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<KeyValueArrayHashMap<TKey, TValue>>();
#endif

        public KeyValueArrayHashMap(int initialCapacity, int keyOffset, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(initialCapacity, keyOffset, ref temp);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        private void Initialize<U>(int initialCapacity, int keyOffset, ref U allocator) where U : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.ToAllocator);

            if (UnsafeUtility.IsNativeContainerType<TKey>() || UnsafeUtility.IsNativeContainerType<TValue>())
                AtomicSafetyHandle.SetNestedContainer(m_Safety, true);

            CollectionHelper.SetStaticSafetyId<KeyValueArrayHashMap<TKey, TValue>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
            
            _unsafeKeyValueArrayHashMap = UnsafeKeyValueArrayHashMap<TKey, TValue>.Create(initialCapacity, keyOffset, ref allocator);
            
        }
        
        public bool ContainsKey(TKey key)
        {
            return _unsafeKeyValueArrayHashMap->TryGetFirstRefValue(key, out var temp0, out var temp1);
        }

        public void SetArrays(NativeArray<TKey> keysArray, NativeArray<TValue> valueArray)
        {
            _unsafeKeyValueArrayHashMap->SetArrays(keysArray, valueArray);
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
                key = key, 
                isFirst = true
            };
        }
        
        // helper jobs
        
        public JobHandle CalculateBuckets(NativeArray<TKey> keysArray, NativeArray<TValue> valuesArray, JobHandle Dependency)
        {
            return new CalculateBucketsJob()
            {
                hashmap = this,
                keys = keysArray,
                values = valuesArray
            }.Schedule(Dependency);
        }
        
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public unsafe struct CalculateBucketsJob : IJob
        {
            [ReadOnly] public NativeArray<TKey> keys;
            [ReadOnly] public NativeArray<TValue> values;

            public KeyValueArrayHashMap<TKey, TValue> hashmap;

            public void Execute()
            {
                hashmap.SetArrays(keys, values);
            }
        }
    }
}