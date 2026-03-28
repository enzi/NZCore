// <copyright project="NZCore.UI" file="Pressable.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UI
{
    public class Pressable : PointerManipulator
    {
        /// <summary>
        /// The event invoked when the element is pressed.
        /// </summary>
        public event Action Clicked;

        /// <summary>
        /// The event invoked when the element is pressed.
        /// </summary>
        public event Action<EventBase> ClickedWithEventInfo;

        /// <summary>
        /// The event invoked when the element is pressed for a long time.
        /// </summary>
        public event Action LongClicked;

        /// <summary>
        /// Check if the element is currently pressed.
        /// </summary>
        public bool Active { get; private set; }

        /// <summary>
        /// The duration of a long press in milliseconds.
        /// <para>
        /// The default value is -1.
        /// </para>
        /// <remarks>
        /// Using a negative value will disable long press.
        /// </remarks>
        /// </summary>
        public int LongPressDuration { get; set; } = -1;

        /// <summary>
        /// When true, the event propagation will not be stopped when the element is pressed.
        /// </summary>
        public bool KeepEventPropagation { get; set; } = true;

        private Event _mMoveEvent;

        private Touch _mTouchMoveEvent;

        private Event _mUpEvent;

        private Touch _mTouchUpEvent;

        private IVisualElementScheduledItem _mDeferDeactivate;

        private IVisualElementScheduledItem _mDeferLongPress;

        /// <summary>
        /// Constructor.
        /// </summary>
        public Pressable()
        {
            _mMoveEvent = new Event { type = EventType.MouseMove };
            _mTouchMoveEvent = new Touch { phase = TouchPhase.Moved };
            _mUpEvent = new Event { type = EventType.MouseUp };
            _mTouchUpEvent = new Touch { phase = TouchPhase.Ended };

            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Alt });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Control });
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="handler"> The event handler to register with the Pressed event.</param>
        public Pressable(Action handler)
            : this()
        {
            Clicked += handler;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="handler"> The event handler to register with the Pressed event.</param>
        public Pressable(Action<EventBase> handler)
            : this()
        {
            ClickedWithEventInfo += handler;
        }

        /// <summary>
        /// Invoke the Pressed event.
        /// </summary>
        /// <param name="evt">The base event to use to invoke the press.</param>
        public void InvokePressed(EventBase evt) => Invoke(evt);

        private void Invoke(EventBase evt)
        {
            Clicked?.Invoke();
            ClickedWithEventInfo?.Invoke(evt);
            PostProcessDisabledState();
        }

        /// <summary>
        /// Invoke the LongPressed event.
        /// </summary>
        public void InvokeLongPressed()
        {
            LongClicked?.Invoke();
            PostProcessDisabledState();
        }

        private void PostProcessDisabledState()
        {
            if (!target.enabledInHierarchy)
            {
                // the element is no more enabled, remove the active and hovered states
                Deactivate(_mPointerId);
                //RemoveHoverState();
            }
        }

        // /// <summary>
        // /// Simulate a single click on the target element.
        // /// </summary>
        // /// <param name="evt">The base event to use to invoke the click.</param>
        // internal void SimulateSingleClickInternal(EventBase evt)
        // {
        //     if (target != null)
        //     {
        //         var pseudoStates = target.GetPseudoStates();
        //         target.SetPseudoStates(pseudoStates | PseudoStates.Active);
        //         target.AddToClassList(Styles.activeUssClassName);
        //         target.schedule
        //             .Execute(() =>
        //             {
        //                 target.SetPseudoStates(target.GetPseudoStates() & ~PseudoStates.Active);
        //                 target.RemoveFromClassList(Styles.activeUssClassName);
        //             })
        //             .ExecuteLater(16L);
        //     }
        //     InvokePressed(evt);
        // }

        // /// <summary>
        // /// Force the active pseudo state on the target element.
        // /// </summary>
        // public void ForceActivePseudoState()
        // {
        //     if (target != null)
        //     {
        //         var pseudoStates = target.GetPseudoStates();
        //         target.SetPseudoStates(pseudoStates | PseudoStates.Active);
        //         target.AddToClassList(Styles.activeUssClassName);
        //     }
        // }

        /// <summary>
        /// Called to register event callbacks from the target element.
        /// </summary>
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
#if !UNITY_2023_1_OR_NEWER
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
#endif
            //target.RegisterCallback<KeyDownEvent>(OnKeyDown);
            //target.RegisterCallback<KeyUpEvent>(OnKeyUp);
        }

        /// <summary>
        /// Called to unregister event callbacks from the target element.
        /// </summary>
        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
