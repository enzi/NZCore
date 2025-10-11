// <copyright project="NZCore.UI" file="IDropTarget.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using UnityEngine;

namespace NZCore.UI
{
    public interface IDropTarget
    {
        bool CanAcceptDrop();

        bool DragEnter(IEnumerable<ISelectable> selections);
        bool DragLeave();
        bool DragExited();

        bool DragUpdated(Vector2 mousePosition);
        bool DragPerform(Vector2 mousePosition);
    }
}