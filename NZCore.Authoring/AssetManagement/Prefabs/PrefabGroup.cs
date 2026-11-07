// <copyright project="NZCore.Authoring" file="PrefabGroup.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.Editor;
using UnityEngine;

namespace NZCore.AssetManagement
{
    [CreateAssetMenu(fileName = "New Prefab Group", menuName = "NZCore/Create Prefab Group")]
    public class PrefabGroup : ScriptableObjectWithAutoID
    {
        public const int GenericId = 1;

        [SerializeField] private int _groupId;

        [SerializableTypeFilter(typeof(ScriptableObjectWithAutoID))]
        public SerializableType BoundType = new();

        public override int AutoID
        {
            get => _groupId;
            set => _groupId = value;
        }

        public override int MaxAutoID => PrefabId.MaxGroup;

        public override HasChangeResult HasChanges(List<IChangeProcessor> allAssets) => HasChangeResult.None;

        public override void ProcessChanges(List<IChangeProcessor> allAssets) { }
    }
}