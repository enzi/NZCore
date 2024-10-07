// <copyright project="NZCore.Editor" file="ScriptableObjectDropdownDrawer.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using NZCore.AssetManagement;
using NZCore.UIToolkit;
using UnityEditor;
using UnityEditor.UIElements;
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

            List<DropdownWrapper> choices = new List<DropdownWrapper>
            {
                new()
                {
                    DisplayName = "None",
                    Value = null
                }
            };

            ScriptableObject currentSelection = (ScriptableObject)property.boxedValue;

            int selectedIndex = 0;

            for (int i = 0; i < data.Count; i++)
            {
                if (data[i] is not IAutoID)
                    continue;

                if (currentSelection == data[i])
                    selectedIndex = i + 1; // acount for "None"

                choices.Add(new DropdownWrapper()
                {
                    DisplayName = data[i].name,
                    Value = data[i]
                });
            }

            var root = new PropertyField();

            if (attr.UseFlags)
            {
            }
            else
            {
                var popupField = new PopupField<DropdownWrapper>(property.displayName, choices, selectedIndex, FormatSelectedValueCallback, FormatListItemCallback);
                popupField.AddToClassList("unity-toggle__input");

                popupField.RegisterCallback<ChangeEvent<DropdownWrapper>>((evt) =>
                {
                    //var id = evt.newValue.Value == null ? 0 : ((IAutoID) evt.newValue.Value).AutoID;
                    //Debug.Log($"{evt.newValue.DisplayName} -> {id} was picked");

                    property.objectReferenceValue = evt.newValue.Value;
                    property.serializedObject.ApplyModifiedProperties();
                });

                popupField.AlignLabel();

                root.Add(popupField);
            }

            return root;
        }

        private static string FormatListItemCallback(DropdownWrapper arg)
        {
            var id = arg.Value == null ? 0 : ((IAutoID)arg.Value).AutoID;
            return $"{arg.DisplayName} ({id})";
        }

        private static string FormatSelectedValueCallback(DropdownWrapper arg)
        {
            var id = arg.Value == null ? 0 : ((IAutoID)arg.Value).AutoID;
            return $"{arg.DisplayName} ({id})";
        }
    }
}