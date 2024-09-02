// <copyright project="NZCore" file="UIToolkitHelper.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using Unity.Entities.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    public static class UIToolkitHelper
    {
        public static VisualElement GetHeaderField(string label)
        {
            var header = new Label(label);
            header.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold);
            header.style.paddingTop = new StyleLength(10);
            header.style.paddingBottom = new StyleLength(10);

            return header;
        }

        /// <summary>
        /// Adds any kind of struct to the VisualElement root
        /// Used in the Entities inspector to show IComps and other data
        /// </summary>
        /// <param name="root"></param>
        /// <param name="data"></param>
        /// <typeparam name="T"></typeparam>
        public static void AddStructInspector<T>(this VisualElement root, T data)
            where T : struct
        {
            var content = new PropertyElement();
            root.Add(content);
            content.SetTarget(data);
        }

        public static VisualElement AlignLabel(this VisualElement root)
        {
            root.AddToClassList(BaseField<TextField>.alignedFieldUssClassName);
            return root;
        }

        public static unsafe ref T DrawPropertyDrawer<T>(this VisualElement root, ref byte* ptr)
            where T : unmanaged
        {
            ref T data = ref *(T*)ptr;
            root.AddBoldLabel($"{typeof(T).Name}");
            root.AddStructInspector(data);
            root.Add(new Label()); // just used as a break
            ptr += sizeof(T);
            return ref data;
        }
        
        public static unsafe ref T DrawPropertyDrawer<T>(this VisualElement root, ref byte* ptr, string propertyName)
            where T : unmanaged
        {
            ref T data = ref *(T*)ptr;
            root.AddBoldLabel(propertyName);
            root.AddStructInspector(data);
            root.Add(new Label()); // just used as a break
            ptr += sizeof(T);
            return ref data;
        }

        public static void AddBoldLabel(this VisualElement root, string text)
        {
            var lbl = new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold)
                }
            };

            root.Add(lbl);
        }
        
        public static void RecreateUI(this SerializedProperty property, VisualElement root, Action<VisualElement, SerializedProperty> createMethod)
        {
            root.ClearUI();

            createMethod(root, property);

            root.Bind(property.serializedObject);
        }
        
        public static void RecreateUI(this SerializedProperty property, VisualElement root, Action createMethod)
        {
            root.ClearUI();

            createMethod();

            root.Bind(property.serializedObject);
        }

        public static void ClearUI(this VisualElement root)
        {
            root.Clear();
            root.ClearBindings();
        }

        public static void AddSpacer(this VisualElement root, int height)
        {
            root.Add(GetSpacer(height));
        }

        public static VisualElement GetSpacer(int height)
        {
            return new VisualElement
            {
                name = "Spacer",
                style =
                {
                    height = height
                }
            };
        }
    }
}