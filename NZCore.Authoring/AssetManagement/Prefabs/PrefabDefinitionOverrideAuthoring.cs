// <copyright project="NZCore.Authoring" file="PrefabDefinitionOverrideAuthoring.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using UnityEngine;

namespace NZCore.AssetManagement
{
    [DisallowMultipleComponent]
    public class PrefabDefinitionOverrideAuthoring : MonoBehaviour
    {
        public GameObject Overrides;

        public static Entity Bake(IBaker baker, PrefabDefinitionOverrideAuthoring authoring)
        {
            if (authoring == null || authoring.Overrides == null)
            {
                return Entity.Null;
            }

            if (baker.GetComponent<PrefabDefinitionOverrideObjectAuthoring>(authoring.Overrides) == null)
            {
                Debug.LogError(
                    $"{authoring.name} uses {authoring.Overrides.name} as its Overrides object, but it has no PrefabDefinitionOverrideObjectAuthoring. " +
                    "Without it, the overrides bake as a live entity, so its spawner copies nothing.", authoring);
                return Entity.Null;
            }

            return baker.GetEntity(authoring.Overrides, TransformUsageFlags.None);
        }
    }
}