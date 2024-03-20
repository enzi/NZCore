using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace NZCore
{
    public static class SubsceneHelper
    {
        /// <summary>
        /// Flag every entity in a subscene as destroyed via `DestroyEntity`
        /// then update the destroy pipeline systems and finally unload the subscene completely
        /// </summary>
        public static void DestroyAndUnloadSubscene(this EntityManager entityManager, Entity sceneEntity, SceneSystem.UnloadParameters unloadParams = SceneSystem.UnloadParameters.Default)
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
    }
}