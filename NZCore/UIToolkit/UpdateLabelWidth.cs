// <copyright project="NZCore" file="UpdateLabelWidth.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
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

        private readonly VisualElement _root;
        private readonly VisualElement _label;

        private VisualElement _cachedContextWidthElement;
        private VisualElement _cachedInspectorElement;

        private float _labelWidthRatio;
        private float _labelExtraPadding;
        private float _labelBaseMinWidth;
        private float _labelExtraContextWidth;

        public LabelWidthUpdater(VisualElement root, VisualElement label, bool addBaseClasses = true)
        {
            this._root = root;
            this._label = label;

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

            _cachedInspectorElement = null;
            _cachedContextWidthElement = null;

            var currentElement = _label.parent;
            while (currentElement != null)
            {
                if (currentElement.ClassListContains("unity-inspector-element"))
                {
                    _cachedInspectorElement = currentElement;
                }

                if (currentElement.ClassListContains("unity-inspector-main-container"))
                {
                    _cachedContextWidthElement = currentElement;
                    break;
                }

                currentElement = currentElement.parent;
            }

            if (_cachedInspectorElement == null)
            {
                _root.RemoveFromClassList(InspectorFieldUssClassName);
                return;
            }

            // These default values are based of IMGUI
            _labelWidthRatio = 0.45f;

            // Those values are 40 and 120 in IMGUI, but they already take in account the fields margin. We readjust them
            // because the uitk margin is being taken in account later.
            _labelExtraPadding = 37.0f;
            _labelBaseMinWidth = 123.0f;

            // The inspector panel has a 1px border we need to consider as part of the context width.
            _labelExtraContextWidth = 1.0f;

            _root.RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            _root.AddToClassList(InspectorFieldUssClassName);
            _root.RegisterCallback<GeometryChangedEvent>(OnInspectorFieldGeometryChanged);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(LabelWidthRatioProperty, out var tmpLabelWidthRatio))
            {
                _labelWidthRatio = tmpLabelWidthRatio;
            }

            if (evt.customStyle.TryGetValue(LabelExtraPaddingProperty, out var tmpLabelExtraPadding))
            {
                _labelExtraPadding = tmpLabelExtraPadding;
            }

            if (evt.customStyle.TryGetValue(LabelBaseMinWidthProperty, out var tmpLabelBaseMinWidth))
            {
                _labelBaseMinWidth = tmpLabelBaseMinWidth;
            }

            if (evt.customStyle.TryGetValue(LabelExtraContextWidthProperty, out var tmpLabelExtraContextWidth))
            {
                _labelExtraContextWidth = tmpLabelExtraContextWidth;
            }

            AlignLabel();
        }

        private void OnInspectorFieldGeometryChanged(GeometryChangedEvent evt)
        {
            AlignLabel();
        }

        private void AlignLabel()
        {
            if (!_root.ClassListContains(AlignedFieldUssClassName) || _cachedInspectorElement == null)
            {
                return;
            }

            // Not all visual input controls have the same padding so we can't base our total padding on
            // that information.  Instead we add a flat value to totalPadding to best match the hard coded
            // calculation in IMGUI
            var totalPadding = _labelExtraPadding;
            var spacing = _root.worldBound.x - _cachedInspectorElement.worldBound.x - _cachedInspectorElement.resolvedStyle.paddingLeft;

            totalPadding += spacing;
            totalPadding += _root.resolvedStyle.paddingLeft;

            var minWidth = _labelBaseMinWidth - spacing - _root.resolvedStyle.paddingLeft;
            var contextWidthElement = _cachedContextWidthElement ?? _cachedInspectorElement;

            _label.style.minWidth = Mathf.Max(minWidth, 0);

            // Formula to follow IMGUI label width settings
            var newWidth = (contextWidthElement.resolvedStyle.width + _labelExtraContextWidth) * _labelWidthRatio - totalPadding;
            if (Mathf.Abs(_label.resolvedStyle.width - newWidth) > Epsilon)
            {
                _label.style.width = Mathf.Max(0f, newWidth);
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