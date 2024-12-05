// <copyright project="NZCore.Editor" file="EditorMouseCursor.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Reflection;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    /// <summary>
    /// Settings the cursor via code. Thanks to:
    /// https://discussions.unity.com/t/uielements-style-cursor-in-code/762774/13
    /// <br />
    /// Usage: style.cursor = UnityDefaultCursor.DefaultCursor(UnityDefaultCursor.CursorType.ResizeHorizontal);
    /// </summary>
    public static class EditorMouseCursor
    {
        public enum CursorType
        {
            Arrow = 0,
            Text = 1,
            ResizeVertical = 2,
            ResizeHorizontal = 3,
            Link = 4,
            SlideArrow = 5,
            ResizeUpRight = 6,
            ResizeUpLeft = 7,
            MoveArrow = 8,
            RotateArrow = 9,
            ScaleArrow = 10,
            ArrowPlus = 11,
            ArrowMinus = 12,
            Pan = 13,
            Orbit = 14,
            Zoom = 15,
            FPS = 16,
            CustomCursor = 17,
            SplitResizeUpDown = 18,
            SplitResizeLeftRight = 19
        }

        private static PropertyInfo _defaultCursorId;

        private static PropertyInfo DefaultCursorId
        {
            get
            {
                if (_defaultCursorId != null)
                    return _defaultCursorId;

                _defaultCursorId = typeof(Cursor).GetProperty("defaultCursorId", BindingFlags.NonPublic | BindingFlags.Instance);

                return _defaultCursorId;
            }
        }

        public static Cursor DefaultCursor(CursorType cursorType)
        {
            var ret = (object)new Cursor();
            DefaultCursorId.SetValue(ret, (int)cursorType);
            return (Cursor)ret;
        }
    }
}