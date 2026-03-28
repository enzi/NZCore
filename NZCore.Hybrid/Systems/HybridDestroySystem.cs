// <copyright project="NZCore.Hybrid" file="HybridDestroySystem.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using UnityEngine;

namespace NZCore.Hybrid
{
    public partial class HybridDestroySystem : SystemBase
    {
        private EntityQuery _query;

        protected override void OnCreate()
        {
            _query = SystemAPI.QueryBuilder()
                              .WithAll<DestroyEntity, HybridObjectBuffer>()
                              .Build();
        }

        protected override void OnUpdate()
        {
            if (_query.IsEmpty)
            {
                return;
            }

            var entities = _query.ToEntityArray(CheckedStateRef.WorldUpdateAllocator);

            foreach (var entity in entities)
            {
                var buffer = SystemAPI.GetBuffer<HybridObjectBuffer>(entity);

                foreach (var element in buffer)
                {
                    Object.Destroy(element.GameObject);
                }
            }
        }
    }
}