// <copyright project="NZCore.Testing" file="ScratchSceneSetUpBase.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace NZCore
{
    public abstract class ScratchSceneSetUpBase
    {
        private readonly string _folderName;

        private Scene _previousActiveScene;
        private Scene _scratchScene;

        protected ScratchSceneSetUpBase(string folderName)
        {
            _folderName = folderName;
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _previousActiveScene = SceneManager.GetActiveScene();
            _scratchScene = TestSubScenes.CreateAndOpen($"{TestAssets.CreateFolder(_folderName)}/Scratch.unity");
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
            AssetDatabase.DeleteAsset($"Assets/{_folderName}");
        }
    }
}