// <copyright project="NZCore" file="SubsceneUtility.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace NZCore
{
    public struct SubSceneUtility
    {
        [ReadOnly] private ComponentLookup<SceneReference> sceneReference_ReadHandle;
        [ReadOnly] private BufferLookup<ResolvedSectionEntity> resolvedSectionEntity_ReadHandle;
        [ReadOnly] private ComponentLookup<SceneSectionStreamingSystem.StreamingState> streamingState_ReadHandle;
        [ReadOnly] private ComponentLookup<RequestSceneLoaded> requestSceneLoaded_ReadHandle;

        public SubSceneUtility(ref SystemState state)
        {
            sceneReference_ReadHandle = state.GetComponentLookup<SceneReference>(true);
            resolvedSectionEntity_ReadHandle = state.GetBufferLookup<ResolvedSectionEntity>(true);
            streamingState_ReadHandle = state.GetComponentLookup<SceneSectionStreamingSystem.StreamingState>(true);
            requestSceneLoaded_ReadHandle = state.GetComponentLookup<RequestSceneLoaded>(true);
        }
        
        public void Update(ref SystemState state)
        {
            sceneReference_ReadHandle.Update(ref state);
            resolvedSectionEntity_ReadHandle.Update(ref state);
            streamingState_ReadHandle.Update(ref state);
            requestSceneLoaded_ReadHandle.Update(ref state);
        }

        public static void OpenScene(ref SystemState state, Entity entity)
        {
            if (!state.EntityManager.HasComponent<RequestSceneLoaded>(entity))
            {
                state.EntityManager.AddComponent<RequestSceneLoaded>(entity);
            }

            var resolvedSectionEntities = state.EntityManager.GetBuffer<ResolvedSectionEntity>(entity);
            foreach (var section in resolvedSectionEntities.ToNativeArray(Allocator.Temp))
            {
                if (!state.EntityManager.HasComponent<RequestSceneLoaded>(section.SectionEntity))
                {
                    state.EntityManager.AddComponent<RequestSceneLoaded>(section.SectionEntity);
                }
            }
        }

        public static void CloseScene(ref SystemState state, Entity entity)
        {
            if (!state.EntityManager.HasComponent<RequestSceneLoaded>(entity))
            {
                Debug.LogWarning("Trying to close SubScene that isn't open.");
                return;
            }

            state.EntityManager.RemoveComponent<RequestSceneLoaded>(entity);

            var resolvedSectionEntities = state.EntityManager.GetBuffer<ResolvedSectionEntity>(entity);

            foreach (var section in resolvedSectionEntities.ToNativeArray(Allocator.Temp))
            {
                if (state.EntityManager.HasComponent<RequestSceneLoaded>(section.SectionEntity))
                {
                    state.EntityManager.RemoveComponent<RequestSceneLoaded>(section.SectionEntity);
                }
            }
        }

        public static bool IsSceneLoaded(ref SystemState state, Entity entity)
        {
            if (!state.EntityManager.HasComponent<SceneReference>(entity))
            {
                return false;
            }

            if (!state.EntityManager.HasBuffer<ResolvedSectionEntity>(entity))
            {
                return false;
            }

            var resolvedSectionEntities = state.EntityManager.GetBuffer<ResolvedSectionEntity>(entity);
            if (resolvedSectionEntities.Length == 0)
            {
                return false;
            }

            foreach (var s in resolvedSectionEntities)
            {
                if (!IsSectionLoaded(ref state, s.SectionEntity))
                {
                    return false;
                }
            }

            return true;
        }
        
        private static bool IsSectionLoaded(ref SystemState state, Entity sectionEntity)
        {
            if (!state.EntityManager.HasComponent<SceneSectionStreamingSystem.StreamingState>(sectionEntity))
            {
                return false;
            }
            
            return (SceneSectionStreamingSystem.StreamingStatus.Loaded ==
                   state.EntityManager.GetComponentData<SceneSectionStreamingSystem.StreamingState>(sectionEntity).Status);
        }

        public bool IsSceneLoaded(Entity entity)
        {
            if (!sceneReference_ReadHandle.HasComponent(entity))
            {
                return false;
            }

            if (!resolvedSectionEntity_ReadHandle.HasBuffer(entity))
            {
                return false;
            }

            var resolvedSectionEntities = resolvedSectionEntity_ReadHandle[entity];

            if (resolvedSectionEntities.Length == 0)
            {
                return false;
            }

            foreach (var s in resolvedSectionEntities)
            {
                if (!IsSectionLoaded(s.SectionEntity))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsSectionLoaded(Entity sectionEntity)
        {
            if (!streamingState_ReadHandle.HasComponent(sectionEntity))
            {
                return false;
            }

            return streamingState_ReadHandle[sectionEntity].Status == SceneSectionStreamingSystem.StreamingStatus.Loaded;
        }
        
        public void OpenScene(EntityCommandBuffer ecb, Entity entity)
        {
            if (requestSceneLoaded_ReadHandle.HasComponent(entity))
            {
                Debug.LogWarning("Trying to open SubScene that is already open.");
                return;
            }

            ecb.AddComponent<RequestSceneLoaded>(entity);

            var resolvedSectionEntities = resolvedSectionEntity_ReadHandle[entity];
            foreach (var section in resolvedSectionEntities)
            {
                ecb.AddComponent<RequestSceneLoaded>(section.SectionEntity);
            }
        }

        public void CloseScene(EntityCommandBuffer ecb, Entity entity)
        {
            if (!requestSceneLoaded_ReadHandle.HasComponent(entity))
            {
                Debug.LogWarning("Trying to close SubScene that isn't open.");
                return;
            }

            ecb.RemoveComponent<RequestSceneLoaded>(entity);

            var resolvedSectionEntities = resolvedSectionEntity_ReadHandle[entity];

            foreach (var section in resolvedSectionEntities)
            {
                if (requestSceneLoaded_ReadHandle.HasComponent(section.SectionEntity))
                {
                    ecb.RemoveComponent<RequestSceneLoaded>(section.SectionEntity);
                }
            }
        }
    }
}