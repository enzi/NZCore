// <copyright project="NZCore.UI" file="ActionBinding.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

#if UNITY_6000
namespace NZCore.UIToolkit
{
    [UxmlObject]
    public partial class ActionBinding : CustomBinding, IDataSourceProvider
    {
        // Caching the delegate used for cleanup purposes.
        private readonly Dictionary<VisualElement, Action> cachedDelegates = new();

        public object dataSource => null;

        [CreateProperty] 
        public PropertyPath dataSourcePath { get; private set; }

        [UxmlAttribute("data-source-path")]
        public string DataSourcePathString
        {
            get => dataSourcePath.ToString();
            set => dataSourcePath = new PropertyPath(value);
        }

        protected override void OnDataSourceChanged(in DataSourceContextChanged context)
        {
            if (context.targetElement is not Button button)
            {
                return;
            }

            if (cachedDelegates.TryGetValue(button, out var action))
            {
                button.clicked -= action;
                cachedDelegates.Remove(button);
            }

            // Extract the `Action` from the hierarchy and register it.
            var source = context.newContext.dataSource;

            if (source == null)
            {
                return;
            }

            var path = context.newContext.dataSourcePath;

            if (PropertyContainer.TryGetValue<object, bool>(ref source, in path, out _))
            {
                action = () => PropertyContainer.TrySetValue(ref source, in path, true);
            }
            else if (PropertyContainer.TryGetValue(ref source, in path, out action))
            {
            }
            else
            {
                throw new NotImplementedException();
            }

            button.clicked += action;
            cachedDelegates.Add(button, action);
        }
    }
}
#endif