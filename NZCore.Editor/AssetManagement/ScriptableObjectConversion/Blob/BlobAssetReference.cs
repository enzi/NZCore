using System;
using Unity.Entities;

namespace NZCore
{
    public interface IConvertToBlob<T>
        where T : unmanaged
    {
        public void ToBlobData(IBaker baker, ref BlobBuilder blobBuilder, ref T blob, Entity blobEntity, Guid guid);
    }

    // 2 blobs

    public interface IConvertToBlob<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        public void ToBlobData(IBaker baker, ref BlobBuilder blobBuilder, ref T1 blob1, ref T2 blob2, Entity blobEntity, Guid guid);
    }
}