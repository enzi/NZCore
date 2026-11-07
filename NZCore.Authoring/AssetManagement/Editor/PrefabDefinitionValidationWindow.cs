// <copyright project="NZCore.Authoring" file="PrefabDefinitionValidationWindow.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.AssetManagement;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    public class PrefabDefinitionValidationWindow : EditorWindow
    {
        private ScrollView _problems;
        private Label _count;

        [MenuItem("Tools/NZCore/Prefab Definition Validation", false, 2)]
        public static void Open()
        {
            GetWindow<PrefabDefinitionValidationWindow>();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Prefab Definitions");
        }

        public void CreateGUI()
        {
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(Refresh) { text = "Refresh" });
            _count = new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 8 } };
            toolbar.Add(_count);
            rootVisualElement.Add(toolbar);

            _problems = new ScrollView { style = { flexGrow = 1 } };
            rootVisualElement.Add(_problems);

            Refresh();
        }

        private void Refresh()
        {
            _problems.Clear();

            var problems = PrefabDefinitionValidation.FindProblems();
            _count.text = problems.Count switch
            {
                0 => "No problems",
                1 => "1 problem",
                _ => $"{problems.Count} problems"
            };

            foreach (var problem in problems)
            {
                var asset = problem.Asset;
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        paddingLeft = 4,
                        paddingRight = 4,
                        paddingTop = 2,
                        paddingBottom = 2
                    }
                };

                row.Add(new Button(() =>
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                })
                {
                    text = asset != null ? asset.name : "Missing",
                    style = { width = 200, flexShrink = 0 }
                });

                row.Add(new Label(problem.Message) { style = { whiteSpace = WhiteSpace.Normal, flexShrink = 1, flexGrow = 1 } });
                _problems.Add(row);
            }
        }
    }
}