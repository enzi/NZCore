// <copyright project="NZCore.UI" file="SlideTransition.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000
using System;
using System.Collections.Generic;
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
            element.style.translate = new StyleTranslate(DirectionToTranslate(_enterFrom, _slideLength));

            // GeometryChangedEvent fires after the first layout pass, guaranteeing opacity=0 and
            // the initial translate are committed before the transition starts.
            element.RegisterCallback<GeometryChangedEvent>(StartEnterTransition);
        }

        private void StartEnterTransition(GeometryChangedEvent evt)
        {
            var element = (VisualElement)evt.target;
            element.UnregisterCallback<GeometryChangedEvent>(StartEnterTransition);
            SetupTransition(element, _durationMs);
            element.style.opacity = 1;
            element.style.translate = new StyleTranslate(new Translate(0, 0));
        }

        public void AnimateExit(VisualElement element, Action onComplete)
        {
            SetupTransition(element, _durationMs);
            element.style.opacity = 0;
            element.style.translate = new StyleTranslate(DirectionToTranslate(OppositeDirection(_enterFrom), _slideLength));
            element.RegisterCallback<TransitionEndEvent>(_ => onComplete());
        }
        
        private static void SetupTransition(VisualElement ve, float durationMs)
        {
            ve.style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { new("opacity"), new("translate") });
            ve.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue>
                { new(durationMs, TimeUnit.Millisecond), new(durationMs, TimeUnit.Millisecond) });
            ve.style.transitionTimingFunction = new StyleList<EasingFunction>(new List<EasingFunction> { new(EasingMode.Ease), new(EasingMode.Ease) });
        }

        private static Translate DirectionToTranslate(SlideDirection direction, float percent) => direction switch
        {
            SlideDirection.Left => new Translate(new Length(-percent, LengthUnit.Percent), 0),
            SlideDirection.Right => new Translate(new Length(percent, LengthUnit.Percent), 0),
            SlideDirection.Up => new Translate(0, new Length(-percent, LengthUnit.Percent)),
            SlideDirection.Down => new Translate(0, new Length(percent, LengthUnit.Percent)),
            _ => new Translate(0, 0)
        };

        private static SlideDirection OppositeDirection(SlideDirection direction) => direction switch
        {
            SlideDirection.Left => SlideDirection.Right,
            SlideDirection.Right => SlideDirection.Left,
            SlideDirection.Up => SlideDirection.Down,
            SlideDirection.Down => SlideDirection.Up,
            _ => direction
        };
    }
}
#endif
