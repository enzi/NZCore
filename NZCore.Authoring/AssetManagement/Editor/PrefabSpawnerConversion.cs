// <copyright project="NZCore.Authoring" file="PrefabSpawnerConversion.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NZCore.Editor
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    [InitializeOnLoad]
    public static partial class PrefabSpawnerConversion
    {
        private const string MenuPath = "Tools/NZCore/Convert Placed Prefabs to Spawners";

        private static readonly List<GameObject> Pending = new();
        private static int pendingUndoGroup = -1;

        static PrefabSpawnerConversion()
        {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        internal static int ConvertAll(Scene scene, Dictionary<GameObject, PrefabDefinition> owners)
        {
            var instances = scene.GetRootGameObjects()
                                 .SelectMany(root => root.GetComponentsInChildren<PrefabDefinitionAuthoring>(true))
                                 .Select(authoring => authoring.gameObject)
                                 .Where(PrefabUtility.IsOutermostPrefabInstanceRoot)
                                 .Distinct()
                                 .ToList();

            return instances.Count(instance => TryConvert(instance, owners, out _));
        }

        internal static bool TryConvert(GameObject instance, Dictionary<GameObject, PrefabDefinition> owners, out GameObject spawner)
        {
            spawner = null;
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(instance);

            if (prefab == null)
            {
                return false;
            }

            if (!owners.TryGetValue(prefab, out var definition))
            {
                Debug.LogWarning($"{instance.name} stays a prefab instance, because {prefab.name} has no PrefabDefinition of its own. " +
                                 "Create one with Create Definitions from Selected Prefabs, then convert it.", instance);
                return false;
            }

            var hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false);
            var transform = instance.transform;

            spawner = new GameObject(instance.name);
            SceneManager.MoveGameObjectToScene(spawner, instance.scene);
            spawner.transform.SetParent(transform.parent, false);
            spawner.transform.SetLocalPositionAndRotation(transform.localPosition, transform.localRotation);
            spawner.transform.localScale = transform.localScale;
            spawner.transform.SetSiblingIndex(transform.GetSiblingIndex());
            spawner.AddComponent<PrefabSpawnerAuthoring>().Definition = definition;

            Undo.RegisterCreatedObjectUndo(spawner, "Convert to Spawner");
            Undo.DestroyObjectImmediate(instance);

            if (hasOverrides)
            {
                Debug.LogWarning($"{spawner.name} had overrides that the spawner doesn't keep. Add them as components on the spawner.", spawner);
            }

            return true;
        }

        [MenuItem(MenuPath)]
        private static void ConvertOpenSubScenes()
        {
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Convert Placed Prefabs to Spawners");

            var owners = PrefabDefinitionTooling.FindOwners();
            var subScenes = 0;
            var converted = 0;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded || !scene.isSubScene)
                {
                    continue;
                }

                subScenes++;
                converted += ConvertAll(scene, owners);
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(subScenes == 0
                ? "No subscene is open for editing, so no placed prefab was converted."
                : $"Converted {converted} placed prefabs to spawners in {subScenes} open subscenes.");
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (Undo.isProcessing)
            {
                return;
            }

            for (var i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.CreateGameObjectHierarchy)
                {
                    continue;
                }

                stream.GetCreateGameObjectHierarchyEvent(i, out var change);

                if (!change.scene.isSubScene || EditorUtility.EntityIdToObject(change.entityId) is not GameObject instance ||
                    !PrefabUtility.IsOutermostPrefabInstanceRoot(instance) || !instance.TryGetComponent<PrefabDefinitionAuthoring>(out _))
                {
                    continue;
                }

                if (Pending.Count == 0)
                {
                    pendingUndoGroup = Undo.GetCurrentGroup();
                    EditorApplication.delayCall += ConvertPending;
                }

                Pending.Add(instance);
            }
        }

        private static void ConvertPending()
        {
            var owners = PrefabDefinitionTooling.FindOwners();

            foreach (var instance in Pending)
            {
                if (instance != null && TryConvert(instance, owners, out var spawner))
                {
                    Selection.activeGameObject = spawner;
                }
            }

            Pending.Clear();
            Undo.CollapseUndoOperations(pendingUndoGroup);
        }
    }
}