#if !UNITY_2023_1_OR_NEWER
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
#endif
            // target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            // target.UnregisterCallback<KeyUpEvent>(OnKeyUp);
        }

        /// <summary>
        /// Custom handling of pointer enter events.
        /// </summary>
        /// <param name="evt"> The event to process.</param>
        /// <param name="localPos"> The local position of the pointer.</param>
        /// <param name="pointerId"> The pointer id.</param>
        protected virtual void ProcessDownEvent(EventBase evt, Vector2 localPos, int pointerId) { }

        /// <summary>
        /// Custom handling of pointer leave events.
        /// </summary>
        /// <param name="evt"> The event to process.</param>
        /// <param name="localPos"> The local position of the pointer.</param>
        /// <param name="pointerId"> The pointer id.</param>
        protected virtual void ProcessUpEvent(EventBase evt, Vector2 localPos, int pointerId) { }

        /// <summary>
        /// Custom handling of pointer move events.
        /// </summary>
        /// <param name="evt"> The event to process.</param>
        /// <param name="localPos"> The local position of the pointer.</param>
        protected virtual void ProcessMoveEvent(EventBase evt, Vector2 localPos) { }

        // void OnKeyDown(KeyDownEvent evt)
        // {
        //     if (evt.keyCode.IsSubmitType())
        //     {
        //         Activate(PointerId.mousePointerId);
        //         evt.StopPropagation();
        //     }
        // }
        //
        // void OnKeyUp(KeyUpEvent evt)
        // {
        //     if (evt.keyCode.IsSubmitType())
        //     {
        //         InvokePressed(evt);
        //         Deactivate(PointerId.mousePointerId);
        //         evt.StopPropagation();
        //     }
        // }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            if (!target.enabledInHierarchy)
            {
                return;
            }

            //if (evt.pointerId == PointerId.mousePointerId)
            //     AddHoveredState();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            //RemoveHoverState();
        }

        // void AddHoveredState()
        // {
        //     var pseudoStates = target.GetPseudoStates();
        //     target.SetPseudoStates(pseudoStates | PseudoStates.Hover);
        //     target.AddToClassList(Styles.hoveredUssClassName);
        // }
        //
        // void RemoveHoverState()
        // {
        //     var pseudoStates = target.GetPseudoStates();
        //     target.SetPseudoStates(pseudoStates & ~PseudoStates.Hover);
        //     target.RemoveFromClassList(Styles.hoveredUssClassName);
        // }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!CanStartManipulation(evt))
            {
                return;
            }

            Activate(evt.pointerId);
            ProcessDownEvent(evt, evt.localPosition, evt.pointerId);
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (Active)
            {
                if (!target.HasMouseCapture())
                {
                    target.CaptureMouse();
                }

                evt.StopPropagation();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!CanStopManipulation(evt))
            {
                return;
            }

            var parent = target?.parent;
            if (parent == null)
            {
                return;
            }

            ProcessMoveEvent(evt, evt.localPosition);

            if (!Active)
            {
                return;
            }

            if (!KeepEventPropagation)
            {
                return;
            }

            _mMoveEvent.mousePosition = evt.originalMousePosition;
            _mMoveEvent.delta = evt.deltaPosition;
            _mMoveEvent.button = evt.button;
            _mMoveEvent.modifiers = evt.modifiers;
            _mMoveEvent.pressure = evt.pressure;
            _mMoveEvent.clickCount = evt.clickCount;

            _mTouchMoveEvent.fingerId = evt.pointerId - PointerId.touchPointerIdBase;
            _mTouchMoveEvent.position = evt.position;
            _mTouchMoveEvent.deltaPosition = evt.deltaPosition;
            _mTouchMoveEvent.deltaTime = evt.deltaTime;
            _mTouchMoveEvent.tapCount = evt.clickCount;
            _mTouchMoveEvent.pressure = evt.pressure;
            _mTouchMoveEvent.azimuthAngle = evt.azimuthAngle;
            _mTouchMoveEvent.altitudeAngle = evt.altitudeAngle;
            _mTouchMoveEvent.radius = evt.radius.x;
            _mTouchMoveEvent.radiusVariance = evt.radiusVariance.x;

            using var e = evt.pointerId == PointerId.mousePointerId
                ? PointerMoveEvent.GetPooled(_mMoveEvent)
                : PointerMoveEvent.GetPooled(_mTouchMoveEvent, evt.modifiers);
            e.target = parent;
            parent.SendEvent(e);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!CanStopManipulation(evt))
            {
                return;
            }

            ProcessUpEvent(evt, evt.localPosition, evt.pointerId);

            if (!Active)
            {
                return;
            }

            InvokePressed(evt);
            Deactivate(evt.pointerId);

            var parent = target?.parent;
            if (parent == null || !KeepEventPropagation)
            {
                return;
            }

            _mUpEvent.mousePosition = evt.originalMousePosition;
            _mUpEvent.delta = evt.deltaPosition;
            _mUpEvent.button = evt.button;
            _mUpEvent.modifiers = evt.modifiers;
            _mUpEvent.pressure = evt.pressure;
            _mUpEvent.clickCount = evt.clickCount;

            _mTouchUpEvent.fingerId = evt.pointerId - PointerId.touchPointerIdBase;
            _mTouchUpEvent.position = evt.position;
            _mTouchUpEvent.deltaPosition = evt.deltaPosition;
            _mTouchUpEvent.deltaTime = evt.deltaTime;
            _mTouchUpEvent.tapCount = evt.clickCount;
            _mTouchUpEvent.pressure = evt.pressure;
            _mTouchUpEvent.azimuthAngle = evt.azimuthAngle;
            _mTouchUpEvent.altitudeAngle = evt.altitudeAngle;
            _mTouchUpEvent.radius = evt.radius.x;
            _mTouchUpEvent.radiusVariance = evt.radiusVariance.x;

            using var e = evt.pointerId == PointerId.mousePointerId
                ? PointerUpEvent.GetPooled(_mUpEvent)
                : PointerUpEvent.GetPooled(_mTouchUpEvent, evt.modifiers);
            e.target = parent;
            parent.SendEvent(e);
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            Deactivate(evt.pointerId);
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            Deactivate(evt.pointerId);
        }

        private void Activate(int pointerId)
        {
            if (!target.HasPointerCapture(pointerId))
            {
                target.CapturePointer(pointerId);
#if !UNITY_2023_1_OR_NEWER
                if (pointerId == PointerId.mousePointerId)
                    target.CaptureMouse();
#endif
            }

            //ForceActivePseudoState();
            //target.AddToClassList(Styles.activeUssClassName);
            _mPointerId = pointerId;
            _mDeferDeactivate = target.schedule.Execute(DeferDeactivate);
            _mDeferDeactivate.ExecuteLater(50L);
            _mDeferLongPress?.Pause();
            _mDeferLongPress = null;
            if (LongPressDuration > 0)
            {
                _mDeferLongPress = target.schedule.Execute(OnLongPress);
                _mDeferLongPress.ExecuteLater(LongPressDuration);
            }

            Active = true;
        }

        private void Deactivate(int pointerId)
        {
            Active = false;

            if (target.HasPointerCapture(pointerId))
            {
                target.ReleasePointer(pointerId);
            }

            if (_mDeferDeactivate != null)
            {
                return;
            }

            // var pseudoStates = target.GetPseudoStates();
            // target.SetPseudoStates(pseudoStates & ~PseudoStates.Active);
            //target.RemoveFromClassList(Styles.activeUssClassName);
        }

        private int _mPointerId;

        private void DeferDeactivate()
        {
            _mDeferDeactivate = null;
            if (!Active)
            {
                Deactivate(_mPointerId);
            }
        }

        private void OnLongPress()
        {
            _mDeferLongPress?.Pause();
            _mDeferLongPress = null;
            if (Active)
            {
                InvokeLongPressed();
                Deactivate(_mPointerId);
            }
        }
    }
}