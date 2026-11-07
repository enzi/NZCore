// <copyright project="NZCore.Authoring" file="PrefabDefinitionCreateWindow.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace NZCore.Editor
{
    public class PrefabDefinitionCreateWindow : EditorWindow
    {
        private const string MenuPath = "Assets/NZCore/Create Definitions from Selected Prefabs";
        private const string GroupKey = "NZCore.PrefabDefinitionCreate.Group";
        private const string FolderKey = "NZCore.PrefabDefinitionCreate.Folder";

        [SerializeField] private List<GameObject> _prefabs = new();

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = CreateInstance<PrefabDefinitionCreateWindow>();
            window._prefabs = SelectedPrefabs().ToList();
            window.titleContent = new GUIContent("Create Prefab Definitions");
            window.minSize = new Vector2(380, 130);
            window.ShowUtility();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanOpen() => SelectedPrefabs().Any();

        private static IEnumerable<GameObject> SelectedPrefabs() =>
            Selection.GetFiltered<GameObject>(SelectionMode.Assets).Where(PrefabUtility.IsPartOfPrefabAsset);

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;

            root.Add(new Label($"Creates a definition for each of the {_prefabs.Count} selected prefabs and for each of their variants.")
                { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 8 } });

            var group = new ObjectField("Group")
            {
                objectType = typeof(PrefabGroup),
                allowSceneObjects = false,
                tooltip = "Leave empty for the Generic group.",
                value = AssetDatabase.LoadAssetAtPath<PrefabGroup>(AssetDatabase.GUIDToAssetPath(EditorUserSettings.GetConfigValue(GroupKey) ?? string.Empty))
            };
            root.Add(group);

            var folderPath = EditorUserSettings.GetConfigValue(FolderKey);

            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                folderPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(_prefabs[0]))!.Replace('\\', '/');
            }

            var folder = new ObjectField("Folder")
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false,
                value = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath)
            };
            root.Add(folder);

            var error = new HelpBox(string.Empty, HelpBoxMessageType.Error) { style = { display = DisplayStyle.None } };
            root.Add(error);

            root.Add(new Button(() =>
            {
                var path = AssetDatabase.GetAssetPath(folder.value);

                if (!AssetDatabase.IsValidFolder(path))
                {
                    error.text = "Pick a folder for the new definitions.";
                    error.style.display = DisplayStyle.Flex;
                    return;
                }

                EditorUserSettings.SetConfigValue(GroupKey,
                    group.value != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(group.value)) : string.Empty);
                EditorUserSettings.SetConfigValue(FolderKey, path);

                Selection.objects = PrefabDefinitionTooling.CreateDefinitions(_prefabs, (PrefabGroup)group.value, path).ToArray<Object>();
                Close();
            })
            {
                text = "Create",
                style = { marginTop = 8 }
            });
        }
    }
}