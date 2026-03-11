// <copyright project="NZCore.MVVM" file="IViewModelManager.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using UnityEngine;

namespace NZCore.MVVM
{
    public interface IViewModelManager
    {
        void AddModel(Model model);
        void RemoveModel(Model model);
        void RemoveModel(Hash128 modelId);

        void RegisterRootViewModel(RootViewModel rootViewModel);
        void UnregisterRootViewModel(RootViewModel rootViewModel);

        void RegisterChildViewModel(ChildViewModel childViewModel, RootViewModel rootViewModel);
        void UnregisterChildViewModel(ChildViewModel childViewModel, RootViewModel rootViewModel);

        ChildViewModel GetChildViewModel(Hash128 modelHash, RootViewModel rootViewModel);
        ChildViewModel GetChildViewModel(Model model, RootViewModel rootViewModel);

        TChildViewModel GetChildViewModel<TChildViewModel>(Hash128 modelHash, RootViewModel rootViewModel)
            where TChildViewModel : ChildViewModel;

        TChildViewModel GetChildViewModel<TChildViewModel>(Model model, RootViewModel rootViewModel)
            where TChildViewModel : ChildViewModel;

        IReadOnlyCollection<ChildViewModel> GetChildViewModels(RootViewModel rootViewModel);
        IReadOnlyCollection<RootViewModel> GetRootViewModels();
        Model GetModel(Hash128 modelId);
        ViewModel GetViewModel(Hash128 modelId);
        void Clear();
        void Reset();
    }
}