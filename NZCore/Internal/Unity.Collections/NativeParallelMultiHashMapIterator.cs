using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace NZCore.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeParallelMultiHashMapIterator<TKey> where TKey : unmanaged
    {
        public TKey key;
        public int NextEntryIndex;
        public int EntryIndex;

        internal ref Unity.Collections.NativeParallelMultiHashMapIterator<TKey> Self =>
            ref UnsafeUtility.AsRef<Unity.Collections.NativeParallelMultiHashMapIterator<TKey>>(UnsafeUtility.AddressOf(ref this));

        public int GetEntryIndex() => Self.GetEntryIndex();

        public static implicit operator Unity.Collections.NativeParallelMultiHashMapIterator<TKey>(NativeParallelMultiHashMapIterator<TKey> iterator) =>
            iterator.Self;

        public static implicit operator NativeParallelMultiHashMapIterator<TKey>(Unity.Collections.NativeParallelMultiHashMapIterator<TKey> iterator) =>
            UnsafeUtility.As<Unity.Collections.NativeParallelMultiHashMapIterator<TKey>, NativeParallelMultiHashMapIterator<TKey>>(ref iterator);
    }
}