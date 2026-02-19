// <copyright project="NZCore.Editor" file="ScriptableObjectDropdownDrawer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.AssetManagement;
using NZCore.UIToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    [CustomPropertyDrawer(typeof(ScriptableObjectDropdownAttribute))]
    public class ScriptableObjectDropdownDrawer : PropertyDrawer
    {
        private LabelWidthUpdater labelWidthUpdater;

        private class DropdownWrapper
        {
            public ScriptableObject Value;
            public string DisplayName;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var baseType = property.type.Substring(6, property.type.Length - 7); // PPtr<$Schema_SpellCasterClass>
            //Debug.Log(baseType);

            ScriptableObjectDropdownAttribute attr = (ScriptableObjectDropdownAttribute)attribute;

            List<ScriptableObject> data = AssetDatabaseUtility.GetSubAssets(baseType);

            var choices = new List<DropdownWrapper>
            {
                new() { DisplayName = "None", Value = null }
            };

            ScriptableObject currentSelection = (ScriptableObject)property.boxedValue;
            int selectedIndex = 0;

            for (int i = 0; i < data.Count; i++)
            {
                if (data[i] is not IAutoID)
                    continue;

                choices.Add(new DropdownWrapper()
                {
                    DisplayName = data[i].name,
                    Value = data[i]
                });

                if (currentSelection == data[i])
                    selectedIndex = choices.Count - 1;
            }

            if (attr.UseFlags)
            {
                return new VisualElement();
            }

            // Use BaseField layout with a popup-styled button + GenericDropdownMenu
            var root = new VisualElement();
            root.AddToClassList("unity-base-field");
            root.style.flexDirection = FlexDirection.Row;
            root.style.flexGrow = 1;

            var label = new Label(property.displayName);
            label.AddToClassList("unity-base-field__label");
            root.Add(label);

            var inputContainer = new VisualElement();
            inputContainer.AddToClassList("unity-base-field__input");
            inputContainer.AddToClassList("unity-base-popup-field__input");
            inputContainer.AddToClassList("unity-popup-field__input");
            inputContainer.AddToClassList("unity-property-field__input");
            inputContainer.style.flexDirection = FlexDirection.Row;
            inputContainer.style.flexGrow = 1;

            var selectedChoice = choices[selectedIndex];
            var buttonText = FormatDisplayString(selectedChoice);

            var textElement = new TextElement { text = buttonText };
            textElement.AddToClassList("unity-text-element");
            textElement.AddToClassList("unity-popup-field__label");
            textElement.style.flexGrow = 1;
            textElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            textElement.style.overflow = Overflow.Hidden;
            textElement.style.textOverflow = TextOverflow.Ellipsis;
            inputContainer.Add(textElement);

            var arrow = new VisualElement();
            arrow.AddToClassList("unity-base-popup-field__arrow");
            inputContainer.Add(arrow);

            inputContainer.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
            });

            root.Add(inputContainer);

            inputContainer.RegisterCallback<PointerDownEvent>(evt =>
            {
                var menu = new GenericDropdownMenu();

                for (int i = 0; i < choices.Count; i++)
                {
                    var choice = choices[i];
                    var isSelected = property.objectReferenceValue == choice.Value;
                    var displayText = FormatDisplayString(choice);

                    menu.AddItem(displayText, isSelected, () =>
                    {
                        property.objectReferenceValue = choice.Value;
                        property.serializedObject.ApplyModifiedProperties();
                        textElement.text = displayText;
                    });
                }

                menu.DropDown(inputContainer.worldBound, inputContainer, DropdownMenuSizeMode.Auto);
            });

            labelWidthUpdater = new LabelWidthUpdater(root, label);

            return root;
        }

        private static string FormatDisplayString(DropdownWrapper arg)
        {
            var id = arg.Value == null ? 0 : ((IAutoID)arg.Value).AutoID;
            return $"{arg.DisplayName} ({id})";
        }
    }
}