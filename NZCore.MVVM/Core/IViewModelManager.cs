// <copyright project="NZCore.MVVM" file="IViewModelManager.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using UnityEngine;

namespace NZCore.MVVM
{
    public interface IViewModelManager
    {
        public void AddModel(Model model);
        public void RemoveModel(Model model);
        public void RemoveModel(Hash128 modelId);
        
        public void RegisterRootView(RootView rootView);
        public void UnregisterRootView(RootView rootView);
        
        public void RegisterChildView(ChildView childView, RootView rootView);
        public void UnregisterChildView(ChildView childView, RootView rootView);

        public ChildView GetChildViewModel(Hash128 modelHash, RootView rootView);
        public ChildView GetChildViewModel(Model model, RootView rootView);
        
        public TChildView GetChildViewModel<TChildView>(Hash128 modelHash, RootView rootView)
            where TChildView : ChildView;
        public TChildView GetChildViewModel<TChildView>(Model model, RootView rootView)
            where TChildView : ChildView;
        
        public IReadOnlyCollection<ChildView> GetChildViews(RootView rootView);
        public IReadOnlyCollection<RootView> GetRootViews();
        public Model GetModel(Hash128 modelId);
        public ViewModel GetViewModel(Hash128 modelId);
        public void Clear();
        public void Reset();
    }
}