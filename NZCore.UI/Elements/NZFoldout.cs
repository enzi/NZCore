// <copyright project="NZCore.UI" file="NZFoldout.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Properties;
using UnityEngine.UIElements;

namespace NZCore.UI
{
#if ENABLE_UXML_SERIALIZED_DATA
    [UxmlElement]
#endif
    public partial class NZFoldout : VisualElement
    {
#if ENABLE_RUNTIME_DATA_BINDINGS
        private static readonly BindingId TextProperty = (BindingId)nameof(Text);
        private static readonly BindingId ToggleOnLabelClickProperty = (BindingId)nameof(ToggleOnLabelClick);
        private static readonly BindingId ValueProperty = (BindingId)nameof(Value);
#endif

        private const string USSClassName = "unity-foldout";
        private static readonly string ToggleUssClassName = Foldout.ussClassName + "__toggle";
        private static readonly string ContentUssClassName = Foldout.ussClassName + "__content";
        private static readonly string USSFoldoutDepthClassName = Foldout.ussClassName + "--depth-";
        private const int USSFoldoutMaxDepth = 4;

        private VisualElement _content;

        private readonly Toggle _mToggle = new();
        private bool _mValue;

        public bool UseMargin;

        [CreateProperty]
        public string Text
        {
            get => _mToggle.text;
            set
            {
                var tmp = Text;
                _mToggle.text = value;

#if ENABLE_RUNTIME_DATA_BINDINGS
                if (string.CompareOrdinal(tmp, Text) == 0)
                {
                    return;
                }

                NotifyPropertyChanged(in TextProperty);
#endif
            }
        }

#if ENABLE_RUNTIME_DATA_BINDINGS
        [CreateProperty]
        public bool ToggleOnLabelClick
        {
            get => _mToggle.toggleOnLabelClick;
            set
            {
                if (_mToggle.toggleOnLabelClick == value)
                {
                    return;
                }

                _mToggle.toggleOnLabelClick = value;
                NotifyPropertyChanged(in ToggleOnLabelClickProperty);
            }
        }
#endif

        [CreateProperty]
        public bool Value
        {
            get => _mValue;
            set
            {
                if (_mValue == value)
                {
                    return;
                }

                using var pooled = ChangeEvent<bool>.GetPooled(_mValue, value);

                pooled.target = this;
                SetValueWithoutNotify(value);
                SendEvent(pooled);

#if ENABLE_RUNTIME_DATA_BINDINGS
                NotifyPropertyChanged(in ValueProperty);
#endif
            }
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public NZFoldout()
        {
            Init();
        }

        public NZFoldout(bool useMargin, bool createContainer)
        {
            Init(null, useMargin, createContainer);
        }

        public NZFoldout(VisualElement container, bool useMargin, bool createContainer)
        {
            Init(container, useMargin, createContainer);
        }

        private void Init(VisualElement container = null, bool useMargin = true, bool createContainer = false)
        {
            UseMargin = useMargin;

            if (container == null && createContainer)
            {
                _content = new VisualElement();
            }
            else
            {
                _content = container;
            }

            AddToClassList(USSClassName);
            delegatesFocus = true;
            focusable = true;

            _mToggle.RegisterValueChangedCallback(evt =>
            {
                Value = _mToggle.value;
                evt.StopPropagation();
            });
            _mToggle.AddToClassList(ToggleUssClassName);
            hierarchy.Add(_mToggle);

            if (_content != null)
            {
                if (useMargin)
                {
                    _content.AddToClassList(ContentUssClassName);
                }

                hierarchy.Add(_content);
                SetValueWithoutNotify(false);
            }

            RegisterCallback(new EventCallback<AttachToPanelEvent>(OnAttachToPanel));
        }

        public void SetContainer(VisualElement newContainer)
        {
            RemoveContainer();

            _content = newContainer;

            if (UseMargin)
            {
                _content.AddToClassList(ContentUssClassName);
            }

            SetValueWithoutNotify(_mValue);
        }

        public void ClearContainerContent()
        {
            _content?.Clear();
        }

        public void RemoveContainer()
        {
            if (_content != null)
            {
                hierarchy.Remove(_content);
            }
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            for (var index = 0; index <= USSFoldoutMaxDepth; ++index)
            {
                RemoveFromClassList(USSFoldoutDepthClassName + index);
            }

            RemoveFromClassList(USSFoldoutDepthClassName + "max");

            var foldoutDepth = GetFoldoutDepth(this);
            if (foldoutDepth > USSFoldoutMaxDepth)
            {
                AddToClassList(USSFoldoutDepthClassName + "max");
            }
            else
            {
                AddToClassList(USSFoldoutDepthClassName + foldoutDepth);
            }
        }

        private static readonly Type SFoldoutType = typeof(Foldout);

        private static int GetFoldoutDepth(VisualElement element)
        {
            var foldoutDepth = 0;
            if (element.parent == null)
            {
                return foldoutDepth;
            }

            for (var parent = element.parent; parent != null; parent = parent.parent)
            {
                if (SFoldoutType.IsAssignableFrom(parent.GetType()))
                {
                    ++foldoutDepth;
                }
            }

            return foldoutDepth;
        }

        private void SetValueWithoutNotify(bool newValue)
        {
            _mValue = newValue;
            _mToggle.SetValueWithoutNotify(_mValue);

            _toggleAction?.Invoke();

            _content.style.display = newValue ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private Action _toggleAction;

        public void SetAction(Action action)
        {
            _toggleAction = action;
        }
    }
}