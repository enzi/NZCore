// <copyright project="NZCore.Authoring" file="PrefabSpawnerGizmo.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEngine;

namespace NZCore.Editor
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    internal static partial class PrefabSpawnerGizmo
    {
        private static readonly Color Color = new(0.4f, 0.8f, 1f, 0.5f);
        private static readonly Color SelectedColor = new(1f, 0.8f, 0.3f, 0.7f);
        private static readonly Dictionary<GameObject, List<(Mesh Mesh, Matrix4x4 Matrix)>> MeshesByPrefab = new();

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.projectChanged += MeshesByPrefab.Clear;
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void Draw(PrefabSpawnerAuthoring spawner, GizmoType gizmoType)
        {
            if (spawner.Definition == null || spawner.Definition.Target is not GameObject prefab)
            {
                return;
            }

            if (!MeshesByPrefab.TryGetValue(prefab, out var meshes))
            {
                meshes = MeshesByPrefab[prefab] = CollectMeshes(prefab);
            }

            Gizmos.color = (gizmoType & GizmoType.Selected) != 0 ? SelectedColor : Color;

            var spawnerMatrix = spawner.transform.localToWorldMatrix;

            foreach (var (mesh, matrix) in meshes)
            {
                Gizmos.matrix = spawnerMatrix * matrix;
                Gizmos.DrawMesh(mesh, Vector3.zero, Quaternion.identity, Vector3.one);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }

        private static List<(Mesh Mesh, Matrix4x4 Matrix)> CollectMeshes(GameObject prefab)
        {
            var meshes = new List<(Mesh Mesh, Matrix4x4 Matrix)>();
            var toPrefab = prefab.transform.worldToLocalMatrix;

            foreach (var meshFilter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh != null && meshFilter.TryGetComponent<MeshRenderer>(out _))
                {
                    meshes.Add((meshFilter.sharedMesh, toPrefab * meshFilter.transform.localToWorldMatrix));
                }
            }

            foreach (var skinnedMeshRenderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinnedMeshRenderer.sharedMesh != null)
                {
                    meshes.Add((skinnedMeshRenderer.sharedMesh, toPrefab * skinnedMeshRenderer.transform.localToWorldMatrix));
                }
            }

            return meshes;
        }
    }
}