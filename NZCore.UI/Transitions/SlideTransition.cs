// <copyright project="NZCore.UI" file="SlideTransition.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    /// <summary>Slide + fade transition — slides in from <see cref="EnterFrom"/> on enter,
    /// slides out in the opposite direction on exit.</summary>
    public class SlideTransition : IViewTransition
    {
        private readonly SlideDirection _enterFrom;
        private readonly float _durationMs;
        private readonly float _slideLength;

        public SlideTransition(SlideDirection enterFrom, float durationMs, float slideLength)
        {
            _enterFrom = enterFrom;
            _durationMs = durationMs;
            _slideLength = slideLength;
        }

        public void AnimateEnter(VisualElement element)
        {
            element.style.opacity = 0;
            element.style.translate = new StyleTranslate(UIToolkitManager.DirectionToTranslate(_enterFrom, _slideLength));

            // GeometryChangedEvent fires after the first layout pass, guaranteeing opacity=0 and
            // the initial translate are committed before the transition starts.
            element.RegisterCallback<GeometryChangedEvent>(StartEnterTransition);
        }

        private void StartEnterTransition(GeometryChangedEvent evt)
        {
            var element = (VisualElement)evt.target;
            element.UnregisterCallback<GeometryChangedEvent>(StartEnterTransition);
            UIToolkitManager.SetupTransition(element, _durationMs);
            element.style.opacity = 1;
            element.style.translate = new StyleTranslate(new Translate(0, 0));
        }

        public void AnimateExit(VisualElement element, Action onComplete)
        {
            UIToolkitManager.SetupTransition(element, _durationMs);
            element.style.opacity = 0;
            element.style.translate = new StyleTranslate(UIToolkitManager.DirectionToTranslate(UIToolkitManager.OppositeDirection(_enterFrom), _slideLength));
            element.RegisterCallback<TransitionEndEvent>(_ => onComplete());
        }
    }
}
#endif
