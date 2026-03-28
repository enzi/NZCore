// <copyright project="NZCore.UI" file="NZProgressBar.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_6000
namespace NZCore.UI
{
    [Serializable]
    public enum NZProgressBarTitleStyle
    {
        Center,
        LeftAndRight
    }

    [UxmlElement]
    public partial class NZProgressBar : VisualElement, INotifyValueChanged<float>
    {
        private const float MinVisibleProgress = 1f;

        private const string USSClassName = "nz-progress-bar";

        private const string ContainerUssClassName = USSClassName + "__container";
        private const string TitleLeftUssClassName = USSClassName + "__title-left";
        private const string TitleRightUssClassName = USSClassName + "__title-right";
        private const string TitleCenterUssClassName = USSClassName + "__title-center";
        private const string TitleContainerUssClassName = USSClassName + "__title-container";
        private const string ProgressUssClassName = USSClassName + "__progress";
        private const string BackgroundUssClassName = USSClassName + "__background";

        private readonly VisualElement _background;
        private readonly VisualElement _progress;
        private readonly VisualElement _titleContainer;

        private Label _titleLeft;
        private Label _titleRight;

        private float _lowValue;
        private float _highValue = 100f;

        private float _value;


        private NZProgressBarTitleStyle _titleStyle;

        [CreateProperty]
        [UxmlAttribute("title-style")]
        public NZProgressBarTitleStyle TitleStyle
        {
            get => _titleStyle;
            set
            {
                _titleStyle = value;
                SetupTitles();
            }
        }

        [CreateProperty]
        [UxmlAttribute("title-left")]
        public string TitleLeft
        {
            get => _titleLeft.text;
            set => _titleLeft.text = value;
        }

        [CreateProperty]
        [UxmlAttribute("title-right")]
        public string TitleRight
        {
            get => _titleRight?.text;
            set
            {
                if (_titleRight != null)
                {
                    _titleRight.text = value;
                }
            }
        }

        [CreateProperty]
        [UxmlAttribute("low-value")]
        public float LowValue
        {
            get => _lowValue;
            set
            {
                _lowValue = value;
                SetProgress(_value);
            }
        }

        [CreateProperty]
        [UxmlAttribute("high-value")]
        public float HighValue
        {
            get => _highValue;
            set
            {
                _highValue = value;
                SetProgress(_value);
            }
        }

        [CreateProperty]
        [UxmlAttribute("value")]
        public float value
        {
            get => _value;
            set
            {
                if (EqualityComparer<float>.Default.Equals(_value, value))
                {
                    return;
                }

                //Debug.Log($"value changed {m_Value}");

                if (panel != null)
                {
                    using var pooled = ChangeEvent<float>.GetPooled(_value, value);

                    pooled.target = this;
                    SetValueWithoutNotify(value);
                    SendEvent(pooled);
                }
                else
                {
                    SetValueWithoutNotify(value);
                }
            }
        }


        public NZProgressBar()
        {
            AddToClassList(USSClassName);
            var root = new VisualElement
            {
                name = USSClassName
            };

            _background = new VisualElement { name = "background" };
            _background.AddToClassList(BackgroundUssClassName);
            root.Add(_background);

            _progress = new VisualElement { name = "progress" };
            _progress.AddToClassList(ProgressUssClassName);
            _background.Add(_progress);

            _titleContainer = new VisualElement { name = "title-container" };
            _titleContainer.AddToClassList(TitleContainerUssClassName);
            _background.Add(_titleContainer);

            SetupTitles();

            root.AddToClassList(ContainerUssClassName);
            hierarchy.Add(root);
            RegisterCallback(new EventCallback<GeometryChangedEvent>(OnGeometryChanged));
        }

        private void SetupTitles()
        {
            _titleContainer.Clear();

            if (TitleStyle == NZProgressBarTitleStyle.Center)
            {
                _titleLeft = new Label { name = "title-center" };
                _titleLeft.AddToClassList(TitleCenterUssClassName);
                _titleContainer.Add(_titleLeft);
            }
            else
            {
                _titleLeft = new Label { name = "title-left" };
                _titleLeft.AddToClassList(TitleLeftUssClassName);
                _titleContainer.Add(_titleLeft);

                _titleRight = new Label { name = "title-right" };
                _titleRight.AddToClassList(TitleRightUssClassName);
                _titleContainer.Add(_titleRight);
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent e) => SetProgress(value);

        public void SetValueWithoutNotify(float newValue)
        {
            _value = newValue;
            SetProgress(value);
        }

        private void SetProgress(float p)
        {
            var right = math.max(p, LowValue);
            right = math.min(right, HighValue);

            right = CalculateProgressWidth(right);

            if (right >= 0)
            {
                _progress.style.right = right;
            }
        }

        private float CalculateProgressWidth(float width)
        {
            if (_background == null || _progress == null)
            {
                return 0f;
            }

            if (float.IsNaN(_background.layout.width))
            {
                return 0f;
            }

            var maxWidth = _background.layout.width - 2;
            var lala = maxWidth - Mathf.Max(maxWidth * width / HighValue, MinVisibleProgress);

            //Debug.Log($"background width: {maxWidth} highValue: {highValue} ratio: {width} -> {lala}");
            return lala;
        }
    }
}
#endif