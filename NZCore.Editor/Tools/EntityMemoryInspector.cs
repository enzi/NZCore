// <copyright project="NZCore.Editor" file="EntityMemoryInspector.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace NZCore.Editor
{
    public class EntityMemoryInspector : EditorWindow
    {
        private enum SortColumn
        {
            Name,
            Chunk,
            Heap
        }

        private int _entityIndex;
        private Vector2 _scrollPos;
        private string _errorMessage;
        private bool _hasResult;
        private SortColumn _sortColumn = SortColumn.Chunk;
        private bool _sortAscending;

        private readonly List<ComponentEntry> _componentEntries = new();
        private readonly List<int> _referencedEntityIndices = new();
        private int _totalBytesInChunk;
        private int _totalBytesHeap;
        private string _foundEntityLabel;

        private struct ComponentEntry
        {
            public string TypeName;
            public TypeManager.TypeCategory Category;
            public int SizeInChunk;
            public int ElementSize;
            public int BufferLength;
            public int BufferCapacity;
            public bool IsZeroSized;
            public int EntityRefCount;
        }

        [MenuItem("Tools/NZCore/Entity Memory Inspector")]
        private static void Init()
        {
            var window = (EntityMemoryInspector)GetWindow(typeof(EntityMemoryInspector));
            window.titleContent = new GUIContent("Entity Memory Inspector");
            window.Show();
        }

        private static readonly Color RowEven = new(0f, 0f, 0f, 0f);
        private static readonly Color RowOdd = new(0f, 0f, 0f, 0.08f);
        private static readonly Color SeparatorColor = new(0.5f, 0.5f, 0.5f, 0.3f);

        private const int ColName = 260;
        private const int ColChunk = 80;
        private const int ColHeap = 80;
        private const int ColExtra = 120;

        private void OnGUI()
        {
            // -- header bar --
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Entity Memory Inspector", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            // -- query row --
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Entity Index", GUILayout.Width(90));
            _entityIndex = EditorGUILayout.IntField(_entityIndex, GUILayout.Width(80));
            if (GUILayout.Button("Inspect", GUILayout.Width(70)))
            {
                Inspect();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            DrawSeparator();

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Warning);
                return;
            }

            if (!_hasResult)
            {
                return;
            }

            GUILayout.Space(4);

            // -- summary --
            EditorGUILayout.LabelField(_foundEntityLabel, EditorStyles.miniLabel);
            GUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Chunk", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField($"{_totalBytesInChunk} B ({Chunk.kChunkSize / _totalBytesInChunk})", GUILayout.Width(80));
            if (_totalBytesHeap > 0)
            {
                EditorGUILayout.LabelField("Heap", EditorStyles.boldLabel, GUILayout.Width(40));
                EditorGUILayout.LabelField($"{_totalBytesHeap} B", GUILayout.Width(80));
            }

            EditorGUILayout.LabelField("Total", EditorStyles.boldLabel, GUILayout.Width(40));
            EditorGUILayout.LabelField($"{_totalBytesInChunk + _totalBytesHeap} B", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            DrawSeparator();
            GUILayout.Space(2);

            // -- column headers --
            EditorGUILayout.BeginHorizontal();
            DrawSortHeader("Component", SortColumn.Name, ColName);
            DrawSortHeader("Chunk", SortColumn.Chunk, ColChunk);
            DrawSortHeader("Heap", SortColumn.Heap, ColHeap);
            EditorGUILayout.LabelField("Notes", EditorStyles.miniLabel, GUILayout.Width(ColExtra));
            EditorGUILayout.EndHorizontal();

            DrawSeparator();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // -- component rows --
            for (var i = 0; i < _componentEntries.Count; i++)
            {
                var entry = _componentEntries[i];
                var rowColor = i % 2 == 0 ? RowEven : RowOdd;
                DrawRow(entry, rowColor);
            }

            // -- referenced entities --
            if (_referencedEntityIndices.Count > 0)
            {
                GUILayout.Space(8);
                DrawSeparator();
                GUILayout.Space(2);
                EditorGUILayout.LabelField("Referenced Entities", EditorStyles.boldLabel);
                GUILayout.Space(2);

                for (var i = 0; i < _referencedEntityIndices.Count; i++)
                {
                    var rowColor = i % 2 == 0 ? RowEven : RowOdd;
                    DrawColoredRect(rowColor);
                    EditorGUILayout.LabelField($"Entity[{_referencedEntityIndices[i]}]", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawRow(ComponentEntry entry, Color bg)
        {
            DrawColoredRect(bg);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(entry.TypeName, GUILayout.Width(ColName));

            if (entry.IsZeroSized)
            {
                EditorGUILayout.LabelField("—", GUILayout.Width(ColChunk));
                EditorGUILayout.LabelField("—", GUILayout.Width(ColHeap));
                EditorGUILayout.LabelField("tag", EditorStyles.miniLabel, GUILayout.Width(ColExtra));
            }
            else if (entry.Category == TypeManager.TypeCategory.ISharedComponentData)
            {
                EditorGUILayout.LabelField("shared", EditorStyles.miniLabel, GUILayout.Width(ColChunk));
                EditorGUILayout.LabelField("—", GUILayout.Width(ColHeap));
                EditorGUILayout.LabelField($"{entry.SizeInChunk} B struct", EditorStyles.miniLabel, GUILayout.Width(ColExtra));
            }
            else if (entry.Category == TypeManager.TypeCategory.BufferData)
            {
                var heapBytes = entry.BufferLength > entry.BufferCapacity ? entry.ElementSize * entry.BufferLength : 0;
                EditorGUILayout.LabelField($"{entry.SizeInChunk} B", GUILayout.Width(ColChunk));
                EditorGUILayout.LabelField(heapBytes > 0 ? $"{heapBytes} B" : "—", GUILayout.Width(ColHeap));
                var bufNote = entry.BufferLength >= 0
                    ? $"buf [{entry.BufferLength}] × {entry.ElementSize} B"
                    : "buffer";
                EditorGUILayout.LabelField(bufNote, EditorStyles.miniLabel, GUILayout.Width(ColExtra));
            }
            else
            {
                EditorGUILayout.LabelField($"{entry.SizeInChunk} B", GUILayout.Width(ColChunk));
                EditorGUILayout.LabelField("—", GUILayout.Width(ColHeap));
                var notes = entry.EntityRefCount > 0
                    ? $"{entry.EntityRefCount} entity ref{(entry.EntityRefCount > 1 ? "s" : "")}"
                    : "";
                EditorGUILayout.LabelField(notes, EditorStyles.miniLabel, GUILayout.Width(ColExtra));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSortHeader(string label, SortColumn col, int width)
        {
            var arrow = _sortColumn == col ? _sortAscending ? " ▲" : " ▼" : "";
            if (GUILayout.Button(label + arrow, EditorStyles.miniLabel, GUILayout.Width(width)))
            {
                if (_sortColumn == col)
                {
                    _sortAscending = !_sortAscending;
                }
                else
                {
                    _sortColumn = col;
                    _sortAscending = col == SortColumn.Name;
                }

                SortEntries();
            }
        }

        private void SortEntries()
        {
            _componentEntries.Sort((a, b) =>
            {
                var cmp = _sortColumn switch
                {
                    SortColumn.Name => string.Compare(a.TypeName, b.TypeName, StringComparison.OrdinalIgnoreCase),
                    SortColumn.Chunk => a.SizeInChunk.CompareTo(b.SizeInChunk),
                    SortColumn.Heap => HeapBytes(a).CompareTo(HeapBytes(b)),
                    _ => 0
                };
                return _sortAscending ? cmp : -cmp;
            });
        }

        private static int HeapBytes(ComponentEntry e) =>
            e.BufferLength > e.BufferCapacity ? e.ElementSize * e.BufferLength : 0;

        private static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, SeparatorColor);
        }

        private static void DrawColoredRect(Color color)
        {
            if (color.a <= 0f)
            {
                return;
            }

            var rect = GUILayoutUtility.GetLastRect();
            rect.y += rect.height;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.DrawRect(rect, color);
        }

        private unsafe void Inspect()
        {
            _componentEntries.Clear();
            _referencedEntityIndices.Clear();
            _totalBytesInChunk = 0;
            _totalBytesHeap = 0;
            _errorMessage = null;
            _hasResult = false;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _errorMessage = "No active world. Enter Play Mode first.";
                return;
            }

            var em = world.EntityManager;

            var entity = Entity.Null;
            using var allEntities = em.GetAllEntities(Allocator.Temp);
            foreach (var e in allEntities)
            {
                if (e.Index == _entityIndex)
                {
                    entity = e;
                    break;
                }
            }

            if (entity == Entity.Null)
            {
                _errorMessage = $"No entity with index {_entityIndex} in world '{world.Name}'.";
                return;
            }

            _foundEntityLabel = $"Entity({entity.Index}, v{entity.Version})  world: {world.Name}";

            using var types = em.GetComponentTypes(entity, Allocator.Temp);
            var seenRefs = new HashSet<int>();

            foreach (var compType in types)
            {
                var typeIndex = compType.TypeIndex;
                var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                var managedType = TypeManager.GetType(typeIndex);

                var entry = new ComponentEntry
                {
                    TypeName = managedType?.FullName ?? $"TypeIndex({typeIndex.Value})",
                    Category = typeInfo.Category,
                    SizeInChunk = typeInfo.SizeInChunk,
                    ElementSize = typeInfo.ElementSize,
                    BufferCapacity = typeInfo.BufferCapacity,
                    BufferLength = -1,
                    IsZeroSized = typeInfo.IsZeroSized,
                    EntityRefCount = typeInfo.EntityOffsetCount
                };

                if (entry.IsZeroSized || entry.Category == TypeManager.TypeCategory.ISharedComponentData)
                {
                    // no per-entity chunk cost
                }
                else if (entry.Category == TypeManager.TypeCategory.BufferData)
                {
                    _totalBytesInChunk += typeInfo.SizeInChunk;

                    var len = em.GetBufferLength(entity, typeIndex);
                    entry.BufferLength = len;

                    if (len > typeInfo.BufferCapacity)
                    {
                        _totalBytesHeap += typeInfo.ElementSize * len;
                    }

                    if (typeInfo.EntityOffsetCount > 0 && len > 0)
                    {
                        var elems = (byte*)em.GetBufferRawRO(entity, typeIndex);
                        var offsets = TypeManager.GetEntityOffsets(typeIndex, out var offsetCount);
                        for (var j = 0; j < len; j++)
                        {
                            var elem = elems + (long)j * typeInfo.ElementSize;
                            for (var k = 0; k < offsetCount; k++)
                            {
                                var entityRef = *(Entity*)(elem + offsets[k].Offset);
                                if (entityRef != Entity.Null)
                                {
                                    seenRefs.Add(entityRef.Index);
                                }
                            }
                        }
                    }
                }
                else
                {
                    _totalBytesInChunk += typeInfo.SizeInChunk;

                    if (typeInfo.EntityOffsetCount > 0)
                    {
                        var data = (byte*)em.GetComponentDataRawRO(entity, typeIndex);
                        var offsets = TypeManager.GetEntityOffsets(typeIndex, out var offsetCount);
                        for (var k = 0; k < offsetCount; k++)
                        {
                            var entityRef = *(Entity*)(data + offsets[k].Offset);
                            if (entityRef != Entity.Null)
                            {
                                seenRefs.Add(entityRef.Index);
                            }
                        }
                    }
                }

                _componentEntries.Add(entry);
            }

            foreach (var idx in seenRefs)
            {
                _referencedEntityIndices.Add(idx);
            }

            _referencedEntityIndices.Sort();

            SortEntries();
            _hasResult = true;
        }
    }
}