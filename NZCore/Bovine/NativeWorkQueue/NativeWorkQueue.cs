// <copyright file="NativeWorkQueue.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NZCore
{
    [NativeContainer]
    public unsafe struct NativeWorkQueue<T>
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private readonly T* queue;

        [NativeDisableUnsafePtrRestriction]
        private readonly int* queueWriteHead;

        [NativeDisableUnsafePtrRestriction]
        private readonly int* queueReadHead;

        [NativeDisableUnsafePtrRestriction]
        private readonly int* currentRef;

        private readonly AllocatorManager.AllocatorHandle allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeWorkQueue<T>>();
#endif

        public NativeWorkQueue(int maxQueueSize, AllocatorManager.AllocatorHandle allocator)
        {
            this.allocator = allocator.Handle;
            queue = (T*)allocator.Allocate(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), maxQueueSize);
            queueWriteHead = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), allocator.ToAllocator); // TODO allocator?
            queueReadHead = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), allocator.ToAllocator);
            currentRef = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), allocator.ToAllocator);

            Capacity = maxQueueSize;

            // UnsafeList<T>.Create(maxQueueSize, allocator);

            *queueWriteHead = 0;
            *queueReadHead = 0;
            *currentRef = 0;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.CheckAllocator(allocator.Handle);

            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.Handle);
            CollectionHelper.InitNativeContainer<T>(m_Safety);
            CollectionHelper.SetStaticSafetyId<NativeWorkQueue<T>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        public int Length
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                // Our queue system can push the length of the list above it's capacity so it's actual length is whatever is smaller
                return math.min(Capacity, *queueWriteHead);
            }
        }

        public int Capacity { get; }

        public bool HasCapacity => Length < Capacity;

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif

            AllocatorManager.Free(allocator, queue, Capacity);
            UnsafeUtility.Free(queueWriteHead, allocator.ToAllocator);
            UnsafeUtility.Free(queueReadHead, allocator.ToAllocator);
            UnsafeUtility.Free(currentRef, allocator.ToAllocator);
        }

        public void Update()
        {
            *queueWriteHead = 0; // math.min(*this.queueWriteHead, this.Capacity);
            *queueReadHead = 0; // math.min(*this.queueReadHead, *this.queueWriteHead);
        }

        public ref T ElementAt(int index)
        {
            return ref UnsafeUtility.AsRef<T>(queue + index);
        }

        /// <summary> Try add some work to the queue. </summary>
        /// <param name="ptr"> The work slot. </param>
        /// <returns> 0 if the queue is full, otherwise a unique ID for the work. </returns>
        public int TryAdd(out T* ptr)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

            if (Capacity < *queueWriteHead + 1)
            {
                // we've gone past end of list, don't write we'll requeue this next frame
                ptr = null;

                return 0;
            }

            *queueWriteHead += 1;

            int queueRef;
            do
            {
                queueRef = ++*currentRef;
            }
            while (Hint.Unlikely(queueRef == 0));

            ptr = queue + *queueWriteHead - 1;
            return queueRef;
        }

        public T* Add(out int queueRef)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

            CheckSufficientCapacity(Capacity, *queueWriteHead + 1);

            *queueWriteHead += 1;

            do
            {
                queueRef = ++*currentRef;
            }
            while (Hint.Unlikely(queueRef == 0));

            return queue + *queueWriteHead - 1;
        }

        public ParallelReader AsParallelReader()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ParallelReader(this, ref m_Safety);
#else
            return new ParallelReader(this);
#endif
        }

        public ParallelWriter AsParallelWriter()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ParallelWriter(this, ref m_Safety);
#else
            return new ParallelWriter(this);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        public static void CheckSufficientCapacity(int capacity, int length)
        {
            if (capacity < length)
            {
                throw new Exception($"Length {length} exceeds Capacity {capacity}");
            }
        }

        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public struct ParallelReader
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly T* queue;

            [NativeDisableUnsafePtrRestriction]
            private readonly int* queueReadHead;

            [NativeDisableUnsafePtrRestriction]
            private readonly int* queueWriteHead;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal readonly AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelWriter>();

            internal ParallelReader(NativeWorkQueue<T> workQueue, ref AtomicSafetyHandle safety)
            {
                queue = workQueue.queue;
                queueReadHead = workQueue.queueReadHead;
                queueWriteHead = workQueue.queueWriteHead;
                Capacity = workQueue.Capacity;
                m_Safety = safety;
                CollectionHelper.SetStaticSafetyId<ParallelWriter>(ref m_Safety, ref s_staticSafetyId.Data);
            }
#else
            internal ParallelReader(NativeWorkQueue<T> workQueue)
            {
                this.queue = workQueue.queue;
                this.queueReadHead = workQueue.queueReadHead;
                this.queueWriteHead = workQueue.queueWriteHead;
                this.Capacity = workQueue.Capacity;
            }
#endif
            public int Capacity { get; }

            public int Length
            {
                get
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                    // Our queue system can push the length of the list above it's capacity so it's actual length is whatever is smaller
                    return math.min(Capacity, *queueWriteHead);
                }
            }

            public bool TryGetNext(out T* value)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                var idx = Interlocked.Increment(ref *queueReadHead) - 1;

                if (idx >= Length)
                {
                    value = null;
                    return false;
                }

                value = queue + idx;
                return true;
            }
        }

        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public struct ParallelWriter
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly T* queue;

            [NativeDisableUnsafePtrRestriction]
            private readonly int* queueWriteHead;

            [NativeDisableUnsafePtrRestriction]
            private readonly int* currentRef;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal readonly AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelWriter>();

            internal ParallelWriter(NativeWorkQueue<T> workQueue, ref AtomicSafetyHandle safety)
            {
                queue = workQueue.queue;
                queueWriteHead = workQueue.queueWriteHead;
                currentRef = workQueue.currentRef;
                Capacity = workQueue.Capacity;
                m_Safety = safety;
                CollectionHelper.SetStaticSafetyId<ParallelWriter>(ref m_Safety, ref s_staticSafetyId.Data);
            }
#else
            internal ParallelWriter(NativeWorkQueue<T> workQueue)
            {
                this.queue = workQueue.queue;
                this.queueWriteHead = workQueue.queueWriteHead;
                this.currentRef = workQueue.currentRef;
                this.Capacity = workQueue.Capacity;
            }
#endif

            public int Capacity { get; }

            /// <summary> Try add some work to the queue. </summary>
            /// <param name="ptr"> The work slot. </param>
            /// <returns> 0 if the queue is full, otherwise a unique ID for the work. </returns>
            public int TryAdd(out T* ptr)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

                var idx = Interlocked.Increment(ref *queueWriteHead) - 1;

                if (idx >= Capacity)
                {
                    // we've gone past end of list, don't write we'll requeue this next frame
                    ptr = null;

                    return 0;
                }

                int queueRef;
                do
                {
                    queueRef = Interlocked.Increment(ref *currentRef);
                }
                while (Hint.Unlikely(queueRef == 0));

                ptr = queue + idx;
                return queueRef;
            }
        }
    }
}
