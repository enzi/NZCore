// <copyright project="NZCore" file="NZFoldout.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Unity.Properties;
using UnityEngine.UIElements;

namespace NZCore.UI.Elements
{
#if ENABLE_UXML_SERIALIZED_DATA
    [UxmlElement]
#endif
    public partial class NZFoldout : VisualElement
    {
#if ENABLE_RUNTIME_DATA_BINDINGS
        private static readonly BindingId textProperty = (BindingId) nameof (text);
        private static readonly BindingId toggleOnLabelClickProperty = (BindingId) nameof (toggleOnLabelClick);
        private static readonly BindingId valueProperty = (BindingId) nameof (value);
#endif

        private const string ussClassName = "unity-foldout";
        private static readonly string toggleUssClassName = Foldout.ussClassName + "__toggle";
        private static readonly string contentUssClassName = Foldout.ussClassName + "__content";
        private static readonly string ussFoldoutDepthClassName = Foldout.ussClassName + "--depth-";
        private const int ussFoldoutMaxDepth = 4;

        private VisualElement content;
        
        private readonly Toggle m_Toggle = new Toggle();
        private bool m_Value;
        
        public bool UseMargin;
        
        [CreateProperty]
        public string text
        {
            get => m_Toggle.text;
            set
            {
                string tmp = text;
                m_Toggle.text = value;
                
#if ENABLE_RUNTIME_DATA_BINDINGS
                if (string.CompareOrdinal(tmp, this.text) == 0)
                    return;
                NotifyPropertyChanged(in textProperty);
#endif
            }
        }
        
#if ENABLE_RUNTIME_DATA_BINDINGS
        [CreateProperty]
        public bool toggleOnLabelClick
        {
            get => m_Toggle.toggleOnLabelClick;
            set
            {
                if (m_Toggle.toggleOnLabelClick == value)
                    return;
                m_Toggle.toggleOnLabelClick = value;
                NotifyPropertyChanged(in toggleOnLabelClickProperty);
            }
        }
#endif
        
        [CreateProperty]
        public bool value
        {
            get => m_Value;
            set
            {
                if (m_Value == value)
                    return;

                using ChangeEvent<bool> pooled = ChangeEvent<bool>.GetPooled(m_Value, value);
                
                pooled.target = this;
                SetValueWithoutNotify(value);
                SendEvent(pooled);
                
#if ENABLE_RUNTIME_DATA_BINDINGS
                NotifyPropertyChanged(in valueProperty);
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
                content = new VisualElement();
            }
            else
            {
                content = container;
            }
            
            AddToClassList(ussClassName);
            delegatesFocus = true;
            focusable = true;
            
            m_Toggle.RegisterValueChangedCallback(evt =>
            {
                value = m_Toggle.value;
                evt.StopPropagation();
            });
            m_Toggle.AddToClassList(toggleUssClassName);
            hierarchy.Add(m_Toggle);

            if (content != null)
            {
                if (useMargin)
                {
                    content.AddToClassList(contentUssClassName);
                }

                hierarchy.Add(content);
                SetValueWithoutNotify(false);
            }

            RegisterCallback(new EventCallback<AttachToPanelEvent>(OnAttachToPanel));
        }

        public void SetContainer(VisualElement newContainer)
        {
            if (content != null)
            {
                hierarchy.Remove(content);
            }

            content = newContainer;

            if (UseMargin)
            {
                content.AddToClassList(contentUssClassName);
            }
            
            SetValueWithoutNotify(m_Value);
        }
        
        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            for (int index = 0; index <= ussFoldoutMaxDepth; ++index)
                RemoveFromClassList(ussFoldoutDepthClassName + index);
            RemoveFromClassList(ussFoldoutDepthClassName + "max");
            
            int foldoutDepth = GetFoldoutDepth(this);
            if (foldoutDepth > ussFoldoutMaxDepth)
                AddToClassList(ussFoldoutDepthClassName + "max");
            else
                AddToClassList(ussFoldoutDepthClassName + foldoutDepth);
        }
        
        private static readonly Type s_FoldoutType = typeof (Foldout);
        private static int GetFoldoutDepth(VisualElement element)
        {
            int foldoutDepth = 0;
            if (element.parent == null) 
                return foldoutDepth;
            
            for (VisualElement parent = element.parent; parent != null; parent = parent.parent)
            {
                if (s_FoldoutType.IsAssignableFrom(parent.GetType()))
                    ++foldoutDepth;
            }
            
            return foldoutDepth;
        }

        private void SetValueWithoutNotify(bool newValue)
        {
            m_Value = newValue;
            m_Toggle.SetValueWithoutNotify(m_Value);
           
            content.style.display = newValue ? DisplayStyle.Flex : DisplayStyle.None;

            toggleAction?.Invoke();
        }

        private Action toggleAction;

        public void SetAction(Action action)
        {
            toggleAction = action;
        }
    }
}