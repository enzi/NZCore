// <copyright project="NZCore" file="BaseSearchProvider.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Search;

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
}