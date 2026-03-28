// <copyright project="NZCore.UI" file="Draggable.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UI
{
    /// <summary>
    /// Manipulator which monitors Press, Hold and Release events in order to drag visuals.
    /// </summary>
    public class Draggable : Pressable
    {
        private static readonly PropertyInfo IsHandledByDraggablePropertyInfo = typeof(PointerMoveEvent)
            .GetProperty("isHandledByDraggable", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// The direction of the drag.
        /// </summary>
        [Flags]
        public enum DragDirection
        {
            /// <summary>
            /// Horizontal drag.
            /// </summary>
            Horizontal = 1 << 0,

            /// <summary>
            /// Vertical drag.
            /// </summary>
            Vertical = 1 << 1,

            /// <summary>
            /// Free drag.
            /// </summary>
            Free = Horizontal | Vertical
        }

        private readonly Action<Draggable> _downHandler;

        private readonly Action<Draggable> _dragHandler;

        private readonly Action<Draggable> _upHandler;

        private bool _isDown;

        private Vector2 _lastPos = Vector2.zero;

        /// <summary>
        /// Construct a Draggable manipulator.
        /// </summary>
        /// <param name="clickHandler">A callback invoked when a <see cref="ClickEvent"/> has been received.</param>
        /// <param name="dragHandler">A callback invoked during dragging state.</param>
        /// <param name="upHandler">A callback invoked when a <see cref="PointerUpEvent"/> has been received.</param>
        /// <param name="downHandler">A callback invoked when a <see cref="PointerDownEvent"/> has been received.</param>
        public Draggable(Action clickHandler, Action<Draggable> dragHandler, Action<Draggable> upHandler, Action<Draggable> downHandler = null)
            : base(clickHandler)
        {
            _dragHandler = dragHandler;
            _upHandler = upHandler;
            _downHandler = downHandler;

            LongPressDuration = -1;
            KeepEventPropagation = false;
        }

        /// <summary>
        /// The direction of the drag.
        /// </summary>
        public DragDirection CurrentDragDirection { get; set; } = DragDirection.Free;

        /// <summary>
        /// The delta position between the last frame and the current one.
        /// </summary>
        public Vector2 DeltaPos { get; internal set; } = Vector2.zero;

        /// <summary>
        /// The delta position between the start of the drag and the current frame.
        /// </summary>
        public Vector2 DeltaStartPos { get; internal set; } = Vector2.zero;

        /// <summary>
        /// The local position received from the imGui native event.
        /// </summary>
        public Vector2 LocalPosition { get; internal set; }

        /// <summary>
        /// The world position received from the imGui native event.
        /// </summary>
        public Vector2 Position { get; internal set; }

        /// <summary>
        /// The start position of the drag, based on the world position received from the imGui native event.
        /// </summary>
        public Vector2 StartPosition { get; internal set; }

        /// <summary>
        /// Has the pointer moved since the last <see cref="PointerDownEvent"/>.
        /// </summary>
        public bool HasMoved { get; internal set; }

        /// <summary>
        /// The threshold in pixels to start the drag operation.
        /// </summary>
        public float Threshold { get; set; } = 4.0f;

        /// <summary>
        /// Cancel the drag operation.
        /// </summary>
        public void Cancel()
        {
            if (Active)
            {
                target?.Blur();
            }
        }

        /// <summary>
        /// This method processes the down event sent to the target Element.
        /// </summary>
        /// <param name="evt"> The event to process.</param>
        /// <param name="localPosition"> The local position of the pointer.</param>
        /// <param name="pointerId"> The pointer id of the pointer.</param>
        protected override void ProcessDownEvent(EventBase evt, Vector2 localPosition, int pointerId)
        {
            DeltaPos = Vector2.zero;
            DeltaStartPos = Vector2.zero;
            this.LocalPosition = localPosition;
            Position = evt is PointerDownEvent e ? e.position : ((MouseDownEvent)evt).mousePosition;
            _lastPos = Position;
            _isDown = true;
            HasMoved = false;
            StartPosition = Position;

            _downHandler?.Invoke(this);
            base.ProcessDownEvent(evt, localPosition, pointerId);
        }

        /// <summary>
        /// This method processes the up event sent to the target Element.
        /// </summary>
        /// <param name="evt"> The event to process.</param>
        /// <param name="localPosition"> The local position of the pointer.</param>
        /// <param name="pointerId"> The pointer id of the pointer.</param>
        protected override void ProcessUpEvent(EventBase evt, Vector2 localPosition, int pointerId)
        {
            _isDown = false;
            DeltaPos = Vector2.zero;
            DeltaStartPos = Vector2.zero;
            this.LocalPosition = localPosition;
            Position = evt is PointerUpEvent e ? e.position : ((MouseUpEvent)evt).mousePosition;

            _upHandler?.Invoke(this);
            base.ProcessUpEvent(evt, localPosition, pointerId);
        }


        /// <summary>
        /// This method processes the move event sent to the target Element.
        /// </summary>
        /// <param name="evt"> The event to process.</param>
        /// <param name="localPosition"> The local position of the pointer.</param>
        protected override void ProcessMoveEvent(EventBase evt, Vector2 localPosition)
        {
            if (_isDown)
            {
                this.LocalPosition = localPosition;
                Position = evt is PointerMoveEvent e ? e.position : ((MouseMoveEvent)evt).mousePosition;
                DeltaPos = Position - _lastPos;
                DeltaStartPos = Position - StartPosition;
                _lastPos = Position;

                var canDrag =
                    HasMoved ||
                    (evt is PointerMoveEvent pme && pme.pointerId == PointerId.mousePointerId) ||
                    evt is MouseMoveEvent ||
                    IsDraggingInDirection();

                if (canDrag || !HasMoved)
                {
                    if (evt is PointerMoveEvent pointerMoveEvent)
                    {
                        if (pointerMoveEvent.pointerId != PointerId.mousePointerId)
                        {
                            IsHandledByDraggablePropertyInfo.SetValue(evt, true);
                            //pointerMoveEvent.SetIsHandledByDraggable(true);
                        }
                    }
                }

                if (canDrag)
                {
                    _dragHandler?.Invoke(this);
                    HasMoved = true;
                }
            }

            base.ProcessMoveEvent(evt, localPosition);
        }

        private bool IsDraggingInDirection()
        {
            var r = CurrentDragDirection switch
            {
                DragDirection.Horizontal => Mathf.Abs(DeltaStartPos.x) >= Threshold,
                DragDirection.Vertical => Mathf.Abs(DeltaStartPos.y) >= Threshold,
                DragDirection.Free => DeltaStartPos.magnitude >= Threshold,
                _ => false
            };

            if (!r)
            {
                var isCrossDirection = CurrentDragDirection switch
                {
                    DragDirection.Horizontal => Mathf.Abs(DeltaStartPos.y) >= Threshold,
                    DragDirection.Vertical => Mathf.Abs(DeltaStartPos.x) >= Threshold,
                    _ => false
                };

                // if we are dragging in a cross direction, we cancel the drag
                if (isCrossDirection)
                {
                    _isDown = false;
                }
            }

            return r;
        }
    }
}