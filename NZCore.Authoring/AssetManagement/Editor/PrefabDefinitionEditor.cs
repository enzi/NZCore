// <copyright project="NZCore.Authoring" file="PrefabDefinitionEditor.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.AssetManagement;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace NZCore.Editor
{
    [CustomEditor(typeof(PrefabDefinition))]
    public class PrefabDefinitionEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var definition = (PrefabDefinition)target;
            var root = new VisualElement();

            if (definition.Retired)
            {
                root.Add(new HelpBox("This definition is retired. Its IDs stay reserved, so no other definition gets them.", HelpBoxMessageType.Info));
            }

            root.Add(new PropertyField(serializedObject.FindProperty(nameof(PrefabDefinition.Group))));

            var targetProperty = serializedObject.FindProperty(nameof(PrefabDefinition.Target));
            var targetField = new ObjectField(targetProperty.displayName)
            {
                objectType = typeof(Object),
                allowSceneObjects = false,
                value = targetProperty.objectReferenceValue
            };
            targetField.AddToClassList(ObjectField.alignedFieldUssClassName);
            targetField.SetEnabled(!definition.Retired);

            var error = new HelpBox(string.Empty, HelpBoxMessageType.Error) { style = { display = DisplayStyle.None } };

            targetField.RegisterValueChangedCallback(change =>
            {
                if (PrefabDefinitionTooling.TryAssignTarget(definition, change.newValue, out var message))
                {
                    error.style.display = DisplayStyle.None;
                    return;
                }

                targetField.SetValueWithoutNotify(change.previousValue);
                error.text = message;
                error.style.display = DisplayStyle.Flex;
            });
            targetField.TrackPropertyValue(targetProperty, property => targetField.SetValueWithoutNotify(property.objectReferenceValue));

            root.Add(targetField);
            root.Add(error);
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(PrefabDefinition.ReservedIds))));
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(PrefabDefinition.FormerIds))));

            if (!definition.Retired)
            {
                root.Add(new Button(() => PrefabDefinitionRetireWindow.Open(definition)) { text = "Retire..." });
            }

            return root;
        }
    }
}