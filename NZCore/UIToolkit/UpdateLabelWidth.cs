// <copyright project="NZCore" file="UpdateLabelWidth.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    /// <summary>
    /// Custom PropertyFields that use their own label and field
    /// have a wrong alignment in the inspector because default PropertyFields
    /// get their label width updated dynamically
    /// This class adds this functionality by calling UpdateLabelWidth
    /// on the root of the VisualElement and providing the Label where the width should be updated
    /// </summary>
    public class LabelWidthUpdater
    {
        private const float Epsilon = 1E-30f;

        private const string USSClassName = "unity-base-field";
        private const string AlignedFieldUssClassName = USSClassName + "__aligned";
        private const string InspectorFieldUssClassName = USSClassName + "__inspector-field";

        private static readonly CustomStyleProperty<float> LabelWidthRatioProperty = new("--unity-property-field-label-width-ratio");
        private static readonly CustomStyleProperty<float> LabelExtraPaddingProperty = new("--unity-property-field-label-extra-padding");
        private static readonly CustomStyleProperty<float> LabelBaseMinWidthProperty = new("--unity-property-field-label-base-min-width");
        private static readonly CustomStyleProperty<float> LabelExtraContextWidthProperty = new("--unity-base-field-extra-context-width");

        private readonly VisualElement root;
        private readonly VisualElement label;

        private VisualElement cachedContextWidthElement;
        private VisualElement cachedInspectorElement;

        private float labelWidthRatio;
        private float labelExtraPadding;
        private float labelBaseMinWidth;
        private float labelExtraContextWidth;

        public LabelWidthUpdater(VisualElement root, VisualElement label, bool addBaseClasses = true)
        {
            this.root = root;
            this.label = label;

            if (addBaseClasses)
            {
                root.AddToClassList(USSClassName);
                root.AddToClassList(AlignedFieldUssClassName);

                label.AddToClassList($"{USSClassName}__label");
                label.AddToClassList("unity-property-field__label");
            }

            root.RegisterCallback(new EventCallback<AttachToPanelEvent>(OnAttachToPanel));
        }

        private void OnAttachToPanel(AttachToPanelEvent e)
        {
            if (e.destinationPanel == null)
            {
                return;
            }

            if (e.destinationPanel.contextType == ContextType.Player)
            {
                return;
            }

            cachedInspectorElement = null;
            cachedContextWidthElement = null;

            var currentElement = label.parent;
            while (currentElement != null)
            {
                if (currentElement.ClassListContains("unity-inspector-element"))
                {
                    cachedInspectorElement = currentElement;
                }

                if (currentElement.ClassListContains("unity-inspector-main-container"))
                {
                    cachedContextWidthElement = currentElement;
                    break;
                }

                currentElement = currentElement.parent;
            }

            if (cachedInspectorElement == null)
            {
                root.RemoveFromClassList(InspectorFieldUssClassName);
                return;
            }

            // These default values are based of IMGUI
            labelWidthRatio = 0.45f;

            // Those values are 40 and 120 in IMGUI, but they already take in account the fields margin. We readjust them
            // because the uitk margin is being taken in account later.
            labelExtraPadding = 37.0f;
            labelBaseMinWidth = 123.0f;

            // The inspector panel has a 1px border we need to consider as part of the context width.
            labelExtraContextWidth = 1.0f;

            root.RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            root.AddToClassList(InspectorFieldUssClassName);
            root.RegisterCallback<GeometryChangedEvent>(OnInspectorFieldGeometryChanged);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(LabelWidthRatioProperty, out var tmpLabelWidthRatio))
            {
                labelWidthRatio = tmpLabelWidthRatio;
            }

            if (evt.customStyle.TryGetValue(LabelExtraPaddingProperty, out var tmpLabelExtraPadding))
            {
                labelExtraPadding = tmpLabelExtraPadding;
            }

            if (evt.customStyle.TryGetValue(LabelBaseMinWidthProperty, out var tmpLabelBaseMinWidth))
            {
                labelBaseMinWidth = tmpLabelBaseMinWidth;
            }

            if (evt.customStyle.TryGetValue(LabelExtraContextWidthProperty, out var tmpLabelExtraContextWidth))
            {
                labelExtraContextWidth = tmpLabelExtraContextWidth;
            }

            AlignLabel();
        }

        private void OnInspectorFieldGeometryChanged(GeometryChangedEvent evt)
        {
            AlignLabel();
        }

        private void AlignLabel()
        {
            if (!root.ClassListContains(AlignedFieldUssClassName) || cachedInspectorElement == null)
            {
                return;
            }

            // Not all visual input controls have the same padding so we can't base our total padding on
            // that information.  Instead we add a flat value to totalPadding to best match the hard coded
            // calculation in IMGUI
            var totalPadding = labelExtraPadding;
            var spacing = root.worldBound.x - cachedInspectorElement.worldBound.x - cachedInspectorElement.resolvedStyle.paddingLeft;

            totalPadding += spacing;
            totalPadding += root.resolvedStyle.paddingLeft;

            var minWidth = labelBaseMinWidth - spacing - root.resolvedStyle.paddingLeft;
            var contextWidthElement = cachedContextWidthElement ?? cachedInspectorElement;

            label.style.minWidth = Mathf.Max(minWidth, 0);

            // Formula to follow IMGUI label width settings
            var newWidth = (contextWidthElement.resolvedStyle.width + labelExtraContextWidth) * labelWidthRatio - totalPadding;
            if (Mathf.Abs(label.resolvedStyle.width - newWidth) > Epsilon)
            {
                label.style.width = Mathf.Max(0f, newWidth);
            }
        }
    }

    public static class LabelWidthUpdaterHelper
    {
        public static void UpdateLabelWidth(this VisualElement root, VisualElement label)
        {
            var _ = new LabelWidthUpdater(root, label);
        }
    }
}