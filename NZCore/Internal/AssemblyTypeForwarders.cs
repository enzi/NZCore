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

// These implementations live in Unity.Entities to retain access to entities internals.
[assembly: TypeForwardedTo(typeof(NZCore.Internal.Archetype))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.ArchetypeChunkData))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.BufferHeader))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.ChunkDataUtility))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.ChunkIndex))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.ChunkIterationUtility))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.ComponentDependencyManager))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.EnabledBitUtility))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.EntityComponentStore))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.EntityDataAccess))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.EntityInChunk))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.EntityInternals))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.EntityQueryData))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.EntityQueryFilter))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.EntityQueryImpl))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.LookupCache))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.MatchingArchetype))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.SharedComponentValues))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.UnsafeCachedChunkList))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.UnsafeChunkCacheIterator))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.UnsafeMatchingArchetypePtrList))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.UntypedUnityObjectRef))]
#if ENABLE_UNITY_COLLECTIONS_CHECKS
[assembly: TypeForwardedTo(typeof(NZCore.Internal.ComponentSafetyHandles))]
[assembly: TypeForwardedTo(typeof(NZCore.Internal.EntityQuerySafetyHandles))]
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
[assembly: TypeForwardedTo(typeof(NZCore.Internal.ForEachDisallowStructuralChangeSupport))]
#endif