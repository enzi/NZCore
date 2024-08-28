// <copyright file="SearchWindow.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BovineLabs.Core.Editor.SearchWindow
{
    /// <summary> Copy of com.unity.platforms\Editor\Unity.Build.Editor\SearchWindow\SearchWindow.cs. </summary>
    public class SearchWindow : EditorWindow
    {
        public const string RootUIPath = "Packages/com.enzisoft.nzcore/NZCore.Editor/Editor Default Resources/SearchWindow/";

        private SearchView searchView;

        public event Action<SearchView.Item> OnSelection;

        public event Action OnClose;

        public List<SearchView.Item> Items
        {
            get => searchView.Items;
            set => searchView.Items = value;
        }

        public string Title
        {
            get => searchView.Title;
            set => searchView.Title = value;
        }

        public static SearchWindow Create()
        {
            var window = CreateInstance<SearchWindow>();
            return window;
        }

        private void OnEnable()
        {
            searchView = new SearchView();
            rootVisualElement.Add(searchView);
            rootVisualElement.style.color = Color.white;
            searchView.OnSelection += e =>
            {
                OnSelection?.Invoke(e);
                Close(false);
            };
        }

        private void OnFocus()
        {
            if (searchView == null)
            {
                return;
            }

            var searchField = searchView.Q<SearchView>();
            var input = searchField.Q("unity-text-input");
            input.Focus();
        }

        private void OnLostFocus()
        {
            Close(true);
        }

        private void Close(bool fireEvent)
        {
            Close();

            if (fireEvent)
            {
                OnClose?.Invoke();
            }
        }
    }
}