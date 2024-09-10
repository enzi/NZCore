// <copyright project="NZCore" file="TypeSearchProvider.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.Search;

namespace NZCore.Editor
{
    public class TypeSearchProvider : BaseSearchProvider
    {
        private readonly Type m_BaseType;
        private readonly HashSet<Assembly> m_Assemblies = new();
        
        public TypeSearchProvider(Type baseType) 
            : base("type", "Type")
        {
            m_BaseType = baseType;
            
            m_QueryEngine.AddFilter("asm", o => o.Assembly.GetName().Name);
            m_QueryEngine.AddFilter("name", o => o.Name);
            m_QueryEngine.AddFilter("ns", o => o.Namespace);
        }

        protected override IEnumerable<SearchProposition> FetchPropositions(SearchContext context, SearchPropositionOptions options)
        {
            yield return new SearchProposition(null, "Name", "name:", "Filter by type name.", 0, TextCursorPlacement.MoveAutoComplete, null, null, null, new Color());
            yield return new SearchProposition(null, "Namespace", "ns:", "Filter by type namespace.", 0, TextCursorPlacement.MoveAutoComplete, null, null, null, new Color());
            
            foreach (Assembly asm in m_Assemblies)
            {
                string assemblyName = asm.GetName().Name;
                yield return new SearchProposition("Assembly", assemblyName, "asm=" + assemblyName, "Filter by assembly name.");
            }
        }

        protected override IEnumerator FetchItems(SearchContext context, List<SearchItem> items, SearchProvider provider)
        {
            if (context.empty)
                yield break;

            var query = m_QueryEngine.ParseQuery(context.searchQuery);
            if (!query.valid)
                yield break;

            var filteredObjects = query.Apply(GetSearchData());
            foreach (var t in filteredObjects)
            {
                yield return provider.CreateItem(context, t.AssemblyQualifiedName, t.Name, t.FullName, null, t);
            }
        }

        private IEnumerable<Type> GetSearchData()
        {
            // Ignore UI Builder types
            var builderAssembly = GetType().Assembly;

            foreach (var t in TypeCache.GetTypesDerivedFrom(m_BaseType))
            {
                if (t.IsGenericType || t.Assembly == builderAssembly)
                    continue;

                m_Assemblies.Add(t.Assembly);
                yield return t;
            }
        }

        protected override SearchTable GetDefaultTableConfig(SearchContext context)
        {
            var defaultColumns = new List<SearchColumn>
            {
                new SearchColumn("Name", "label")
                {
                    width = 400
                }
            };
            defaultColumns.AddRange(FetchColumns(context, null));
            return new SearchTable("type", defaultColumns);
        }

        protected override IEnumerable<SearchColumn> FetchColumns(SearchContext context, IEnumerable<SearchItem> searchData)
        {
            // Note: The getter is serialized into the window so we need to use a method
            // instead of a lambda or it will break when the window is reloaded.
            // For the same reasons you should avoid renaming the methods or moving them around.

            yield return new SearchColumn("Namespace")
            {
                getter = GetNamespace,
                width = 250
            };
            yield return new SearchColumn("Assembly")
            {
                getter = GetAssemblyName,
                width = 250
            };
        }

        private static object GetNamespace(SearchColumnEventArgs args)
        {
            return args.item.data is not Type t ? null : t.Namespace;
        }

        private static object GetAssemblyName(SearchColumnEventArgs args)
        {
            return args.item.data is not Type t ? null : t.Assembly.GetName().Name;
        }

        protected override IEnumerable<string> GetSearchableData(Type t)
        {
            // The string that will be evaluated by default
            yield return t.AssemblyQualifiedName;
        }

        public static void Show(Action<SearchItem, bool> selectHandler)
        {
            var provider = new TypeSearchProvider(typeof(object));
        
            var context = SearchService.CreateContext(provider, "type:");
            var state = new SearchViewState(context)
            {
                title = "Type",
                queryBuilderEnabled = true,
                hideTabs = true,
                selectHandler = selectHandler,
                flags = SearchViewFlags.TableView |
                        SearchViewFlags.DisableBuilderModeToggle |
                        SearchViewFlags.DisableInspectorPreview
            };

            SearchService.ShowPicker(state);
        }
    }
}