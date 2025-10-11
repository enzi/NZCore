// <copyright project="NZCore.MVVM" file="ViewModelManager.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace NZCore.MVVM
{
    /// <summary>
    /// Manages ViewModels and their relationships to Models across multiple RootViews.
    /// Provides lookup and creation services for the MVVM framework.
    /// </summary>
    [UsedImplicitly]
    public class ViewModelManager : IViewModelManager
    {
        private readonly Dictionary<Hash128, Model> _modelLookup = new();
        private readonly Dictionary<Hash128, ViewModel> _viewModelLookup = new();
        private readonly Dictionary<RootView, Dictionary<Hash128, ChildView>> _childViewLookups = new();

        /// <summary>
        /// Registers a RootView with the manager, creating its child view lookup.
        /// </summary>
        /// <param name="rootView">The RootView to register.</param>
        /// <param name="model">The model of the RootView</param>
        public void RegisterRootView(RootView rootView)
        {
            if (rootView == null)
            {
                throw new ArgumentNullException(nameof(rootView));
            }

            if (!_childViewLookups.ContainsKey(rootView))
            {
                _childViewLookups[rootView] = new Dictionary<Hash128, ChildView>();
            }

            if (rootView.Model != null)
            {
                rootView.Model.ClearCache();
                _viewModelLookup.TryAdd(rootView.Model.Guid, rootView);
                AddModel(rootView.Model);
            }
        }

        /// <summary>
        /// Unregisters a RootView and cleans up its child views.
        /// </summary>
        /// <param name="rootView">The RootView to unregister.</param>
        public void UnregisterRootView(RootView rootView)
        {
            if (rootView == null)
            {
                return;
            }

            if (_childViewLookups.TryGetValue(rootView, out var childViews))
            {
                // Dispose all child views
                foreach (var childView in childViews.Values)
                {
                    childView?.Dispose();
                }

                childViews.Clear();
                _childViewLookups.Remove(rootView);
            }

            if (rootView.Model != null)
            {
                rootView.Model.ClearCache();
                _viewModelLookup.Remove(rootView.Model.Guid);
                RemoveModel(rootView.Model);
            }
        }

        public void RegisterChildView(ChildView childView, RootView rootView)
        {
            if (childView.Model == null)
            {
                return;
            }

            childView.Model.ClearCache();
            AddModel(childView.Model);

            if (_childViewLookups.TryGetValue(rootView, out var childLookup))
            {
                childLookup.Add(childView.Model.Guid, childView);
            }
            else
            {
                Debug.LogError($"RootView  {rootView.name} not found!");
            }
        }

        public void UnregisterChildView(ChildView childView, RootView rootView)
        {
            if (childView.Model == null)
            {
                return;
            }

            RemoveModel(childView.Model);
            var childLookup = _childViewLookups[rootView];
            childLookup.Remove(childView.Model.Guid);
        }

        /// <summary>
        /// Adds a model to the lookup system.
        /// </summary>
        /// <param name="model">The model to add.</param>
        public void AddModel(Model model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            //Debug.Log($"AddModel: {model.Guid} {typeof(Model).Name}");
            _modelLookup.TryAdd(model.Guid, model);
        }

        /// <summary>
        /// Removes a model from the lookup system.
        /// </summary>
        /// <param name="model">The model to remove.</param>
        public void RemoveModel(Model model)
        {
            if (model == null)
            {
                return;
            }

            //Debug.Log($"RemoveModel: {model.Guid} {typeof(Model).Name}");
            RemoveModel(model.Guid);
        }

        /// <summary>
        /// Removes a model by its GUID from the lookup system.
        /// </summary>
        /// <param name="modelId">The GUID of the model to remove.</param>
        public void RemoveModel(Hash128 modelId)
        {
            _modelLookup.Remove(modelId);

            // Remove associated ViewModels
            if (_viewModelLookup.TryGetValue(modelId, out var viewModel))
            {
                viewModel?.Dispose();
                _viewModelLookup.Remove(modelId);
            }

            // Remove from all child view lookups
            foreach (var childViewLookup in _childViewLookups.Values)
            {
                if (childViewLookup.TryGetValue(modelId, out var childView))
                {
                    childView?.Dispose();
                    childViewLookup.Remove(modelId);
                }
            }
        }

        public TChildView GetChildViewModel<TChildView>(Hash128 modelHash, RootView rootView)
            where TChildView : ChildView
        {
            var model = GetModel(modelHash);

            if (model == null)
            {
                Debug.LogError($"Model is null - {modelHash}");
                return null;
            }
            
            return (TChildView)GetChildViewModel(model, rootView);
        }

        /// <summary>
        /// Gets a ChildView for the specified model and RootView, creating it if necessary.
        /// </summary>
        /// <typeparam name="TChildView">The type of ChildView to get or create.</typeparam>
        /// <param name="model">The model to get the view for.</param>
        /// <param name="rootView">The RootView that will contain the child view.</param>
        /// <returns>The ChildView associated with the model and RootView.</returns>
        public TChildView GetChildViewModel<TChildView>(Model model, RootView rootView)
            where TChildView : ChildView =>
            (TChildView)GetChildViewModel(model, rootView);

        /// <summary>
        /// Gets a ChildView for the specified model and RootView, creating it if necessary.
        /// </summary>
        /// <param name="model">The model to get the view for.</param>
        /// <param name="rootView">The RootView that will contain the child view.</param>
        /// <returns>The ChildView associated with the model and RootView.</returns>
        public ChildView GetChildViewModel(Model model, RootView rootView)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            // Try to find existing child view for this model in this RootView
            return GetChildViewModel(model.Guid, rootView);
        }

        public ChildView GetChildViewModel(Hash128 modelHash, RootView rootView)
        {
            if (rootView == null)
            {
                throw new ArgumentNullException(nameof(rootView));
            }

            // Try to find existing child view for this model in this RootView
            return _childViewLookups[rootView].GetValueOrDefault(modelHash);
        }

        /// <summary>
        /// Gets all child views for a specific RootView.
        /// </summary>
        /// <param name="rootView">The RootView to get child views for.</param>
        /// <returns>A read-only collection of child views.</returns>
        public IReadOnlyCollection<ChildView> GetChildViews(RootView rootView)
        {
            if (rootView == null)
            {
                return Array.Empty<ChildView>();
            }

            return _childViewLookups.TryGetValue(rootView, out var childViews)
                ? childViews.Values.ToArray()
                : Array.Empty<ChildView>();
        }

        /// <summary>
        /// Gets all registered RootViews.
        /// </summary>
        /// <returns>A read-only collection of RootViews.</returns>
        public IReadOnlyCollection<RootView> GetRootViews() => _childViewLookups.Keys.ToArray();

        /// <summary>
        /// Gets a model by its GUID.
        /// </summary>
        /// <param name="modelId">The GUID of the model.</param>
        /// <returns>The model if found; otherwise, null.</returns>
        public Model GetModel(Hash128 modelId) => _modelLookup.GetValueOrDefault(modelId);

        /// <summary>
        /// Gets a ViewModel by its associated model GUID.
        /// </summary>
        /// <param name="modelId">The GUID of the model.</param>
        /// <returns>The ViewModel if found; otherwise, null.</returns>
        public ViewModel GetViewModel(Hash128 modelId) => _viewModelLookup.GetValueOrDefault(modelId);

        /// <summary>
        /// Clears all lookups and disposes ViewModels.
        /// </summary>
        public void Clear()
        {
            // Dispose all ViewModels
            foreach (var viewModel in _viewModelLookup.Values)
            {
                viewModel?.Dispose();
            }

            // Dispose all child views
            foreach (var childViewLookup in _childViewLookups.Values)
            {
                foreach (var childView in childViewLookup.Values)
                {
                    childView?.Dispose();
                }
            }

            _modelLookup.Clear();
            _childViewLookups.Clear();
            _viewModelLookup.Clear();
        }

        // todo prototype method to fully reset
        public void Reset()
        {
            Clear();
        }
    }
}