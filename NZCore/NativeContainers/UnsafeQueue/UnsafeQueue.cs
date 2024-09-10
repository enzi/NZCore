// <copyright project="NZCore" file="UnsafeQueue.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace NZCore
{
    unsafe struct UnsafeQueueBlockHeader
    {
        public UnsafeQueueBlockHeader* m_NextBlock;
        public int m_NumItems;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct UnsafeQueueBlockPoolData
    {
        internal IntPtr m_FirstBlock;
        internal int m_NumBlocks;
        internal int m_MaxBlocks;
        internal const int m_BlockSize = 16 * 1024;
        internal int m_AllocLock;

        public UnsafeQueueBlockHeader* AllocateBlock()
        {
            // There can only ever be a single thread allocating an entry from the free list since it needs to
            // access the content of the block (the next pointer) before doing the CAS.
            // If there was no lock thread A could read the next pointer, thread B could quickly allocate
            // the same block then free it with another next pointer before thread A performs the CAS which
            // leads to an invalid free list potentially causing memory corruption.
            // Having multiple threads freeing data concurrently to each other while another thread is allocating
            // is no problems since there is only ever a single thread modifying global data in that case.
            while (Interlocked.CompareExchange(ref m_AllocLock, 1, 0) != 0)
            {
            }

            UnsafeQueueBlockHeader* checkBlock = (UnsafeQueueBlockHeader*)m_FirstBlock;
            UnsafeQueueBlockHeader* block;

            do
            {
                block = checkBlock;
                if (block == null)
                {
                    Interlocked.Exchange(ref m_AllocLock, 0);
                    Interlocked.Increment(ref m_NumBlocks);
                    block = (UnsafeQueueBlockHeader*)Memory.Unmanaged.Allocate(m_BlockSize, 16, Allocator.Persistent);
                    return block;
                }

                checkBlock = (UnsafeQueueBlockHeader*)Interlocked.CompareExchange(ref m_FirstBlock, (IntPtr)block->m_NextBlock, (IntPtr)block);
            } while (checkBlock != block);

            Interlocked.Exchange(ref m_AllocLock, 0);

            return block;
        }

        public void FreeBlock(UnsafeQueueBlockHeader* block)
        {
            if (m_NumBlocks > m_MaxBlocks)
            {
                if (Interlocked.Decrement(ref m_NumBlocks) + 1 > m_MaxBlocks)
                {
                    Memory.Unmanaged.Free(block, Allocator.Persistent);
                    return;
                }

                Interlocked.Increment(ref m_NumBlocks);
            }

            UnsafeQueueBlockHeader* checkBlock = (UnsafeQueueBlockHeader*)m_FirstBlock;
            UnsafeQueueBlockHeader* nextPtr;

            do
            {
                nextPtr = checkBlock;
                block->m_NextBlock = checkBlock;
                checkBlock = (UnsafeQueueBlockHeader*)Interlocked.CompareExchange(ref m_FirstBlock, (IntPtr)block, (IntPtr)checkBlock);
            } while (checkBlock != nextPtr);
        }
    }

    internal unsafe class UnsafeQueueBlockPool
    {
        private static readonly SharedStatic<IntPtr> Data = SharedStatic<IntPtr>.GetOrCreate<UnsafeQueueBlockPool>();

        internal static UnsafeQueueBlockPoolData* GetQueueBlockPool()
        {
            var pData = (UnsafeQueueBlockPoolData**)Data.UnsafeDataPointer;
            var data = *pData;

            if (data == null)
            {
                data = (UnsafeQueueBlockPoolData*)Memory.Unmanaged.Allocate(UnsafeUtility.SizeOf<UnsafeQueueBlockPoolData>(), 8, Allocator.Persistent);
                *pData = data;
                data->m_NumBlocks = data->m_MaxBlocks = 256;
                data->m_AllocLock = 0;
                // Allocate MaxBlocks items
                UnsafeQueueBlockHeader* prev = null;

                for (int i = 0; i < data->m_MaxBlocks; ++i)
                {
                    UnsafeQueueBlockHeader* block = (UnsafeQueueBlockHeader*)Memory.Unmanaged.Allocate(UnsafeQueueBlockPoolData.m_BlockSize, 16, Allocator.Persistent);
                    block->m_NextBlock = prev;
                    prev = block;
                }

                data->m_FirstBlock = (IntPtr)prev;

                AppDomainOnDomainUnload();
            }

            return data;
        }

        [BurstDiscard]
        static void AppDomainOnDomainUnload()
        {
#if !UNITY_DOTSRUNTIME
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
#endif
        }

#if !UNITY_DOTSRUNTIME
        private static void OnDomainUnload(object sender, EventArgs e)
        {
            var pData = (UnsafeQueueBlockPoolData**)Data.UnsafeDataPointer;
            var data = *pData;

            while (data->m_FirstBlock != IntPtr.Zero)
            {
                UnsafeQueueBlockHeader* block = (UnsafeQueueBlockHeader*)data->m_FirstBlock;
                data->m_FirstBlock = (IntPtr)block->m_NextBlock;
                Memory.Unmanaged.Free(block, Allocator.Persistent);
                --data->m_NumBlocks;
            }

            Memory.Unmanaged.Free(data, Allocator.Persistent);
            *pData = null;
        }
#endif
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct UnsafeQueueData
    {
        public IntPtr m_FirstBlock;
        public IntPtr m_LastBlock;
        public int m_CurrentRead;

        private int m_MaxItems;
        private byte* m_CurrentWriteBlockTLS;

        internal UnsafeQueueBlockHeader* GetCurrentWriteBlockTLS(int threadIndex)
        {
            var data = (UnsafeQueueBlockHeader**)&m_CurrentWriteBlockTLS[threadIndex * JobsUtility.CacheLineSize];
            return *data;
        }

        internal void SetCurrentWriteBlockTLS(int threadIndex, UnsafeQueueBlockHeader* currentWriteBlock)
        {
            var data = (UnsafeQueueBlockHeader**)&m_CurrentWriteBlockTLS[threadIndex * JobsUtility.CacheLineSize];
            *data = currentWriteBlock;
        }

        public static UnsafeQueueBlockHeader* AllocateWriteBlockMT<T>(UnsafeQueueData* data, UnsafeQueueBlockPoolData* pool, int threadIndex) where T : struct
        {
            UnsafeQueueBlockHeader* currentWriteBlock = data->GetCurrentWriteBlockTLS(threadIndex);

            if (currentWriteBlock != null
                && currentWriteBlock->m_NumItems == data->m_MaxItems)
            {
                currentWriteBlock = null;
            }

            if (currentWriteBlock == null)
            {
                currentWriteBlock = pool->AllocateBlock();
                currentWriteBlock->m_NextBlock = null;
                currentWriteBlock->m_NumItems = 0;
                UnsafeQueueBlockHeader* prevLast = (UnsafeQueueBlockHeader*)Interlocked.Exchange(ref data->m_LastBlock, (IntPtr)currentWriteBlock);

                if (prevLast == null)
                {
                    data->m_FirstBlock = (IntPtr)currentWriteBlock;
                }
                else
                {
                    prevLast->m_NextBlock = currentWriteBlock;
                }

                data->SetCurrentWriteBlockTLS(threadIndex, currentWriteBlock);
            }

            return currentWriteBlock;
        }

        public static void AllocateQueue<T>(AllocatorManager.AllocatorHandle label, out UnsafeQueueData* outBuf) where T : struct
        {
            var queueDataSize = CollectionHelper.Align(UnsafeUtility.SizeOf<UnsafeQueueData>(), JobsUtility.CacheLineSize);

            var data = (UnsafeQueueData*)Memory.Unmanaged.Allocate(
                queueDataSize
                + JobsUtility.CacheLineSize * JobsUtility.ThreadIndexCount
                , JobsUtility.CacheLineSize
                , label
            );

            data->m_CurrentWriteBlockTLS = (((byte*)data) + queueDataSize);

            data->m_FirstBlock = IntPtr.Zero;
            data->m_LastBlock = IntPtr.Zero;
            data->m_MaxItems = (UnsafeQueueBlockPoolData.m_BlockSize - UnsafeUtility.SizeOf<UnsafeQueueBlockHeader>()) / UnsafeUtility.SizeOf<T>();

            data->m_CurrentRead = 0;
            for (int threadIndex = 0; threadIndex < JobsUtility.ThreadIndexCount; ++threadIndex)
            {
                data->SetCurrentWriteBlockTLS(threadIndex, null);
            }

            outBuf = data;
        }

        public static void DeallocateQueue(UnsafeQueueData* data, UnsafeQueueBlockPoolData* pool, AllocatorManager.AllocatorHandle allocation)
        {
            UnsafeQueueBlockHeader* firstBlock = (UnsafeQueueBlockHeader*)data->m_FirstBlock;

            while (firstBlock != null)
            {
                UnsafeQueueBlockHeader* next = firstBlock->m_NextBlock;
                pool->FreeBlock(firstBlock);
                firstBlock = next;
            }

            Memory.Unmanaged.Free(data, allocation);
        }
    }

    /// <summary>
    /// An unmanaged queue.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UnsafeQueue<T>
        : INativeDisposable
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private UnsafeQueueData* m_Buffer;
        [NativeDisableUnsafePtrRestriction] private readonly UnsafeQueueBlockPoolData* m_QueuePool;
        private readonly AllocatorManager.AllocatorHandle m_AllocatorLabel;

        /// <summary>
        /// Initializes and returns an instance of UnsafeQueue.
        /// </summary>
        /// <param name="allocator">The allocator to use.</param>
        public UnsafeQueue(AllocatorManager.AllocatorHandle allocator)
        {
            CollectionHelper.CheckIsUnmanaged<T>();

            m_QueuePool = UnsafeQueueBlockPool.GetQueueBlockPool();
            m_AllocatorLabel = allocator;

            UnsafeQueueData.AllocateQueue<T>(allocator, out m_Buffer);
        }

        /// <summary>
        /// Returns true if this queue is empty.
        /// </summary>
        /// <value>True if this queue has no items or if the queue has not been constructed.</value>
        public bool IsEmpty()
        {
            if (!IsCreated)
            {
                return true;
            }

            int count = 0;
            var currentRead = m_Buffer->m_CurrentRead;

            for (UnsafeQueueBlockHeader* block = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock;
                 block != null;
                 block = block->m_NextBlock
                )
            {
                count += block->m_NumItems;

                if (count > currentRead)
                {
                    return false;
                }
            }

            return count == currentRead;
        }

        /// <summary>
        /// Returns the current number of elements in this queue.
        /// </summary>
        /// <remarks>Note that getting the count requires traversing the queue's internal linked list of blocks.
        /// Where possible, cache this value instead of reading the property repeatedly.</remarks>
        /// <returns>The current number of elements in this queue.</returns>
        public int Count
        {
            get
            {
                int count = 0;

                for (UnsafeQueueBlockHeader* block = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock;
                     block != null;
                     block = block->m_NextBlock
                    )
                {
                    count += block->m_NumItems;
                }

                return count - m_Buffer->m_CurrentRead;
            }
        }

        internal static int PersistentMemoryBlockCount
        {
            get { return UnsafeQueueBlockPool.GetQueueBlockPool()->m_MaxBlocks; }
            set { Interlocked.Exchange(ref UnsafeQueueBlockPool.GetQueueBlockPool()->m_MaxBlocks, value); }
        }

        internal static int MemoryBlockSize
        {
            get { return UnsafeQueueBlockPoolData.m_BlockSize; }
        }

        /// <summary>
        /// Returns the element at the end of this queue without removing it.
        /// </summary>
        /// <returns>The element at the end of this queue.</returns>
        public T Peek()
        {
            CheckReadNotEmpty();

            UnsafeQueueBlockHeader* firstBlock = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock;
            return UnsafeUtility.ReadArrayElement<T>(firstBlock + 1, m_Buffer->m_CurrentRead);
        }

        /// <summary>
        /// Adds an element at the front of this queue.
        /// </summary>
        /// <param name="value">The value to be enqueued.</param>
        public void Enqueue(T value)
        {
            UnsafeQueueBlockHeader* writeBlock = UnsafeQueueData.AllocateWriteBlockMT<T>(m_Buffer, m_QueuePool, 0);
            UnsafeUtility.WriteArrayElement(writeBlock + 1, writeBlock->m_NumItems, value);
            ++writeBlock->m_NumItems;
        }

        /// <summary>
        /// Removes and returns the element at the end of this queue.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if this queue is empty.</exception>
        /// <returns>The element at the end of this queue.</returns>
        public T Dequeue()
        {
            if (!TryDequeue(out T item))
            {
                ThrowEmpty();
            }

            return item;
        }

        /// <summary>
        /// Removes and outputs the element at the end of this queue.
        /// </summary>
        /// <param name="item">Outputs the removed element.</param>
        /// <returns>True if this queue was not empty.</returns>
        public bool TryDequeue(out T item)
        {
            UnsafeQueueBlockHeader* firstBlock = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock;

            if (firstBlock == null)
            {
                item = default(T);
                return false;
            }

            var currentRead = m_Buffer->m_CurrentRead++;
            item = UnsafeUtility.ReadArrayElement<T>(firstBlock + 1, currentRead);

            if (m_Buffer->m_CurrentRead >= firstBlock->m_NumItems)
            {
                m_Buffer->m_CurrentRead = 0;
                m_Buffer->m_FirstBlock = (IntPtr)firstBlock->m_NextBlock;

                if (m_Buffer->m_FirstBlock == IntPtr.Zero)
                {
                    m_Buffer->m_LastBlock = IntPtr.Zero;
                }

                for (int threadIndex = 0; threadIndex < JobsUtility.ThreadIndexCount; ++threadIndex)
                {
                    if (m_Buffer->GetCurrentWriteBlockTLS(threadIndex) == firstBlock)
                    {
                        m_Buffer->SetCurrentWriteBlockTLS(threadIndex, null);
                    }
                }

                m_QueuePool->FreeBlock(firstBlock);
            }

            return true;
        }

        /// <summary>
        /// Returns an array containing a copy of this queue's content.
        /// </summary>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>An array containing a copy of this queue's content. The elements are ordered in the same order they were
        /// enqueued, *e.g.* the earliest enqueued element is copied to index 0 of the array.</returns>
        public NativeArray<T> ToArray(AllocatorManager.AllocatorHandle allocator)
        {
            UnsafeQueueBlockHeader* firstBlock = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock;
            var outputArray = CollectionHelper.CreateNativeArray<T>(Count, allocator);

            UnsafeQueueBlockHeader* currentBlock = firstBlock;
            var arrayPtr = (byte*)outputArray.GetUnsafePtr();
            int size = UnsafeUtility.SizeOf<T>();
            int dstOffset = 0;
            int srcOffset = m_Buffer->m_CurrentRead * size;
            int srcOffsetElements = m_Buffer->m_CurrentRead;
            while (currentBlock != null)
            {
                int bytesToCopy = (currentBlock->m_NumItems - srcOffsetElements) * size;
                UnsafeUtility.MemCpy(arrayPtr + dstOffset, (byte*)(currentBlock + 1) + srcOffset, bytesToCopy);
                srcOffset = srcOffsetElements = 0;
                dstOffset += bytesToCopy;
                currentBlock = currentBlock->m_NextBlock;
            }

            return outputArray;
        }

        /// <summary>
        /// Removes all elements of this queue.
        /// </summary>
        /// <remarks>Does not change the capacity.</remarks>
        public void Clear()
        {
            UnsafeQueueBlockHeader* firstBlock = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock;

            while (firstBlock != null)
            {
                UnsafeQueueBlockHeader* next = firstBlock->m_NextBlock;
                m_QueuePool->FreeBlock(firstBlock);
                firstBlock = next;
            }

            m_Buffer->m_FirstBlock = IntPtr.Zero;
            m_Buffer->m_LastBlock = IntPtr.Zero;
            m_Buffer->m_CurrentRead = 0;

            for (int threadIndex = 0; threadIndex < JobsUtility.ThreadIndexCount; ++threadIndex)
            {
                m_Buffer->SetCurrentWriteBlockTLS(threadIndex, null);
            }
        }

        /// <summary>
        /// Whether this queue has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this queue has been allocated (and not yet deallocated).</value>
        public bool IsCreated => m_Buffer != null;

        /// <summary>
        /// Releases all resources (memory and safety handles).
        /// </summary>
        public void Dispose()
        {
            UnsafeQueueData.DeallocateQueue(m_Buffer, m_QueuePool, m_AllocatorLabel);
            m_Buffer = null;
        }

        /// <summary>
        /// Creates and schedules a job that releases all resources (memory and safety handles) of this queue.
        /// </summary>
        /// <param name="inputDeps">The dependency for the new job.</param>
        /// <returns>The handle of the new job. The job depends upon `inputDeps` and releases all resources (memory and safety handles) of this queue.</returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = new UnsafeQueueDisposeJob
            {
                Data = new UnsafeQueueDispose
                {
                    m_Buffer = m_Buffer,
                    m_QueuePool = m_QueuePool,
                    m_AllocatorLabel = m_AllocatorLabel
                }
            }.Schedule(inputDeps);

            m_Buffer = null;

            return jobHandle;
        }

        /// <summary>
        /// Returns a parallel writer for this queue.
        /// </summary>
        /// <returns>A parallel writer for this queue.</returns>
        public ParallelWriter AsParallelWriter()
        {
            ParallelWriter writer;

            writer.m_Buffer = m_Buffer;
            writer.m_QueuePool = m_QueuePool;
            writer.m_ThreadIndex = 0;

            return writer;
        }

        /// <summary>
        /// A parallel writer for a UnsafeQueue.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AsParallelWriter"/> to create a parallel writer for a UnsafeQueue.
        /// </remarks>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public struct ParallelWriter
        {
            [NativeDisableUnsafePtrRestriction] internal UnsafeQueueData* m_Buffer;

            [NativeDisableUnsafePtrRestriction] internal UnsafeQueueBlockPoolData* m_QueuePool;

            [NativeSetThreadIndex] internal int m_ThreadIndex;

            /// <summary>
            /// Adds an element at the front of the queue.
            /// </summary>
            /// <param name="value">The value to be enqueued.</param>
            public void Enqueue(T value)
            {
                UnsafeQueueBlockHeader* writeBlock = UnsafeQueueData.AllocateWriteBlockMT<T>(m_Buffer, m_QueuePool, m_ThreadIndex);
                UnsafeUtility.WriteArrayElement(writeBlock + 1, writeBlock->m_NumItems, value);
                ++writeBlock->m_NumItems;
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        void CheckReadNotEmpty()
        {
            if (m_Buffer->m_FirstBlock == (IntPtr)0)
            {
                ThrowEmpty();
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        static void ThrowEmpty()
        {
            throw new InvalidOperationException("Trying to read from an empty queue.");
        }
    }

    [NativeContainer]
    internal unsafe struct UnsafeQueueDispose
    {
        [NativeDisableUnsafePtrRestriction] internal UnsafeQueueData* m_Buffer;

        [NativeDisableUnsafePtrRestriction] internal UnsafeQueueBlockPoolData* m_QueuePool;

        internal AllocatorManager.AllocatorHandle m_AllocatorLabel;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif

        public void Dispose()
        {
            UnsafeQueueData.DeallocateQueue(m_Buffer, m_QueuePool, m_AllocatorLabel);
        }
    }

    [BurstCompile]
    struct UnsafeQueueDisposeJob : IJob
    {
        public UnsafeQueueDispose Data;

        public void Execute()
        {
            Data.Dispose();
        }
    }
}