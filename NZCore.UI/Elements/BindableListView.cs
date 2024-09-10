// <copyright project="NZCore" file="BindableListView.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Properties;
using UnityEngine.UIElements;

#if UNITY_6000
namespace NZCore.UI
{
    [UxmlElement]
    public partial class BindableListView : ListView
    {
        private bool internalChanged;
        
        [UxmlAttribute]
        [CreateProperty]
        public bool changed
        {
            get
            {
                var tmp = internalChanged;
                internalChanged = false;
                return tmp;
            }
            set
            {
                if (!internalChanged && value)
                {
                    internalChanged = true;
                    NotifyPropertyChanged(nameof(changed));
                }
            }
        }
        
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            base.HandleEventBubbleUp(evt);
            
            if (!internalChanged && evt is IChangeEvent)
            {
                schedule.Execute(() =>
                {
                    changed = true;
                }).ExecuteLater(150);
            }
        }
    }
}
#endif