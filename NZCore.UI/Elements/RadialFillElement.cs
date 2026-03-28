// <copyright project="NZCore.UI" file="RadialFillElement.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_6000
namespace NZCore.UI
{
    [UxmlElement]
    public partial class RadialFillElement : VisualElement, INotifyValueChanged<float>
    {
        public enum FillDirectionType
        {
            Clockwise,
            AntiClockwise
        }

        private readonly VisualElement _radialFill;
        private readonly VisualElement _overlayImage;
        private readonly VisualElement _radialBoundary;

        private float _value;
        private float _overlayImageScale;
        private float _angleOffset;
        private string _overlayImagePath;
        private Sprite _icon;

        [UxmlAttribute("fill-color")] public Color FillColor { get; set; }

        [UxmlAttribute("value")]
        [CreateProperty]
        public float value
        {
            get
            {
                _value = Mathf.Clamp(_value, 0, 1);
                return _value;
            }
            set
            {
                if (EqualityComparer<float>.Default.Equals(_value, value))
                {
                    return;
                }

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

        [CreateProperty]
        public Sprite Icon
        {
            get => _icon;
            set
            {
                if (_icon == value)
                {
                    return;
                }

                _icon = value;
                style.backgroundImage = new StyleBackground(_icon);
            }
        }

        [UxmlAttribute("angle-offset")]
        public float AngleOffset
        {
            get => _angleOffset;

            set
            {
                _angleOffset = value;

                // Angle Offset determines the rotation of the radialFill VE, overlayImage will use the inverse of this
                // rotation so the image remains upright
                _radialFill.style.rotate = Quaternion.Euler(0, 0, _angleOffset);
                _overlayImage.style.rotate = Quaternion.Euler(0, 0, -_angleOffset);
            }
        }

        [UxmlAttribute("overlay-image-path")]
        public string OverlayImagePath
        {
            get => _overlayImagePath;
            set
            {
                _overlayImagePath = value;
#if UNITY_EDITOR
                var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(_overlayImagePath);
                if (tex != null)
                {
                    _overlayImage.style.backgroundImage = tex;
                }
#endif
            }
        }

        [UxmlAttribute("fill-direction")] public FillDirectionType FillDirection { get; set; }

        [UxmlAttribute("overlay-image-scale")]
        public float OverlayImageScale
        {
            get
            {
                _overlayImageScale = Mathf.Clamp(_overlayImageScale, 0, 1);
                return _overlayImageScale;
            }
            set
            {
                _overlayImageScale = value;
                _overlayImage.style.scale = new Scale(new Vector2(_overlayImageScale, _overlayImageScale));
            }
        }

        public RadialFillElement()
        {
            _radialFill = new VisualElement { name = "radial-fill", pickingMode = PickingMode.Ignore };
            _overlayImage = new VisualElement { name = "overlay-image", pickingMode = PickingMode.Ignore };
            _radialBoundary = new VisualElement { name = "radial-boundary", pickingMode = PickingMode.Ignore };

            _radialFill.style.flexGrow = 1;
            _radialFill.Add(_overlayImage);
            _radialFill.generateVisualContent += OnGenerateVisualContent;

            _overlayImage.style.flexGrow = 1;
            _overlayImage.style.backgroundImage = null;

            _radialBoundary.style.overflow = Overflow.Hidden;
            _radialBoundary.style.flexGrow = 1;
            _radialBoundary.Add(_radialFill);

            Add(_radialBoundary);
        }

        public void AngleUpdate(ChangeEvent<float> evt)
        {
            _radialFill?.MarkDirtyRepaint();
        }

        public void SetValueWithoutNotify(float newValue)
        {
            _value = newValue;
            _radialFill.MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            // default draw 1 triangle
            var triCount = 3;
            var indiceCount = 3;
            _value = Mathf.Clamp(_value, 0, 360);
            if (_value * 360 < 240)
            {
                // Draw only 2 triangles
                if (value * 360 > 120)
                {
                    triCount = 4;
                    indiceCount = 6;
                }
            }
            // Draw 3 triangles
            else
            {
                triCount = 4;
                indiceCount = 9;
                if (_value < 1)
                {
                    triCount = 5;
                    indiceCount = 9;
                }
            }

            // Create our MeshWriteData object, allocate the least amount of vertices and triangle indices required

            var width = _radialBoundary.layout.width;
            var height = _radialBoundary.layout.height;

            var mwd = mgc.Allocate(triCount, indiceCount);
            var origin = new Vector3(width / 2, height / 2, 0);

            var radius = width > height ? width / 2 : height / 2;
            var diameter = 4 * radius;
            var degrees = (_value * 360 - 90) / Mathf.Rad2Deg;

            //First two vertex are mandatory for 1 triangle
            mwd.SetNextVertex(new Vertex { position = origin + new Vector3(0 * diameter, 0 * diameter, Vertex.nearZ), tint = FillColor });
            mwd.SetNextVertex(new Vertex { position = origin + new Vector3(0 * diameter, -1 * diameter, Vertex.nearZ), tint = FillColor });

            float direction = 1;
            if (FillDirection == FillDirectionType.AntiClockwise)
            {
                direction = -1;
            }

            mwd.SetNextIndex(0);
            mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)2 : (ushort)1);
            if (_value * 360 <= 120)
            {
                mwd.SetNextVertex(new Vertex
                {
                    position = origin + new Vector3(Mathf.Cos(degrees) * diameter * direction, Mathf.Sin(degrees) * diameter, Vertex.nearZ), tint = FillColor
                });
                mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)1 : (ushort)2);
            }

