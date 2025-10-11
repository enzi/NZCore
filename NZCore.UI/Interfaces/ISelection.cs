using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UI
{
    public interface ISelection
    {
        List<ISelectable> Selections { get; }
        void AddToSelection(ISelectable selectable);
        void RemoveFromSelection(ISelectable selectable);
        void ClearSelection();
    }

    public interface ISelectable
    {
        bool IsSelectable();
        bool HitTest(Vector2 localPoint);
        bool Overlaps(Rect rectangle);
        void Select(VisualElement selectionContainer, bool additive);
        void Unselect(VisualElement selectionContainer);
        bool IsSelected(VisualElement selectionContainer);
    }
}