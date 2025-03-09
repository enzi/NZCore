// <copyright project="NZCore" file="SubsceneHelper.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace NZCore
{
    public static class SubSceneHelper
    {
        /// <summary>
        /// Flag every entity in a subscene as destroyed via `DestroyEntity`
        /// then update the destroy pipeline systems and finally unload the subscene completely
        /// </summary>
        public static void DestroyAndUnloadSubscene(this EntityManager entityManager, Entity sceneEntity, SceneSystem.UnloadParameters unloadParams = SceneSystem.UnloadParameters.DestroyMetaEntities)
        {
            var guid = entityManager.GetComponentData<SceneReference>(sceneEntity).SceneGUID;
            var sections = entityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity).Length;

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SceneSection>()
                .WithDisabled<DestroyEntity>()
                .Build(entityManager);

            for (var i = 0; i < sections; i++)
            {
                query.SetSharedComponentFilter(new SceneSection { SceneGUID = guid, Section = i });
                entityManager.SetComponentEnabled<DestroyEntity>(query, true);
            }

            var destroyGroup = entityManager.World.GetExistingSystem<NZDestroySystemGroup>();
            destroyGroup.Update(entityManager.WorldUnmanaged);

            SceneSystem.UnloadScene(entityManager.World.Unmanaged, sceneEntity, unloadParams);
        }
        
        public static bool TryGetSceneEntity(ref SystemState state, Hash128 sceneGUID, out Entity sceneEntity)
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SceneReference>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeSystems | EntityQueryOptions.IncludeMetaChunks)
                .Build(ref state);
            
            var entities = query.ToEntityArray(Allocator.Temp);
            
            foreach (var entity in entities)
            {
                if (state.EntityManager.GetComponentData<SceneReference>(entity).SceneGUID != sceneGUID)
                {
                    continue;
                }

                sceneEntity = entity;
                return true;
            }
         
            sceneEntity = Entity.Null;
            return false;
        }

        public static NativeArray<Entity> GetAllOpenSubScenes(ref SystemState state, SubSceneUtility subSceneUtility)
        {
            subSceneUtility.Update(ref state);
            
            var subsceneQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<SceneReference, ResolvedSectionEntity>()
                    .Build(ref state);

            var entities = subsceneQuery.ToEntityArray(Allocator.Temp);

            NativeList<Entity> openScenes = new NativeList<Entity>(0, Allocator.Temp);
          
            foreach (var entity in entities)
            {
                if (subSceneUtility.IsSceneLoaded(entity))
                {
                    openScenes.Add(entity);
                }
            }
            
            return openScenes.AsArray();
        }

        public static NativeArray<Entity> GetAllOpenSubScenesWithIgnoreFilter(ref SystemState state, SubSceneUtility subSceneUtility)
        {
            subSceneUtility.Update(ref state);
            
            var subsceneQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SceneReference, ResolvedSectionEntity>()
                .Build(ref state);

            var entities = subsceneQuery.ToEntityArray(Allocator.Temp);

            NativeList<Entity> openScenes = new NativeList<Entity>(0, Allocator.Temp);
            
            var ignoreQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<IgnoreInOpenSubSceneQuery, SceneSection>()
                .Build(ref state);
                
            foreach (var entity in entities)
            {
                bool isOpened = subSceneUtility.IsSceneLoaded(entity);

                if (!isOpened)
                {
                    continue;
                }

                bool isIgnored = false;
                var sections = GetSections(ref state, entity);
                    
                foreach (var section in sections)
                {
                    ignoreQuery.SetSharedComponentFilter(section);

                    if (ignoreQuery.IsEmpty)
                    {
                        continue;
                    }

                    isIgnored = true;
                    break;
                }
                    
                if (!isIgnored)
                {
                    openScenes.Add(entity);
                }
            }
            
            return openScenes.AsArray();
        }

        public static bool IsSceneLoaded(ref SystemState state, Entity entity)
        {
            var subsceneUtility = new SubSceneUtility(ref state);
            return subsceneUtility.IsSceneLoaded(entity);
        }
        
        public static NativeArray<SceneSection> GetSections(ref SystemState state, NativeArray<Entity> openSubScenes)
        {
            NativeList<SceneSection> sections = new NativeList<SceneSection>(0, state.WorldUpdateAllocator);

            foreach (var subsceneEntity in openSubScenes)
            {
                var sectionsBuffer = state.EntityManager.GetBuffer<ResolvedSectionEntity>(subsceneEntity);
                
                foreach (var section in sectionsBuffer.AsNativeArray())
                {
#if UNITY_EDITOR
                    if (!state.EntityManager.HasComponent<SceneSectionData>(section.SectionEntity))
                    {
                        Debug.LogError("SubScene has been skipped. It's probably in open state!");
                        continue;
                    }
#endif
                    
                    var sceneSection = state.EntityManager.GetComponentData<SceneSectionData>(section.SectionEntity);
                    var key = new SceneSection()
                    {
                        SceneGUID = sceneSection.SceneGUID,
                        Section = sceneSection.SubSectionIndex
                    };
                
                    sections.Add(key);
                }
            }

            return sections.AsArray();
        }

        public static NativeArray<SceneSection> GetSections(ref SystemState state, Entity subSceneEntity)
        {
            NativeList<SceneSection> sections = new NativeList<SceneSection>(0, state.WorldUpdateAllocator);
            
            var sectionsBuffer = state.EntityManager.GetBuffer<ResolvedSectionEntity>(subSceneEntity);
            
            foreach (var section in sectionsBuffer.AsNativeArray())
            {
#if UNITY_EDITOR
                if (!state.EntityManager.HasComponent<SceneSectionData>(section.SectionEntity))
                {
                    Debug.LogError("SubScene has been skipped. It's probably in open state!");
                    continue;
                }
#endif
                
                var sceneSection = state.EntityManager.GetComponentData<SceneSectionData>(section.SectionEntity);
                var key = new SceneSection()
                {
                    SceneGUID = sceneSection.SceneGUID,
                    Section = sceneSection.SubSectionIndex
                };
            
                sections.Add(key);
            }
            

            return sections.AsArray();
        }
    }
}