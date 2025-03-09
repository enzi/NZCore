// <copyright project="NZCore.Authoring" file="IgnoreInOpenSubSceneQueryAuthoring.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using UnityEngine;

namespace NZCore.Authoring
{
    public class IgnoreInOpenSubSceneQueryAuthoring : MonoBehaviour
    {
        private class IgnoreInOpenSubSceneQueryAuthoringBaker : Baker<IgnoreInOpenSubSceneQueryAuthoring>
        {
            public override void Bake(IgnoreInOpenSubSceneQueryAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                AddComponent(entity, new IgnoreInOpenSubSceneQuery());
            }
        }
    }
}