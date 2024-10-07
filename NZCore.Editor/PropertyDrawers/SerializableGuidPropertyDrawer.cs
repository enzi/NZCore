// <copyright project="NZCore.Editor" file="SerializableGuidPropertyDrawer.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    [CustomPropertyDrawer(typeof(SerializableGuid))]
    public class SerializableGuidPropertyDrawer : PropertyDrawer
    {
        private readonly float ySep = 20;
        private float buttonSize;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();

            SerializedProperty serializedGuid = property.FindPropertyRelative("serializedGuid");

            var propGuid = new PropertyField(serializedGuid);
            propGuid.BindProperty(serializedGuid);

            Button btnNewGuid = new Button
            {
                text = "NEW GUID"
            };

            btnNewGuid.style.width = new Length(50, LengthUnit.Percent);

            btnNewGuid.clicked += () =>
            {
                serializedGuid.stringValue = Guid.NewGuid().ToString();
                property.serializedObject.ApplyModifiedProperties();
            };

            root.Add(btnNewGuid);
            root.Add(propGuid);

            return root;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Start property draw
            EditorGUI.BeginProperty(position, label, property);

            // Get property
            SerializedProperty serializedGuid = property.FindPropertyRelative("serializedGuid");

            // Draw label
            position = EditorGUI.PrefixLabel(new Rect(position.x, position.y + ySep / 2, position.width, position.height), GUIUtility.GetControlID(FocusType.Passive), label);
            position.y -= ySep / 2; // Offsets position so we can draw the label for the field centered

            buttonSize = position.width / 3; // Update size of buttons to always fit perfeftly above the string representation field

            // Buttons
            if (GUI.Button(new Rect(position.xMin, position.yMin, buttonSize, ySep - 2), "New"))
            {
                serializedGuid.stringValue = Guid.NewGuid().ToString();
            }

            if (GUI.Button(new Rect(position.xMin + buttonSize, position.yMin, buttonSize, ySep - 2), "Copy"))
            {
                EditorGUIUtility.systemCopyBuffer = serializedGuid.stringValue;
            }

            if (GUI.Button(new Rect(position.xMin + buttonSize * 2, position.yMin, buttonSize, ySep - 2), "Empty"))
            {
                serializedGuid.stringValue = string.Empty;
            }

            // Draw fields - passs GUIContent.none to each so they are drawn without labels
            Rect pos = new Rect(position.xMin, position.yMin + ySep, position.width, ySep - 2);
            EditorGUI.PropertyField(pos, serializedGuid, GUIContent.none);

            // End property
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Field height never changes, so ySep * 2 will always return the proper hight of the field
            return ySep * 2;
        }
    }
}