using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace NZCore
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UnsafeParallelList<T> : INativeDisposable
        where T : unmanaged
    {
        //[NativeDisableUnsafePtrRestriction] private UnsafeParallelList<T>* self;
        [NativeDisableUnsafePtrRestriction] private UnsafeParallelListHeader* header;
        [NativeDisableUnsafePtrRestriction] private PerThreadList* m_perThreadLists;
        [NativeDisableUnsafePtrRestriction] private UnsafeParallelListRange* Ranges;

        private AllocatorManager.AllocatorHandle m_Allocator;
        public bool IsCreated;

        public int Length => Count();
        
        
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeParallelList<T>* Create<U>(int initialCapacity, ref U allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where U : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeParallelList<T>* unsafeParallelList = allocator.Allocate(default(UnsafeParallelList<T>), 1);

            //unsafeParallelList->self = unsafeParallelList;
            unsafeParallelList->m_Allocator = allocator.Handle;
            
            int size = UnsafeUtility.SizeOf<PerThreadList>();
            int align = UnsafeUtility.AlignOf<PerThreadList>();
            int maxThreadCount = JobsUtility.MaxJobThreadCount;

            unsafeParallelList->m_perThreadLists = (PerThreadList*)UnsafeUtility.Malloc(size * maxThreadCount, align, allocator.ToAllocator);
            
            for (int i = 0; i < maxThreadCount; i++)
            {
                unsafeParallelList->m_perThreadLists[i].list = UnsafeList<T>.Create(initialCapacity, allocator.ToAllocator, NativeArrayOptions.UninitializedMemory);
            }
            
            int allocationSize = sizeof(UnsafeParallelListHeader);
            byte* buffer = (byte*)Memory.Unmanaged.Allocate(allocationSize, UnsafeUtility.AlignOf<UnsafeParallelListHeader>(), allocator.ToAllocator);   // could be just temp memory
            UnsafeUtility.MemClear(buffer, allocationSize);

            unsafeParallelList->header = (UnsafeParallelListHeader*)buffer;

            unsafeParallelList->header->chunkCount = 0;
            unsafeParallelList->Ranges = null;
            
            unsafeParallelList->IsCreated = true;

            return unsafeParallelList;
        }

        public void SetChunkCount(int chunkCount)
        {
            bool sameChunkCount = chunkCount == header->chunkCount;
            bool allocatedRanges = Ranges != null;
            int allocationSize = sizeof(UnsafeParallelListRange) * chunkCount;

            if (sameChunkCount && allocatedRanges)
            {
                UnsafeUtility.MemClear(Ranges, allocationSize);
            }
            else
            {
                if (allocatedRanges)
                    DeallocateRanges();

                header->chunkCount = chunkCount;
            
                Ranges = (UnsafeParallelListRange*)Memory.Unmanaged.Allocate(allocationSize, UnsafeUtility.AlignOf<UnsafeParallelListRange>(), m_Allocator);
            
                UnsafeUtility.MemClear(Ranges, allocationSize);
            }
        }

        public int GetChunkCount()
        {
            return header->chunkCount;
        }

        private void DeallocateRanges()
        {
            Memory.Unmanaged.Free(Ranges, m_Allocator);
        }
        
        public void Clear()
        {
            if (!IsCreated)
                return;

            for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
            {
                var blockList = m_perThreadLists + i;

                blockList->list->Clear();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(in T value, int threadIndex)
        {
            m_perThreadLists[threadIndex].list->Add(in value);
        }

        public int Count()
        {
            int result = 0;
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
            {
                result += m_perThreadLists[i].list->m_length;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref UnsafeList<T> GetUnsafeList(int threadId)
        {
            return ref *m_perThreadLists[threadId].list;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetUnsafePtr(int threadId)
        {
            return (byte*) m_perThreadLists[threadId].list->Ptr;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BlockCount(int threadId)
        {
            return m_perThreadLists[threadId].list->m_length;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BlockCountToIndex(int threadId)
        {
            int count = 0;
            
            for (int i = 0; i < threadId && i < JobsUtility.MaxJobThreadCount; i++)
                count += m_perThreadLists[i].list->m_length;

            return count;
        }
        
        public void GetLengthArray(ref NativeArray<int> lengths)
        {
            int count = 0;
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
            {
                lengths[i] = count;
                count += m_perThreadLists[i].list->m_length;
            }
        }

        public NativeArray<int> GetLengthArray(ref SystemState state)
        {
            //NativeArray<int> lengths = new NativeArray<int>(128, Allocator.TempJob);
            NativeArray<int> lengths = new NativeArray<int>();
            lengths.Initialize(128, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);

            int count = 0;
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
            {
                lengths[i] = count;
                count += m_perThreadLists[i].list->m_length;
            }

            return lengths;
        }
        
        public Reader AsReader()
        {
            return new Reader(ref this);
        }

        public Writer AsWriter()
        {
            return new Writer(ref this);
        }
        
        public static void Destroy(UnsafeParallelList<T>* unsafeParallelList)
        {
            //CheckNull(listData);
            var allocator = unsafeParallelList->m_Allocator;
            unsafeParallelList->Dispose();
            AllocatorManager.Free(allocator, unsafeParallelList);
        }

        public void Dispose()
        {
            if (!IsCreated)
                return;

            for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
            {
                var threadList = m_perThreadLists[i];
                if (threadList.list->IsCreated)
                    threadList.list->Dispose();
            }
            
            UnsafeUtility.Free(m_perThreadLists, m_Allocator.ToAllocator);

            DeallocateRanges();
            Memory.Unmanaged.Free(header, m_Allocator);
            header = null;

            //UnsafeUtility.Free(self, m_Allocator.ToAllocator);
            
            m_Allocator = Allocator.None;
        }

        public bool CheckRangesForNull()
        {
            return Ranges == null;
        }

        [BurstCompile]
        private struct DisposeJob : IJob
        {
            public UnsafeParallelList<T> list;

            public void Execute()
            {
                list.Dispose();
            }
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = new DisposeJob()
            {
                list = this
            }.Schedule(inputDeps);
            return jobHandle;
        }

        private struct PerThreadList
        {
            public UnsafeList<T>* list;
        }

        private struct UnsafeParallelListHeader
        {
            
            public int chunkCount;
        }

        private struct UnsafeParallelListRange
        {
            public int listIndex;
            public int startIndex;
            //public int endIndex;

            public int ElementCount;
        }

        public struct Writer
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly PerThreadList* m_perThreadLists;
            [NativeDisableUnsafePtrRestriction]
            private UnsafeList<T>* m_List;
            [NativeDisableUnsafePtrRestriction]
            private readonly UnsafeParallelListRange* m_Ranges;
            
            [NativeSetThreadIndex]
            int m_ThreadIndex;

            private int m_ChunkIndex;
            private int m_StartIndex;

            internal Writer(ref UnsafeParallelList<T> stream)
            {
                m_perThreadLists = stream.m_perThreadLists;
                m_Ranges = stream.Ranges;
                
                m_ChunkIndex = int.MinValue;
                m_ThreadIndex = 0;
                m_StartIndex = 0;
                m_List = null;
            }
            
            public void BeginForEachChunk(int chunkIndex)
            {
                m_List = m_perThreadLists[m_ThreadIndex].list;
                
                m_ChunkIndex = chunkIndex;
                m_StartIndex = m_List->m_length;
            }
            
            public void Write(in T value)
            {
                m_List->Add(in value);
            }

            public void WriteMemCpy(ref T value)
            {
                var idx = m_List->m_length;

                if (m_List->m_length + 1 > m_List->Capacity)
                {
                    m_List->Resize(idx + 1);
                }
                else
                {
                    m_List->m_length += 1;
                }
                
                //UnsafeUtility.WriteArrayElement(Ptr, idx, value);
                
                UnsafeUtility.MemCpy(m_List->Ptr + idx, UnsafeUtility.AddressOf(ref value), UnsafeUtility.SizeOf<T>());
            }
            
            public void EndForEachChunk()
            {
                m_Ranges[m_ChunkIndex] = new UnsafeParallelListRange()
                {
                    ElementCount = m_List->m_length - m_StartIndex,
                    startIndex = m_StartIndex,
                    listIndex = m_ThreadIndex
                };
            }

            public void SetManualThreadIndex(int threadIndex)
            {
                m_ThreadIndex = threadIndex;
            }
        }

        public struct Reader
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly PerThreadList* m_perThreadLists;
            [NativeDisableUnsafePtrRestriction]
            private readonly UnsafeParallelListRange* m_Ranges;
            [NativeDisableUnsafePtrRestriction]
            private byte* ptr;
            
            [NativeSetThreadIndex]
            int m_ThreadIndex;
            
            private readonly int size;
            private int currentIndex;
            
            internal Reader(ref UnsafeParallelList<T> stream)
            {
                m_perThreadLists = stream.m_perThreadLists;
                m_Ranges = stream.Ranges;
                
                m_ThreadIndex = 0;
                size = UnsafeUtility.SizeOf<T>();
                currentIndex = 0;
                ptr = null;
            }
            
            public int BeginForEachChunk(int chunkIndex)
            {
                if (m_Ranges == null)
                    return 0;
                
                var range = m_Ranges[chunkIndex];
                int m_RemainingItemCount = range.ElementCount;
                
                ptr = m_RemainingItemCount > 0 ? (byte*) m_perThreadLists[range.listIndex].list->Ptr : null;
                currentIndex = m_RemainingItemCount > 0 ? m_Ranges[chunkIndex].startIndex : 0;
                
                //Debug.Log($"[{m_ThreadIndex}] - BeginForEachChunk chunkIndex {chunkIndex} startIndex {currentIndex} count: {m_Ranges[chunkIndex].ElementCount}");

                return m_RemainingItemCount;
            }
            
            public ref T Read()
            {
                // int index = currentIndex;
                // currentIndex++;
                // return ref UnsafeUtility.AsRef<T>(ptr + index * size);
                ref var returnValue = ref UnsafeUtility.AsRef<T>(ptr + currentIndex * size);
                currentIndex++;
                return ref returnValue;
            }

            public T* GetPtr()
            {
                return (T*) ptr;
            }

            public void Reset(int chunkIndex)
            {
                var range = m_Ranges[chunkIndex];
                int m_RemainingItemCount = range.ElementCount;
                currentIndex = m_RemainingItemCount > 0 ? m_Ranges[chunkIndex].startIndex : 0;
            }

            public int GetListIndex(int chunkIndex)
            {
                var range = m_Ranges[chunkIndex];
                return range.listIndex;
            }
        }
    }
}