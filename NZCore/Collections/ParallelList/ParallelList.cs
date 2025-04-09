// <copyright project="NZCore" file="ParallelList.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;

namespace NZCore
{
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct ParallelList<T> : IDisposable
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] internal UnsafeParallelList<T>* _unsafeParallelList;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private int m_SafetyIndexHint;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelList<T>>();
#endif

        public int Length => _unsafeParallelList->Count();

        public ParallelList(AllocatorManager.AllocatorHandle allocator)
            : this(1, allocator)
        {
        }

        public ParallelList(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(initialCapacity, ref temp);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        private void Initialize<TAllocator>(int initialCapacity, ref TAllocator allocator) where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var totalSize = sizeof(T) * (long)initialCapacity;
            CollectionHelper.CheckAllocator(allocator.Handle);
            CheckInitialCapacity(initialCapacity);
            CheckTotalSize(initialCapacity, totalSize);

            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.Handle);
            CollectionHelper.InitNativeContainer<T>(m_Safety);

            CollectionHelper.SetStaticSafetyId<ParallelList<T>>(ref m_Safety, ref s_staticSafetyId.Data);

            m_SafetyIndexHint = (allocator.Handle).AddSafetyHandle(m_Safety);

            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif

            _unsafeParallelList = UnsafeParallelList<T>.Create(initialCapacity, ref allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetChunkCount()
        {
            return _unsafeParallelList->GetChunkCount();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetChunkCount(int chunkCount)
        {
            _unsafeParallelList->SetChunkCount(chunkCount);
        }

        public byte* GetPerThreadListPtr()
        {
            return _unsafeParallelList->GetPerThreadListPtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref UnsafeList<T> GetUnsafeList(int threadIndex)
        {
            return ref _unsafeParallelList->GetUnsafeList(threadIndex);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeList<T>* GetUnsafeListPtr(int threadIndex)
        {
            return _unsafeParallelList->GetUnsafeListPtr(threadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBlockCount(int threadIndex)
        {
            return _unsafeParallelList->BlockCount(threadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBlockCountToIndex(int threadIndex)
        {
            return _unsafeParallelList->BlockCountToIndex(threadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeArray<int> GetStartIndexArray(ref SystemState state)
        {
            return _unsafeParallelList->GetStartIndexArray(ref state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetStartIndexArray(ref NativeArray<int> lengths)
        {
            return _unsafeParallelList->GetStartIndexArray(ref lengths);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _unsafeParallelList->Clear();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckInitialCapacity(int initialCapacity)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Capacity must be >= 0");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckTotalSize(int initialCapacity, long totalSize)
        {
            // Make sure we cannot allocate more than int.MaxValue (2,147,483,647 bytes)
            // because the underlying UnsafeUtility.Malloc is expecting a int.
            // TODO: change UnsafeUtility.Malloc to accept a UIntPtr length instead to match C++ API
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), $"Capacity * sizeof(T) cannot exceed {int.MaxValue} bytes");
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            UnsafeParallelList<T>.Destroy(_unsafeParallelList);
            _unsafeParallelList = null;
        }

        public ChunkReader AsChunkReader()
        {
            return new ChunkReader(ref this);
        }

        public ChunkWriter AsChunkWriter()
        {
            return new ChunkWriter(ref this);
        }

        public ThreadReader AsThreadReader()
        {
            return new ThreadReader(ref this);
        }

        public ThreadWriter AsThreadWriter()
        {
            return new ThreadWriter(ref this);
        }

        public ParallelListEnumerator GetEnumerator()
        {
            return new ParallelListEnumerator(this);
        }

        public void Report()
        {
            for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                ref UnsafeList<T> parallelList = ref _unsafeParallelList->GetUnsafeList(i);

                Debug.Log($"Thread {i} has {parallelList.Length} elements.");
            }
        }

        [NativeContainer]
        [GenerateTestsForBurstCompatibility]
        [NativeContainerIsAtomicWriteOnly]
        public struct ChunkWriter
        {
            private UnsafeParallelList<T>.ChunkWriter chunkWriter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private readonly AtomicSafetyHandle m_Safety;
            private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<ChunkWriter>();
#endif

            internal ChunkWriter(ref ParallelList<T> parallelList)
            {
                chunkWriter = parallelList._unsafeParallelList->AsChunkWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = parallelList.m_Safety;
                CollectionHelper.SetStaticSafetyId(ref m_Safety, ref staticSafetyId.Data, "NZCore.ChunkWriter");

                if (parallelList._unsafeParallelList->CheckRangesForNull())
                    Debug.LogError($"Ranges have not been allocated. SetChunkCount(int chunkCount) before writing something."); // {Environment.StackTrace}");
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void BeginForEachChunk(int chunkIndex)
            {
                chunkWriter.BeginForEachChunk(chunkIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write(in T value)
            {
                chunkWriter.Write(in value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteMemCpy(ref T value)
            {
                chunkWriter.WriteMemCpy(ref value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EndForEachChunk()
            {
                chunkWriter.EndForEachChunk();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetManualThreadIndex(int threadIndex)
            {
                chunkWriter.SetManualThreadIndex(threadIndex);
            }

            public int GetThreadIndex()
            {
                return chunkWriter.GetThreadIndex();
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        [GenerateTestsForBurstCompatibility]
        public struct ChunkReader
        {
            private UnsafeParallelList<T>.ChunkReader chunkReader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private AtomicSafetyHandle m_Safety;
            private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<ChunkReader>();
#endif

            internal ChunkReader(ref ParallelList<T> stream)
            {
                chunkReader = stream._unsafeParallelList->AsChunkReader();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = stream.m_Safety;
                CollectionHelper.SetStaticSafetyId(ref m_Safety, ref staticSafetyId.Data, "NZCore.ChunkReader");
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int BeginForEachChunk(int chunkIndex)
            {
                return chunkReader.BeginForEachChunk(chunkIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T Read()
            {
                return ref chunkReader.Read();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T* GetPtr()
            {
                return chunkReader.GetPtr();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset(int chunkIndex)
            {
                chunkReader.Reset(chunkIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetListIndex(int chunkIndex)
            {
                return chunkReader.GetListIndex(chunkIndex);
            }
        }

        [NativeContainer]
        [GenerateTestsForBurstCompatibility]
        [NativeContainerIsAtomicWriteOnly]
        public struct ThreadWriter
        {
            private UnsafeParallelList<T>.ThreadWriter threadWriter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private AtomicSafetyHandle m_Safety;
            private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<ThreadWriter>();
#endif

            internal ThreadWriter(ref ParallelList<T> parallelList)
            {
                threadWriter = parallelList._unsafeParallelList->AsThreadWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = parallelList.m_Safety;
                CollectionHelper.SetStaticSafetyId(ref m_Safety, ref staticSafetyId.Data, "NZCore.ThreadWriter");
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Begin()
            {
                threadWriter.Begin();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Begin(int threadIndex)
            {
                threadWriter.Begin(threadIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write(in T value)
            {
                threadWriter.Write(in value);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T* WriteAndReturn(in T value)
            {
                return threadWriter.WriteAndReturn(in value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteMemCpy(ref T value)
            {
                threadWriter.WriteMemCpy(ref value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref UnsafeList<T> GetList()
            {
                return ref threadWriter.GetList();
            }

            public int GetThreadIndex()
            {
                return threadWriter.GetThreadIndex();
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        [GenerateTestsForBurstCompatibility]
        public struct ThreadReader
        {
            private UnsafeParallelList<T>.ThreadReader threadReader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private AtomicSafetyHandle m_Safety;
            private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<ThreadReader>();
#endif

            internal ThreadReader(ref ParallelList<T> stream)
            {
                threadReader = stream._unsafeParallelList->AsThreadReader();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = stream.m_Safety;
                CollectionHelper.SetStaticSafetyId(ref m_Safety, ref staticSafetyId.Data, "NZCore.ThreadReader");
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Begin()
            {
                return threadReader.Begin();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Begin(int threadIndex)
            {
                return threadReader.Begin(threadIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T Read()
            {
                return ref threadReader.Read();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T* GetPtr()
            {
                return threadReader.GetPtr();
            }
        }

        // helper jobs

        public JobHandle CopyToArraySingle(
            ref NativeList<T> nativeList,
            JobHandle dependency)
        {
            return new UnsafeParallelListToArraySingleThreaded()
            {
                ParallelList = _unsafeParallelList,
                List = nativeList.m_ListData
            }.Schedule(dependency);
        }

        public JobHandle CopyToArraySingleAndIndex<TKey>(
            ref NativeList<T> nativeList,
            ref ArrayHashMap<TKey, T> arrayHashMap,
            JobHandle dependency,
            ParallelListToArrayAndIndexSingleThreaded<TKey> jobParam = default)
            where TKey : unmanaged, IEquatable<TKey>
        {
            return new ParallelListToArrayAndIndexSingleThreaded<TKey>()
            {
                ParallelList = this,
                List = nativeList,
                ArrayHashMap = arrayHashMap
            }.Schedule(dependency);
        }

        public JobHandle CopyToArraySingleAndIndex<TKey>(
            ref NativeList<T> nativeList,
            ref ArrayHashMap<TKey, T> arrayHashMap,
            ref ArrayHashMap<TKey, T> arrayHashMap2,
            JobHandle dependency,
            ParallelListToArrayAndIndexSingleThreadedForTwoMaps<TKey> jobParam = default)
            where TKey : unmanaged, IEquatable<TKey>
        {
            return new ParallelListToArrayAndIndexSingleThreadedForTwoMaps<TKey>()
            {
                ParallelList = this,
                List = nativeList,
                ArrayHashMap1 = arrayHashMap,
                ArrayHashMap2 = arrayHashMap2
            }.Schedule(dependency);
        }

        [BurstCompile]
        public struct ParallelListToArraySingleThreaded : IJob
        {
            [ReadOnly] public ParallelList<T> ParallelList;
            public NativeList<T> Array;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute()
            {
                int parallelListLength = ParallelList.Length;
                int oldArrayLength = Array.Length;

                Array.ResizeUninitialized(oldArrayLength + parallelListLength);

                //Debug.Log($"Copying {parallelListLength} elements");

                var sizeOf = sizeof(T);
                UnsafeList<T>* listData = Array.m_ListData;

                for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
                {
                    ref var threadList = ref ParallelList.GetUnsafeList(i);

                    void* dst = ((byte*)listData->Ptr) + oldArrayLength * sizeOf;
                    UnsafeUtility.MemCpy(dst, threadList.Ptr, threadList.m_length * sizeOf);
                    oldArrayLength += threadList.m_length;
                }
            }
        }

        [BurstCompile]
        public struct UnsafeParallelListToArraySingleThreaded : IJob
        {
            [NativeDisableUnsafePtrRestriction] [ReadOnly]
            public UnsafeParallelList<T>* ParallelList;

            [NativeDisableUnsafePtrRestriction] public UnsafeList<T>* List;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Execute()
            {
                var sizeOf = sizeof(T);
                int parallelListLength = ParallelList->Length;
                int oldListLength = List->Length;

                List->Resize(oldListLength + parallelListLength, NativeArrayOptions.UninitializedMemory);
                byte* listPtr = (byte*) (List->Ptr + oldListLength);
                //Debug.Log($"Copying {parallelListLength} elements");
                
                UnsafeParallelList<T>.PerThreadList* perThreadListPtr = (UnsafeParallelList<T>.PerThreadList*)ParallelList->GetPerThreadListPtr();
                
                for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
                {
                    var threadList = perThreadListPtr[i].List;

                    if (threadList.Length == 0)
                    {
                        continue;
                    }
                    
                    UnsafeUtility.MemCpy(listPtr, threadList.Ptr, threadList.m_length * sizeOf);
                    oldListLength += threadList.m_length;
                }
            }
        }

        [BurstCompile]
        public struct ParallelListToArrayAndIndexSingleThreaded<TKey> : IJob
            where TKey : unmanaged, IEquatable<TKey>
        {
            [ReadOnly] public ParallelList<T> ParallelList;
            public NativeList<T> List;
            public ArrayHashMap<TKey, T> ArrayHashMap;

            public void Execute()
            {
                int parallelListLength = ParallelList.Length;
                int oldListLength = List.Length;
                int newLength = oldListLength + parallelListLength;

                List.ResizeUninitialized(newLength);
                ArrayHashMap.SetCapacity(newLength);
                byte* listPtr = (byte*)List.m_ListData->Ptr;

                //Debug.Log($"Copying {parallelListLength} elements");

                var sizeOf = sizeof(T);

                UnsafeParallelList<T>.PerThreadList* perThreadListPtr = (UnsafeParallelList<T>.PerThreadList*)ParallelList.GetPerThreadListPtr();

                byte* dst = listPtr + oldListLength * sizeOf;

                for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
                {
                    //var threadList = ParallelList.GetUnsafeList(i);
                    var threadList = perThreadListPtr[i].List;
                    var tmpByteLength = threadList.m_length * sizeOf;

                    UnsafeUtility.MemCpy(dst, threadList.Ptr, tmpByteLength);
                    dst += tmpByteLength;

                    //ArrayHashMap.CalculateBucketsSingle(threadList.Ptr, threadList.m_length);
                }

                ArrayHashMap.SetValuesPtr(listPtr);
                ArrayHashMap.CalculateBuckets(oldListLength, parallelListLength);
            }
        }

        [BurstCompile]
        public struct ParallelListToArrayAndIndexSingleThreadedForTwoMaps<TKey> : IJob
            where TKey : unmanaged, IEquatable<TKey>
        {
            [ReadOnly] public ParallelList<T> ParallelList;
            public NativeList<T> List;
            public ArrayHashMap<TKey, T> ArrayHashMap1;
            public ArrayHashMap<TKey, T> ArrayHashMap2;

            public void Execute()
            {
                int parallelListLength = ParallelList.Length;
                int oldListLength = List.Length;
                int newLength = oldListLength + parallelListLength;

                List.ResizeUninitialized(newLength);

                byte* listPtr = (byte*)List.m_ListData->Ptr;

                //Debug.Log($"Copying {parallelListLength} elements");

                var sizeOf = sizeof(T);

                UnsafeParallelList<T>.PerThreadList* perThreadListPtr = (UnsafeParallelList<T>.PerThreadList*)ParallelList.GetPerThreadListPtr();

                byte* dst = listPtr + oldListLength * sizeOf;

                for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
                {
                    //var threadList = ParallelList.GetUnsafeList(i);
                    var threadList = perThreadListPtr[i].List;
                    var tmpByteLength = threadList.m_length * sizeOf;

                    UnsafeUtility.MemCpy(dst, threadList.Ptr, tmpByteLength);
                    dst += tmpByteLength;

                    //ArrayHashMap.CalculateBucketsSingle(threadList.Ptr, threadList.m_length);
                }

                ArrayHashMap1.SetCapacity(newLength);
                ArrayHashMap1.SetValuesPtr(listPtr);
                ArrayHashMap1.CalculateBuckets(oldListLength, parallelListLength);

                ArrayHashMap2.SetCapacity(newLength);
                ArrayHashMap2.SetValuesPtr(listPtr);
                ArrayHashMap2.CalculateBuckets(oldListLength, parallelListLength);
            }
        }


        /////////////////////////////// 
        /// parallel
        ///////////////////////////////
        public JobHandle CopyToArrayMulti(
            ref NativeList<T> list,
            JobHandle dependency)
        {
            var prepareHandle = new PrepareParallelListCopyJob()
            {
                ParallelList = this,
                List = list
            }.Schedule(dependency);

            var copyHandle = new ParallelListToArrayMultiThreaded()
            {
                ParallelList = this,
                List = list
            }.ScheduleParallel(JobsUtility.ThreadIndexCount, 1, prepareHandle);

            return copyHandle;
        }

        public JobHandle CopyToArrayParallelAndIndex<TKey>(ref NativeList<T> nativeList, ref ArrayHashMap<TKey, T> arrayHashMap, JobHandle dependency)
            where TKey : unmanaged, IEquatable<TKey>
        {
            var prepareHandle = new PrepareParallelListCopyAndIndexJob<TKey>()
            {
                ParallelList = this,
                List = nativeList,
                ArrayHashMap = arrayHashMap
            }.Schedule(dependency);

            var copyAndIndexHandle = new ParallelListToArrayAndIndexMultiThreaded<TKey>()
            {
                ParallelList = this,
                List = nativeList,
                ArrayHashMap = arrayHashMap
            }.ScheduleParallel(JobsUtility.ThreadIndexCount, 1, prepareHandle);

            return copyAndIndexHandle;
        }

        public JobHandle CopyToArrayParallelAndIndex<TKey>(ref NativeList<T> nativeList, ref ArrayHashMap<TKey, T> arrayHashMap, ref ArrayHashMap<TKey, T> arrayHashMap2, JobHandle dependency)
            where TKey : unmanaged, IEquatable<TKey>
        {
            var prepareHandle = new PrepareParallelListCopyAndIndexJobForTwoMaps<TKey>()
            {
                ParallelList = this,
                List = nativeList,
                ArrayHashMap = arrayHashMap,
                ArrayHashMap2 = arrayHashMap2
            }.Schedule(dependency);

            var copyAndIndexHandle1 = new ParallelListToArrayAndIndexMultiThreadedForTwoMaps<TKey>()
            {
                ParallelList = this,
                List = nativeList,
                ArrayHashMap1 = arrayHashMap,
                ArrayHashMap2 = arrayHashMap2
            }.ScheduleParallel(JobsUtility.ThreadIndexCount, 1, prepareHandle);

            return copyAndIndexHandle1;
        }

        [BurstCompile]
        public struct PrepareParallelListCopyJob : IJob
        {
            [ReadOnly] public ParallelList<T> ParallelList;
            public NativeList<T> List;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Execute()
            {
                int parallelListCount = ParallelList.Length;
                List.SetCapacity(List.Length + parallelListCount);
            }
        }

        [BurstCompile]
        public struct ParallelListToArrayMultiThreaded : IJobFor
        {
            [ReadOnly] public ParallelList<T> ParallelList;

            [NativeDisableContainerSafetyRestriction]
            public NativeList<T> List;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Execute(int index)
            {
                var threadList = ParallelList.GetUnsafeList(index);
                int threadListLength = threadList.Length;

                if (threadListLength == 0)
                    return;

                var sizeOf = sizeof(T);

                void* dst = List.GetUnsafePtr() + List.m_ListData->m_length;
                Interlocked.Add(ref List.m_ListData->m_length, threadListLength);
                UnsafeUtility.MemCpy(dst, threadList.Ptr, threadListLength * sizeOf);
            }
        }

        [BurstCompile]
        public struct PrepareParallelListCopyAndIndexJob<TKey> : IJob
            where TKey : unmanaged, IEquatable<TKey>
        {
            [ReadOnly] public ParallelList<T> ParallelList;
            public NativeList<T> List;
            public ArrayHashMap<TKey, T> ArrayHashMap;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Execute()
            {
                int parallelListCount = ParallelList.Length;
                List.SetCapacity(List.Length + parallelListCount);
                //List.Clear();
                ArrayHashMap.SetCapacity(List.Length + parallelListCount);
                ArrayHashMap.SetValuesPtr((byte*)List.m_ListData->Ptr);
            }
        }


        [BurstCompile]
        public struct PrepareParallelListCopyAndIndexJobForTwoMaps<TKey> : IJob
            where TKey : unmanaged, IEquatable<TKey>
        {
            [ReadOnly] public ParallelList<T> ParallelList;
            public NativeList<T> List;
            public ArrayHashMap<TKey, T> ArrayHashMap;
            public ArrayHashMap<TKey, T> ArrayHashMap2;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Execute()
            {
                int parallelListCount = ParallelList.Length;
                List.SetCapacity(List.Length + parallelListCount);

                ArrayHashMap.SetCapacity(List.Length + parallelListCount);
                ArrayHashMap.SetValuesPtr((byte*)List.m_ListData->Ptr);

                ArrayHashMap2.SetCapacity(List.Length + parallelListCount);
                ArrayHashMap2.SetValuesPtr((byte*)List.m_ListData->Ptr);
            }
        }

        [BurstCompile]
        public struct ParallelListToArrayAndIndexMultiThreaded<TKey> : IJobFor
            where TKey : unmanaged, IEquatable<TKey>
        {
            [ReadOnly] public ParallelList<T> ParallelList;

            [NativeDisableContainerSafetyRestriction]
            public NativeList<T> List;

            [NativeDisableContainerSafetyRestriction]
            public ArrayHashMap<TKey, T> ArrayHashMap;

            //[MethodImpl(MethodImplOptions.NoInlining)]
            public void Execute(int index)
            {
                var threadList = ParallelList.GetUnsafeList(index);
                int threadListLength = threadList.Length;

                if (threadListLength == 0)
                    return;

                var sizeOf = sizeof(T);
                //void* dst = List.GetUnsafePtr() + List.m_ListData->m_length;
                //Interlocked.Add(ref List.m_ListData->m_length, threadListLength);
                var newLength = Interlocked.Add(ref List.m_ListData->m_length, threadListLength);
                void* dst = List.GetUnsafePtr() + (newLength - threadListLength);
                UnsafeUtility.MemCpy(dst, threadList.Ptr, threadListLength * sizeOf);

                ArrayHashMap.CalculateBucketsParallel(threadList.Ptr, threadList.m_length);
            }
        }

        [BurstCompile]
        public struct ParallelListToArrayAndIndexMultiThreadedForTwoMaps<TKey> : IJobFor
            where TKey : unmanaged, IEquatable<TKey>
        {
            [ReadOnly] public ParallelList<T> ParallelList;

            [NativeDisableContainerSafetyRestriction]
            public NativeList<T> List;

            [NativeDisableContainerSafetyRestriction]
            public ArrayHashMap<TKey, T> ArrayHashMap1;

            [NativeDisableContainerSafetyRestriction]
            public ArrayHashMap<TKey, T> ArrayHashMap2;

            [NativeDisableContainerSafetyRestriction]
            private NativeList<T> tmpList1;

            public void Execute(int index)
            {
                var threadList = ParallelList.GetUnsafeList(index);
                int threadListLength = threadList.Length;

                if (threadListLength == 0)
                    return;

                // if (!tmpList1.IsCreated)
                // {
                //     tmpList1 = new NativeList<T>(threadListLength, Allocator.Temp);
                // }
                // else
                // {
                //     tmpList1.Clear();
                // }
                //
                var sizeOf = UnsafeUtility.SizeOf<T>();
                //
                // tmpList1.ResizeUninitialized(threadListLength);
                // var tmpListArray = tmpList1.AsArray();
                // var tmpListPtr = (T*) tmpListArray.GetUnsafeReadOnlyPtr();
                //
                // for (int i = 0; i < threadListLength; i++)
                // {
                //     tmpListArray[i] = threadList[i];
                // }


                var newLength = Interlocked.Add(ref List.m_ListData->m_length, threadListLength);
                void* dst = List.GetUnsafePtr() + (newLength - threadListLength);
                //UnsafeUtility.MemCpy(dst, threadList.Ptr, threadListLength * sizeOf);
                UnsafeUtility.MemCpy(dst, threadList.Ptr, threadListLength * sizeOf);

                //ArrayHashMap1.CalculateBucketsParallel(tmpListPtr, threadListLength);
                //ArrayHashMap2.CalculateBucketsParallel(tmpListPtr, threadListLength);

                //ArrayHashMap1.CalculateBucketsParallel(threadList.Ptr, threadListLength);
                ArrayHashMap2.CalculateBucketsParallel(threadList.Ptr, threadListLength);
            }
        }

        [BurstCompile]
        public struct ParallelListEnumerator
        {
            private ParallelList<T> parallelList;
            private int currentIndex;

            private UnsafeList<T>* listPtr;
            public ref UnsafeList<T> Current => ref *listPtr;

            public ParallelListEnumerator(ParallelList<T> list)
            {
                parallelList = list;
                currentIndex = 0;
                listPtr = null;
            }

            public bool MoveNext()
            {
                while(currentIndex < JobsUtility.ThreadIndexCount)
                {
                    listPtr = parallelList.GetUnsafeListPtr(currentIndex);
                    currentIndex++;
                    
                    if (listPtr->Length > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}