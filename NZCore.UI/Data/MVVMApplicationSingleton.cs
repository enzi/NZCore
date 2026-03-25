// <copyright project="NZCore.UI" file="MVVMApplicationSingleton.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using NZCore.Interfaces;
using NZCore.MVVM;

namespace NZCore.UIToolkit.Data
{
    public class MVVMApplicationSingleton : IInitSingleton, IDisposable
    {
        public static MVVMApplicationSingleton Instance;
        
        public MVVMApplication App;
        public UIToolkitManager Manager;

        public void Init()
        {
            Instance = this;
            
            App = new MVVMApplication();

            Manager = new UIToolkitManager();
            App.RegisterServices(provider =>
            {
                provider.RegisterSingleton(Manager);
                provider.RegisterSingleton<IVisualAssetStore>(new VisualAssetStore(Manager.Assets.VisualTreeAssets));
            });
        }

        public void Dispose()
        {
            App.Shutdown();
        }

        public IViewFactory GetViewFactory() => App.GetService<IViewFactory>();
    }
}