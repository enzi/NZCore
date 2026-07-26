// <copyright project="NZCore" file="ArenaBufferReleaseSystem.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace NZCore
{
    /// <summary>
    /// Hands the arena blocks of doomed entities back before <see cref="DestructionSystem"/> destroys them.
    ///
    /// Arena buffers have no cleanup component, so the block is only reachable while the <c>*Ref</c> component
    /// is still on the entity. This system sits between <see cref="PreDestructionSystem"/> (OrderFirst, which
    /// cascades <see cref="DestroyEntity"/> onto children) and <see cref="DestructionSystem"/> (OrderLast,
    /// which does the destroying), so it sees the full set of entities that are about to go away.
    ///
    /// <see cref="DestroyEntity"/> is an <see cref="IEnableableComponent"/>, so a matched chunk still holds
    /// entities that are staying alive - the release goes through the enabled mask rather than the whole
    /// chunk, otherwise it would free blocks out from under living entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(NZDestroySystemGroup))]
    public partial struct ArenaBufferReleaseSystem : ISystem
    {
        private EntityQuery _destroyQuery;
        private ComponentTypeHandle<DestroyEntity> _destroyHandle;
        private ArenaBufferReleaseHandles _releaseHandles;

        public void OnCreate(ref SystemState state)
        {
            _releaseHandles = ArenaBufferReleaseHandles.Create(ref state);

            if (_releaseHandles.TypeCount == 0)
            {
                state.Enabled = false;
                return;
            }

            _destroyQuery = SystemAPI.QueryBuilder()
                                     .WithAll<DestroyEntity>()
                                     .Build();

            _destroyHandle = state.GetComponentTypeHandle<DestroyEntity>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
            _releaseHandles.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_destroyQuery.IsEmpty)
            {
                return;
            }

            _releaseHandles.Update(ref state);
            _destroyHandle.Update(ref state);

            var chunks = _destroyQuery.ToArchetypeChunkArray(Allocator.Temp);

            for (var c = 0; c < chunks.Length; c++)
            {
                var chunk = chunks[c];
                var enabledMask = chunk.GetEnabledMask(ref _destroyHandle);

                _releaseHandles.ReleaseChunk(chunk, enabledMask);
            }

            chunks.Dispose();
        }
    }
}
