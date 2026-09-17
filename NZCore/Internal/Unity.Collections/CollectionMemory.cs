using Unity.Collections;

namespace NZCore.Internal
{
    // Preserve Unity's allocation tracking, alignment and custom-allocator behavior without duplicating it.
    internal static unsafe class CollectionMemory
    {
        internal static class Unmanaged
        {
            internal static void* Allocate(long size, int alignment, AllocatorManager.AllocatorHandle allocator) =>
                Memory.Unmanaged.Allocate(size, alignment, allocator);

            internal static T* Allocate<T>(AllocatorManager.AllocatorHandle allocator) where T : unmanaged => Memory.Unmanaged.Allocate<T>(allocator);

            internal static void Free(void* pointer, AllocatorManager.AllocatorHandle allocator) => Memory.Unmanaged.Free(pointer, allocator);

            internal static void Free<T>(T* pointer, AllocatorManager.AllocatorHandle allocator) where T : unmanaged =>
                Memory.Unmanaged.Free(pointer, allocator);
        }
    }
}