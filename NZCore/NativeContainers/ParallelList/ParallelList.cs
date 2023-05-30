using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeParallelList<T>* _unsafeParallelList;
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        internal int m_SafetyIndexHint;
        internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelList<T>>();
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
        private void Initialize<U>(int initialCapacity, ref U allocator) where U : unmanaged, AllocatorManager.IAllocator
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref UnsafeList<T> GetUnsafeList(int threadIndex)
        {
            return ref _unsafeParallelList->GetUnsafeList(threadIndex);
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
        public void GetStartIndexArray(ref NativeArray<int> lengths)
        {
            _unsafeParallelList->GetStartIndexArray(ref lengths);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _unsafeParallelList->Clear();
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckInitialCapacity(int initialCapacity)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Capacity must be >= 0");
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckTotalSize(int initialCapacity, long totalSize)
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

        [NativeContainer]
        [GenerateTestsForBurstCompatibility]
        [NativeContainerIsAtomicWriteOnly]
        public struct ChunkWriter
        {
            private UnsafeParallelList<T>.ChunkWriter _mChunkWriter;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ChunkWriter>();
#endif
            
            internal ChunkWriter(ref ParallelList<T> parallelList)
            {
                _mChunkWriter = parallelList._unsafeParallelList->AsChunkWriter();;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = parallelList.m_Safety;
                CollectionHelper.SetStaticSafetyId(ref m_Safety, ref s_staticSafetyId.Data, "NZCore.ChunkWriter");

                if (parallelList._unsafeParallelList->CheckRangesForNull())
                    Debug.LogError($"Ranges have not been allocated. SetChunkCount(int chunkCount) before writing something.");// {Environment.StackTrace}");
#endif
            }
            
            public void BeginForEachChunk(int chunkIndex)
            {
                _mChunkWriter.BeginForEachChunk(chunkIndex);
            }
            
            public void Write(in T value)
            {
                _mChunkWriter.Write(in value);
            }
            
            public void WriteMemCpy(ref T value)
            {
                _mChunkWriter.WriteMemCpy(ref value);
            }
            
            public void EndForEachChunk()
            {
                _mChunkWriter.EndForEachChunk();
            }

            public void SetManualThreadIndex(int threadIndex)
            {
                _mChunkWriter.SetManualThreadIndex(threadIndex);
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        [GenerateTestsForBurstCompatibility]
        public struct ChunkReader
        {
            private UnsafeParallelList<T>.ChunkReader _mChunkReader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int m_RemainingBlocks;
            internal AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ChunkReader>();
#endif

            internal ChunkReader(ref ParallelList<T> stream)
            {
                _mChunkReader = stream._unsafeParallelList->AsChunkReader();;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_RemainingBlocks = 0;
                m_Safety = stream.m_Safety;
                CollectionHelper.SetStaticSafetyId(ref m_Safety, ref s_staticSafetyId.Data, "NZCore.ChunkReader");
#endif
            }
            
            public int BeginForEachChunk(int chunkIndex)
            {
                return _mChunkReader.BeginForEachChunk(chunkIndex);
            }
            
            public ref T Read()
            {
                return ref _mChunkReader.Read();
            }

            public T* GetPtr()
            {
                return _mChunkReader.GetPtr();
            }

            public void Reset(int chunkIndex)
            {
                _mChunkReader.Reset(chunkIndex);
            }

            public int GetListIndex(int chunkIndex)
            {
                return _mChunkReader.GetListIndex(chunkIndex);
            }
        }
        
        [NativeContainer]
        [GenerateTestsForBurstCompatibility]
        [NativeContainerIsAtomicWriteOnly]
        public struct ThreadWriter
        {
            private UnsafeParallelList<T>.ThreadWriter _mThreadWriter;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ThreadWriter>();
#endif
            
            internal ThreadWriter(ref ParallelList<T> parallelList)
            {
                _mThreadWriter = parallelList._unsafeParallelList->AsThreadWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = parallelList.m_Safety;
                CollectionHelper.SetStaticSafetyId(ref m_Safety, ref s_staticSafetyId.Data, "NZCore.ThreadWriter");
#endif
            }
            
            public void Begin()
            {
                _mThreadWriter.Begin();
            }
            
            public void Begin(int threadIndex)
            {
                _mThreadWriter.Begin(threadIndex);
            }
            
            public void Write(in T value)
            {
                _mThreadWriter.Write(in value);
            }
            
            public void WriteMemCpy(ref T value)
            {
                _mThreadWriter.WriteMemCpy(ref value);
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        [GenerateTestsForBurstCompatibility]
        public struct ThreadReader
        {
            public UnsafeParallelList<T>.ThreadReader _mThreadReader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ThreadReader>();
#endif

            internal ThreadReader(ref ParallelList<T> stream)
            {
                _mThreadReader = stream._unsafeParallelList->AsThreadReader();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = stream.m_Safety;
                CollectionHelper.SetStaticSafetyId(ref m_Safety, ref s_staticSafetyId.Data, "NZCore.ThreadReader");
#endif
            }
            
            public int Begin()
            {
                return _mThreadReader.Begin();
            }
            
            public int Begin(int threadIndex)
            {
                return _mThreadReader.Begin(threadIndex);
            }
            
            public ref T Read()
            {
                return ref _mThreadReader.Read();
            }

            public T* GetPtr()
            {
                return _mThreadReader.GetPtr();
            }
        }
        
        // helper jobs

        public JobHandle CopyToArrayMulti(ref NativeList<T> list, ref SystemState state, JobHandle Dependency)
        {
            NativeArray<int> startIndexArray = new NativeArray<int>();
            startIndexArray.Initialize(JobsUtility.ThreadIndexCount, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);

            var prepareHandle = new PrepareParallelListCopyJob()
            {
                startIndexArray = startIndexArray,
                parallelList = this,
                list = list
            }.Schedule(Dependency);
            
            var copyHandle = new ParallelListToArrayMultiThreaded()
            {
                startIndexArray = startIndexArray,
                parallelList = this,
                array = list
            }.ScheduleParallel(JobsUtility.ThreadIndexCount, 1, prepareHandle);

            return copyHandle;
        }
        
        [BurstCompile]
        public struct PrepareParallelListCopyJob : IJob
        {
            [ReadOnly] public ParallelList<T> parallelList;
            public NativeList<T> list; 
            public NativeArray<int> startIndexArray;
            
            public void Execute()
            {
                int parallelListLength = parallelList.Length;
            
                if (list.Capacity < list.Length + parallelListLength)
                    list.Capacity = list.Length + parallelListLength;
            
                list.Length += parallelListLength;

                parallelList.GetStartIndexArray(ref startIndexArray);
            }
        }
        
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public struct ParallelListToArrayMultiThreaded : IJobFor
        {
            [ReadOnly] public NativeArray<int> startIndexArray;
            [ReadOnly] public ParallelList<T> parallelList;
            [NativeDisableContainerSafetyRestriction] public NativeList<T> array; 
        
            public void Execute(int index)
            {
                ref var threadList = ref parallelList.GetUnsafeList(index);
                
                if (threadList.Length == 0)
                    return;

                var sizeOf = sizeof(T);
                var listPtr = array.GetUnsafePtr();
                var threadListPtr = threadList.Ptr;
                //int startIndex = parallelList.GetBlockCountToIndex(index);

                void* dst = (byte*)listPtr + startIndexArray[index] * sizeOf;
                
                UnsafeUtility.MemCpy(dst, threadListPtr, threadList.Length * sizeOf);
            }
        }
        
        public JobHandle CopyToArraySingle(ref NativeList<T> nativeList, JobHandle Dependency)
        {
            return new ParallelListToArraySingleThreaded()
            {
                parallelList = this,
                array = nativeList
            }.Schedule(Dependency);
        }
        
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public struct ParallelListToArraySingleThreaded : IJob
        {
            [ReadOnly] public ParallelList<T> parallelList;
            public NativeList<T> array;
        
            public void Execute()
            {
                int parallelListLength = parallelList.Length;
                
                if (array.Capacity < array.Length + parallelListLength)
                    array.Capacity = array.Length + parallelListLength;
                
                for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
                {
                    ref var threadList = ref parallelList.GetUnsafeList(i);
                    array.AddRangeNoResize(threadList.Ptr, threadList.m_length);
                }
            }
        }
    }
}