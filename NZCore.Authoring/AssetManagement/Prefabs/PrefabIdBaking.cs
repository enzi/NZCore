// <copyright project="NZCore.Authoring" file="PrefabIdBaking.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace NZCore.AssetManagement
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    public static partial class PrefabIdBaking
    {
        private static List<PrefabGroup> groups;
        private static Dictionary<(Type Type, string Key), List<PrefabGroup>> groupsByBinding;

        public static PrefabId FromDefinition(IBaker baker, PrefabDefinition definition, string key = null)
        {
            baker.DependsOn(definition);
            baker.DependsOn(definition.Group);

            if (definition.TryGetPrefabId(key, out var prefabId))
            {
                return prefabId;
            }

            var keyName = string.IsNullOrEmpty(key) ? "its default key" : $"key {key}";
            Debug.LogError($"{definition.name} has no ID for {keyName}. Bakers can't write assets, so save the definition to let the allocator assign one.",
                definition);
            return PrefabId.Null;
        }

        public static PrefabId FromAutoID(IBaker baker, ScriptableObjectWithAutoID asset, string key = null)
        {
            baker.DependsOn(asset);

            if (groups == null)
            {
                groups = PrefabGroup.FindAll();
                groupsByBinding = groups.Where(group => group.BoundType.Type != null)
                                        .GroupBy(group => group.Binding)
                                        .ToDictionary(binding => binding.Key, binding => binding.ToList());
            }

            foreach (var group in groups)
            {
                baker.DependsOn(group);
            }

            var type = asset.ProcessGroupType;
            key ??= string.Empty;
            var bindingName = PrefabGroup.BindingName(type, key);

            if (!groupsByBinding.TryGetValue((type, key), out var boundGroups))
            {
                Debug.LogError($"No PrefabGroup is bound to {bindingName}, so {asset.name} has no PrefabId.", asset);
                return PrefabId.Null;
            }

            if (boundGroups.Count > 1)
            {
                Debug.LogError($"{bindingName} is bound to more than one PrefabGroup: {string.Join(", ", boundGroups.Select(group => group.name))}. " +
                               $"{asset.name} has no PrefabId until only one is.", asset);
                return PrefabId.Null;
            }

            if (asset.AutoID < 1 || asset.AutoID > PrefabId.MaxId)
            {
                Debug.LogError($"{asset.name} has AutoID {asset.AutoID}, but a PrefabId needs an ID between 1 and {PrefabId.MaxId}.", asset);
                return PrefabId.Null;
            }

            return new PrefabId(boundGroups[0].AutoID, asset.AutoID);
        }

        internal static void ClearGroupCache()
        {
            groups = null;
            groupsByBinding = null;
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PreBakingSystemGroup))]
    internal partial class PrefabIdBakingCacheSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            PrefabIdBaking.ClearGroupCache();
        }
    }
}