using Unity.Collections;

namespace NZCore.Interfaces
{
    public interface IBlobIndex
    {
        public int BlobIndex { get; }
    }

    public interface IBlobIndexList<T>
        where T : unmanaged
    {
        public NativeList<T> GetIndexList();
    }
}