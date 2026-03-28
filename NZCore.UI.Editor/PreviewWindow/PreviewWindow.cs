// <copyright project="NZCore.UI.Editor" file="PreviewWindow.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using NZCore.Editor;
using NZCore.Hybrid;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;
using GizmoType = NZCore.Hybrid.GizmoType;
using Object = UnityEngine.Object;

namespace NZCore.UI.Editor
{
    [UxmlElement]
    public partial class PreviewWindow : VisualElement
    {
        private class PersistentData
        {
            public Vector2 PreviewDir = new(120, -20);
            public float AvatarScale = 1.0f;
            public float ZoomFactor = 1.0f;
            public Vector3 PivotPositionOffset = Vector3.zero;
        }

        private PersistentData _viewData;

        private const string ViewDataKey = "nz-preview-window";

        public static PreviewWindow Instance;
        public static readonly List<DeferredGizmo> DeferredGizmos = new();

        public Animator Animator;
        private HybridAnimator _hybridAnimator;
        public ref HybridAnimator HybridAnimator => ref _hybridAnimator;

        public Action OnSetupFinished;

        private NZAvatarPreview _avatarPreview;
        private PreviewRenderUtility _previewRenderUtility;
        private GameObject _previewObject;
        private AnimationClip _previewAnimationClip;

        private readonly Material _wireframeMaterial;
        private readonly IMGUIContainer _previewContainer;
        private RenderTexture _gizmoRenderTexture;

        public PreviewWindow()
        {
            Instance = this;

            viewDataKey = ViewDataKey;

            _wireframeMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));

            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.enzisoft.nzcore/NZCore.UI.Editor/PreviewWindow/PreviewWindow.uss");
            styleSheets.Add(styles);

            AddToClassList("preview-container");

            var objectField = new ObjectField
            {
                label = "Preview Object",
                objectType = typeof(GameObject),
                viewDataKey = "preview-window-object"
            };

            objectField.RegisterValueChangedCallback(evt =>
            {
                _previewObject = evt.newValue as GameObject;

                SetupAvatarPreview();
            });

            Add(objectField);

            var resetButton = new Button(() =>
            {
                _viewData = new PersistentData();
                UpdateFromViewState();
                SaveViewDataState();
            })
            {
                text = "Reset"
            };

            Add(resetButton);

            _previewContainer = new IMGUIContainer();
            _previewContainer.onGUIHandler = () => { Draw(Event.current.type); };

            _previewContainer.AddToClassList("imgui-preview-container");

            Add(_previewContainer);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChangedEvent);
        }

        private void SaveViewDataState()
        {
            if (_avatarPreview == null)
            {
                return;
            }

            _viewData.PivotPositionOffset = _avatarPreview.PivotPositionOffset;
            _viewData.AvatarScale = _avatarPreview.AvatarScale;
            _viewData.ZoomFactor = _avatarPreview.ZoomFactor;
            _viewData.PreviewDir = _avatarPreview.PreviewDir;

            ExposedViewData.ExposedViewData.SaveViewData(this);
        }

        private void UpdateFromViewState()
        {
            _avatarPreview.PivotPositionOffset = _viewData.PivotPositionOffset;
            _avatarPreview.AvatarScale = _viewData.AvatarScale;
            _avatarPreview.ZoomFactor = _viewData.ZoomFactor;
            _avatarPreview.PreviewDir = _viewData.PreviewDir;
        }

        private void OnGeometryChangedEvent(GeometryChangedEvent evt)
        {
            var pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;

            var r = _previewContainer.contentRect;

            var width = (int)(r.width * (double)pixelsPerPoint);
            var height = (int)(r.height * (double)pixelsPerPoint);

            _gizmoRenderTexture =
                new RenderTexture(width, height, GraphicsFormat.R16G16B16A16_SFloat, SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil));

            if (_wireframeMaterial != null)
            {
                // clear the RT once
                RenderTexture.active = _gizmoRenderTexture;
                _wireframeMaterial.SetPass(0);
                GL.Clear(true, true, Color.clear);
                GL.Flush();
                RenderTexture.active = null;
            }
        }

        public void Cleanup()
        {
            _avatarPreview?.OnDisable();

            if (_gizmoRenderTexture != null)
            {
                _gizmoRenderTexture.Release();
                Object.DestroyImmediate(_gizmoRenderTexture);
            }
        }

        private void SetupAvatarPreview()
        {
            // Cleanup existing preview
            if (_avatarPreview != null)
            {
                _avatarPreview.OnDisable();
                _avatarPreview = null;
                _previewRenderUtility = null;
            }

            if (_previewObject == null)
            {
                return;
            }

            // Get the Animator and Avatar
            var previewObjectAnimator = _previewObject.GetComponent<Animator>();
            if (previewObjectAnimator == null || previewObjectAnimator.avatar == null)
            {
                return;
            }

            // Create Avatar Preview
            _avatarPreview = new NZAvatarPreview(previewObjectAnimator, null);

            Animator = _avatarPreview.Animator;
            Animator.enabled = true;
            _hybridAnimator = HybridEntity.CreatePlayableGraph(Animator);

            _previewRenderUtility = _avatarPreview._previewUtility;

            _viewData = new PersistentData();
            _viewData = ExposedViewData.ExposedViewData.GetOrCreateViewData(this, ViewDataKey, _viewData);

            UpdateFromViewState();

            OnSetupFinished();
        }

        public void Draw(EventType eventType)
        {
            if (_avatarPreview != null)
            {
                _avatarPreview.DoAvatarPreview(_previewContainer.contentRect, GUIStyle.none);
            }

            if (eventType == EventType.Repaint)
            {
                DrawGizmos();

                SaveViewDataState();
            }
        }

        private void DrawGizmos()
        {
            if (_previewContainer == null || _previewRenderUtility == null)
            {
                return;
            }

            Handles.SetCamera(_previewContainer.contentRect, _previewRenderUtility.camera);
            Handles.color = Color.green;

            foreach (var deferredGizmo in DeferredGizmos)
            {
                switch (deferredGizmo.Type)
                {
                    case GizmoType.Sphere:
                        Handles.DrawWireArc(deferredGizmo.Position, Vector3.up, Vector3.forward, 360f, deferredGizmo.Size.x);
                        Handles.DrawWireArc(deferredGizmo.Position, Vector3.right, Vector3.up, 360f, deferredGizmo.Size.x);
                        Handles.DrawWireArc(deferredGizmo.Position, Vector3.forward, Vector3.up, 360f, deferredGizmo.Size.x);
                        break;
                    case GizmoType.Capsule:
                        var point2 = math.mul(deferredGizmo.Rotation, new Vector3(0, 0, deferredGizmo.Size.z * deferredGizmo.Scale.z));
                        GizmosUtility.DrawWireCapsule(deferredGizmo.Position, deferredGizmo.Position + point2, deferredGizmo.Size.x * deferredGizmo.Scale.x);
                        break;
                    case GizmoType.Box:
                        Handles.DrawWireCube(deferredGizmo.Position, deferredGizmo.Size);
                        break;
                    case GizmoType.Circle:
                    {
                        Handles.DrawWireDisc(deferredGizmo.Position, Vector3.up, deferredGizmo.Size.x, 1.0f);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            DeferredGizmos.Clear();
        }

        public void SetTimeRecursively(double time)
        {
            _hybridAnimator.SetTimeRecursively(time);
        }
    }
}