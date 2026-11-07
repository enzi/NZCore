// <copyright project="NZCore.Editor" file="DynamicHashMapInspector.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Entities.Editor;
using Unity.Entities.UI;
using UnityEngine;
using UnityEngine.UIElements;

[assembly: InternalsVisibleTo("NZCore.Tests")]

namespace NZCore.Editor
{
    internal class DynamicHashMapInspector<TElement> : PropertyInspector<DynamicBufferContainer<TElement>>, IExperimentalInspector
        where TElement : unmanaged, IBufferElementData
    {
        private const float RowHeight = 18;
        private const int MaxVisibleRows = 15;

        private static readonly DynamicHashMapReader Reader = DynamicHashMapReader.Create(typeof(TElement));

        private readonly List<(string Key, string Value)> _entries = new();
        private Label _count;
        private ListView _list;

        public override VisualElement Build()
        {
            if (Reader == null)
            {
                return DoDefaultGui();
            }

            var root = new VisualElement();

            _count = new Label();
            root.Add(_count);

            _list = new ListView
            {
                itemsSource = _entries,
                fixedItemHeight = RowHeight,
                showBorder = true,
                selectionType = SelectionType.None,
                makeItem = MakeRow,
                bindItem = BindRow
            };
            root.Add(_list);

            Refresh();
            return root;
        }

        public override void Update()
        {
            if (_list == null)
            {
                return;
            }

            var count = _entries.Count;
            Refresh();

            if (count != _entries.Count)
            {
                _list.Rebuild();
            }
            else
            {
                _list.RefreshItems();
            }
        }

        private void Refresh()
        {
            _entries.Clear();

            var context = GetContext<EntityInspectorContext>();

            if (context != null && context.TargetExists())
            {
                Reader.Read(context.EntityManager, context.Entity, _entries);
            }

            _count.text = _entries.Count == 1 ? "1 entry" : $"{_entries.Count} entries";
            _list.style.height = Mathf.Clamp(_entries.Count, 1, MaxVisibleRows) * RowHeight + 2;
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(new Label { style = { width = Length.Percent(50), unityTextAlign = TextAnchor.MiddleLeft } });
            row.Add(new Label { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft } });
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            ((Label)row[0]).text = _entries[index].Key;
            ((Label)row[1]).text = _entries[index].Value;
        }
    }

    internal abstract class DynamicHashMapReader
    {
        public abstract void Read(EntityManager entityManager, Entity entity, List<(string Key, string Value)> entries);

        public static DynamicHashMapReader Create(Type bufferType)
        {
            var marker = bufferType.GetInterfaces().FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDynamicHashMap<,>));

            if (marker == null)
            {
                return null;
            }

            var arguments = marker.GetGenericArguments();
            return (DynamicHashMapReader)Activator.CreateInstance(typeof(DynamicHashMapReader<,,>).MakeGenericType(bufferType, arguments[0], arguments[1]));
        }
    }

    internal sealed class DynamicHashMapReader<TBuffer, TKey, TValue> : DynamicHashMapReader
        where TBuffer : unmanaged, IDynamicHashMap<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        public override void Read(EntityManager entityManager, Entity entity, List<(string Key, string Value)> entries)
        {
            if (!entityManager.HasBuffer<TBuffer>(entity))
            {
                return;
            }

            var buffer = entityManager.GetBuffer<TBuffer>(entity, true);

            if (buffer.Length == 0)
            {
                return;
            }

            foreach (var (key, value) in buffer.AsHashMap<TBuffer, TKey, TValue>(true))
            {
                entries.Add((key.ToString(), value.ToString()));
            }
        }
    }
}