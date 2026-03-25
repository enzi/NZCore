// <copyright project="Assembly-CSharp" file="LoadAddressablesRequestAuthoring.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.UIToolkit.Data;
using Unity.Entities;
using UnityEngine;

namespace NZCore.UI.Authoring
{
    public class LoadAddressablesRequestAuthoring : MonoBehaviour
    {
        public bool PrintLoadedAssets;
        
        private class LoadAddressablesRequestBaker : Baker<LoadAddressablesRequestAuthoring>
        {
            public override void Bake(LoadAddressablesRequestAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new LoadAddressablesRequest()
                {
                    PrintLoadedAssets = authoring.PrintLoadedAssets
                });
            }
        }
    }
}