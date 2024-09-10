// <copyright project="NZCore" file="NZPropertyDrawer.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    [CustomPropertyDrawer(typeof(NZPropertyFieldAttribute))]
    public class NZPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var attr = (NZPropertyFieldAttribute)attribute;

            var root = new VisualElement();

            if (attr.Inline)
            {
                Debug.Log("NZPropertyDrawer inline");
                GroupBox groupBox = new GroupBox
                {
                    style =
                    {
                        borderBottomWidth = new StyleFloat(2.0f),
                        borderTopWidth = new StyleFloat(2.0f),
                        borderLeftWidth = new StyleFloat(2.0f),
                        borderRightWidth = new StyleFloat(2.0f),
                        borderBottomColor = new StyleColor(Color.gray),
                        borderTopColor = new StyleColor(Color.gray),
                        borderLeftColor = new StyleColor(Color.gray),
                        borderRightColor = new StyleColor(Color.gray),
                        // foldout.style.marginBottom = new StyleLength(10.0f);
                        // foldout.style.marginTop = new StyleLength(10.0f);
                        // foldout.style.marginLeft = new StyleLength(10.0f);
                        // foldout.style.marginRight = new StyleLength(10.0f);
                        paddingBottom = new StyleLength(10.0f),
                        paddingTop = new StyleLength(10.0f),
                        paddingLeft = new StyleLength(16.0f),
                        paddingRight = new StyleLength(10.0f),
                        borderBottomRightRadius = new StyleLength(5.0f),
                        borderTopRightRadius = new StyleLength(5.0f),
                        borderBottomLeftRadius = new StyleLength(5.0f),
                        borderTopLeftRadius = new StyleLength(5.0f)
                    }
                };


                root.Add(groupBox);

                foreach (var child in property.GetChildren())
                {
                    if (child.name == "name")
                    {
                        Label lbl = new Label(child.stringValue)
                        {
                            style =
                            {
                                unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold),
                                unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.MiddleCenter),
                                fontSize = new StyleLength(17)
                            }
                        };

                        groupBox.Add(lbl);
                    }
                    else
                    {
                        var element = CreatePropertyField(child, property.serializedObject);
                        groupBox.Add(element);
                    }
                }
            }
            else
            {
                Debug.Log("NZPropertyDrawer default");
                var element = CreatePropertyField(property, property.serializedObject);
                root.Add(element);
            }

            return root;
        }

        private static PropertyField CreatePropertyField(SerializedProperty property, SerializedObject serializedObject)
        {
            var field = new PropertyField(property);
            field.Bind(serializedObject);
            return field;
        }
    }
}