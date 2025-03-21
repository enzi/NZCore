﻿// <copyright project="NZCore.Editor" file="DefaultAutoIDEditor.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.AssetManagement;
using NZCore.Editor.AssetManagement;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    [CustomEditor(typeof(ScriptableObjectWithDefaultAutoID), true)]
    [CanEditMultipleObjects]
    public class DefaultAutoIDEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var propertyFields = serializedObject.FillDefaultInspector(root, true);

            var soDefault = serializedObject.FindProperty("DefaultValue");

            propertyFields["DefaultValue"].TrackPropertyValue(soDefault, (_) => { DefaultAutoIDProcessor.ProcessDefaultAutoIDs(target.GetType()); });

            return root;
        }
    }
}