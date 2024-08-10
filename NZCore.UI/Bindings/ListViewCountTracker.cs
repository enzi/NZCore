// <copyright project="NZCore" file="ListViewCountTracker.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

#if UNITY_6000
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    [UxmlObject]
    public partial class ListViewCountTracker : CustomBinding
    {
        private readonly Dictionary<ListView, int> m_CachedCount = new();

        protected override void OnActivated(in BindingActivationContext context)
        {
            if (context.targetElement is not ListView listView)
                return;

            // Ensures the refresh will be called on the next update
            m_CachedCount[listView] = -1;
        }

        protected override void OnDeactivated(in BindingActivationContext context)
        {
            if (context.targetElement is not ListView listView)
                return;

            m_CachedCount.Remove(listView);
        }

        // todo consider OnDataSourceChanged
        protected override BindingResult Update(in BindingContext context)
        {
            if (context.targetElement is not ListView listView)
                return new BindingResult(BindingStatus.Failure, "'ListViewCountTracker' should only be added to a 'ListView'");

            if (!m_CachedCount.TryGetValue(listView, out var previousCount) || previousCount == listView.itemsSource?.Count)
                return new BindingResult(BindingStatus.Failure, "");

            listView.RefreshItems();
            m_CachedCount[listView] = listView.itemsSource?.Count ?? -1;

            return new BindingResult(BindingStatus.Success);
        }
    }
}
#endif