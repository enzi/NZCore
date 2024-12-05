// <copyright project="NZCore.UI" file="SplitView.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEngine.UIElements;

namespace NZCore.UI.Elements
{
    [UxmlElement]
    public partial class SplitView : TwoPaneSplitView
    {
    }

    public static class SplitViewExtensions
    {
        private const string draglineAnchorIdentifier = "unity-dragline-anchor";
        
        public static void LinkSplitViews(params SplitView[] splitViews)
        {
            foreach (var splitView in splitViews)
            {
                var anchor = splitView.Q<VisualElement>(draglineAnchorIdentifier);
                
                anchor.RegisterCallback(new EventCallback<PointerMoveEvent>(evt =>
                {
                    if (evt.target.HasPointerCapture(evt.pointerId))
                    {
                        SyncSplitViews(splitView, splitViews);
                    }
                }));
            }
        }

        private static void SyncSplitViews(SplitView source, SplitView[] splitViews)
        {
            float newPos = source.orientation == TwoPaneSplitViewOrientation.Horizontal ? source.fixedPane.style.width.value.value : source.fixedPane.style.height.value.value;
            if (newPos == 0)
            {
                return;
            }

            foreach (var splitView in splitViews)
            {
                if (splitView == source || splitView.fixedPane == null)
                {
                    continue;
                }

                splitView.fixedPane.style.width = new StyleLength(newPos);

                var anchor = splitView.Q<VisualElement>(draglineAnchorIdentifier);
                if (anchor == null)
                {
                    continue;
                }

                if (source.orientation == TwoPaneSplitViewOrientation.Horizontal)
                {
                    anchor.style.left = new StyleLength(newPos);
                }
                else
                {
                    anchor.style.top = new StyleLength(newPos);
                }
            }
        }
    }
}