// <copyright project="NZCore" file="NativeArrayExtensions.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    public static class NativeArrayExtensions
    {
        public static NativeArray<T> MergeNativeArray<T>(this NativeArray<T> array1, NativeArray<T> array2, Allocator allocator) where T : unmanaged
        {
            unsafe
            {
                var array1Ptr = array1.GetUnsafeReadOnlyPtr();
                var array2Ptr = array2.GetUnsafeReadOnlyPtr();

                int array1Length = array1.Length;
                int array2Length = array2.Length;

                NativeArray<T> merged = new NativeArray<T>(array1Length + array2Length, allocator);
                var mergedPtr = (T*)merged.GetUnsafePtr();

                var size = UnsafeUtility.SizeOf<T>();

                UnsafeUtility.MemCpy(mergedPtr, array1Ptr, array1Length * size);
                mergedPtr += array1Length;
                UnsafeUtility.MemCpy(mergedPtr, array2Ptr, array2Length * size);

                return merged;
            }
        }

        public static NativeArray<T> CopySlice<T>(this NativeArray<T> array1, int count, Allocator allocator) where T : unmanaged
        {
            NativeArray<T> slicedArray = new NativeArray<T>(count * UnsafeUtility.SizeOf<T>(), allocator);

            unsafe
            {
                UnsafeUtility.MemCpy(slicedArray.GetUnsafePtr(), array1.GetUnsafeReadOnlyPtr(), count * UnsafeUtility.SizeOf<T>());
            }

            return slicedArray;
        }

        public static unsafe NativeArray<T> ToNativeArray<T>(void* ptr, int length) where T : struct
        {
            return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, length, Allocator.None);
        }

        public static unsafe NativeArray<T> ToNativeArray<T>(ref this BlobArray<T> blobArray) where T : struct
        {
            return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(blobArray.GetUnsafePtr(), blobArray.Length, Allocator.None);
        }

        public static unsafe ref T ElementAt<T>(this NativeArray<T> array, int index)
            where T : struct
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
        }

        public static unsafe ref readonly T ElementAtRO<T>(this NativeArray<T> array, int index)
            where T : struct
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafeReadOnlyPtr(), index);
        }

        public static void AddArrayToBlob<T>(this NativeArray<T> array, ref BlobBuilder builder, ref BlobArray<T> blobArray)
            where T : unmanaged
        {
            BlobBuilderArray<T> tmp2 = builder.Allocate(ref blobArray, array.Length);

            for (int i = 0; i < array.Length; i++)
                tmp2[i] = array[i];
        }
    }
}