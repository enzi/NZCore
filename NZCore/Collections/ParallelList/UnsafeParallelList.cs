// <copyright project="NZCore" file="UnsafeParallelList.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

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
        // this is set to 64 bytes so any write to length doesn't invalidate cache lines from other threads
        // also known as false sharing
        public const int PER_THREAD_LIST_SIZE = JobsUtility.CacheLineSize;

        [NativeDisableUnsafePtrRestriction] private UnsafeParallelListHeader* header;
        [NativeDisableUnsafePtrRestriction] private byte* perThreadLists;
        [NativeDisableUnsafePtrRestriction] private UnsafeParallelListRange* ranges;

        private AllocatorManager.AllocatorHandle allocator;
        public bool IsCreated;

        public int Length => Count();

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeParallelList<T>* Create<TAllocator>(int initialCapacity, ref TAllocator allocator)
            where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeParallelList<T>* unsafeParallelList = allocator.Allocate(default(UnsafeParallelList<T>), 1);

            unsafeParallelList->allocator = allocator.Handle;

            //Debug.Log($"parallelList alignOf: {UnsafeUtility.AlignOf<PerThreadList>()}");
            int maxThreadCount = JobsUtility.ThreadIndexCount;
            var perThreadListSize = PER_THREAD_LIST_SIZE * maxThreadCount;
            unsafeParallelList->perThreadLists = (byte*)UnsafeUtility.Malloc(perThreadListSize, 64, allocator.ToAllocator);

            for (int i = 0; i < maxThreadCount; i++)
            {
                unsafeParallelList->GetPerThreadList(i).List = new UnsafeList<T>(initialCapacity, allocator.ToAllocator, NativeArrayOptions.UninitializedMemory);
            }

            int allocationSize = sizeof(UnsafeParallelListHeader);
            byte* buffer = (byte*)Memory.Unmanaged.Allocate(allocationSize, UnsafeUtility.AlignOf<UnsafeParallelListHeader>(), allocator.ToAllocator); // could be just temp memory
            UnsafeUtility.MemClear(buffer, allocationSize);

            unsafeParallelList->header = (UnsafeParallelListHeader*)buffer;

            unsafeParallelList->header->ChunkCount = 0;
            unsafeParallelList->ranges = null;

            unsafeParallelList->IsCreated = true;

            return unsafeParallelList;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref PerThreadList GetPerThreadList(int index)
        {
            return ref UnsafeUtility.AsRef<PerThreadList>(perThreadLists + index * PER_THREAD_LIST_SIZE);
        }

        public byte* GetPerThreadListPtr()
        {
            return perThreadLists;
        }

        public void SetChunkCount(int chunkCount)
        {
            bool sameChunkCount = chunkCount == header->ChunkCount;
            bool allocatedRanges = ranges != null;
            int allocationSize = sizeof(UnsafeParallelListRange) * chunkCount;

            if (sameChunkCount && allocatedRanges)
            {
                UnsafeUtility.MemClear(ranges, allocationSize);
            }
            else
            {
                if (allocatedRanges)
                    DeallocateRanges();

                header->ChunkCount = chunkCount;

                ranges = (UnsafeParallelListRange*)Memory.Unmanaged.Allocate(allocationSize, UnsafeUtility.AlignOf<UnsafeParallelListRange>(), allocator);

                UnsafeUtility.MemClear(ranges, allocationSize);
            }
        }

        public int GetChunkCount()
        {
            return header->ChunkCount;
        }

        private void DeallocateRanges()
        {
            Memory.Unmanaged.Free(ranges, allocator);
        }

        public void Clear()
        {
            if (!IsCreated)
                return;

            for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                ref var parallelList = ref GetPerThreadList(i);

                parallelList.List.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(in T value, int threadIndex)
        {
            ref var parallelList = ref GetPerThreadList(threadIndex);
            parallelList.List.Add(in value);
        }

        public int Count()
        {
            int result = 0;
            for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                var list = *(UnsafeList<T>*)(perThreadLists + i * PER_THREAD_LIST_SIZE);
                result += list.m_length;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref UnsafeList<T> GetUnsafeList(int threadId)
        {
            return ref GetPerThreadList(threadId).List;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeList<T>* GetUnsafeListPtr(int listIndex)
        {
            return (UnsafeList<T>*) (perThreadLists + listIndex * PER_THREAD_LIST_SIZE);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetUnsafePtr(int threadId)
        {
            return (byte*)GetPerThreadList(threadId).List.Ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BlockCount(int threadId)
        {
            return GetPerThreadList(threadId).List.m_length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BlockCountToIndex(int threadId)
        {
            int count = 0;

            for (int i = 0; i < threadId && i < JobsUtility.ThreadIndexCount; i++)
                count += GetPerThreadList(i).List.m_length;

            return count;
        }

        public int GetStartIndexArray(ref NativeArray<int> lengths)
        {
            int count = 0;
            for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                lengths[i] = count;
                count += GetPerThreadList(i).List.m_length;
            }

            return count;
        }

        public NativeArray<int> GetStartIndexArray(ref SystemState state)
        {
            NativeArray<int> lengths = new NativeArray<int>();
            lengths.Initialize(JobsUtility.ThreadIndexCount, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);

            int count = 0;
            for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                lengths[i] = count;
                count += GetPerThreadList(i).List.m_length;
            }

            return lengths;
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

        public static void Destroy(UnsafeParallelList<T>* unsafeParallelList)
        {
            var allocator = unsafeParallelList->allocator;
            unsafeParallelList->Dispose();
            AllocatorManager.Free(allocator, unsafeParallelList);
        }

        public void Dispose()
        {
            if (!IsCreated)
                return;

            for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                GetPerThreadList(i).List.Dispose();
            }

            UnsafeUtility.Free(perThreadLists, allocator.ToAllocator);

            DeallocateRanges();
            Memory.Unmanaged.Free(header, allocator);
            header = null;

            allocator = Allocator.None;
        }

        public bool CheckRangesForNull()
        {
            return ranges == null;
        }

        [BurstCompile]
        private struct DisposeJob : IJob
        {
            public UnsafeParallelList<T> List;

            public void Execute()
            {
                List.Dispose();
            }
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = new DisposeJob()
            {
                List = this
            }.Schedule(inputDeps);
            return jobHandle;
        }

        public struct PerThreadList // 64 bytes
        {
            public UnsafeList<T> List; // 24 bytes

            public ulong padding1; // 8 bytes
            public ulong padding2;
            public ulong padding3;
            public ulong padding4;
            public ulong padding5;
        }

        private struct UnsafeParallelListHeader
        {
            public int ChunkCount;
        }

        private struct UnsafeParallelListRange
        {
            public int ListIndex;
            public int StartIndex;
            public int ElementCount;
        }

        public struct ChunkWriter
        {
            [NativeDisableUnsafePtrRestriction] private readonly byte* perThreadListsPtr;
            [NativeDisableUnsafePtrRestriction] private UnsafeList<T>* list;
            [NativeDisableUnsafePtrRestriction] private readonly UnsafeParallelListRange* ranges;

            [NativeSetThreadIndex] private int threadIndex;

            private int chunkIndex;
            private int startIndex;

            internal ChunkWriter(ref UnsafeParallelList<T> stream)
            {
                perThreadListsPtr = stream.perThreadLists;
                ranges = stream.ranges;

                chunkIndex = int.MinValue;
                threadIndex = 0;
                startIndex = 0;
                list = default;
            }

            public void BeginForEachChunk(int newChunkIndex)
            {
                chunkIndex = newChunkIndex;

                list = (UnsafeList<T>*)(perThreadListsPtr + threadIndex * PER_THREAD_LIST_SIZE);
                startIndex = list->m_length;
            }

            public void Write(in T value)
            {
                list->Add(in value);
            }

            public void WriteMemCpy(ref T value)
            {
                var idx = list->m_length;

                if (list->m_length + 1 > list->Capacity)
                    list->Resize(idx + 1);
                else
                    list->m_length += 1;

                UnsafeUtility.MemCpy(list->Ptr + idx, UnsafeUtility.AddressOf(ref value), UnsafeUtility.SizeOf<T>());
            }

            public void EndForEachChunk()
            {
                ranges[chunkIndex] = new UnsafeParallelListRange()
                {
                    ElementCount = list->m_length - startIndex,
                    StartIndex = startIndex,
                    ListIndex = threadIndex
                };
            }

            public void SetManualThreadIndex(int newThreadIndex)
            {
                threadIndex = newThreadIndex;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetThreadIndex()
            {
                return threadIndex;
            }
        }

        public struct ChunkReader
        {
            [NativeDisableUnsafePtrRestriction] private readonly byte* perThreadListsPtr;
            [NativeDisableUnsafePtrRestriction] private readonly UnsafeParallelListRange* ranges;
            [NativeDisableUnsafePtrRestriction] private byte* ptr;

            private readonly int size;
            private int currentIndex;

            internal ChunkReader(ref UnsafeParallelList<T> stream)
            {
                perThreadListsPtr = stream.perThreadLists;
                ranges = stream.ranges;

                size = UnsafeUtility.SizeOf<T>();
                currentIndex = 0;
                ptr = null;
            }

            public int BeginForEachChunk(int chunkIndex)
            {
                if (ranges == null)
                    return 0;

                var range = ranges[chunkIndex];
                int remainingItemCount = range.ElementCount;

                if (remainingItemCount > 0)
                {
                    ptr = (byte*)((UnsafeList<T>*)(perThreadListsPtr + chunkIndex * PER_THREAD_LIST_SIZE))->Ptr;
                    currentIndex = ranges[chunkIndex].StartIndex;
                }
                else
                {
                    ptr = null;
                    currentIndex = 0;
                }

                return remainingItemCount;
            }

            public ref T Read()
            {
                ref var returnValue = ref UnsafeUtility.AsRef<T>(ptr + currentIndex * size);
                currentIndex++;
                return ref returnValue;
            }

            public T* GetPtr()
            {
                return (T*)ptr;
            }

            public void Reset(int chunkIndex)
            {
                var range = ranges[chunkIndex];
                currentIndex = range.ElementCount > 0 ? ranges[chunkIndex].StartIndex : 0;
            }

            public int GetListIndex(int chunkIndex)
            {
                var range = ranges[chunkIndex];
                return range.ListIndex;
            }
        }

        public struct ThreadWriter
        {
            [NativeDisableUnsafePtrRestriction] private readonly byte* perThreadListsPtr;
            [NativeDisableUnsafePtrRestriction] private UnsafeList<T>* list;

            [NativeSetThreadIndex] private int threadIndex;

            internal ThreadWriter(ref UnsafeParallelList<T> stream)
            {
                perThreadListsPtr = stream.perThreadLists;

                threadIndex = 0;
                list = null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Begin()
            {
                list = (UnsafeList<T>*)(perThreadListsPtr + threadIndex * PER_THREAD_LIST_SIZE);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Begin(int newThreadIndex)
            {
                threadIndex = newThreadIndex;
                list = (UnsafeList<T>*)(perThreadListsPtr + newThreadIndex * PER_THREAD_LIST_SIZE);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write(in T value)
            {
                list->Add(in value);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T* WriteAndReturn(in T value)
            {
                var idx = list->m_length;
                if (list->m_length + 1 > list->Capacity)
                    list->Resize(idx + 1);
                else
                    list->m_length += 1;
                
                list->Ptr[idx] = value;
                return list->Ptr + idx;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteMemCpy(ref T value)
            {
                var idx = list->m_length;

                if (list->m_length + 1 > list->Capacity)
                    list->Resize(idx + 1);
                else
                    list->m_length += 1;

                UnsafeUtility.MemCpy(list->Ptr + idx, UnsafeUtility.AddressOf(ref value), UnsafeUtility.SizeOf<T>());
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref UnsafeList<T> GetList()
            {
                return ref *list;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetThreadIndex()
            {
                return threadIndex;
            }
        }

        public struct ThreadReader
        {
            [NativeDisableUnsafePtrRestriction] private readonly byte* perThreadListsPtr;
            [NativeDisableUnsafePtrRestriction] private T* ptr;

            [NativeSetThreadIndex] private int threadIndex;

            private int currentIndex;

            internal ThreadReader(ref UnsafeParallelList<T> stream)
            {
                perThreadListsPtr = stream.perThreadLists;

                threadIndex = 0;
                currentIndex = 0;
                ptr = null;
            }

            public int Begin()
            {
                var list = (UnsafeList<T>*)(perThreadListsPtr + threadIndex * PER_THREAD_LIST_SIZE);
                ptr = list->Ptr;

                currentIndex = 0;

                return list->Length;
            }

            public int Begin(int newThreadIndex)
            {
                var list = (UnsafeList<T>*)(perThreadListsPtr + newThreadIndex * PER_THREAD_LIST_SIZE);
                ptr = list->Ptr;

                currentIndex = 0;
                threadIndex = newThreadIndex;

                return list->Length;
            }

            public ref T Read()
            {
                ref var returnValue = ref UnsafeUtility.AsRef<T>(ptr + currentIndex);
                currentIndex++;
                return ref returnValue;
            }

            public T* GetPtr()
            {
                return ptr;
            }
        }
    }
}