// <copyright project="NZCore.UI.Authoring" file="LoadCustomUIAssetsRequestAuthoring.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.UIToolkit;
using NZCore.UIToolkit.Data;
using Unity.Entities;
using UnityEngine;

namespace NZCore.UI.Authoring
{
    public class LoadCustomUIAssetsRequestAuthoring : MonoBehaviour
    {
        public List<CustomUIAsset> CustomAssets;

        private class LoadCustomUIAssetsRequestBaker : Baker<LoadCustomUIAssetsRequestAuthoring>
        {
            public override void Bake(LoadCustomUIAssetsRequestAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponentObject(entity, new LoadCustomUIAssetsRequest()
                {
                    CustomAssets = authoring.CustomAssets
                });
            }
        }
    }
}