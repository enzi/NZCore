// <copyright project="NZCore.MVVM" file="BindableViewModel.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_2023_2_OR_NEWER
using System;
using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

namespace NZCore.MVVM
{
    /// <summary>
    /// Base class for ViewModels that need to support UI Toolkit data binding.
    /// Extends ViewModel with INotifyBindablePropertyChanged support.
    /// </summary>
    public abstract class BindableViewModel : ViewModel, INotifyBindablePropertyChanged
    {
        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        public override void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            base.OnPropertyChanged(propertyName);
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(propertyName));
        }

        internal override void OnRegisterViewModel() { }
        internal override void OnUnregisterViewModel() { }

        protected override void OnDispose()
        {
            base.OnDispose();
            propertyChanged = null;
        }
    }
}
#endif
