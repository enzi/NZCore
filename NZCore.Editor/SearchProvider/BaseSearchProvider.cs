// <copyright project="NZCore.Editor" file="BaseSearchProvider.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Search;
using UnityEngine.Search;

namespace NZCore.Editor
{
    public abstract class BaseSearchProvider : SearchProvider
    {
        public readonly QueryEngine<Type> m_QueryEngine = new();
        
        protected BaseSearchProvider(string providerId, string displayName)
            : base(providerId, displayName)
        {
            fetchPropositions = FetchPropositions;
            fetchItems = FetchItems;
            ReflectionUtility.SetReflectedProperty<SearchProvider, Func<SearchContext, SearchTable>>(this, "tableConfig", GetDefaultTableConfig);
            fetchColumns = FetchColumns;

            m_QueryEngine.SetSearchDataCallback(GetSearchableData, StringComparison.OrdinalIgnoreCase);
        }

        protected abstract IEnumerable<SearchProposition> FetchPropositions(SearchContext context, SearchPropositionOptions options);
        protected abstract IEnumerator FetchItems(SearchContext context, List<SearchItem> items, SearchProvider provider);
        protected abstract SearchTable GetDefaultTableConfig(SearchContext context);
        protected abstract IEnumerable<SearchColumn> FetchColumns(SearchContext context, IEnumerable<SearchItem> searchData);
        protected abstract IEnumerable<string> GetSearchableData(Type t);
    }

    public static class BaseSearchProviderExtensions
    {
        public static void Show<T>(this T provider, Action<SearchItem> selectHandler, Action<SearchItem[]> multipleSelectHandler)
            where T : BaseSearchProvider
        {
            var context = SearchService.CreateContext((IEnumerable<SearchProvider>) new SearchProvider[1]
            {
                provider
            }, "type:", SearchFlags.Sorted | SearchFlags.Multiselect);
            
            var state = new SearchViewState(context)
            {
                title = "Type",
                queryBuilderEnabled = true,
                hideTabs = true,
                flags = SearchViewFlags.TableView |
                        SearchViewFlags.DisableBuilderModeToggle |
                        SearchViewFlags.DisableInspectorPreview |
                        SearchViewFlags.ObjectPickerAdvancedUI
            };
            
            var searchViewInstance = SearchService.ShowPicker(state);
            state.selectHandler = SelectHandler;
            return;

            void SelectHandler(SearchItem item, bool b)
            {
                if (searchViewInstance != null && searchViewInstance.selection.Count > 0)
                {
                    var returnArray = new SearchItem[searchViewInstance.selection.Count];
                    var enumerator = searchViewInstance.selection.GetEnumerator();
                    int index = 0;
                    while (enumerator.MoveNext())
                    {
                        returnArray[index] = enumerator.Current;
                        index++;
                    }
                    enumerator.Dispose();
                    
                    multipleSelectHandler.Invoke(returnArray);
                }
                else
                {
                    selectHandler.Invoke(item);
                }
            }
        }
    }
}