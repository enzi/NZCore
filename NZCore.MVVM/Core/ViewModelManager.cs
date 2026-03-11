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
    /// Manages ViewModels and their relationships to Models across multiple RootViewModels.
    /// Provides lookup and creation services for the MVVM framework.
    /// </summary>
    [UsedImplicitly]
    public class ViewModelManager : IViewModelManager
    {
        private readonly Dictionary<Hash128, Model> _modelLookup = new();
        private readonly Dictionary<Hash128, ViewModel> _viewModelLookup = new();
        private readonly Dictionary<RootViewModel, Dictionary<Hash128, ChildViewModel>> _childViewLookups = new();

        /// <summary>
        /// Registers a RootViewModel with the manager.
        /// </summary>
        public void RegisterRootViewModel(RootViewModel rootViewModel)
        {
            if (rootViewModel == null)
            {
                throw new ArgumentNullException(nameof(rootViewModel));
            }

            if (!_childViewLookups.ContainsKey(rootViewModel))
            {
                _childViewLookups[rootViewModel] = new Dictionary<Hash128, ChildViewModel>();
            }

            if (rootViewModel.Model != null)
            {
                rootViewModel.Model.ClearCache();
                _viewModelLookup.TryAdd(rootViewModel.Model.Guid, rootViewModel);
                AddModel(rootViewModel.Model);
            }
        }

        /// <summary>
        /// Unregisters a RootViewModel and cleans up its child ViewModels.
        /// </summary>
        public void UnregisterRootViewModel(RootViewModel rootViewModel)
        {
            if (rootViewModel == null)
            {
                return;
            }

            if (_childViewLookups.TryGetValue(rootViewModel, out var childViewModels))
            {
                foreach (var childViewModel in childViewModels.Values)
                {
                    childViewModel?.Dispose();
                }

                childViewModels.Clear();
                _childViewLookups.Remove(rootViewModel);
            }

            if (rootViewModel.Model != null)
            {
                rootViewModel.Model.ClearCache();
                _viewModelLookup.Remove(rootViewModel.Model.Guid);
                RemoveModel(rootViewModel.Model);
            }
        }

        public void RegisterChildViewModel(ChildViewModel childViewModel, RootViewModel rootViewModel)
        {
            if (childViewModel.Model == null)
            {
                return;
            }

            childViewModel.Model.ClearCache();
            AddModel(childViewModel.Model);

            if (_childViewLookups.TryGetValue(rootViewModel, out var childLookup))
            {
                childLookup.Add(childViewModel.Model.Guid, childViewModel);
            }
            else
            {
                Debug.LogError($"RootViewModel {rootViewModel.GetType().Name} not found!");
            }
        }

        public void UnregisterChildViewModel(ChildViewModel childViewModel, RootViewModel rootViewModel)
        {
            if (childViewModel.Model == null)
            {
                return;
            }

            RemoveModel(childViewModel.Model);
            var childLookup = _childViewLookups[rootViewModel];
            childLookup.Remove(childViewModel.Model.Guid);
        }

        /// <summary>
        /// Adds a model to the lookup system.
        /// </summary>
        public void AddModel(Model model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            _modelLookup.TryAdd(model.Guid, model);
        }

        /// <summary>
        /// Removes a model from the lookup system.
        /// </summary>
        public void RemoveModel(Model model)
        {
            if (model == null)
            {
                return;
            }

            RemoveModel(model.Guid);
        }

        /// <summary>
        /// Removes a model by its GUID from the lookup system.
        /// </summary>
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
                if (childViewLookup.TryGetValue(modelId, out var childViewModel))
                {
                    childViewModel?.Dispose();
                    childViewLookup.Remove(modelId);
                }
            }
        }

        public TChildViewModel GetChildViewModel<TChildViewModel>(Hash128 modelHash, RootViewModel rootViewModel)
            where TChildViewModel : ChildViewModel
        {
            var model = GetModel(modelHash);

            if (model == null)
            {
                Debug.LogError($"Model is null - {modelHash}");
                return null;
            }

            return (TChildViewModel)GetChildViewModel(model, rootViewModel);
        }

        public TChildViewModel GetChildViewModel<TChildViewModel>(Model model, RootViewModel rootViewModel)
            where TChildViewModel : ChildViewModel =>
            (TChildViewModel)GetChildViewModel(model, rootViewModel);

        public ChildViewModel GetChildViewModel(Model model, RootViewModel rootViewModel)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return GetChildViewModel(model.Guid, rootViewModel);
        }

        public ChildViewModel GetChildViewModel(Hash128 modelHash, RootViewModel rootViewModel)
        {
            if (rootViewModel == null)
            {
                throw new ArgumentNullException(nameof(rootViewModel));
            }

            return _childViewLookups[rootViewModel].GetValueOrDefault(modelHash);
        }

        /// <summary>
        /// Gets all child ViewModels for a specific RootViewModel.
        /// </summary>
        public IReadOnlyCollection<ChildViewModel> GetChildViewModels(RootViewModel rootViewModel)
        {
            if (rootViewModel == null)
            {
                return Array.Empty<ChildViewModel>();
            }

            return _childViewLookups.TryGetValue(rootViewModel, out var childViewModels)
                ? childViewModels.Values.ToArray()
                : Array.Empty<ChildViewModel>();
        }

        /// <summary>
        /// Gets all registered RootViewModels.
        /// </summary>
        public IReadOnlyCollection<RootViewModel> GetRootViewModels() => _childViewLookups.Keys.ToArray();

        /// <summary>
        /// Gets a model by its GUID.
        /// </summary>
        public Model GetModel(Hash128 modelId) => _modelLookup.GetValueOrDefault(modelId);

        /// <summary>
        /// Gets a ViewModel by its associated model GUID.
        /// </summary>
        public ViewModel GetViewModel(Hash128 modelId) => _viewModelLookup.GetValueOrDefault(modelId);

        /// <summary>
        /// Clears all lookups and disposes ViewModels.
        /// </summary>
        public void Clear()
        {
            foreach (var viewModel in _viewModelLookup.Values)
            {
                viewModel?.Dispose();
            }

            foreach (var childViewLookup in _childViewLookups.Values)
            {
                foreach (var childViewModel in childViewLookup.Values)
                {
                    childViewModel?.Dispose();
                }
            }

            _modelLookup.Clear();
            _childViewLookups.Clear();
            _viewModelLookup.Clear();
        }

        public void Reset()
        {
            Clear();
        }
    }
}