// <copyright project="NZCore.Editor" file="SerializedPropertyUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
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
                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

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
            var propertyFields = new Dictionary<string, PropertyField>();

            var list = GetChildren(root).ToList();

            foreach (var serializedProperty in list)
            {
                var tmp = new PropertyField(serializedProperty);
                propertyFields.Add(serializedProperty.propertyPath, tmp);
            }

            return propertyFields;
        }

        public static Dictionary<string, PropertyField> FillDefaultInspector(
            this SerializedProperty root, VisualElement visualElement, bool autoBind = true, params string[] ignored)
        {
            var propertyFields = new Dictionary<string, PropertyField>();

            var list = GetChildren(root)
                       .Where(serializedProperty => !ignored.Contains(serializedProperty.name))
                       .ToList();

            for (var i = 0; i < list.Count; i++)
            {
                var serializedProperty = list[i];

                if (ShouldDrawNZReorderableList(serializedProperty))
                {
                    visualElement.Add(new NZReorderableListField(serializedProperty));
                    continue;
                }

                var tmp = new PropertyField(serializedProperty);
                visualElement.Add(tmp);
                propertyFields.Add(serializedProperty.name, tmp);

                if (autoBind)
                {
                    tmp.BindProperty(serializedProperty);
                }

                if (i == 0)
                {
                    tmp.AddToClassList("first-child");
                }

                if (i == list.Count - 1)
                {
                    tmp.AddToClassList("last-child");
                }

                //Debug.Log($"added {serializedProperty.name}");
            }

            return propertyFields;
        }

        public static Dictionary<string, PropertyField> FillDefaultInspector(
            this SerializedObject serializedObject, VisualElement container, bool autoBind, params string[] ignored)
        {
            if (serializedObject == null)
            {
                return null;
            }

            var iterator = serializedObject.GetIterator();

            if (!iterator.NextVisible(true))
            {
                return null;
            }

            var propertyFields = new Dictionary<string, PropertyField>();

            do
            {
                var ignoreField = iterator.propertyPath == "m_Script";

                foreach (var ignoreString in ignored)
                {
                    if (iterator.name == ignoreString)
                    {
                        ignoreField = true;
                    }
                }

                if (ignoreField)
                {
                    continue;
                }

                if (ShouldDrawNZReorderableList(iterator))
                {
                    container.Add(new NZReorderableListField(iterator.Copy()));
                    continue;
                }

                var propertyField = new PropertyField(iterator)
                {
                    name = iterator.propertyPath,
                    pickingMode = PickingMode.Ignore
                };

                container.Add(propertyField);
                propertyFields.Add(propertyField.name, propertyField);

                if (autoBind)
                {
                    propertyField.BindProperty(iterator);
                }
            } while (iterator.NextVisible(false));

            return propertyFields;
        }

        // ReSharper disable once UnusedMember.Local
        private static Type GetPropertyType(SerializedProperty property)
        {
            var parentType = property.serializedObject.targetObject.GetType();
            var fieldInfo = parentType.GetField(property.propertyPath);
            if (fieldInfo != null)
            {
                return fieldInfo.FieldType;
            }

            return null;
        }

        private static bool ShouldDrawNZReorderableList(SerializedProperty property) =>
            NZReorderableListField.CanDraw(property) && HasAttribute<NZPropertyFieldAttribute>(property);

        private static bool HasAttribute<T>(SerializedProperty property)
            where T : Attribute
        {
            var fieldInfo = GetFieldInfo(property);
            return fieldInfo != null && fieldInfo.GetCustomAttribute<T>() != null;
        }

        private static FieldInfo GetFieldInfo(SerializedProperty property)
        {
            var type = property.serializedObject.targetObject.GetType();
            var path = property.propertyPath.Replace(".Array.data[", "[");
            var elements = path.Split('.');

            FieldInfo fieldInfo = null;
            foreach (var element in elements)
            {
                var fieldName = element;
                var bracketIndex = fieldName.IndexOf('[');
                if (bracketIndex >= 0)
                {
                    fieldName = fieldName[..bracketIndex];
                }

                fieldInfo = GetField(type, fieldName);
                if (fieldInfo == null)
                {
                    return null;
                }

                type = GetElementOrFieldType(fieldInfo.FieldType);
            }

            return fieldInfo;
        }

        private static FieldInfo GetField(Type type, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            while (type != null)
            {
                var fieldInfo = type.GetField(fieldName, flags);
                if (fieldInfo != null)
                {
                    return fieldInfo;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static Type GetElementOrFieldType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type.IsGenericType && type.GetInterfaces()
                                           .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)))
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }
    }
}