            if (_value * 360 > 120 && _value * 360 <= 240)
            {
                mwd.SetNextVertex(
                    new Vertex
                    {
                        position = origin + new Vector3(Mathf.Cos(30 / Mathf.Rad2Deg) * diameter * direction, Mathf.Sin(30 / Mathf.Rad2Deg) * diameter,
                            Vertex.nearZ),
                        tint = FillColor
                    });
                mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)1 : (ushort)2);
                mwd.SetNextVertex(new Vertex
                {
                    position = origin + new Vector3(Mathf.Cos(degrees) * diameter * direction, Mathf.Sin(degrees) * diameter, Vertex.nearZ), tint = FillColor
                });
                mwd.SetNextIndex(0);
                mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)3 : (ushort)2);
                mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)2 : (ushort)3);
            }

            if (_value * 360 > 240)
            {
                mwd.SetNextVertex(
                    new Vertex
                    {
                        position = origin + new Vector3(Mathf.Cos(30 / Mathf.Rad2Deg) * diameter * direction, Mathf.Sin(30 / Mathf.Rad2Deg) * diameter,
                            Vertex.nearZ),
                        tint = FillColor
                    });
                mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)1 : (ushort)2);
                mwd.SetNextVertex(new Vertex
                {
                    position = origin + new Vector3(Mathf.Cos(150 / Mathf.Rad2Deg) * diameter * direction, Mathf.Sin(150 / Mathf.Rad2Deg) * diameter,
                        Vertex.nearZ),
                    tint = FillColor
                });
                mwd.SetNextIndex(0);
                mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)3 : (ushort)2);
                mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)2 : (ushort)3);

                if (_value * 360 >= 360)
                {
                    mwd.SetNextIndex(0);
                    mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)1 : (ushort)3);
                    mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)3 : (ushort)1);
                }
                else
                {
                    mwd.SetNextVertex(new Vertex
                    {
                        position = origin + new Vector3(Mathf.Cos(degrees) * diameter * direction, Mathf.Sin(degrees) * diameter, Vertex.nearZ),
                        tint = FillColor
                    });
                    mwd.SetNextIndex(0);
                    mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)4 : (ushort)3);
                    mwd.SetNextIndex(FillDirection == FillDirectionType.AntiClockwise ? (ushort)3 : (ushort)4);
                }
            }
        }
    }
}
#endif