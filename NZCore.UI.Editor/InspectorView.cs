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
        private UnityEditor.Editor _editor;

        public void UpdateSelection(ScriptableObject obj)
        {
            Clear();

            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
            }

            _editor = UnityEditor.Editor.CreateEditor(obj);

            var inspector = _editor.CreateInspectorGUI();
            inspector.Bind(_editor.serializedObject);

            Add(inspector);
        }
    }
}