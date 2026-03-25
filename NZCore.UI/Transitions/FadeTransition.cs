// <copyright project="NZCore.UI" file="FadeTransition.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    /// <summary>Simple opacity fade — fades in on enter, fades out on exit.</summary>
    public class FadeTransition : IViewTransition
    {
        private readonly float _durationMs;

        public FadeTransition(float durationMs = 200f)
        {
            _durationMs = durationMs;
        }

        public void AnimateEnter(VisualElement element)
        {
            element.style.opacity = 0;
            element.RegisterCallback<GeometryChangedEvent>(StartEnterTransition);
        }

        private void StartEnterTransition(GeometryChangedEvent evt)
        {
            var element = (VisualElement)evt.target;
            element.UnregisterCallback<GeometryChangedEvent>(StartEnterTransition);
            SetupOpacityTransition(element, _durationMs);
            element.style.opacity = 1;
        }

        public void AnimateExit(VisualElement element, Action onComplete)
        {
            SetupOpacityTransition(element, _durationMs);
            element.style.opacity = 0;
            element.RegisterCallback<TransitionEndEvent>(_ => onComplete());
        }

        private static void SetupOpacityTransition(VisualElement ve, float durationMs)
        {
            ve.style.transitionProperty = new StyleList<StylePropertyName>(
                new List<StylePropertyName> { new("opacity") });
            ve.style.transitionDuration = new StyleList<TimeValue>(
                new List<TimeValue> { new(durationMs, TimeUnit.Millisecond) });
            ve.style.transitionTimingFunction = new StyleList<EasingFunction>(
                new List<EasingFunction> { new(EasingMode.Ease) });
        }
    }
}
#endif
