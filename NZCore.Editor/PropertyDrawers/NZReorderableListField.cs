// <copyright project="NZCore.Editor" file="NZReorderableListField.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    internal sealed class NZReorderableListField : VisualElement
    {
        private const int DefaultPageSize = 8;

        private readonly string _label;
        private readonly string _propertyPath;
        private readonly SerializedObject _serializedObject;
        private int _page;
        private int _pageSize = DefaultPageSize;

        public NZReorderableListField(SerializedProperty property)
        {
            _serializedObject = property.serializedObject;
            _propertyPath = property.propertyPath;
            _label = property.displayName;

            name = $"nz-reorderable-list-{property.name}";
            pickingMode = PickingMode.Position;

            style.marginTop = 2.0f;
            style.marginBottom = 4.0f;

            Refresh();
        }

        public static bool CanDraw(SerializedProperty property) =>
            property is { isArray: true } && property.propertyType != SerializedPropertyType.String;

        private SerializedProperty GetProperty()
        {
            _serializedObject.UpdateIfRequiredOrScript();
            return _serializedObject.FindProperty(_propertyPath);
        }

        private void Refresh()
        {
            Clear();

            var property = GetProperty();
            if (property == null)
            {
                return;
            }

            ClampPage(property);
            Add(CreateHeader(property));

            if (!property.isExpanded)
            {
                return;
            }

            Add(CreatePager(property));
            Add(CreateRows(property));
        }

        private VisualElement CreateHeader(SerializedProperty property)
        {
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = 24.0f,
                    paddingLeft = 4.0f,
                    paddingRight = 4.0f,
                    backgroundColor = HeaderColor
                }
            };

            SetBorder(header, BorderColor);

            var foldoutButton = new Button(() =>
            {
                var current = GetProperty();
                current.isExpanded = !current.isExpanded;
                current.serializedObject.ApplyModifiedProperties();
                Refresh();
            })
            {
                text = property.isExpanded ? "v" : ">"
            };
            StyleSmallButton(foldoutButton, 22.0f);
            header.Add(foldoutButton);

            var label = new Label(_label)
            {
                style =
                {
                    flexGrow = 1.0f,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginLeft = 4.0f
                }
            };
            header.Add(label);

            var sizeField = new IntegerField
            {
                value = property.arraySize,
                style =
                {
                    width = 48.0f,
                    marginRight = 4.0f
                }
            };
            sizeField.RegisterValueChangedCallback(evt =>
            {
                var current = GetProperty();
                current.arraySize = Mathf.Max(0, evt.newValue);
                current.serializedObject.ApplyModifiedProperties();
                Refresh();
            });
            header.Add(sizeField);

            var addButton = new Button(() =>
            {
                var current = GetProperty();
                current.InsertArrayElementAtIndex(current.arraySize);
                current.isExpanded = true;
                current.serializedObject.ApplyModifiedProperties();
                _page = GetPageCount(current) - 1;
                Refresh();
            })
            {
                text = "+"
            };
            StyleSmallButton(addButton, 24.0f);
            header.Add(addButton);

            return header;
        }

        private VisualElement CreatePager(SerializedProperty property)
        {
            var pager = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = 24.0f,
                    paddingLeft = 6.0f,
                    paddingRight = 6.0f,
                    backgroundColor = SubHeaderColor
                }
            };
            SetBorder(pager, BorderColor);
            pager.style.borderTopWidth = 0.0f;

            var previousButton = new Button(() =>
            {
                _page = Mathf.Max(0, _page - 1);
                Refresh();
            })
            {
                text = "<"
            };
            StyleSmallButton(previousButton, 24.0f);
            previousButton.SetEnabled(_page > 0);
            pager.Add(previousButton);

            var pageCount = GetPageCount(property);
            var nextButton = new Button(() =>
            {
                _page = Mathf.Min(pageCount - 1, _page + 1);
                Refresh();
            })
            {
                text = ">"
            };
            StyleSmallButton(nextButton, 24.0f);
            nextButton.SetEnabled(_page < pageCount - 1);
            pager.Add(nextButton);

            var start = property.arraySize == 0 ? 0 : _page * _pageSize + 1;
            var end = Mathf.Min(property.arraySize, (_page + 1) * _pageSize);
            var pageLabel = new Label($"{start}-{end} / {property.arraySize}")
            {
                style =
                {
                    flexGrow = 1.0f,
                    marginLeft = 8.0f
                }
            };
            pager.Add(pageLabel);

            pager.Add(new Label("Rows")
            {
                style =
                {
                    marginRight = 4.0f
                }
            });

            var pageSizeField = new IntegerField
            {
                value = _pageSize,
                style =
                {
                    width = 44.0f
                }
            };
            pageSizeField.RegisterValueChangedCallback(evt =>
            {
                var firstVisibleIndex = _page * _pageSize;
                _pageSize = Mathf.Max(1, evt.newValue);
                _page = firstVisibleIndex / _pageSize;
                Refresh();
            });
            pager.Add(pageSizeField);

            return pager;
        }

        private VisualElement CreateRows(SerializedProperty property)
        {
            var rows = new VisualElement();

            if (property.arraySize == 0)
            {
                rows.Add(CreateEmptyRow());
                return rows;
            }

            var startIndex = _page * _pageSize;
            var endIndex = Mathf.Min(property.arraySize, startIndex + _pageSize);
            for (var i = startIndex; i < endIndex; i++)
            {
                rows.Add(CreateRow(property, i));
            }

            return rows;
        }

        private VisualElement CreateEmptyRow()
        {
            var row = new Label("Empty")
            {
                style =
                {
                    minHeight = 26.0f,
                    paddingLeft = 8.0f,
                    paddingTop = 4.0f,
                    backgroundColor = RowColor(0)
                }
            };
            SetBorder(row, BorderColor);
            row.style.borderTopWidth = 0.0f;
            return row;
        }

        private VisualElement CreateRow(SerializedProperty property, int index)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingTop = 4.0f,
                    paddingBottom = 4.0f,
                    paddingLeft = 4.0f,
                    paddingRight = 4.0f,
                    backgroundColor = RowColor(index)
                }
            };
            SetBorder(row, BorderColor);
            row.style.borderTopWidth = 0.0f;

            row.Add(new Label($"#{index}")
            {
                style =
                {
                    width = 36.0f,
                    minWidth = 36.0f,
                    unityTextAlign = TextAnchor.UpperLeft
                }
            });

            var content = new VisualElement
            {
                style =
                {
                    flexGrow = 1.0f
                }
            };

            var element = property.GetArrayElementAtIndex(index);
            if (element.propertyType == SerializedPropertyType.Generic)
            {
                var childCount = 0;
                foreach (var child in element.GetChildren())
                {
                    var childCopy = child.Copy();
                    var field = new PropertyField(childCopy);
                    field.BindProperty(childCopy);
                    content.Add(field);
                    childCount++;
                }

                if (childCount == 0)
                {
                    content.Add(new Label($"Element {index}"));
                }
            }
            else
            {
                var elementCopy = element.Copy();
                var field = new PropertyField(elementCopy, string.Empty);
                field.BindProperty(elementCopy);
                content.Add(field);
            }

            row.Add(content);
            row.Add(CreateRowButtons(index, property.arraySize));
            return row;
        }

        private VisualElement CreateRowButtons(int index, int arraySize)
        {
            var buttons = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginLeft = 6.0f
                }
            };

            var upButton = new Button(() => Move(index, index - 1)) { text = "^" };
            StyleSmallButton(upButton, 24.0f);
            upButton.SetEnabled(index > 0);
            buttons.Add(upButton);

            var downButton = new Button(() => Move(index, index + 1)) { text = "v" };
            StyleSmallButton(downButton, 24.0f);
            downButton.SetEnabled(index < arraySize - 1);
            buttons.Add(downButton);

            var removeButton = new Button(() => Remove(index)) { text = "-" };
            StyleSmallButton(removeButton, 24.0f);
            buttons.Add(removeButton);

            return buttons;
        }

        private void Move(int from, int to)
        {
            var property = GetProperty();
            property.MoveArrayElement(from, to);
            property.serializedObject.ApplyModifiedProperties();
            _page = to / _pageSize;
            Refresh();
        }

        private void Remove(int index)
        {
            var property = GetProperty();
            var previousSize = property.arraySize;
            property.DeleteArrayElementAtIndex(index);

            if (property.arraySize == previousSize)
            {
                property.DeleteArrayElementAtIndex(index);
            }

            property.serializedObject.ApplyModifiedProperties();
            ClampPage(property);
            Refresh();
        }

        private void ClampPage(SerializedProperty property)
        {
            _pageSize = Mathf.Max(1, _pageSize);
            _page = Mathf.Clamp(_page, 0, GetPageCount(property) - 1);
        }

        private int GetPageCount(SerializedProperty property) =>
            Mathf.Max(1, Mathf.CeilToInt(property.arraySize / (float)_pageSize));

        private static void StyleSmallButton(Button button, float width)
        {
            button.style.width = width;
            button.style.minWidth = width;
            button.style.height = 20.0f;
            button.style.marginLeft = 1.0f;
            button.style.marginRight = 1.0f;
            button.style.paddingLeft = 0.0f;
            button.style.paddingRight = 0.0f;
        }

        private static void SetBorder(VisualElement element, Color color)
        {
            element.style.borderBottomWidth = 1.0f;
            element.style.borderLeftWidth = 1.0f;
            element.style.borderRightWidth = 1.0f;
            element.style.borderTopWidth = 1.0f;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
        }

        private static Color HeaderColor => EditorGUIUtility.isProSkin
            ? new Color(0.18f, 0.18f, 0.18f, 1.0f)
            : new Color(0.72f, 0.72f, 0.72f, 1.0f);

        private static Color SubHeaderColor => EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.16f, 0.16f, 1.0f)
            : new Color(0.68f, 0.68f, 0.68f, 1.0f);

        private static Color BorderColor => EditorGUIUtility.isProSkin
            ? new Color(0.08f, 0.08f, 0.08f, 1.0f)
            : new Color(0.52f, 0.52f, 0.52f, 1.0f);

        private static Color RowColor(int index)
        {
            if (EditorGUIUtility.isProSkin)
            {
                return index % 2 == 0
                    ? new Color(0.23f, 0.23f, 0.23f, 1.0f)
                    : new Color(0.20f, 0.20f, 0.20f, 1.0f);
            }

            return index % 2 == 0
                ? new Color(0.83f, 0.83f, 0.83f, 1.0f)
                : new Color(0.78f, 0.78f, 0.78f, 1.0f);
        }
    }
}
