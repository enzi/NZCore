﻿// <copyright file="SearchView.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#nullable disable

namespace BovineLabs.Core.Editor.SearchWindow
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BovineLabs.Core.Editor.UI;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class SearchView : VisualElement
    {
        private static readonly UITemplate SearchViewTemplate = new(SearchWindow.RootUIPath + "SearchView");

        private readonly ListView list;
        private readonly Button returnButton;
        private readonly VisualElement returnIcon;

        private TreeNode<Item> currentNode;
        private List<Item> items;
        private TreeNode<Item> rootNode;
        private TreeNode<Item> searchNode;
        private string title;

        public SearchView()
        {
            this.AddToClassList("SearchView");
            this.AddToClassList(EditorGUIUtility.isProSkin ? "UnityThemeDark" : "UnityThemeLight");

            SearchViewTemplate.Clone(this);

            var searchField = this.Q<SearchField>();
            this.returnButton = this.Q<Button>("ReturnButton");
            this.returnButton.clicked += this.OnNavigationReturn;
            this.returnIcon = this.Q("ReturnIcon");
            this.list = this.Q<ListView>("SearchResults");
            this.list.selectionType = SelectionType.Single;
            this.list.makeItem = () => new SearchViewItem();
            this.list.bindItem = (element, index) =>
            {
                var searchItem = (SearchViewItem)element;
                searchItem.Item = this.currentNode[index];
            };

            this.list.selectionChanged += this.OnListSelectionChange;
            this.list.itemsChosen += this.OnItemsChosen;

            this.Title = "Root";

            searchField.RegisterValueChangedCallback(this.OnSearchQueryChanged);
        }

        public event Action<Item> OnSelection;

        public List<Item> Items
        {
            get => this.items;
            set
            {
                this.items = value;
                this.Reset();
            }
        }

        public string Title
        {
            get => this.title;
            set
            {
                this.title = value;
                this.RefreshTitle();
            }
        }

        public SelectionType SelectionType
        {
            get => this.list.selectionType;
            set => this.list.selectionType = value;
        }

        public void Reset()
        {
            this.rootNode = new TreeNode<Item>(new Item { Path = this.title, Data = null, Icon = null });
            for (var i = 0; i < this.items.Count; ++i)
            {
                this.Add(this.items[i]);
            }

            this.SetCurrentSelectionNode(this.rootNode);
        }

        private static TreeNode<Item> FindNodeByPath(TreeNode<Item> parent, string path)
        {
            if ((parent == null) || (path.Length == 0))
            {
                return null;
            }

            for (var i = 0; i < parent.ChildCount; ++i)
            {
                if (parent[i].Value.Path.Equals(path))
                {
                    return parent[i];
                }
            }

            return null;
        }

        private void OnSearchQueryChanged(ChangeEvent<string> changeEvent)
        {
            if ((this.searchNode != null) && (this.currentNode == this.searchNode))
            {
                this.currentNode = this.searchNode.Parent;
                this.searchNode = null;
                if (changeEvent.newValue.Length == 0)
                {
                    this.SetCurrentSelectionNode(this.currentNode);
                    return;
                }
            }

            if (changeEvent.newValue.Length == 0)
            {
                return;
            }

            var searchResults = new List<TreeNode<Item>>();
            this.rootNode.Traverse(delegate(TreeNode<Item> itemNode)
            {
                if (itemNode.Value.Name.IndexOf(changeEvent.newValue, StringComparison.CurrentCultureIgnoreCase) != -1)
                {
                    searchResults.Add(itemNode);
                }
            });

            this.searchNode = new TreeNode<Item>(new Item { Path = "Search" }, searchResults)
            {
                Parent = this.currentNode,
            };

            this.SetCurrentSelectionNode(this.searchNode);
        }

        private void OnListSelectionChange(IEnumerable<object> selection)
        {
            if (this.SelectionType == SelectionType.Single)
            {
                this.OnItemsChosen(selection);
            }
        }

        private void OnItemsChosen(IEnumerable<object> selection)
        {
            var node = (TreeNode<Item>)selection.First();
            if (node.ChildCount == 0)
            {
                this.OnSelection?.Invoke(node.Value);
            }
            else
            {
                this.SetCurrentSelectionNode(node);
            }
        }

        private void RefreshTitle()
        {
            if (this.rootNode != null)
            {
                this.rootNode.Value = new Item { Path = this.title, Data = null, Icon = null };
            }

            if (this.currentNode == null)
            {
                this.returnButton.text = this.title;
                return;
            }

            this.returnButton.text = this.currentNode.Value.Name;
        }

        private void SetCurrentSelectionNode(TreeNode<Item> node)
        {
            this.currentNode = node;
            this.list.itemsSource = this.currentNode.Children;
            this.returnButton.text = this.currentNode.Value.Name;
            this.list.RefreshItems();

            if (node.Parent == null)
            {
                this.returnButton.SetEnabled(false);
                this.returnIcon.style.visibility = Visibility.Hidden;
            }
            else
            {
                this.returnButton.SetEnabled(true);
                this.returnIcon.style.visibility = Visibility.Visible;
            }
        }

        private void OnNavigationReturn()
        {
            if ((this.currentNode != null) && (this.currentNode.Parent != null))
            {
                this.SetCurrentSelectionNode(this.currentNode.Parent);
            }
        }

        private void Add(Item item)
        {
            if (item.Path.Length == 0)
            {
                return;
            }

            var pathParts = item.Path.Split('/');
            var root = this.rootNode;
            var currentPath = string.Empty;
            for (var i = 0; i < pathParts.Length; ++i)
            {
                if (currentPath.Length == 0)
                {
                    currentPath += pathParts[i];
                }
                else
                {
                    currentPath += "/" + pathParts[i];
                }

                var node = FindNodeByPath(root, currentPath)
                           ?? root.AddChild(new Item { Path = currentPath, Data = null, Icon = null });

                if (i == pathParts.Length - 1)
                {
                    node.Value = item;
                }
                else
                {
                    root = node;
                }
            }
        }

        public struct Item
        {
            public string Path;
            public Texture2D Icon;
            public object Data;

            public string Name => System.IO.Path.GetFileName(this.Path);
        }
    }
}