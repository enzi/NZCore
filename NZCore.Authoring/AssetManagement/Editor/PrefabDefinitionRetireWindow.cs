// <copyright project="NZCore.Authoring" file="PrefabDefinitionRetireWindow.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.AssetManagement;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    public class PrefabDefinitionRetireWindow : EditorWindow
    {
        [SerializeField] private PrefabDefinition _definition;

        public static void Open(PrefabDefinition definition)
        {
            var window = CreateInstance<PrefabDefinitionRetireWindow>();
            window._definition = definition;
            window.titleContent = new GUIContent($"Retire {definition.name}");
            window.minSize = new Vector2(380, 130);
            window.ShowUtility();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;

            root.Add(new Label("Old saves of this definition load the replacement's prefab instead. Without a replacement, they find no prefab.")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginBottom = 8 }
            });

            var replacement = new ObjectField("Replacement")
            {
                objectType = typeof(PrefabDefinition),
                allowSceneObjects = false
            };
            root.Add(replacement);

            var error = new HelpBox(string.Empty, HelpBoxMessageType.Error) { style = { display = DisplayStyle.None } };
            root.Add(error);

            root.Add(new Button(() =>
            {
                var replacementDefinition = (PrefabDefinition)replacement.value;
                var migrationAsset = replacementDefinition != null ? PrefabDefinitionTooling.FindOrCreateMigrationAsset() : null;

                if (PrefabDefinitionTooling.TryRetire(_definition, replacementDefinition, migrationAsset, out var message))
                {
                    Close();
                    return;
                }

                error.text = message;
                error.style.display = DisplayStyle.Flex;
            })
            {
                text = "Retire",
                style = { marginTop = 8 }
            });
        }
    }
}