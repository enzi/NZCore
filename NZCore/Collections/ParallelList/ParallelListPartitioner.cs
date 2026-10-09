// <copyright project="NZCore" file="ParallelListPartitioner.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NZCore
{
    public interface IPartitionKey<T>
        where T : unmanaged
    {
        int GetKey(in T item);
    }

    /// <summary>
    /// Groups the items of ParallelLists by key with a parallel counting sort: every thread list counts its items per partition,
    /// a prefix sum turns the counts into write offsets and every thread list copies its items to them.
    /// The items of a partition end up contiguous in the output list, so a parallel job that handles one partition per index owns every key in it.
    /// Keys are dealt to the partitions in blocks of consecutive keys, entity indices that sit next to each other in chunks share a partition.
    /// A secondary key groups the output indices a second time, e.g. combat events by target and their indices by source.
    /// </summary>
    public struct ParallelListPartitioner : IDisposable
    {
        private const int KeyBlockBits = 7;
        private const int MaxInputs = 2;

        // partition p of the output list is [Starts[p], Starts[p + 1])
        public NativeArray<int> Starts;

        // output indices grouped by the secondary key, partition p is [SecondaryStarts[p], SecondaryStarts[p + 1])
        public NativeList<int> SecondaryIndices;
        public NativeArray<int> SecondaryStarts;

        // one row per input thread list, the prefix sum turns the counts into write offsets
        private NativeArray<int> _counts;
        private NativeArray<int> _secondaryCounts;

        public int PartitionCount => Starts.Length - 1;

        public ParallelListPartitioner(int partitionCount, AllocatorManager.AllocatorHandle allocator)
        {
            CheckPartitionCount(partitionCount);

            var maxListCount = MaxInputs * JobsUtility.ThreadIndexCount;

            Starts = CollectionHelper.CreateNativeArray<int>(partitionCount + 1, allocator);
            SecondaryIndices = new NativeList<int>(0, allocator);
            SecondaryStarts = CollectionHelper.CreateNativeArray<int>(partitionCount + 1, allocator);
            _counts = CollectionHelper.CreateNativeArray<int>(maxListCount * partitionCount, allocator);
            _secondaryCounts = CollectionHelper.CreateNativeArray<int>(maxListCount * partitionCount, allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetPartition(int key, int partitionCount) => (int)(((uint)key >> KeyBlockBits) & (uint)(partitionCount - 1));

        /// <summary>
        /// Writes the items of the input into the output list, grouped by key.
        /// </summary>
        public JobHandle Schedule<T, TKey>(ref NativeList<T> output, ParallelList<T> input, JobHandle dependency)
            where T : unmanaged
            where TKey : struct, IPartitionKey<T> =>
            Schedule<T, TKey, TKey>(ref output, input, input, 1, false, dependency);

        /// <summary>
        /// Writes the items of both inputs into the output list, grouped by key.
        /// </summary>
        public JobHandle Schedule<T, TKey>(ref NativeList<T> output, ParallelList<T> input, ParallelList<T> secondInput, JobHandle dependency)
            where T : unmanaged
            where TKey : struct, IPartitionKey<T> =>
            Schedule<T, TKey, TKey>(ref output, input, secondInput, 2, false, dependency);

        /// <summary>
        /// Writes the items of the input into the output list grouped by key, and their output indices into SecondaryIndices grouped by the secondary key.
        /// </summary>
        public JobHandle Schedule<T, TKey, TSecondaryKey>(ref NativeList<T> output, ParallelList<T> input, JobHandle dependency)
            where T : unmanaged
            where TKey : struct, IPartitionKey<T>
            where TSecondaryKey : struct, IPartitionKey<T> =>
            Schedule<T, TKey, TSecondaryKey>(ref output, input, input, 1, true, dependency);

        // a single input is passed twice so every container in the jobs is valid, the second one isn't read
        private JobHandle Schedule<T, TKey, TSecondaryKey>(ref NativeList<T> output, ParallelList<T> input, ParallelList<T> secondInput, int inputCount,
            bool secondary, JobHandle dependency)
            where T : unmanaged
            where TKey : struct, IPartitionKey<T>
            where TSecondaryKey : struct, IPartitionKey<T>
        {
            var listCount = inputCount * JobsUtility.ThreadIndexCount;
            var partitionCount = PartitionCount;

            var countHandle = new CountJob<T, TKey, TSecondaryKey>
            {
                Input = input,
                SecondInput = secondInput,
                Counts = _counts,
                SecondaryCounts = _secondaryCounts,
                PartitionCount = partitionCount,
                Secondary = secondary
            }.ScheduleParallel(listCount, 1, dependency);

            var prefixSumHandle = new PrefixSumJob<T>
            {
                Output = output,
                Starts = Starts,
                Counts = _counts,
                SecondaryIndices = SecondaryIndices,
                SecondaryStarts = SecondaryStarts,
                SecondaryCounts = _secondaryCounts,
                ListCount = listCount,
                Secondary = secondary
            }.Schedule(countHandle);

            return new ScatterJob<T, TKey, TSecondaryKey>
            {
                Input = input,
                SecondInput = secondInput,
                Output = output.AsDeferredJobArray(),
                Counts = _counts,
                SecondaryIndices = SecondaryIndices.AsDeferredJobArray(),
                SecondaryCounts = _secondaryCounts,
                PartitionCount = partitionCount,
                Secondary = secondary
            }.ScheduleParallel(listCount, 1, prefixSumHandle);
        }

        public void Dispose()
        {
            Starts.Dispose();
            SecondaryIndices.Dispose();
            SecondaryStarts.Dispose();
            _counts.Dispose();
            _secondaryCounts.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref UnsafeList<T> GetInputList<T>(ref ParallelList<T> input, ref ParallelList<T> secondInput, int listIndex)
            where T : unmanaged
        {
            var threadCount = JobsUtility.ThreadIndexCount;
            return ref listIndex < threadCount ? ref input.GetUnsafeList(listIndex) : ref secondInput.GetUnsafeList(listIndex - threadCount);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckPartitionCount(int partitionCount)
        {
            if (partitionCount < 1 || !math.ispow2(partitionCount))
            {
                throw new ArgumentException($"Partition count {partitionCount} must be a power of two");
            }
        }

        // every thread list counts into its own row, no atomics, so a key that gets every item doesn't make the threads contend
        [BurstCompile]
        public unsafe struct CountJob<T, TKey, TSecondaryKey> : IJobFor
            where T : unmanaged
            where TKey : struct, IPartitionKey<T>
            where TSecondaryKey : struct, IPartitionKey<T>
        {
            [ReadOnly] public ParallelList<T> Input;
            [ReadOnly] public ParallelList<T> SecondInput;
            [NativeDisableParallelForRestriction] public NativeArray<int> Counts;
            [NativeDisableParallelForRestriction] public NativeArray<int> SecondaryCounts;
            public int PartitionCount;
            public bool Secondary;

            public void Execute(int listIndex)
            {
                ref var list = ref GetInputList(ref Input, ref SecondInput, listIndex);

                var counts = (int*)Counts.GetUnsafePtr() + listIndex * PartitionCount;
                UnsafeUtility.MemClear(counts, PartitionCount * sizeof(int));

                var key = default(TKey);
                for (var i = 0; i < list.Length; i++)
                {
                    counts[GetPartition(key.GetKey(in list.Ptr[i]), PartitionCount)]++;
                }

                if (!Secondary)
                {
                    return;
                }

                var secondaryCounts = (int*)SecondaryCounts.GetUnsafePtr() + listIndex * PartitionCount;
                UnsafeUtility.MemClear(secondaryCounts, PartitionCount * sizeof(int));

                var secondaryKey = default(TSecondaryKey);
                for (var i = 0; i < list.Length; i++)
                {
                    secondaryCounts[GetPartition(secondaryKey.GetKey(in list.Ptr[i]), PartitionCount)]++;
                }
            }
        }

        // partition-major: partition 0 of every thread list, then partition 1, ...
        [BurstCompile]
        public struct PrefixSumJob<T> : IJob
            where T : unmanaged
        {
            public NativeList<T> Output;
            public NativeArray<int> Starts;
            public NativeArray<int> Counts;
            public NativeList<int> SecondaryIndices;
            public NativeArray<int> SecondaryStarts;
            public NativeArray<int> SecondaryCounts;
            public int ListCount;
            public bool Secondary;

            public void Execute()
            {
                var itemCount = PrefixSum(ref Counts, ref Starts);
                Output.ResizeUninitialized(itemCount);

                if (Secondary)
                {
                    PrefixSum(ref SecondaryCounts, ref SecondaryStarts);
                    SecondaryIndices.ResizeUninitialized(itemCount);
                }
            }

            private int PrefixSum(ref NativeArray<int> counts, ref NativeArray<int> starts)
            {
                var partitionCount = starts.Length - 1;
                var offset = 0;

                for (var partition = 0; partition < partitionCount; partition++)
                {
                    starts[partition] = offset;

                    for (var list = 0; list < ListCount; list++)
                    {
                        var index = list * partitionCount + partition;
                        var count = counts[index];
                        counts[index] = offset;
                        offset += count;
                    }
                }

                starts[partitionCount] = offset;
                return offset;
            }
        }

        // every thread list writes its items to its own offsets, items keep their order within a thread list
        [BurstCompile]
        public unsafe struct ScatterJob<T, TKey, TSecondaryKey> : IJobFor
            where T : unmanaged
            where TKey : struct, IPartitionKey<T>
            where TSecondaryKey : struct, IPartitionKey<T>
        {
            [ReadOnly] public ParallelList<T> Input;
            [ReadOnly] public ParallelList<T> SecondInput;
            [NativeDisableParallelForRestriction] public NativeArray<T> Output;
            [NativeDisableParallelForRestriction] public NativeArray<int> Counts;
            [NativeDisableParallelForRestriction] public NativeArray<int> SecondaryIndices;
            [NativeDisableParallelForRestriction] public NativeArray<int> SecondaryCounts;
            public int PartitionCount;
            public bool Secondary;

            public void Execute(int listIndex)
            {
                ref var list = ref GetInputList(ref Input, ref SecondInput, listIndex);

                if (list.Length == 0)
                {
                    return;
                }

                var offsets = (int*)Counts.GetUnsafePtr() + listIndex * PartitionCount;
                var secondaryOffsets = (int*)SecondaryCounts.GetUnsafePtr() + listIndex * PartitionCount;
                var output = (T*)Output.GetUnsafePtr();
                var secondaryIndices = (int*)SecondaryIndices.GetUnsafePtr();

                var key = default(TKey);
                var secondaryKey = default(TSecondaryKey);

                for (var i = 0; i < list.Length; i++)
                {
                    ref var item = ref list.Ptr[i];

                    var position = offsets[GetPartition(key.GetKey(in item), PartitionCount)]++;
                    output[position] = item;

                    if (Secondary)
                    {
                        secondaryIndices[secondaryOffsets[GetPartition(secondaryKey.GetKey(in item), PartitionCount)]++] = position;
                    }
                }
            }
        }
    }
}