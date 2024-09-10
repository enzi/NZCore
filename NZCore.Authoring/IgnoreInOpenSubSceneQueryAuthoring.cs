// <copyright project="NZCore" file="IgnoreInOpenSubSceneQueryAuthoring.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
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