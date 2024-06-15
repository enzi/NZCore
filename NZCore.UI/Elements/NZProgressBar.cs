using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZSpellCasting.UI
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

        private readonly VisualElement background;
        private readonly VisualElement progress;
        private readonly VisualElement titleContainer;

        private Label titleLeft;
        private Label titleRight;

        private float lowValue;
        private float highValue = 100f;

        private float _value;


        private NZProgressBarTitleStyle titleStyle;

        [CreateProperty]
        [UxmlAttribute("title-style")]
        public NZProgressBarTitleStyle TitleStyle
        {
            get => titleStyle;
            set
            {
                titleStyle = value;
                SetupTitles();
            }
        }

        [CreateProperty]
        [UxmlAttribute("title-left")]
        public string TitleLeft
        {
            get => titleLeft.text;
            set => titleLeft.text = value;
        }

        [CreateProperty]
        [UxmlAttribute("title-right")]
        public string TitleRight
        {
            get => titleRight?.text;
            set
            {
                if (titleRight != null)
                    titleRight.text = value;
            }
        }

        [CreateProperty]
        [UxmlAttribute("low-value")]
        public float LowValue
        {
            get => lowValue;
            set
            {
                lowValue = value;
                SetProgress(_value);
            }
        }

        [CreateProperty]
        [UxmlAttribute("high-value")]
        public float HighValue
        {
            get => highValue;
            set
            {
                highValue = value;
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
                    return;

                //Debug.Log($"value changed {m_Value}");

                if (panel != null)
                {
                    using ChangeEvent<float> pooled = ChangeEvent<float>.GetPooled(_value, value);

                    pooled.target = this;
                    SetValueWithoutNotify(value);
                    SendEvent(pooled);
                }
                else
                    SetValueWithoutNotify(value);
            }
        }


        public NZProgressBar()
        {
            AddToClassList(USSClassName);
            VisualElement root = new VisualElement()
            {
                name = USSClassName,
            };

            background = new VisualElement() { name = "background" };
            background.AddToClassList(BackgroundUssClassName);
            root.Add(background);

            progress = new VisualElement() { name = "progress" };
            progress.AddToClassList(ProgressUssClassName);
            background.Add(progress);

            titleContainer = new VisualElement() { name = "title-container" };
            titleContainer.AddToClassList(TitleContainerUssClassName);
            background.Add(titleContainer);

            SetupTitles();

            root.AddToClassList(ContainerUssClassName);
            hierarchy.Add(root);
            RegisterCallback(new EventCallback<GeometryChangedEvent>(OnGeometryChanged));
        }

        private void SetupTitles()
        {
            titleContainer.Clear();

            if (TitleStyle == NZProgressBarTitleStyle.Center)
            {
                titleLeft = new Label() { name = "title-center" };
                titleLeft.AddToClassList(TitleCenterUssClassName);
                titleContainer.Add(titleLeft);
            }
            else
            {
                titleLeft = new Label() { name = "title-left" };
                titleLeft.AddToClassList(TitleLeftUssClassName);
                titleContainer.Add(titleLeft);

                titleRight = new Label() { name = "title-right" };
                titleRight.AddToClassList(TitleRightUssClassName);
                titleContainer.Add(titleRight);
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
                progress.style.right = right;
        }

        private float CalculateProgressWidth(float width)
        {
            if (background == null || progress == null)
                return 0f;

            if (float.IsNaN(background.layout.width))
                return 0f;

            var maxWidth = background.layout.width - 2;
            float lala = maxWidth - Mathf.Max((maxWidth) * width / HighValue, MinVisibleProgress);

            //Debug.Log($"background width: {maxWidth} highValue: {highValue} ratio: {width} -> {lala}");
            return lala;
        }
    }
}