// <copyright project="NZCore" file="BlobExtension.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NZCore
{
    public static unsafe class BlobExtension
    {
        public static int GetLength<T>(this BlobAssetReference<T> blob)
            where T : unmanaged
        {
            return blob.m_data.Header->Length;
        }

        /// <summary>
        /// Evaluate EXISTING blobs for valid data. used for live-rebaking
        /// </summary>
        /// <param name="blob">the previously or current existing blob</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool IsInvalid<T>(this BlobAssetReference<T> blob)
            where T : unmanaged
        {
            if (blob.m_data.m_Ptr == null) // likely default blob data which has never been created in the first place
                return false; // so don't throw an invalid here

            var validationPtr = blob.m_data.Header->ValidationPtr;
            return validationPtr != blob.m_data.m_Ptr;
        }

        public static void AddBufferToBlob<T>(this BufferLookup<T> bufferLookup, Entity entity, ref BlobBuilder builder, ref BlobArray<T> blobArray)
            where T : unmanaged, IBufferElementData
        {
            if (!bufferLookup.HasBuffer(entity))
                return;

            var buffer = bufferLookup[entity];
            var tmp2 = builder.Allocate(ref blobArray, buffer.Length);

            for (int i = 0; i < buffer.Length; i++)
                tmp2[i] = buffer[i];
        }

        public static T* CopyBlob<T>(this ref BlobAssetReference<T> blobReference)
            where T : unmanaged
        {
            var header = blobReference.m_data.Header;
            var length = header->Length;

            var copyPtr = UnsafeUtility.Malloc(length, 16, Allocator.Persistent);

            UnsafeUtility.MemCpy(copyPtr, blobReference.m_data.m_Ptr, length);
            return (T*)copyPtr;
        }
    }
}