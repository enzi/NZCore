// <copyright project="NZCore.MVVM" file="BindableModel.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_2023_2_OR_NEWER
using System;
using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

namespace NZCore.MVVM
{
    /// <summary>
    /// Base class for models that need to support UI Toolkit data binding.
    /// Extends ObservableModel with INotifyBindablePropertyChanged support.
    /// </summary>
    [Serializable]
    public abstract class BindableModel : ObservableModel, INotifyBindablePropertyChanged
    {
        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        public override void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            base.OnPropertyChanged(propertyName);
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(propertyName));
        }

        public override void Dispose()
        {
            base.Dispose();
            propertyChanged = null;
        }
    }
}
#endif
