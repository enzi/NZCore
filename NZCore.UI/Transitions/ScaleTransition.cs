// <copyright project="NZCore.UI" file="ScaleTransition.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    public class ScaleTransition : IViewTransition
    {
        private readonly float _durationMs;

        public ScaleTransition(float durationMs = 150f)
        {
            _durationMs = durationMs;
        }

        public void AnimateEnter(VisualElement element)
        {
            element.style.scale = new StyleScale(new Scale(Vector2.zero));
            element.style.opacity = 0;
            element.RegisterCallback<GeometryChangedEvent>(StartEnterTransition);
        }

        private void StartEnterTransition(GeometryChangedEvent evt)
        {
            var element = (VisualElement)evt.target;
            element.UnregisterCallback<GeometryChangedEvent>(StartEnterTransition);
            SetupTransition(element, _durationMs);
            element.style.scale = new StyleScale(new Scale(Vector2.one));
            element.style.opacity = 1;
        }

        public void AnimateExit(VisualElement element, Action onComplete)
        {
            SetupTransition(element, _durationMs);
            element.style.scale = new StyleScale(new Scale(Vector2.zero));
            element.style.opacity = 0;
            element.RegisterCallback<TransitionEndEvent>(_ => onComplete());
        }

        public static void SetupTransition(VisualElement ve, float durationMs)
        {
            ve.style.transitionProperty = new StyleList<StylePropertyName>(
                new List<StylePropertyName> { new("scale"), new("opacity") });
            ve.style.transitionDuration = new StyleList<TimeValue>(
                new List<TimeValue> { new(durationMs, TimeUnit.Millisecond), new(durationMs, TimeUnit.Millisecond)
                });
            ve.style.transitionTimingFunction = new StyleList<EasingFunction>(
                new List<EasingFunction> { new(EasingMode.EaseOut), new(EasingMode.EaseOut) });
        }
    }
}
#endif