using System.Runtime.CompilerServices;

// These implementations live in Unity.Collections to retain access to collection internals.
// [assembly: TypeForwardedTo(typeof(NZCore.UnsafeHashMapExtensions))]
// [assembly: TypeForwardedTo(typeof(NZCore.NativeParallelHashMapExtensions))]
// [assembly: TypeForwardedTo(typeof(NZCore.NativeParallelMultiHashMapExtensions))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.CollectionMemory))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.CollectionInternals))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.Bitwise))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.HashMapHelper<>))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.NativeParallelMultiHashMapIterator<>))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.UnsafeParallelHashMapBase<,>))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.UnsafeParallelHashMapData))]