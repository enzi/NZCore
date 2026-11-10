// <copyright project="NZCore.Authoring" file="PrefabDefinition.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore.AssetManagement
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    [CreateAssetMenu(fileName = "New Prefab Definition", menuName = "NZCore/Create Prefab Definition")]
    public partial class PrefabDefinition : ScriptableObject
    {
        public const string DefaultKey = "";

        private static Dictionary<PrefabId, (PrefabDefinition Owner, bool IsFormerId)> owners;

        public PrefabGroup Group;
        public Object Target;

        [Tooltip("What an instance leaves behind when gameplay requests its destroy (DestroyRequest), for example a corpse. Empty leaves nothing.")]
        public PrefabDefinition ReplaceOnDestroy;

        public List<ReservedPrefabId> ReservedIds = new();
        public List<FormerPrefabId> FormerIds = new();
        [HideInInspector] public int ReservedGroupId;
        [HideInInspector] public bool Retired;

        public int GroupId => Group != null ? Group.AutoID : PrefabGroup.GenericId;

        public IEnumerable<string> GetKeys()
        {
            var source = Target switch
            {
                IMultiPrefabSource multiPrefabSource => multiPrefabSource,
                GameObject gameObject => gameObject.GetComponent<IMultiPrefabSource>(),
                _ => null
            };

            var keys = new List<string> { DefaultKey };

            if (source != null)
            {
                keys.AddRange(source.PrefabNames);
            }

            return keys.Distinct();
        }

        public bool TryGetPrefabId(string key, out PrefabId prefabId)
        {
            key ??= DefaultKey;

            foreach (var reservedId in ReservedIds)
            {
                if (reservedId.Key == key && reservedId.Id != 0)
                {
                    prefabId = new PrefabId(GroupId, reservedId.Id);
                    return true;
                }
            }

            prefabId = PrefabId.Null;
            return false;
        }

        public static List<PrefabDefinition> FindAll() =>
            AssetDatabase.FindAssets($"t:{nameof(PrefabDefinition)}")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Distinct()
                         .Select(AssetDatabase.LoadAssetAtPath<PrefabDefinition>)
                         .Where(definition => definition != null)
                         .ToList();

        public static bool TryFindOwner(PrefabId prefabId, out PrefabDefinition owner, out bool isFormerId)
        {
            owners ??= BuildOwnerIndex();

            var found = owners.TryGetValue(prefabId, out var entry);
            owner = entry.Owner;
            isFormerId = entry.IsFormerId;
            return found;
        }

        internal static void ClearOwnerIndex()
        {
            owners = null;
        }

        private static Dictionary<PrefabId, (PrefabDefinition Owner, bool IsFormerId)> BuildOwnerIndex()
        {
            var index = new Dictionary<PrefabId, (PrefabDefinition Owner, bool IsFormerId)>();

            foreach (var definition in FindAll())
            {
                var groupId = definition.GroupId;

                if (groupId is >= 1 and <= PrefabId.MaxGroup)
                {
                    foreach (var reservedId in definition.ReservedIds)
                    {
                        if (reservedId.Id is >= 1 and <= PrefabId.MaxId)
                        {
                            index.TryAdd(new PrefabId(groupId, reservedId.Id), (definition, false));
                        }
                    }
                }

                foreach (var formerId in definition.FormerIds)
                {
                    index.TryAdd(formerId.Id, (definition, true));
                }
            }

            return index;
        }
    }

    [Serializable]
    public struct ReservedPrefabId
    {
        public string Key;
        public int Id;
    }

    [Serializable]
    public struct FormerPrefabId
    {
        public string Key;
        public PrefabId Id;
    }
}
