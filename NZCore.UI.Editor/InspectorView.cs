// <copyright project="NZCore.UI.Editor" file="InspectorView.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UI.Editor
{
    [UxmlElement]
    public partial class InspectorView : VisualElement
    {
        private UnityEditor.Editor editor;

        public void UpdateSelection(ScriptableObject obj)
        {
            Clear();

            Object.DestroyImmediate(editor);
            editor = UnityEditor.Editor.CreateEditor(obj);

            var inspector = editor.CreateInspectorGUI();
            inspector.Bind(editor.serializedObject);

            Add(inspector);
        }
    }
}