// <copyright project="NZCore.Authoring" file="PrefabGroup.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using NZCore.Editor;
using UnityEditor;
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

        [Tooltip("Binds the type once per kind of prefab it builds. The baker passes the same key to PrefabIdBaking.FromAutoID.")]
        public string BoundKey = string.Empty;

        internal (Type Type, string Key) Binding => (BoundType.Type, BoundKey ?? string.Empty);

        public override int AutoID
        {
            get => _groupId;
            set => _groupId = value;
        }

        public override int MaxAutoID => PrefabId.MaxGroup;

        public override AutoIDConflictMode ConflictMode => AutoIDConflictMode.Error;

        public override HasChangeResult HasChanges(List<IChangeProcessor> allAssets) => HasChangeResult.None;

        public override void ProcessChanges(List<IChangeProcessor> allAssets) { }

        internal static string BindingName(Type type, string key) => string.IsNullOrEmpty(key) ? type.Name : $"{type.Name} with key {key}";

        public static List<PrefabGroup> FindAll() =>
            AssetDatabase.FindAssets($"t:{nameof(PrefabGroup)}")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Distinct()
                         .Select(AssetDatabase.LoadAssetAtPath<PrefabGroup>)
                         .Where(group => group != null)
                         .ToList();
    }
}