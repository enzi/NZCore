using NZSpellCasting;
using Unity.Collections;
using Unity.Entities;

namespace NZCore
{
    public struct EntityRemapBuffer : IBufferElementData
    {
        public Entity RemappedEntity;
        public int DeferredEntityIndex;
        public int DeferredEntityVersion;
    }
    
    [UpdateInGroup(typeof(NZSpellCastingInitializationSystemGroup))]
    [UpdateAfter(typeof(BeginEffectsSystemGroupCommandBufferSystem))]
    public partial struct EntityRemapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var entity = state.EntityManager.CreateEntity();

            state.EntityManager.AddBuffer<EntityRemapBuffer>(entity);
        }
    }
    
    [UpdateInGroup(typeof(NZSpellCastingInitializationSystemGroup))]
    public partial struct EntityRemapClearSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var remapBuffer = SystemAPI.GetSingletonBuffer<EntityRemapBuffer>();
            remapBuffer.Clear();
        }
    }

    public static class EntityRemapSystemExtensions
    {
        public static void AddRemapEntity(this ref EntityCommandBuffer commandBuffer, Entity remapEntity, Entity deferredEntity)
        {
            commandBuffer.AppendToBuffer(remapEntity, new EntityRemapBuffer()
            {
                RemappedEntity = deferredEntity,
                DeferredEntityIndex = deferredEntity.Index,
                DeferredEntityVersion = deferredEntity.Version
            });
        }
        
        public static void AddRemapEntityParallel(this ref EntityCommandBuffer.ParallelWriter commandBuffer, int threadIndex, Entity remapEntity, Entity deferredEntity)
        {
            commandBuffer.AppendToBuffer(threadIndex, remapEntity, new EntityRemapBuffer()
            {
                RemappedEntity = deferredEntity,
                DeferredEntityIndex = deferredEntity.Index,
                DeferredEntityVersion = deferredEntity.Version
            });
        }

        public static bool GetRemappedEntity(this ref NativeArray<EntityRemapBuffer> buffer, Entity deferredEntity, out Entity remappedEntity)
        {
            foreach (var remapElement in buffer)
            {
                if (remapElement.DeferredEntityIndex == deferredEntity.Index && remapElement.DeferredEntityVersion == deferredEntity.Version)
                {
                    remappedEntity = remapElement.RemappedEntity;
                    return true;
                }
            }
            
            remappedEntity = Entity.Null;
            return false;
        }
    }
}