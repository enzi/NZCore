// <copyright project="NZCore" file="NZFoldout.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UIElements;

namespace NZCore.UI.Elements
{
    [UxmlElement]
    public partial class NZFoldout : VisualElement
    {
        internal static readonly BindingId textProperty = (BindingId) nameof (text);
        internal static readonly BindingId toggleOnLabelClickProperty = (BindingId) nameof (toggleOnLabelClick);
        internal static readonly BindingId valueProperty = (BindingId) nameof (value);
        
        public static readonly string ussClassName = "unity-foldout";
        public static readonly string toggleUssClassName = Foldout.ussClassName + "__toggle";
        public static readonly string contentUssClassName = Foldout.ussClassName + "__content";
        public static readonly string inputUssClassName = Foldout.ussClassName + "__input";
        public static readonly string checkmarkUssClassName = Foldout.ussClassName + "__checkmark";
        public static readonly string textUssClassName = Foldout.ussClassName + "__text";
        internal static readonly string toggleInspectorUssClassName = Foldout.toggleUssClassName + "--inspector";
        internal static readonly string ussFoldoutDepthClassName = Foldout.ussClassName + "--depth-";
        internal static readonly int ussFoldoutMaxDepth = 4;
        
        public VisualElement contentContainer;
        
        private readonly Toggle m_Toggle = new Toggle();
        private bool m_Value;
        private bool useMargin;
        
        [CreateProperty]
        public string text
        {
            get => m_Toggle.text;
            set
            {
                string text = this.text;
                m_Toggle.text = value;
                //m_Toggle.visualInput.Q(null, Toggle.textUssClassName)?.AddToClassList(Foldout.textUssClassName);
                if (string.CompareOrdinal(text, this.text) == 0)
                    return;
                NotifyPropertyChanged(in textProperty);
            }
        }
        
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
        
        [CreateProperty]
        public bool value
        {
            get => m_Value;
            set
            {
                if (m_Value == value)
                    return;
                
                using (ChangeEvent<bool> pooled = ChangeEvent<bool>.GetPooled(m_Value, value))
                {
                    pooled.target = this;
                    SetValueWithoutNotify(value);
                    SendEvent(pooled);
                    NotifyPropertyChanged(in valueProperty);
                }
            }
        }

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
            this.useMargin = useMargin;
            
            if (container == null && createContainer)
            {
                contentContainer = new VisualElement();
            }
            else
            {
                contentContainer = container;
            }
            
            AddToClassList(ussClassName);
            delegatesFocus = true;
            focusable = true;
            //this.isEligibleToReceiveFocusFromDisabledChild = false;
            m_Toggle.RegisterValueChangedCallback(evt =>
            {
                value = m_Toggle.value;
                evt.StopPropagation();
            });
            m_Toggle.AddToClassList(toggleUssClassName);
            //this.m_Toggle.visualInput.AddToClassList(inputUssClassName);
            //this.m_Toggle.visualInput.Q((string) null, Toggle.checkmarkUssClassName).AddToClassList(checkmarkUssClassName);
            //this.m_Toggle.AddManipulator((IManipulator) (this.m_NavigationManipulator = new KeyboardNavigationManipulator(new Action<KeyboardNavigationOperation, EventBase>(this.Apply))));
            hierarchy.Add(m_Toggle);

            if (contentContainer != null)
            {
                if (useMargin)
                {
                    contentContainer.AddToClassList(contentUssClassName);
                }

                hierarchy.Add(contentContainer);
                SetValueWithoutNotify(false);
            }

            RegisterCallback(new EventCallback<AttachToPanelEvent>(OnAttachToPanel));
        }

        public void SetContainer(VisualElement newContainer)
        {
            if (contentContainer != null)
            {
                hierarchy.Remove(contentContainer);
            }

            contentContainer = newContainer;

            if (useMargin)
            {
                contentContainer.AddToClassList(contentUssClassName);
            }
            
            SetValueWithoutNotify(m_Value);
        }
        
        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            for (int index = 0; index <= ussFoldoutMaxDepth; ++index)
                RemoveFromClassList(ussFoldoutDepthClassName + index);
            RemoveFromClassList(ussFoldoutDepthClassName + "max");
            //this.m_Toggle.AssignInspectorStyleIfNecessary(toggleInspectorUssClassName);
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
            if (element.parent != null)
            {
                for (VisualElement parent = element.parent; parent != null; parent = parent.parent)
                {
                    if (s_FoldoutType.IsAssignableFrom(parent.GetType()))
                        ++foldoutDepth;
                }
            }
            return foldoutDepth;
        }
        
        private static readonly string s_InspectorElementUssClassName = "unity-inspector-element";
        
        
        public void SetValueWithoutNotify(bool newValue)
        {
            m_Value = newValue;
            m_Toggle.SetValueWithoutNotify(m_Value);
           
            contentContainer.style.display = newValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}