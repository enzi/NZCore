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
        public const int PerThreadListSize = JobsUtility.CacheLineSize;

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
            var unsafeParallelList = allocator.Allocate(default(UnsafeParallelList<T>), 1);

            unsafeParallelList->allocator = allocator.Handle;

            //Debug.Log($"parallelList alignOf: {UnsafeUtility.AlignOf<PerThreadList>()}");
            var maxThreadCount = JobsUtility.ThreadIndexCount;
            var perThreadListSize = PerThreadListSize * maxThreadCount;
            unsafeParallelList->perThreadLists = (byte*)UnsafeUtility.Malloc(perThreadListSize, 64, allocator.ToAllocator);

            for (var i = 0; i < maxThreadCount; i++)
            {
                unsafeParallelList->GetPerThreadList(i).List =
                    new UnsafeList<T>(initialCapacity, allocator.ToAllocator, NativeArrayOptions.UninitializedMemory);
            }

            var allocationSize = sizeof(UnsafeParallelListHeader);
            var buffer = (byte*)Memory.Unmanaged.Allocate(allocationSize, UnsafeUtility.AlignOf<UnsafeParallelListHeader>(),
                allocator.ToAllocator); // could be just temp memory
            UnsafeUtility.MemClear(buffer, allocationSize);

            unsafeParallelList->header = (UnsafeParallelListHeader*)buffer;

            unsafeParallelList->header->ChunkCount = 0;
            unsafeParallelList->ranges = null;

            unsafeParallelList->IsCreated = true;

            return unsafeParallelList;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref PerThreadList GetPerThreadList(int index) => ref UnsafeUtility.AsRef<PerThreadList>(perThreadLists + index * PerThreadListSize);

        public byte* GetPerThreadListPtr() => perThreadLists;

        public void SetChunkCount(int chunkCount)
        {
            var sameChunkCount = chunkCount == header->ChunkCount;
            var allocatedRanges = ranges != null;
            var allocationSize = sizeof(UnsafeParallelListRange) * chunkCount;

            if (sameChunkCount && allocatedRanges)
            {
                UnsafeUtility.MemClear(ranges, allocationSize);
            }
            else
            {
                if (allocatedRanges)
                {
                    DeallocateRanges();
                }

                header->ChunkCount = chunkCount;

                ranges = (UnsafeParallelListRange*)Memory.Unmanaged.Allocate(allocationSize, UnsafeUtility.AlignOf<UnsafeParallelListRange>(), allocator);

                UnsafeUtility.MemClear(ranges, allocationSize);
            }
        }

        public int GetChunkCount() => header->ChunkCount;

        private void DeallocateRanges()
        {
            Memory.Unmanaged.Free(ranges, allocator);
        }

        public void Clear()
        {
            if (!IsCreated)
            {
                return;
            }

            for (var i = 0; i < JobsUtility.ThreadIndexCount; i++)
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
            var result = 0;
            for (var i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                var list = *(UnsafeList<T>*)(perThreadLists + i * PerThreadListSize);
                result += list.m_length;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref UnsafeList<T> GetUnsafeList(int threadId) => ref GetPerThreadList(threadId).List;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeList<T>* GetUnsafeListPtr(int listIndex) => (UnsafeList<T>*)(perThreadLists + listIndex * PerThreadListSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetUnsafePtr(int threadId) => (byte*)GetPerThreadList(threadId).List.Ptr;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BlockCount(int threadId) => GetPerThreadList(threadId).List.m_length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BlockCountToIndex(int threadId)
        {
            var count = 0;

            for (var i = 0; i < threadId && i < JobsUtility.ThreadIndexCount; i++)
            {
                count += GetPerThreadList(i).List.m_length;
            }

            return count;
        }

        public int GetStartIndexArray(ref NativeArray<int> lengths)
        {
            var count = 0;
            for (var i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                lengths[i] = count;
                count += GetPerThreadList(i).List.m_length;
            }

            return count;
        }

        public NativeArray<int> GetStartIndexArray(ref SystemState state)
        {
            var lengths = new NativeArray<int>();
            lengths.Initialize(JobsUtility.ThreadIndexCount, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);

            var count = 0;
            for (var i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                lengths[i] = count;
                count += GetPerThreadList(i).List.m_length;
            }

            return lengths;
        }

        public ChunkReader AsChunkReader() => new(ref this);

        public ChunkWriter AsChunkWriter() => new(ref this);

        public ThreadReader AsThreadReader() => new(ref this);

        public ThreadWriter AsThreadWriter() => new(ref this);

        public static void Destroy(UnsafeParallelList<T>* unsafeParallelList)
        {
            var allocator = unsafeParallelList->allocator;
            unsafeParallelList->Dispose();
            AllocatorManager.Free(allocator, unsafeParallelList);
        }

        public void Dispose()
        {
            if (!IsCreated)
            {
                return;
            }

            for (var i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                GetPerThreadList(i).List.Dispose();
            }

            UnsafeUtility.Free(perThreadLists, allocator.ToAllocator);

            DeallocateRanges();
            Memory.Unmanaged.Free(header, allocator);
            header = null;

            allocator = Allocator.None;
        }

        public bool CheckRangesForNull() => ranges == null;

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
            var jobHandle = new DisposeJob
            {
                List = this
            }.Schedule(inputDeps);
            return jobHandle;
        }

        public struct PerThreadList // 64 bytes
        {
            public UnsafeList<T> List; // 24 bytes

            public ulong Padding1; // 8 bytes
            public ulong Padding2;
            public ulong Padding3;
            public ulong Padding4;
            public ulong Padding5;
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
            [NativeDisableUnsafePtrRestriction] private readonly byte* _perThreadListsPtr;
            [NativeDisableUnsafePtrRestriction] private UnsafeList<T>* _list;
            [NativeDisableUnsafePtrRestriction] private readonly UnsafeParallelListRange* _ranges;

            [NativeSetThreadIndex] private int _threadIndex;

            private int _chunkIndex;
            private int _startIndex;

            internal ChunkWriter(ref UnsafeParallelList<T> stream)
            {
                _perThreadListsPtr = stream.perThreadLists;
                _ranges = stream.ranges;

                _chunkIndex = int.MinValue;
                _threadIndex = 0;
                _startIndex = 0;
                _list = default;
            }

            public void BeginForEachChunk(int newChunkIndex)
            {
                _chunkIndex = newChunkIndex;

                _list = (UnsafeList<T>*)(_perThreadListsPtr + _threadIndex * PerThreadListSize);
                _startIndex = _list->m_length;
            }

            public void Write(in T value)
            {
                _list->Add(in value);
            }

            public void WriteMemCpy(ref T value)
            {
                var idx = _list->m_length;

                if (_list->m_length + 1 > _list->Capacity)
                {
                    _list->Resize(idx + 1);
                }
                else
                {
                    _list->m_length += 1;
                }

                UnsafeUtility.MemCpy(_list->Ptr + idx, UnsafeUtility.AddressOf(ref value), UnsafeUtility.SizeOf<T>());
            }

            public void EndForEachChunk()
            {
                _ranges[_chunkIndex] = new UnsafeParallelListRange
                {
                    ElementCount = _list->m_length - _startIndex,
                    StartIndex = _startIndex,
                    ListIndex = _threadIndex
                };
            }

            public void SetManualThreadIndex(int newThreadIndex)
            {
                _threadIndex = newThreadIndex;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetThreadIndex() => _threadIndex;
        }

        public struct ChunkReader
        {
            [NativeDisableUnsafePtrRestriction] private readonly byte* _perThreadListsPtr;
            [NativeDisableUnsafePtrRestriction] private readonly UnsafeParallelListRange* _ranges;
            [NativeDisableUnsafePtrRestriction] private byte* _ptr;

            private readonly int _size;
            private int _currentIndex;

            internal ChunkReader(ref UnsafeParallelList<T> stream)
            {
                _perThreadListsPtr = stream.perThreadLists;
                _ranges = stream.ranges;

                _size = UnsafeUtility.SizeOf<T>();
                _currentIndex = 0;
                _ptr = null;
            }

            public int BeginForEachChunk(int chunkIndex)
            {
                if (_ranges == null)
                {
                    return 0;
                }

                var range = _ranges[chunkIndex];
                var remainingItemCount = range.ElementCount;

                if (remainingItemCount > 0)
                {
                    _ptr = (byte*)((UnsafeList<T>*)(_perThreadListsPtr + chunkIndex * PerThreadListSize))->Ptr;
                    _currentIndex = _ranges[chunkIndex].StartIndex;
                }
                else
                {
                    _ptr = null;
                    _currentIndex = 0;
                }

                return remainingItemCount;
            }

            public ref T Read()
            {
                ref var returnValue = ref UnsafeUtility.AsRef<T>(_ptr + _currentIndex * _size);
                _currentIndex++;
                return ref returnValue;
            }

            public T* GetPtr() => (T*)_ptr;

            public void Reset(int chunkIndex)
            {
                var range = _ranges[chunkIndex];
                _currentIndex = range.ElementCount > 0 ? _ranges[chunkIndex].StartIndex : 0;
            }

            public int GetListIndex(int chunkIndex)
            {
                var range = _ranges[chunkIndex];
                return range.ListIndex;
            }
        }

        public struct ThreadWriter
        {
            [NativeDisableUnsafePtrRestriction] private readonly byte* _perThreadListsPtr;
            [NativeDisableUnsafePtrRestriction] private UnsafeList<T>* _list;

            [NativeSetThreadIndex] private int _threadIndex;

            internal ThreadWriter(ref UnsafeParallelList<T> stream)
            {
                _perThreadListsPtr = stream.perThreadLists;

                _threadIndex = 0;
                _list = null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Begin()
            {
                _list = (UnsafeList<T>*)(_perThreadListsPtr + _threadIndex * PerThreadListSize);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Begin(int newThreadIndex)
            {
                _threadIndex = newThreadIndex;
                _list = (UnsafeList<T>*)(_perThreadListsPtr + newThreadIndex * PerThreadListSize);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Write(in T value)
            {
                _list->Add(in value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T* WriteAndReturn(in T value)
            {
                var idx = _list->m_length;
                if (_list->m_length + 1 > _list->Capacity)
                {
                    _list->Resize(idx + 1);
                }
                else
                {
                    _list->m_length += 1;
                }

                _list->Ptr[idx] = value;
                return _list->Ptr + idx;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteMemCpy(ref T value)
            {
                var idx = _list->m_length;

                if (_list->m_length + 1 > _list->Capacity)
                {
                    _list->Resize(idx + 1);
                }
                else
                {
                    _list->m_length += 1;
                }

                UnsafeUtility.MemCpy(_list->Ptr + idx, UnsafeUtility.AddressOf(ref value), UnsafeUtility.SizeOf<T>());
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref UnsafeList<T> GetList() => ref *_list;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetThreadIndex() => _threadIndex;
        }

        public struct ThreadReader
        {
            [NativeDisableUnsafePtrRestriction] private readonly byte* _perThreadListsPtr;
            [NativeDisableUnsafePtrRestriction] private T* _ptr;

            [NativeSetThreadIndex] private int _threadIndex;

            private int _currentIndex;

            internal ThreadReader(ref UnsafeParallelList<T> stream)
            {
                _perThreadListsPtr = stream.perThreadLists;

                _threadIndex = 0;
                _currentIndex = 0;
                _ptr = null;
            }

            public int Begin()
            {
                var list = (UnsafeList<T>*)(_perThreadListsPtr + _threadIndex * PerThreadListSize);
                _ptr = list->Ptr;

                _currentIndex = 0;

                return list->Length;
            }

            public int Begin(int newThreadIndex)
            {
                var list = (UnsafeList<T>*)(_perThreadListsPtr + newThreadIndex * PerThreadListSize);
                _ptr = list->Ptr;

                _currentIndex = 0;
                _threadIndex = newThreadIndex;

                return list->Length;
            }

            public ref T Read()
            {
                ref var returnValue = ref UnsafeUtility.AsRef<T>(_ptr + _currentIndex);
                _currentIndex++;
                return ref returnValue;
            }

            public T* GetPtr() => _ptr;
        }
    }
}