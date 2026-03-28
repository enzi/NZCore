// <copyright project="NZCore" file="SubsceneUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace NZCore
{
    public struct SubSceneUtility
    {
        [ReadOnly] private ComponentLookup<SceneReference> _sceneReferenceReadHandle;
        [ReadOnly] private BufferLookup<ResolvedSectionEntity> _resolvedSectionEntityReadHandle;
        [ReadOnly] private ComponentLookup<SceneSectionStreamingSystem.StreamingState> _streamingStateReadHandle;
        [ReadOnly] private ComponentLookup<RequestSceneLoaded> _requestSceneLoadedReadHandle;

        public SubSceneUtility(ref SystemState state)
        {
            _sceneReferenceReadHandle = state.GetComponentLookup<SceneReference>(true);
            _resolvedSectionEntityReadHandle = state.GetBufferLookup<ResolvedSectionEntity>(true);
            _streamingStateReadHandle = state.GetComponentLookup<SceneSectionStreamingSystem.StreamingState>(true);
            _requestSceneLoadedReadHandle = state.GetComponentLookup<RequestSceneLoaded>(true);
        }

        public void Update(ref SystemState state)
        {
            _sceneReferenceReadHandle.Update(ref state);
            _resolvedSectionEntityReadHandle.Update(ref state);
            _streamingStateReadHandle.Update(ref state);
            _requestSceneLoadedReadHandle.Update(ref state);
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

            return state.EntityManager.GetComponentData<SceneSectionStreamingSystem.StreamingState>(sectionEntity).Status ==
                   SceneSectionStreamingSystem.StreamingStatus.Loaded;
        }

        public bool IsSceneLoaded(Entity entity)
        {
            if (!_sceneReferenceReadHandle.HasComponent(entity))
            {
                return false;
            }

            if (!_resolvedSectionEntityReadHandle.HasBuffer(entity))
            {
                return false;
            }

            var resolvedSectionEntities = _resolvedSectionEntityReadHandle[entity];

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
            if (!_streamingStateReadHandle.HasComponent(sectionEntity))
            {
                return false;
            }

            return _streamingStateReadHandle[sectionEntity].Status == SceneSectionStreamingSystem.StreamingStatus.Loaded;
        }

        public void OpenScene(EntityCommandBuffer ecb, Entity entity)
        {
            if (_requestSceneLoadedReadHandle.HasComponent(entity))
            {
                Debug.LogWarning("Trying to open SubScene that is already open.");
                return;
            }

            ecb.AddComponent<RequestSceneLoaded>(entity);

            var resolvedSectionEntities = _resolvedSectionEntityReadHandle[entity];
            foreach (var section in resolvedSectionEntities)
            {
                ecb.AddComponent<RequestSceneLoaded>(section.SectionEntity);
            }
        }

        public void CloseScene(EntityCommandBuffer ecb, Entity entity)
        {
            if (!_requestSceneLoadedReadHandle.HasComponent(entity))
            {
                Debug.LogWarning("Trying to close SubScene that isn't open.");
                return;
            }

            ecb.RemoveComponent<RequestSceneLoaded>(entity);

            var resolvedSectionEntities = _resolvedSectionEntityReadHandle[entity];

            foreach (var section in resolvedSectionEntities)
            {
                if (_requestSceneLoadedReadHandle.HasComponent(section.SectionEntity))
                {
                    ecb.RemoveComponent<RequestSceneLoaded>(section.SectionEntity);
                }
            }
        }
    }
}