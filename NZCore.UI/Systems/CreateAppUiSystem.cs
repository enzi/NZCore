// <copyright project="NZCore.UI" file="CreateAppUiSystem.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NZCore.MVVM;
using NZCore.UIToolkit.Data;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CreateAppUiSystem : SystemBase
    {
        private MVVMApplication _app;
        
        protected override void OnCreate() { }

        protected override void OnUpdate()
        {
            var uiDoc = MonoBehaviour.FindAnyObjectByType<UIDocument>();

            if (uiDoc != null && uiDoc.rootVisualElement != null)
            {
                _app = CheckedStateRef.CreateManagedSingleton<MVVMApplicationSingleton>().App;
            }
            
            Enabled = false;
        }
    }
}