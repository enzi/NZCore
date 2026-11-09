// <copyright project="NZCore.Authoring" file="PrefabDefinitionOverrideEditor.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Linq;
using NZCore.AssetManagement;
using Unity.Entities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace NZCore.Editor
{
    [CustomEditor(typeof(PrefabDefinitionOverrideAuthoring))]
    public class PrefabDefinitionOverrideEditor : UnityEditor.Editor
    {
        private VisualElement _root;

        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();
            _root.RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += Rebuild);
            _root.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= Rebuild);
            Rebuild();
            return _root;
        }

        private void Rebuild()
        {
            var authoring = (PrefabDefinitionOverrideAuthoring)target;
            _root.Clear();

            _root.Add(UIToolkitHelper.GetHeaderField("Authoring Components"));
            var overridesField = new ObjectField("Overrides Object") { value = authoring.Overrides, objectType = typeof(GameObject) };
            overridesField.SetEnabled(false);
            _root.Add(overridesField);

            if (authoring.Overrides != null)
            {
                foreach (var component in authoring.Overrides.GetComponents<MonoBehaviour>()
                                                   .Where(component => component is not PrefabDefinitionOverrideObjectAuthoring))
                {
                    var foldout = new Foldout { text = ObjectNames.NicifyVariableName(component.GetType().Name), value = true };
                    foldout.Add(new InspectorElement(component));
                    foldout.Add(new Button(() => Remove(component)) { text = "Remove" });
                    _root.Add(foldout);
                }
            }

            _root.Add(new Button(() => PickAuthoringComponent(authoring)) { text = "Add Authoring Component" });
        }

        private void PickAuthoringComponent(PrefabDefinitionOverrideAuthoring authoring)
        {
            var bakedTypes = TypeCache.GetTypesDerivedFrom<IBaker>().Select(GetAuthoringType).Where(type => type != null).ToHashSet();

            new TypeSearchProvider(typeof(MonoBehaviour), type => bakedTypes.Contains(type) && type != typeof(PrefabDefinitionOverrideObjectAuthoring)).Show(
                item => AddAuthoringComponent(authoring, item?.data as Type),
                items => AddAuthoringComponent(authoring, items is { Length: > 0 } ? items[0].data as Type : null));
        }

        private void AddAuthoringComponent(PrefabDefinitionOverrideAuthoring authoring, Type type)
        {
            if (type == null)
            {
                return;
            }

            if (authoring.Overrides == null)
            {
                var overrides = new GameObject("Overrides");
                overrides.transform.SetParent(authoring.transform, false);
                overrides.AddComponent<PrefabDefinitionOverrideObjectAuthoring>();
                Undo.RegisterCreatedObjectUndo(overrides, "Add Override");
                Edit(authoring, "Add Override", () => authoring.Overrides = overrides);
            }

            Undo.AddComponent(authoring.Overrides, type);
            Rebuild();
        }

        private void Remove(Object component)
        {
            Undo.DestroyObjectImmediate(component);
            Rebuild();
        }

        private void Edit(PrefabDefinitionOverrideAuthoring authoring, string undoName, Action change)
        {
            Undo.RecordObject(authoring, undoName);
            change();
            Rebuild();
        }

        private static Type GetAuthoringType(Type bakerType)
        {
            for (var type = bakerType; type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Baker<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }
}