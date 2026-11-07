// <copyright project="NZCore.Authoring" file="PrefabDefinition.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NZCore.AssetManagement
{
    [CreateAssetMenu(fileName = "New Prefab Definition", menuName = "NZCore/Create Prefab Definition")]
    public class PrefabDefinition : ScriptableObject
    {
        public const string DefaultKey = "";

        public PrefabGroup Group;
        public Object Target;
        public List<ReservedPrefabId> ReservedIds = new();

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
    }

    [Serializable]
    public struct ReservedPrefabId
    {
        public string Key;
        public int Id;
    }
}