// <copyright project="NZCore file="PreviewWindow.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using NZCore.Editor;
using NZCore.Hybrid;
using NZCore.UI.Elements;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Playables;
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
            public Vector2 m_PreviewDir = new Vector2(120, -20);
            public float m_AvatarScale = 1.0f;
            public float m_ZoomFactor = 1.0f;
            public Vector3 m_PivotPositionOffset = Vector3.zero;
        }

        private PersistentData ViewData;
        
        private const string ViewDataKey = "nz-preview-window";
        
        public static PreviewWindow Instance;
        public static readonly List<DeferredGizmo> DeferredGizmos = new();
        
        private static readonly Type previewRenderUtilityType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PreviewRenderUtility");
        
        private static readonly FieldInfo k_RenderTexture = previewRenderUtilityType.GetField("m_RenderTexture", BindingFlags.Instance | BindingFlags.NonPublic);

        public Animator Animator;
        private HybridAnimator hybridAnimator;
        public ref HybridAnimator HybridAnimator => ref hybridAnimator;
        
        public Action OnSetupFinished;
        
        private NZAvatarPreview avatarPreview;
        private PreviewRenderUtility previewRenderUtility;
        private GameObject previewObject;
        private AnimationClip previewAnimationClip;

        private readonly Material wireframeMaterial;
        private readonly IMGUIContainer previewContainer;
        private RenderTexture gizmoRenderTexture;

        public PreviewWindow()
        {
            Instance = this;

            viewDataKey = ViewDataKey;
            
            wireframeMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.enzisoft.nzcore/NZCore.UI.Editor/PreviewWindow/PreviewWindow.uss");
            styleSheets.Add(styles);
            
            AddToClassList("preview-container");
            
            var objectField = new ObjectField
            {
                label = "Preview Object",
                objectType = typeof(GameObject),
                viewDataKey = "preview-window-object"
            };

            objectField.RegisterValueChangedCallback(evt => {
                previewObject = evt.newValue as GameObject;
                
                SetupAvatarPreview();
            });
            
            Add(objectField);

            var resetButton = new Button(() =>
            {
                ViewData = new PersistentData();
                UpdateFromViewState();
                SaveViewDataState();
            })
            {
                text = "Reset"
            };

            Add(resetButton);
            
            previewContainer = new IMGUIContainer();
            previewContainer.onGUIHandler = () =>
            {
                Draw(Event.current.type);
            };
            
            previewContainer.AddToClassList("imgui-preview-container");
            
            Add(previewContainer);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanelEvent);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChangedEvent);
        }

        private void SaveViewDataState()
        {
            if (avatarPreview == null)
            {
                return;
            }
            
            ViewData.m_PivotPositionOffset = avatarPreview.m_PivotPositionOffset;
            ViewData.m_AvatarScale = avatarPreview.m_AvatarScale;
            ViewData.m_ZoomFactor = avatarPreview.m_ZoomFactor;
            ViewData.m_PreviewDir = avatarPreview.m_PreviewDir;
            
            ExposedViewData.ExposedViewData.SaveViewData(this);
        }

        private void UpdateFromViewState()
        {
            avatarPreview.m_PivotPositionOffset = ViewData.m_PivotPositionOffset;
            avatarPreview.m_AvatarScale = ViewData.m_AvatarScale;
            avatarPreview.m_ZoomFactor = ViewData.m_ZoomFactor;
            avatarPreview.m_PreviewDir = ViewData.m_PreviewDir;
        }

        private void OnGeometryChangedEvent(GeometryChangedEvent evt)
        {
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;

            Rect r = previewContainer.contentRect;
            
            int width = (int) (r.width * (double) pixelsPerPoint);
            int height = (int) (r.height * (double) pixelsPerPoint);
            
            gizmoRenderTexture = new RenderTexture(width, height, GraphicsFormat.R16G16B16A16_SFloat, SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil));
            
            // clear the RT once
            RenderTexture.active = gizmoRenderTexture;
            wireframeMaterial.SetPass(0);
            GL.Clear(true, true, Color.clear);
            GL.Flush();
            RenderTexture.active = null;
        }
        
        private void OnDetachFromPanelEvent(DetachFromPanelEvent evt)
        {
            Debug.Log("OnDetachFromPanelEvent");
            
            if (avatarPreview != null)
            {
                var disposeMethod = avatarPreview.GetType().GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                disposeMethod?.Invoke(avatarPreview, null);
            }

            if (gizmoRenderTexture != null)
            {
                gizmoRenderTexture.Release();
                Object.DestroyImmediate(gizmoRenderTexture);
            }
        }

        private void SetupAvatarPreview()
        {
            // Cleanup existing preview
            if (avatarPreview != null)
            {
                avatarPreview.OnDisable();
                avatarPreview = null;
                previewRenderUtility = null;
            }

            if (previewObject == null)
            {
                return;
            }

            // Get the Animator and Avatar
            var previewObjectAnimator = previewObject.GetComponent<Animator>();
            if (previewObjectAnimator == null || previewObjectAnimator.avatar == null)
            {
                return;
            }

            // Create Avatar Preview
            avatarPreview = new NZAvatarPreview(previewObjectAnimator, null);

            Animator = avatarPreview.Animator;
            Animator.enabled = true;
            hybridAnimator = HybridEntity.CreatePlayableGraph(Animator);

            previewRenderUtility = avatarPreview.m_PreviewUtility;

            ViewData = new PersistentData();
            ViewData = ExposedViewData.ExposedViewData.GetOrCreateViewData(this, ViewDataKey, ViewData);

            UpdateFromViewState();

            OnSetupFinished();
        }

        public void Draw(EventType eventType)
        {
            if (avatarPreview != null)
            {
                avatarPreview.DoAvatarPreview(previewContainer.contentRect, GUIStyle.none);
            }
                
            if (eventType == EventType.Repaint)
            {
                DrawGizmos(previewContainer.contentRect);

                SaveViewDataState();
            }
        }

        private void DrawGizmos(Rect r)
        {
            if (previewContainer == null || previewRenderUtility == null)
            {
                return;
            }
            
            Handles.SetCamera(previewContainer.contentRect, previewRenderUtility.camera);
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
            hybridAnimator.SetTimeRecursively(time);
        }
    }
}