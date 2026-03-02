// <copyright project="NZCore.UI" file="MVVMApplicationSingleton.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Interfaces;
using NZCore.MVVM;
using Unity.Entities;

namespace NZCore.UIToolkit.Data
{
    public class MVVMApplicationSingleton : IComponentData, IInitSingleton, IDisposable
    {
        public MVVMApplication App;
        public UIToolkitManager Manager;

        public void Init()
        {
            App = new MVVMApplication();
            Manager = new UIToolkitManager();

            App.RegisterServices((provider =>
            {
                provider.RegisterSingleton(Manager);
            }));
        }
        
        public void Dispose()
        {
            App.Shutdown();
        }

        public IViewFactory GetViewFactory()
        {
            return App.GetService<IViewFactory>();
        }
    }
}