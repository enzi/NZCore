// Original code and credits go to: https://gist.github.com/Okay-Roman/8ba84316968cd3aac72f3984ad5a6251

using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZSpellCasting.UI
{
    [UxmlElement]
    public partial class RadialFillElement : VisualElement, INotifyValueChanged<float>
    {
        public enum FillDirectionType
        {
            Clockwise,
            AntiClockwise
        }

        private readonly VisualElement radialFill;
        private readonly VisualElement overlayImage;
        private readonly VisualElement radialBoundary;

        private float _value;
        private float overlayImageScale;
        private float angleOffset;
        private string overlayImagePath;
        private Sprite icon;

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
                    return;

                if (panel != null)
                {
                    using ChangeEvent<float> pooled = ChangeEvent<float>.GetPooled(_value, value);
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
            get => icon;
            set
            {
                if (icon == value)
                    return;

                icon = value;
                style.backgroundImage = new StyleBackground(icon);
            }
        }

        [UxmlAttribute("angle-offset")]
        public float AngleOffset
        {
            get => angleOffset;

            set
            {
                angleOffset = value;

                // Angle Offset determines the rotation of the radialFill VE, overlayImage will use the inverse of this
                // rotation so the image remains upright
                radialFill.transform.rotation = Quaternion.Euler(0, 0, angleOffset);
                overlayImage.transform.rotation = Quaternion.Euler(0, 0, -angleOffset);
            }
        }

        [UxmlAttribute("overlay-image-path")]
        public string OverlayImagePath
        {
            get => overlayImagePath;
            set
            {
                overlayImagePath = value;
#if UNITY_EDITOR
                Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(overlayImagePath);
                if (tex != null)
                {
                    overlayImage.style.backgroundImage = tex;
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
                overlayImageScale = Mathf.Clamp(overlayImageScale, 0, 1);
                return overlayImageScale;
            }
            set
            {
                overlayImageScale = value;
                overlayImage.style.scale = new Scale(new Vector2(overlayImageScale, overlayImageScale));
            }
        }

        public RadialFillElement()
        {
            radialFill = new VisualElement() { name = "radial-fill", pickingMode = PickingMode.Ignore };
            overlayImage = new VisualElement() { name = "overlay-image", pickingMode = PickingMode.Ignore };
            radialBoundary = new VisualElement() { name = "radial-boundary", pickingMode = PickingMode.Ignore };

            radialFill.style.flexGrow = 1;
            radialFill.Add(overlayImage);
            radialFill.generateVisualContent += OnGenerateVisualContent;

            overlayImage.style.flexGrow = 1;
            overlayImage.style.backgroundImage = null;

            radialBoundary.style.overflow = Overflow.Hidden;
            radialBoundary.style.flexGrow = 1;
            radialBoundary.Add(radialFill);

            Add(radialBoundary);
        }

        public void AngleUpdate(ChangeEvent<float> evt)
        {
            radialFill?.MarkDirtyRepaint();
        }

        public void SetValueWithoutNotify(float newValue)
        {
            _value = newValue;
            radialFill.MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            // default draw 1 triangle
            int triCount = 3;
            int indiceCount = 3;
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

            var width = radialBoundary.layout.width;
            var height = radialBoundary.layout.height;

            MeshWriteData mwd = mgc.Allocate(triCount, indiceCount);
            Vector3 origin = new Vector3(width / 2, height / 2, 0);

            float radius = (width > height) ? width / 2 : height / 2;
            float diameter = 4 * radius;
            float degrees = ((_value * 360) - 90) / Mathf.Rad2Deg;

            //First two vertex are mandatory for 1 triangle
            mwd.SetNextVertex(new Vertex { position = origin + new Vector3(0 * diameter, 0 * diameter, Vertex.nearZ), tint = FillColor });
            mwd.SetNextVertex(new Vertex { position = origin + new Vector3(0 * diameter, -1 * diameter, Vertex.nearZ), tint = FillColor });

            float direction = 1;
            if (FillDirection == FillDirectionType.AntiClockwise)
            {
                direction = -1;
            }

            mwd.SetNextIndex(0);
            mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)2 : (ushort)1);
            if (_value * 360 <= 120)
            {
                mwd.SetNextVertex(new Vertex { position = origin + new Vector3(Mathf.Cos(degrees) * diameter * direction, Mathf.Sin(degrees) * diameter, Vertex.nearZ), tint = FillColor });
                mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)1 : (ushort)2);
            }

            if (_value * 360 > 120 && _value * 360 <= 240)
            {
                mwd.SetNextVertex(
                    new Vertex { position = origin + new Vector3(Mathf.Cos(30 / Mathf.Rad2Deg) * diameter * direction, Mathf.Sin(30 / Mathf.Rad2Deg) * diameter, Vertex.nearZ), tint = FillColor });
                mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)1 : (ushort)2);
                mwd.SetNextVertex(new Vertex { position = origin + new Vector3(Mathf.Cos(degrees) * diameter * direction, Mathf.Sin(degrees) * diameter, Vertex.nearZ), tint = FillColor });
                mwd.SetNextIndex(0);
                mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)3 : (ushort)2);
                mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)2 : (ushort)3);
            }

            if (_value * 360 > 240)
            {
                mwd.SetNextVertex(
                    new Vertex { position = origin + new Vector3(Mathf.Cos(30 / Mathf.Rad2Deg) * diameter * direction, Mathf.Sin(30 / Mathf.Rad2Deg) * diameter, Vertex.nearZ), tint = FillColor });
                mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)1 : (ushort)2);
                mwd.SetNextVertex(new Vertex
                    { position = origin + new Vector3(Mathf.Cos(150 / Mathf.Rad2Deg) * diameter * direction, Mathf.Sin(150 / Mathf.Rad2Deg) * diameter, Vertex.nearZ), tint = FillColor });
                mwd.SetNextIndex(0);
                mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)3 : (ushort)2);
                mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)2 : (ushort)3);

                if (_value * 360 >= 360)
                {
                    mwd.SetNextIndex(0);
                    mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)1 : (ushort)3);
                    mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)3 : (ushort)1);
                }
                else
                {
                    mwd.SetNextVertex(new Vertex { position = origin + new Vector3(Mathf.Cos(degrees) * diameter * direction, Mathf.Sin(degrees) * diameter, Vertex.nearZ), tint = FillColor });
                    mwd.SetNextIndex(0);
                    mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)4 : (ushort)3);
                    mwd.SetNextIndex((FillDirection == FillDirectionType.AntiClockwise) ? (ushort)3 : (ushort)4);
                }
            }
        }
    }
}