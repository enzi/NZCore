// <copyright project="NZCore.Authoring" file="PrefabIdDrawer.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.AssetManagement;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    [CustomPropertyDrawer(typeof(PrefabId))]
    public class PrefabIdDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var value = property.FindPropertyRelative(nameof(PrefabId.Value));

            var root = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };

            var label = new Label(property.displayName)
            {
                tooltip = property.tooltip,
                style =
                {
                    minWidth = 120,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    flexShrink = 0
                }
            };

            var group = CreateField("Group");
            var id = CreateField("ID");

            var owner = new Label
            {
                style =
                {
                    flexGrow = 2,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            void Refresh()
            {
                var prefabId = new PrefabId { Value = value.intValue };
                group.SetValueWithoutNotify(prefabId.Group);
                id.SetValueWithoutNotify(prefabId.Id);
                owner.text = OwnerText(prefabId);
            }

            void Apply(ChangeEvent<int> _)
            {
                if (group.value == 0 && id.value == 0)
                {
                    value.intValue = 0;
                }
                else if (group.value is >= 1 and <= PrefabId.MaxGroup && id.value is >= 1 and <= PrefabId.MaxId)
                {
                    value.intValue = new PrefabId(group.value, id.value).Value;
                }
                else
                {
                    Refresh();
                    return;
                }

                value.serializedObject.ApplyModifiedProperties();
                Refresh();
            }

            group.RegisterValueChangedCallback(Apply);
            id.RegisterValueChangedCallback(Apply);
            root.TrackPropertyValue(value, _ => Refresh());
            Refresh();

            root.Add(label);
            root.Add(group);
            root.Add(id);
            root.Add(owner);
            return root;
        }

        private static IntegerField CreateField(string fieldLabel)
        {
            var field = new IntegerField(fieldLabel)
            {
                isDelayed = true,
                style =
                {
                    flexGrow = 1
                }
            };

            field.labelElement.style.minWidth = 0;
            return field;
        }

        private static string OwnerText(PrefabId prefabId)
        {
            if (prefabId.IsNull)
            {
                return string.Empty;
            }

            if (!PrefabDefinition.TryFindOwner(prefabId, out var owner, out var isFormerId))
            {
                return "No definition";
            }

            return isFormerId ? $"{owner.name} (former ID)" : owner.name;
        }
    }
}