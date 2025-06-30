// <copyright project="NZCore" file="EntityRemapSystem.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if NZSPELLCASTING
using NZSpellCasting;
#endif
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;

namespace NZCore
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct EntityRemapBuffer : IBufferElementData
    {
        [FieldOffset(0)]
        public Entity RemappedEntity;
        
        // automatic 
        [FieldOffset(8)]
        public Entity* RewritePtr;
        
        // manual
        [FieldOffset(8)]
        public int DeferredEntityIndex;
        [FieldOffset(12)]
        public int DeferredEntityVersion;
        
        [FieldOffset(16)]
        public byte Automatic;
    }
    
#if NZSPELLCASTING
    [UpdateInGroup(typeof(NZSpellCastingInitializationSystemGroup))]
    [UpdateAfter(typeof(BeginEffectsSystemGroupCommandBufferSystem))]
#else
    [UpdateInGroup(typeof(NZCoreInitializationSystemGroup))]
#endif
    public partial struct EntityRemapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var entity = state.EntityManager.CreateEntity();

            state.EntityManager.AddBuffer<EntityRemapBuffer>(entity);
        }

        public unsafe void OnUpdate(ref SystemState state)
        {
            var buffer = SystemAPI.GetSingletonBuffer<EntityRemapBuffer>();

            foreach (var element in buffer)
            {
                if (element.Automatic == 1)
                {
                    *element.RewritePtr = element.RemappedEntity;
                }
            }
        }
    }
#if NZSPELLCASTING
    [UpdateInGroup(typeof(NZSpellCastingInitializationSystemGroup), OrderLast = true)]
#else
    [UpdateInGroup(typeof(InitializationSystemGroup))]
#endif
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
        public static void AddRemapEntity(this ref EntityCommandBuffer commandBuffer, Entity remapBufferEntity, Entity deferredEntity)
        {
            commandBuffer.AppendToBuffer(remapBufferEntity, new EntityRemapBuffer()
            {
                RemappedEntity = deferredEntity,
                DeferredEntityIndex = deferredEntity.Index,
                DeferredEntityVersion = deferredEntity.Version
            });
        }
        
        /// <summary>
        /// Automatically patch a deferred entity, just make sure the rewritePtr doesn't change for any reason like a list resize
        /// </summary>
        public static unsafe void AddRemapEntityParallel(this ref EntityCommandBuffer.ParallelWriter commandBuffer, int threadIndex, Entity remapBufferEntity, 
            Entity deferredEntity, Entity* rewritePtr)
        {
            commandBuffer.AppendToBuffer(threadIndex, remapBufferEntity, new EntityRemapBuffer()
            {
                Automatic = 1,
                RewritePtr = rewritePtr,
                RemappedEntity = deferredEntity
            });
        }

        public static void AddRemapEntityParallel(this ref EntityCommandBuffer.ParallelWriter commandBuffer, int threadIndex, Entity remapBufferEntity, 
            Entity deferredEntity)
        {
            commandBuffer.AppendToBuffer(threadIndex, remapBufferEntity, new EntityRemapBuffer()
            {
                RemappedEntity = deferredEntity,
                DeferredEntityIndex = deferredEntity.Index,
                DeferredEntityVersion = deferredEntity.Version,
                Automatic = 0
            });
        }

        public static bool GetRemappedEntity(this NativeArray<EntityRemapBuffer> buffer, Entity deferredEntity, out Entity remappedEntity)
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