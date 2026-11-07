// <copyright project="NZCore.Tests" file="ScratchSceneSetUp.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace NZCore.Tests.AssetManagement
{
    [SetUpFixture]
    public class ScratchSceneSetUp
    {
        private const string FolderName = "NZCoreTestsScratch";

        private Scene _previousActiveScene;
        private Scene _scratchScene;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _previousActiveScene = SceneManager.GetActiveScene();
            _scratchScene = TestSubScenes.CreateAndOpen($"{TestAssets.CreateFolder(FolderName)}/Scratch.unity");
            SceneManager.SetActiveScene(_scratchScene);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(_previousActiveScene);
            }

            EditorSceneManager.CloseScene(_scratchScene, true);
            AssetDatabase.DeleteAsset($"Assets/{FolderName}");
        }
    }
}