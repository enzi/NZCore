// <copyright project="NZCore.Editor" file="SerializableTypeDrawer.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    [CustomPropertyDrawer(typeof(SerializableType))]
    public class SerializableTypeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var nameProp = property.FindPropertyRelative("_assemblyQualifiedName");

            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };

            var label = new Label("Type")
            {
                style =
                {
                    minWidth = 120,
                    unityTextAlign = UnityEngine.TextAnchor.MiddleLeft,
                    flexShrink = 0
                }
            };

            var button = new Button
            {
                style =
                {
                    flexGrow = 1
                }
            };
            RefreshLabel(button, nameProp.stringValue);

            button.clicked += () =>
            {
                new TypeSearchProvider(typeof(object)).Show(
                    item =>
                    {
                        if (item?.data is Type t)
                            Apply(nameProp, button, t.AssemblyQualifiedName);
                    },
                    items =>
                    {
                        if (items is { Length: > 0 } && items[0].data is Type t)
                            Apply(nameProp, button, t.AssemblyQualifiedName);
                    });
            };

            container.Add(label);
            container.Add(button);
            return container;
        }

        private static void Apply(SerializedProperty nameProp, Button button, string assemblyQualifiedName)
        {
            nameProp.stringValue = assemblyQualifiedName;
            nameProp.serializedObject.ApplyModifiedProperties();
            RefreshLabel(button, assemblyQualifiedName);
        }

        private static void RefreshLabel(Button button, string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName))
            {
                button.text = "(None)";
                return;
            }

            var t = Type.GetType(assemblyQualifiedName);
            button.text = t != null ? t.FullName : "(Unknown)";
        }
    }
}
