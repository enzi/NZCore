// <copyright project="NZCore" file="RemoveFromLinkedEntityGroupSystem.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.Components;
using Unity.Collections;
using Unity.Entities;

namespace NZCore
{
    [UpdateInGroup(typeof(NZDestroySystemGroup))]
    public partial struct RemoveFromLinkedEntityGroupSystem : ISystem
    {
        private EntityQuery _addedQuery;
        private EntityQuery _removedQuery;

        public void OnCreate(ref SystemState state)
        {
            _addedQuery = SystemAPI.QueryBuilder()
                                   .WithAll<RemoveFromLinkedEntityGroupCleanupSetup>()
                                   .WithNone<RemoveFromLinkedEntityGroupCleanup>()
                                   .Build();

            _removedQuery = SystemAPI.QueryBuilder()
                                     .WithAll<RemoveFromLinkedEntityGroupCleanup>()
                                     .WithNone<RemoveFromLinkedEntityGroupCleanupSetup>()
                                     .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_addedQuery.IsEmpty)
            {
                var addedEntities = _addedQuery.ToEntityArray(Allocator.Temp);

                foreach (var addedEntity in addedEntities)
                {
                    var info = SystemAPI.GetComponent<RemoveFromLinkedEntityGroupCleanupSetup>(addedEntity);

                    state.EntityManager.AddComponentData(addedEntity, new RemoveFromLinkedEntityGroupCleanup
                    {
                        Parent = info.Parent
                    });
                }

                state.EntityManager.RemoveComponent<RemoveFromLinkedEntityGroupCleanupSetup>(_addedQuery);
            }

            if (!_removedQuery.IsEmpty)
            {
                var destructionMap = SystemAPI.GetSingleton<DestructionMap>();
                var removedEntities = _removedQuery.ToEntityArray(Allocator.Temp);

                foreach (var removedEntity in removedEntities)
                {
                    var cleanupData = SystemAPI.GetComponent<RemoveFromLinkedEntityGroupCleanup>(removedEntity);

                    //Debug.Log($"Get leg from {cleanupData.Parent} - removing {removedEntity}");
                    if (SystemAPI.HasBuffer<LinkedEntityGroup>(cleanupData.Parent))
                    {
                        var leg = SystemAPI.GetBuffer<LinkedEntityGroup>(cleanupData.Parent);
                        leg.Remove(removedEntity);
                    }

                    destructionMap.Remove(cleanupData.Parent, removedEntity);
                }

                state.EntityManager.RemoveComponent<RemoveFromLinkedEntityGroupCleanup>(_removedQuery);
            }
        }
    }

    // public static class RemoveFromLinkedEntityGroupExtension
    // {
    //     public static void AddRemoveFromLinkedEntityGroup(this IBaker baker, Entity entity, Entity parent)
    //     {
    //         baker.AddComponent(entity, new RemoveFromLinkedEntityGroupFUCK()
    //         {
    //             Parent = parent
    //         });
    //         baker.AddComponent<RemoveFromLinkedEntityGroupTag>(entity);
    //     }
    // }
    // [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    // [UpdateInGroup(typeof(PostBakingSystemGroup))]
    // public partial struct RemoveFromLinkedEntityBakingSystem : ISystem
    // {
    //     public void OnUpdate(ref SystemState state)
    //     {
    //         foreach (var (linkedEntityGroup, additionalBakingData, entitiesToRemove, entity) in SystemAPI
    //                      .Query<DynamicBuffer<LinkedEntityGroupBakingData>, DynamicBuffer<AdditionalEntitiesBakingData>, DynamicBuffer<RemoveFromLinkedEntityGroup>>()
    //                      .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
    //                      .WithEntityAccess())
    //         {
    //             Debug.Log($"Hit {entity}");
    //             foreach (var entityGroup in entitiesToRemove)
    //             {
    //                 linkedEntityGroup.Remove(entityGroup.Entity);
    //                 
    //                 for (int i = additionalBakingData.Length - 1; i >= 0; i--)
    //                 {
    //                     if (additionalBakingData[i].Value != entityGroup.Entity) 
    //                         continue;
    //             
    //                     additionalBakingData.RemoveAt(i);
    //                 }
    //             }
    //         }
    //     }
    // }
}