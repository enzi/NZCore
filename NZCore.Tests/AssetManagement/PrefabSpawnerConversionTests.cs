// <copyright project="NZCore.Tests" file="PrefabSpawnerConversionTests.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NZCore.AssetManagement;
using NZCore.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NZCore.Tests.AssetManagement
{
    public class PrefabSpawnerConversionTests
    {
        private string _folder;
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _folder = TestAssets.CreateFolder("PrefabSpawnerConversionTestsTemp");
            _scene = TestSubScenes.CreateAndOpen($"{_folder}/Placed.unity");
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.CloseScene(_scene, true);
            AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void PlacedPrefab_BecomesASpawnerGameObjectWithItsDefinitionAtTheSameTransform()
        {
            var prefab = TestAssets.CreatePrefab(_folder, new GameObject("Placed"));
            var definition = PrefabDefinitionTooling.CreateDefinitions(new[] { prefab }, null, _folder)[0];
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
            instance.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0));
            instance.transform.localScale = Vector3.one * 2;

            Assert.AreEqual(1, PrefabSpawnerConversion.ConvertAll(_scene, PrefabDefinitionTooling.FindOwners()));

            var spawner = _scene.GetRootGameObjects().Single();
            Assert.IsFalse(PrefabUtility.IsPartOfAnyPrefab(spawner), "The subscene must not hold the prefab itself anymore");
            Assert.AreEqual("Placed", spawner.name);
            Assert.AreEqual(definition, spawner.GetComponent<PrefabSpawnerAuthoring>().Definition);
            Assert.AreEqual(new Vector3(1, 2, 3), spawner.transform.position);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0, 90, 0), spawner.transform.rotation), 0.01f);
            Assert.AreEqual(Vector3.one * 2, spawner.transform.localScale);
        }

        [Test]
        public void VariantWithoutItsOwnDefinition_StaysAPrefabInstanceAndWarns()
        {
            var prefab = TestAssets.CreatePrefab(_folder, new GameObject("Base"));
            PrefabDefinitionTooling.CreateDefinitions(new[] { prefab }, null, _folder);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(TestAssets.CreateVariant(_folder, prefab, "Variant"), _scene);

            LogAssert.Expect(LogType.Warning, new Regex("^Variant stays a prefab instance, because Variant has no PrefabDefinition of its own"));

            Assert.AreEqual(0, PrefabSpawnerConversion.ConvertAll(_scene, PrefabDefinitionTooling.FindOwners()));
            Assert.IsTrue(instance != null, "Converting it would spawn the base prefab and drop the variant's overrides");
        }

        [Test]
        public void PlacedPrefabWithOverrides_IsConvertedAndWarnsThatTheOverridesAreLost()
        {
            var prefab = TestAssets.CreatePrefab(_folder, new GameObject("Placed"));
            PrefabDefinitionTooling.CreateDefinitions(new[] { prefab }, null, _folder);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
            instance.AddComponent<BoxCollider>();

            LogAssert.Expect(LogType.Warning, new Regex("^Placed had overrides that the spawner doesn't keep"));

            Assert.AreEqual(1, PrefabSpawnerConversion.ConvertAll(_scene, PrefabDefinitionTooling.FindOwners()));
        }
    }
}