using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace NZCore
{
    public interface IEnabledChunkEnumerator
    {
        bool MoveNext(out int nextIndex);
    }
    
    public delegate bool MoveNextFunction(ref EnabledChunkEnumerator_Base thiz, out int index);

    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    public struct EnabledChunkEnumerator_Base
    {
        public FunctionPointer<MoveNextFunction> BaseFP;
    }

    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EnabledChunkEnumerator_NoMask
    {
        private EnabledChunkEnumerator_Base Base;
        private static readonly FunctionPointer<MoveNextFunction> MoveNextFP = BurstCompiler.CompileFunctionPointer<MoveNextFunction>(MoveNext);

        private readonly int chunkEntityCount;
        private int entityIndexInChunk;

        public EnabledChunkEnumerator_NoMask(int chunkEntityCount)
        {
            entityIndexInChunk = 0;
            this.chunkEntityCount = chunkEntityCount;
            
            Base = new EnabledChunkEnumerator_Base
            {
                BaseFP = MoveNextFP
            };
        }
        
        [BurstCompile]
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MoveNext(
            ref EnabledChunkEnumerator_Base thiz,
            out int nextIndex)
        {
            var ptr = UnsafeUtility.AddressOf(ref thiz);
            ref var enumerator = ref UnsafeUtility.AsRef<EnabledChunkEnumerator_NoMask>(ptr);
            
            if (enumerator.entityIndexInChunk < enumerator.chunkEntityCount)
            {
                nextIndex = enumerator.entityIndexInChunk;
                enumerator.entityIndexInChunk++;
                return true;
            }

            nextIndex = -1;
            return false;
        }
    }
    
    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EnabledChunkEnumerator_MaskFast
    {
        private EnabledChunkEnumerator_Base Base;
        private static readonly FunctionPointer<MoveNextFunction> MoveNextFP = BurstCompiler.CompileFunctionPointer<MoveNextFunction>(MoveNext);

        private readonly v128 chunkEnabledMask;
        
        private int chunkEndIndex;
        private int entityIndexInChunk;
        
        public EnabledChunkEnumerator_MaskFast(v128 chunkEnabledMask)
        {
            Base = new EnabledChunkEnumerator_Base { BaseFP = MoveNextFP };
            
            this.chunkEnabledMask = chunkEnabledMask;
            
            EnabledBitUtility.TryGetNextRange(chunkEnabledMask, 0, out entityIndexInChunk, out chunkEndIndex);
        }

        [BurstCompile]
        private static bool MoveNext(
            ref EnabledChunkEnumerator_Base thiz,
            out int nextIndex)
        {
            var ptr = UnsafeUtility.AddressOf(ref thiz);
            ref var enumerator = ref UnsafeUtility.AsRef<EnabledChunkEnumerator_MaskFast>(ptr);
            
            if (enumerator.entityIndexInChunk < enumerator.chunkEndIndex)
            {
                nextIndex = enumerator.entityIndexInChunk;
                enumerator.entityIndexInChunk++;
                return true;
            }

            if (EnabledBitUtility.TryGetNextRange(enumerator.chunkEnabledMask, enumerator.chunkEndIndex, out enumerator.entityIndexInChunk, out enumerator.chunkEndIndex))
            {
                nextIndex = enumerator.entityIndexInChunk;
                return true;
            }

            nextIndex = -1;
            return false;
        }
    }
    
    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EnabledChunkEnumerator_MaskSlow
    {
        private EnabledChunkEnumerator_Base Base;
        
        private static readonly FunctionPointer<MoveNextFunction> MoveNextFP = BurstCompiler.CompileFunctionPointer<MoveNextFunction>(MoveNext);

        private readonly int chunkEntityCount;
        private readonly int count;
        
        private ulong mask64_0;
        private ulong mask64_1;

        private int currentIndex;

        public EnabledChunkEnumerator_MaskSlow(v128 chunkEnabledMask, int chunkEntityCount)
        {
            Base = new EnabledChunkEnumerator_Base { BaseFP = MoveNextFP };
            
            currentIndex = 0;
            this.chunkEntityCount = chunkEntityCount;
            count = math.min(64,chunkEntityCount);
            
            mask64_0 = chunkEnabledMask.ULong0;
            mask64_1 = chunkEnabledMask.ULong1;
        }

        [BurstCompile]
        private static bool MoveNext(
            ref EnabledChunkEnumerator_Base thiz,
            out int nextIndex)
        {
            var ptr = UnsafeUtility.AddressOf(ref thiz);
            ref var enumerator = ref UnsafeUtility.AsRef<EnabledChunkEnumerator_MaskSlow>(ptr);
        
            while(enumerator.currentIndex < enumerator.count)
            {
                if ((enumerator.mask64_0 & 1) != 0)
                {
                    nextIndex = enumerator.currentIndex;
                    enumerator.currentIndex++;
                    enumerator.mask64_0 >>= 1;
                    return true;
                }

                enumerator.mask64_0 >>= 1;
                enumerator.currentIndex++;
            }

            while(enumerator.currentIndex < enumerator.chunkEntityCount)
            {
                if ((enumerator.mask64_1 & 1) != 0)
                {
                    nextIndex = enumerator.currentIndex;
                    enumerator.currentIndex++;
                    enumerator.mask64_1 >>= 1;
                    return true;
                }

                enumerator.mask64_1 >>= 1;
                enumerator.currentIndex++;
            }

            nextIndex = enumerator.currentIndex;
            return false;
        }
    }
    
    [BurstCompile]
    public unsafe struct EnabledChunkEnumerator
    {
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static ref EnabledChunkEnumerator_Base GetEnabledChunkEnumerator(in ArchetypeChunk chunk, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!useEnabledMask)
            {
                var noMask = new EnabledChunkEnumerator_NoMask(chunk.Count);
                return ref UnsafeUtility.AsRef<EnabledChunkEnumerator_Base>(UnsafeUtility.AddressOf(ref noMask));
            }

            if (chunkEnabledMask.DetermineFastPath())
            {
                var maskFast = new EnabledChunkEnumerator_MaskFast(chunkEnabledMask);
                return ref UnsafeUtility.AsRef<EnabledChunkEnumerator_Base>(UnsafeUtility.AddressOf(ref maskFast));
            }

            var maskSlow = new EnabledChunkEnumerator_MaskSlow(chunkEnabledMask, chunk.Count);
            return ref UnsafeUtility.AsRef<EnabledChunkEnumerator_Base>(UnsafeUtility.AddressOf(ref maskSlow));
        }

        // public static void GetEntityIndexes(ref NativeList<int> entities, in ArchetypeChunk chunk, bool useEnabledMask, in v128 chunkEnabledMask)
        // {
        //     if (!entities.IsCreated)
        //     {
        //         entities = new NativeList<int>(0, Allocator.Temp);
        //     }
        //     else
        //     {
        //         entities.Clear();
        //     }
        //     
        //     if (!useEnabledMask)
        //     {
        //         //Debug.Log("no mask");
        //         //var noMaskEnumerator = new EnabledChunkEnumerator_NoMask(chunk.Count);
        //         entities.ResizeUninitialized(chunk.Count);
        //         for (int i = 0; i < chunk.Count; i++)
        //             entities[i] = i;
        //         
        //         return;
        //     }
        //
        //     if (chunkEnabledMask.DetermineFastPath())
        //     {
        //         //Debug.Log($"Fast Path {chunk.Count}");
        //         var enumerator = new EnabledChunkEnumerator_MaskFast(chunkEnabledMask);
        //         int chunkCount = EnabledBitUtility.countbits(chunkEnabledMask);
        //         entities.ResizeUninitialized(chunkCount);
        //
        //         int index = 0;
        //         while (enumerator.MoveNext(out int i))
        //         {
        //             entities[index] = i;
        //             index++;
        //         }
        //     }
        //     else
        //     {
        //         //Debug.Log("slow Path");
        //         var enumerator = new EnabledChunkEnumerator_MaskSlow(chunkEnabledMask, chunk.Count);
        //         int chunkCount = EnabledBitUtility.countbits(chunkEnabledMask);
        //         entities.ResizeUninitialized(chunkCount);
        //
        //         int index = 0;
        //         while (enumerator.MoveNext(out int i))
        //         {
        //             entities[index] = i;
        //             index++;
        //         }
        //     }
        // }
    }
}