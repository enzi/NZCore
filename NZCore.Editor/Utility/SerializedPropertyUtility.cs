// <copyright project="NZCore" file="SerializedPropertyUtility.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    public static class SerializedPropertyUtility
    {
        public static IEnumerable<SerializedProperty> IterateAllChildren(this SerializedObject root, bool siblingProperties = false)
        {
            var iterator = root.GetIterator();

            for (var enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
            {
                if (iterator.propertyPath != "m_Script")
                {
                    yield return iterator.Copy();

                    if (siblingProperties)
                    {
                        foreach (var child in GetChildren(iterator))
                        {
                            yield return child;
                        }
                    }
                }
            }
        }

        public static IEnumerable<SerializedProperty> GetChildren(this SerializedProperty property)
        {
            var currentProperty = property.Copy();
            var nextSiblingProperty = property.Copy();
            nextSiblingProperty.Next(false);

            if (currentProperty.Next(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(currentProperty, nextSiblingProperty))
                    {
                        yield break;
                    }

                    yield return currentProperty.Copy();
                } while (currentProperty.Next(false));
            }
        }

        public static Dictionary<string, PropertyField> GetPropertyFieldDictionary(this SerializedProperty root)
        {
            Dictionary<string, PropertyField> propertyFields = new Dictionary<string, PropertyField>();

            var list = GetChildren(root).ToList();

            foreach (var serializedProperty in list)
            {
                var tmp = new PropertyField(serializedProperty);
                propertyFields.Add(serializedProperty.propertyPath, tmp);
            }

            return propertyFields;
        }

        public static Dictionary<string, PropertyField> GetPropertyFieldDictionary(this SerializedProperty root, VisualElement visualElement, bool autoBind = true)
        {
            Dictionary<string, PropertyField> propertyFields = new Dictionary<string, PropertyField>();

            var list = GetChildren(root).ToList();

            foreach (var serializedProperty in list)
            {
                var tmp = new PropertyField(serializedProperty);
                visualElement.Add(tmp);
                propertyFields.Add(serializedProperty.name, tmp);

                if (autoBind)
                    tmp.BindProperty(serializedProperty);

                //Debug.Log($"added {serializedProperty.name}");
            }

            return propertyFields;
        }

        public static Dictionary<string, PropertyField> FillDefaultInspector(this SerializedObject serializedObject, VisualElement container, bool autoBind, params string[] ignored)
        {
            if (serializedObject == null)
                return null;

            var iterator = serializedObject.GetIterator();

            if (!iterator.NextVisible(true))
                return null;

            Dictionary<string, PropertyField> propertyFields = new Dictionary<string, PropertyField>();

            do
            {
                bool ignoreField = iterator.propertyPath == "m_Script";

                foreach (var ignoreString in ignored)
                {
                    if (iterator.name == ignoreString)
                        ignoreField = true;
                }

                if (ignoreField)
                    continue;

                var propertyField = new PropertyField(iterator)
                {
                    name = iterator.propertyPath,
                    pickingMode = PickingMode.Ignore
                };

                //propertyField.Q<VisualElement>("unity-input-Value").pickingMode = PickingMode.Ignore;

                container.Add(propertyField);
                propertyFields.Add(propertyField.name, propertyField);

                if (autoBind)
                    propertyField.BindProperty(iterator);

                //Debug.Log($"Added {propertyField.name}");
            } while (iterator.NextVisible(false));

            return propertyFields;
        }

        // ReSharper disable once UnusedMember.Local
        private static Type GetPropertyType(SerializedProperty property)
        {
            Type parentType = property.serializedObject.targetObject.GetType();
            FieldInfo fieldInfo = parentType.GetField(property.propertyPath);
            if (fieldInfo != null)
            {
                return fieldInfo.FieldType;
            }

            return null;
        }
    }
}