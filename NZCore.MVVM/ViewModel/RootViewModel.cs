// <copyright project="NZCore.MVVM" file="RootViewModel.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

namespace NZCore.MVVM
{
    /// <summary>
    /// Base class for root-level ViewModels that manage application or major section navigation.
    /// Pure C# — no VisualElement dependency.
    /// </summary>
    public abstract class RootViewModel : ViewModel
    {
        protected override void OnInitialize()
        {
            base.OnInitialize();
            ViewModelManager.RegisterRootViewModel(this);
        }

        internal override void OnRegisterViewModel()
        {
            ViewModelManager.RegisterRootViewModel(this);
        }

        internal override void OnUnregisterViewModel()
        {
            ViewModelManager.UnregisterRootViewModel(this);
        }

        protected override void OnDispose()
        {
            base.OnDispose();
        }
    }
}