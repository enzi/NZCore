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
        private static void CheckInitialCapacity(int initialCapacity)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Capacity must be >= 0");
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
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
                    Debug.LogError($"Ranges have not been allocated. SetChunkCount(int chunkCount) before writing something.");// {Environment.StackTrace}");
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
            public void WriteMemCpy(ref T value)
            {
                threadWriter.WriteMemCpy(ref value);
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

        public JobHandle CopyToArrayMulti(ref NativeList<T> list, ref SystemState state, JobHandle dependency)
        {
            NativeArray<int> startIndexArray = new NativeArray<int>();
            startIndexArray.Initialize(JobsUtility.ThreadIndexCount, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);

            var prepareHandle = new PrepareParallelListCopyJob()
            {
                StartIndexArray = startIndexArray,
                ParallelList = this,
                List = list
            }.Schedule(dependency);
            
            var copyHandle = new ParallelListToArrayMultiThreaded()
            {
                StartIndexArray = startIndexArray,
                ParallelList = this,
                Array = list
            }.ScheduleParallel(JobsUtility.ThreadIndexCount, 1, prepareHandle);

            return copyHandle;
        }
        
        [BurstCompile]
        public struct PrepareParallelListCopyJob : IJob
        {
            [ReadOnly] public ParallelList<T> ParallelList;
            public NativeList<T> List; 
            public NativeArray<int> StartIndexArray;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute()
            {
                List.ResizeUninitialized(List.Length + ParallelList.Length);
                ParallelList.GetStartIndexArray(ref StartIndexArray);
            }
        }
        
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public struct ParallelListToArrayMultiThreaded : IJobFor
        {
            [ReadOnly] public NativeArray<int> StartIndexArray;
            [ReadOnly] public ParallelList<T> ParallelList;
            [NativeDisableContainerSafetyRestriction] public NativeList<T> Array; 
        
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(int index)
            {
                ref var threadList = ref ParallelList.GetUnsafeList(index);
                
                if (threadList.Length == 0)
                    return;

                var sizeOf = sizeof(T);
                var listPtr = Array.GetUnsafePtr();
                var threadListPtr = threadList.Ptr;
                //int startIndex = parallelList.GetBlockCountToIndex(index);

                void* dst = listPtr + StartIndexArray[index];
                
                UnsafeUtility.MemCpy(dst, threadListPtr, threadList.Length * sizeOf);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JobHandle CopyToArraySingle(ref NativeList<T> nativeList, JobHandle dependency)
        {
            return new UnsafeParallelListToArraySingleThreaded()
            {
                ParallelList = _unsafeParallelList,
                Array = nativeList.m_ListData
            }.Schedule(dependency);
        }
        
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public struct ParallelListToArraySingleThreaded : IJob
        {
            [ReadOnly] 
            public ParallelList<T> ParallelList;
            public NativeList<T> Array;
        
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute()
            {
                int parallelListLength = ParallelList.Length;

                if (Array.Capacity < Array.Length + parallelListLength)
                    Array.Capacity = Array.Length + parallelListLength;

                //Debug.Log($"Copying {parallelListLength} elements");
                
                var sizeOf = sizeof(T);
                
                for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
                {
                    ref var threadList = ref ParallelList.GetUnsafeList(i);
                    //array.AddRangeNoResize(threadList.Ptr, threadList.m_length);
                    
                    
                    void* dst = (byte*)Array.m_ListData->Ptr + Array.m_ListData->m_length * sizeOf;
                    UnsafeUtility.MemCpy(dst, threadList.Ptr, threadList.m_length * sizeOf);
                    Array.m_ListData->m_length += threadList.m_length;
                }
            }
        }
        
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public struct UnsafeParallelListToArraySingleThreaded : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            [ReadOnly] 
            public UnsafeParallelList<T>* ParallelList;
            
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<T>* Array;
        
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute()
            {
                int parallelListLength = ParallelList->Length;

                if (Array->Capacity < Array->Length + parallelListLength)
                    Array->Capacity = Array->Length + parallelListLength;

                //Debug.Log($"Copying {parallelListLength} elements");
                
                var sizeOf = sizeof(T);

                for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
                {
                    ref var threadList = ref ParallelList->GetUnsafeList(i);
                    //array.AddRangeNoResize(threadList.Ptr, threadList.m_length);
                    
                    void* dst = ((byte*)Array->Ptr) + Array->m_length * sizeOf;
                    UnsafeUtility.MemCpy(dst, threadList.Ptr, threadList.m_length * sizeOf);
                    Array->m_length += threadList.m_length;
                }
            }
        }
    }
}