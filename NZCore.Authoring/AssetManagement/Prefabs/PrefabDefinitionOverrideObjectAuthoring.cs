// <copyright project="NZCore.Authoring" file="PrefabDefinitionOverrideObjectAuthoring.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using UnityEngine;

namespace NZCore.AssetManagement
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class PrefabDefinitionOverrideObjectAuthoring : MonoBehaviour
    {
        private class Baker : Baker<PrefabDefinitionOverrideObjectAuthoring>
        {
            public override void Bake(PrefabDefinitionOverrideObjectAuthoring authoring)
            {
                if (IsActive())
                {
                    AddComponent<Disabled>(GetEntity(TransformUsageFlags.None));
                }
            }
        }
    }
}