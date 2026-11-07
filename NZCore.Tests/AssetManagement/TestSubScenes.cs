// <copyright project="NZCore.Tests" file="TestSubScenes.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.IO;
using NUnit.Framework;
using Unity.Entities;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;

namespace NZCore.Tests.AssetManagement
{
    internal static class TestSubScenes
    {
        public static Scene CreateAndOpen(string scenePath)
        {
            File.WriteAllText(scenePath, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(scenePath);

            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        public static void SaveAndClose(Scene scene)
        {
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        public static Entity Load(World world, string scenePath)
        {
            var guid = AssetDatabase.AssetPathToGUID(scenePath);
            Assert.That(guid, Is.Not.Empty, $"Scene not found: {scenePath}");

            var sceneEntity = SceneSystem.LoadSceneAsync(
                world.Unmanaged,
                new Hash128(guid),
                new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                });

            var sceneSystemGroup = world.GetExistingSystemManaged<SceneSystemGroup>();

            for (var update = 0; update < 1000; update++)
            {
                sceneSystemGroup.Update();
                world.EntityManager.CompleteAllTrackedJobs();

                var state = SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity);

                if (state == SceneSystem.SceneStreamingState.LoadedSuccessfully)
                {
                    return sceneEntity;
                }

                Assert.That(state,
                    Is.Not.EqualTo(SceneSystem.SceneStreamingState.FailedLoadingSceneHeader)
                      .And.Not.EqualTo(SceneSystem.SceneStreamingState.LoadedWithSectionErrors),
                    $"Failed to load scene {scenePath}: {state}");
            }

            Assert.Fail($"Timed out loading scene: {scenePath}");
            return Entity.Null;
        }

        public static void Unload(World world, Entity sceneEntity)
        {
            SceneSystem.UnloadScene(world.Unmanaged, sceneEntity, SceneSystem.UnloadParameters.DestroyMetaEntities);
            world.GetExistingSystemManaged<SceneSystemGroup>().Update();
            world.EntityManager.CompleteAllTrackedJobs();
        }
    }
}