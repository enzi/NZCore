// <copyright project="NZCore" file="EnabledChunkIterator.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Mathematics;

namespace NZCore
{
    public interface IEnabledChunkIterator
    {
        void Execute(int entityIndexInChunk);
    }

    public struct EnabledChunkIterator<T>
        where T : unmanaged, IEnabledChunkIterator
    {
        public T JobData;

        public EnabledChunkIterator(T jobData)
        {
            JobData = jobData;
        }

        public void Execute(in ArchetypeChunk chunk, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var chunkEntityCount = chunk.Count;

            if (!useEnabledMask)
            {
                for (int entityIndexInChunk = 0; entityIndexInChunk < chunkEntityCount; ++entityIndexInChunk)
                {
                    JobData.Execute(entityIndexInChunk);
                }
            }
            else
            {
                if (chunkEnabledMask.DetermineFastPath())
                {
                    int chunkEndIndex = 0;
                    while (EnabledBitUtility.TryGetNextRange(chunkEnabledMask, chunkEndIndex, out var entityIndexInChunk, out chunkEndIndex))
                    {
                        while (entityIndexInChunk < chunkEndIndex)
                        {
                            JobData.Execute(entityIndexInChunk);
                            entityIndexInChunk++;
                        }
                    }
                }
                else
                {
                    ulong mask64 = chunkEnabledMask.ULong0;
                    int count = math.min(64, chunkEntityCount);
                    for (int entityIndexInChunk = 0; entityIndexInChunk < count; ++entityIndexInChunk)
                    {
                        if ((mask64 & 1) != 0)
                        {
                            JobData.Execute(entityIndexInChunk);
                        }

                        mask64 >>= 1;
                    }

                    mask64 = chunkEnabledMask.ULong1;
                    for (var entityIndexInChunk = 64; entityIndexInChunk < chunkEntityCount; ++entityIndexInChunk)
                    {
                        if ((mask64 & 1) != 0)
                        {
                            JobData.Execute(entityIndexInChunk);
                        }

                        mask64 >>= 1;
                    }
                }
            }
        }
    }
}