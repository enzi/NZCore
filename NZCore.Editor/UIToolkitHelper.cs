// <copyright project="NZCore.Editor" file="UIToolkitHelper.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
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
            var header = new Label(label)
            {
                style =
                {
                    unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold),
                    paddingTop = new StyleLength(10),
                    paddingBottom = new StyleLength(10)
                }
            };

            return header;
        }
        
        public static void SetPickingModeRecursive(this VisualElement element, PickingMode pickingMode)
        {
            element.pickingMode = pickingMode;
            
            foreach (var child in element.hierarchy.Children()) 
            {
                SetPickingModeRecursive(child, pickingMode);
            }
        }
        
        public static VisualElement GetStructInspector<T>(T data)
            where T : struct
        {
            var content = new PropertyElement();
            content.SetTarget(data);
            return content;
        }
        
        public static unsafe VisualElement GetStructInspector<T>(byte* ptr)
            where T : unmanaged
        {
            ref T data = ref *(T*)ptr;
            var content = new PropertyElement();
            content.SetTarget(data);
            return content;
        }

        /// <summary>
        /// Adds any kind of struct to the VisualElement root
        /// Used in the Entities inspector to show IComps and other data
        /// </summary>
        /// <param name="root"></param>
        /// <param name="data"></param>
        /// <typeparam name="T"></typeparam>
        public static void AddStructInspector<T>(this VisualElement root, T data)
            where T : unmanaged
        {
            root.Add(GetStructInspector(data));
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
            root.Add(new VisualElement() { pickingMode = PickingMode.Ignore}); // just used as a break
            ptr += sizeof(T);
            return ref data;
        }
        
        public static unsafe ref T DrawPropertyDrawer<T>(this VisualElement root, byte* ptr, string propertyName)
            where T : unmanaged
        {
            ref T data = ref *(T*)ptr;
            root.AddBoldLabel(propertyName);
            root.AddStructInspector(data);
            root.Add(new VisualElement() { pickingMode = PickingMode.Ignore}); // just used as a break
            return ref data;
        }

        public static void AddBoldLabel(this VisualElement root, string text)
        {
            var lbl = new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold)
                },
                pickingMode = PickingMode.Ignore
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
            
#if ENABLE_RUNTIME_DATA_BINDINGS
            root.ClearBindings();
#endif
        }

        public static VisualElement AddSpacer(this VisualElement root, int height)
        {
            var spacer = GetSpacer(height);
            root.Add(spacer);
            return spacer;
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

        public static void SetBorder(this VisualElement target, float width)
        {
            target.style.borderLeftWidth = width;
            target.style.borderRightWidth = width;
            target.style.borderTopWidth = width;
            target.style.borderBottomWidth = width;
        }
        
        public static void SetBorderColor(this VisualElement target, Color color)
        {
            target.style.borderTopColor = color;
            target.style.borderBottomColor = color;
            target.style.borderLeftColor = color;
            target.style.borderRightColor = color;
        }


        public static void SetEditorIcon(this VisualElement target, string iconName)
        {
            var guiContent = EditorGUIUtility.TrIconContent(iconName, "Play the timeline");
            var tmpImage = (Texture2D) guiContent.image;
            tmpImage.filterMode = FilterMode.Bilinear;
            target.style.backgroundImage = new StyleBackground(tmpImage);
        }

        public static void AddEditorIcon(this VisualElement target, string iconName, float percentCoverage)
        {
            var child = new VisualElement
            {
                style =
                {
                    position = new StyleEnum<Position>(Position.Absolute),
                    left = 0,
                    right = 0,
                    top = 0,
                    bottom = 0,
                    
                    // replaces unityBackgroundScaleMode as it's obsolete 
                    backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center),
                    backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center),
                    backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat),
                    backgroundSize = new BackgroundSize(new Length(percentCoverage, LengthUnit.Percent), new Length(percentCoverage, LengthUnit.Percent))
                },
            };

            child.SetEditorIcon(iconName);
            
            target.Add(child);
        }
        
        public static void DrawVerticalLineFast(this MeshGenerationContext ctx, float x, float minY, float maxY, Color color, float thickness = 1.0f)
        {
            var mesh = ctx.Allocate(4, 6);

            float halfThickness = thickness * 0.5f;
            
            // TL 0 ... TR 1
            // BL 2 ... BR 3
            
            mesh.SetNextVertex(new Vertex { position = new Vector3(x - halfThickness, minY, 0), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x + halfThickness, minY, 0), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x - halfThickness, maxY, 0), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x + halfThickness, maxY, 0), tint = color });
            
            // needs clockwise :)
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(1);
            mesh.SetNextIndex(2);
            
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(1);
            mesh.SetNextIndex(3);
        }
    }
}