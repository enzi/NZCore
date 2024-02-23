using Unity.Collections;
using Unity.Entities;

namespace NZCore
{
    public static unsafe class EntityManagerExtensions
    {
        public static Entity GetSingletonEntity<T>(this EntityManager entityManager)
            where T : unmanaged
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<T>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(entityManager);

            var entity = query.GetSingletonEntity();
            query.Dispose();

            return entity;
        }
        
        public static bool HasSingleton<T>(this EntityManager entityManager)
            where T : IComponentData
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<T>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(entityManager);
            
            bool has = !query.IsEmptyIgnoreFilter;
            query.Dispose();
            return has;
        }
        
        public static bool HasSingletonInManagedSystem<TSystem, TComponent>(this EntityManager entityManager)
            where TComponent : unmanaged, IComponentData
            where TSystem : ComponentSystemBase
        {
            var systemHandle = entityManager.World.GetExistingSystem<TSystem>();
            return entityManager.HasComponent<TComponent>(systemHandle);
        }
        
        public static T GetSingleton<T>(this EntityManager entityManager)
            where T : unmanaged, IComponentData
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<T>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(entityManager);
            
            var comp = query.GetSingleton<T>();
            query.Dispose();
            
            return comp;
        }
        
        public static void SetSingleton<T>(this EntityManager entityManager, T data)
            where T : unmanaged, IComponentData
        {
            //var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<T>());
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<T>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(entityManager);
            
            query.SetSingleton(data);
            query.Dispose();
        }
        
        public static T GetSingletonManaged<T>(this EntityManager entityManager)
            where T : class, IComponentData
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<T>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(entityManager);
            
            var entity = query.GetSingletonEntity();
            var comp = entityManager.GetComponentObject<T>(entity);
            query.Dispose();
            
            return comp;
        }

        public static TComponent GetSystemSingleton<TSystem, TComponent>(this EntityManager entityManager)
            where TComponent : unmanaged, IComponentData
            where TSystem : unmanaged, ISystem
        {
            var systemHandle = entityManager.WorldUnmanaged.GetExistingUnmanagedSystem<TSystem>();
            return entityManager.GetComponentData<TComponent>(systemHandle);
        }
        
        public static TComponent GetManagedSystemSingleton<TSystem, TComponent>(this EntityManager entityManager)
            where TComponent : unmanaged, IComponentData
            where TSystem : ComponentSystemBase
        {
            var systemHandle = entityManager.World.GetExistingSystem<TSystem>();
            return entityManager.GetComponentData<TComponent>(systemHandle);
        }
        
        public static void ManualIncrement(this EntityManager entityManager)
        {
            entityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
        }
        
        public static void* GetComponentDataRaw(this EntityManager entityManager, Entity entity, ComponentType componentType, bool isReadOnly)
        {
            return GetComponentDataRaw(entityManager, entity, componentType.TypeIndex, isReadOnly);
        }
        
        public static void* GetComponentDataRaw(this EntityManager entityManager, Entity entity, TypeIndex typeIndex, bool isReadOnly)
        {
            var access = entityManager.GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;

            return isReadOnly ? 
                ecs->GetComponentDataWithTypeRO(entity, typeIndex) : 
                ecs->GetComponentDataWithTypeRW(entity, typeIndex, ecs->GlobalSystemVersion);
        }
        
        public static byte* GetComponentDataRaw(this EntityManager entityManager, TypeIndex typeIndex, Entity entity, bool isReadOnly)
        {
            var access = entityManager.GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var entityInChunk = ecs->GetEntityInChunk(entity);

            LookupCache lookupCache = default;
            
            return isReadOnly ? 
                ChunkDataUtility.GetOptionalComponentDataWithTypeRO(entityInChunk.Chunk, ecs->GetArchetype(entityInChunk.Chunk), entityInChunk.IndexInChunk, typeIndex, ref lookupCache) : 
                ChunkDataUtility.GetOptionalComponentDataWithTypeRW(entityInChunk.Chunk, ecs->GetArchetype(entityInChunk.Chunk), entityInChunk.IndexInChunk, typeIndex, ecs->GlobalSystemVersion, ref lookupCache);
        }
        
        /////////////////////////////
        /// UnsafeComponentLookup ///
        /////////////////////////////
        
        public static UnsafeComponentLookup<T> GetUnsafeComponentLookup<T>(this EntityManager entityManager, bool isReadOnly = false)
            where T : unmanaged, IComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            return GetUnsafeComponentLookup<T>(entityManager, typeIndex, isReadOnly);
        }

        internal static UnsafeComponentLookup<T> GetUnsafeComponentLookup<T>(this EntityManager entityManager, TypeIndex typeIndex, bool isReadOnly)
            where T : unmanaged, IComponentData
        {
            var access = entityManager.GetCheckedEntityDataAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = &access->DependencyManager->Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new UnsafeComponentLookup<T>(typeIndex, access, isReadOnly);
#else
            return new UnsafeComponentLookup<T>(typeIndex, access);
#endif
        }
        
        /////////////////////////////
        /// SharedComponentLookup ///
        /////////////////////////////
        
        public static SharedComponentLookup<T> GetSharedComponentLookup<T>(this EntityManager entityManager, bool isReadOnly)
            where T : unmanaged, ISharedComponentData
        {
            var access = entityManager.GetCheckedEntityDataAccess();
            var typeIndex = TypeManager.GetTypeIndex<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new SharedComponentLookup<T>(typeIndex, access, isReadOnly);
#else
            return new SharedComponentLookup<T>(typeIndex, access);
#endif
        }
        
        public static SharedComponentLookup<T> GetSharedComponentLookup<T>(ref this SystemState system, bool isReadOnly)
            where T : unmanaged, ISharedComponentData
        {
            system.AddReaderWriter(isReadOnly ? ComponentType.ReadOnly<T>() : ComponentType.ReadWrite<T>());
            return system.EntityManager.GetSharedComponentLookup<T>(isReadOnly);
        }
    }
}