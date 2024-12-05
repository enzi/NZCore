// <copyright project="NZSpellCasting.SpellBuilder.Authoring" file="PreviewWindow.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using NZCore.Editor;
using NZCore.Hybrid;
using NZCore.UI.Elements;
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
        public static PreviewWindow Instance;
        public static readonly List<DeferredGizmo> DeferredGizmos = new();
        
        private static readonly Type avatarPreviewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AvatarPreview");
        private static readonly Type previewRenderUtilityType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PreviewRenderUtility");
        
        private static readonly MethodInfo k_Cleanup = avatarPreviewType.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo k_DoAvatarPreview = avatarPreviewType.GetMethod("DoAvatarPreview", BindingFlags.Instance | BindingFlags.Public);
        private static readonly PropertyInfo k_Animator = avatarPreviewType.GetProperty("Animator", BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo k_PreviewUtility = avatarPreviewType.GetField("m_PreviewUtility", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo k_RenderTexture = previewRenderUtilityType.GetField("m_RenderTexture", BindingFlags.Instance | BindingFlags.NonPublic);
        
        private object avatarPreview;
        private PreviewRenderUtility previewRenderUtility;

        private GameObject previewObject;

        public HybridAnimator HybridAnimator;

        private AnimationClip previewAnimationClip;

        public Action OnSetupFinished;

        public PreviewWindow()
        {
            Instance = this;
            
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.enzisoft.nzcore/NZCore.UI.Editor/PreviewWindow/PreviewWindow.uss");
            styleSheets.Add(styles);
            
            AddToClassList("preview-container");
            
            var objectField = new ObjectField
            {
                label = "Preview Object",
                objectType = typeof(GameObject)
            };

            objectField.viewDataKey = "preview-window-object";
            
            objectField.RegisterValueChangedCallback(evt => {
                previewObject = evt.newValue as GameObject;
                
                SetupAvatarPreview();
            });
            
            Add(objectField);
            
            var previewContainer = new IMGUIContainer();
            previewContainer.onGUIHandler = () => 
            {
                if (avatarPreview != null)
                {
                    k_DoAvatarPreview.Invoke(avatarPreview, new object[] { previewContainer.contentRect, GUIStyle.none });
                }
                
                if (Event.current.type == EventType.Repaint)
                {
                    DrawGizmos(previewContainer.contentRect);
                }
            };
            
            previewContainer.AddToClassList("imgui-preview-container");
            
            Add(previewContainer);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanelEvent);
        }

        private void SetupAvatarPreview()
        {
            // Cleanup existing preview
            if (avatarPreview != null)
            {
                //avatarPreview.Cleanup();
                k_Cleanup.Invoke(avatarPreview, new object[] { });
                avatarPreview = null;
            }

            if (previewObject == null) return;

            // Get the Animator and Avatar
            var animator = previewObject.GetComponent<Animator>();
            if (animator == null || animator.avatar == null) 
                return;

            // Create Avatar Preview
            avatarPreview = Activator.CreateInstance(avatarPreviewType, BindingFlags.Instance | BindingFlags.Public, null, new object[] { animator, null }, null);

            var previewAnimator = k_Animator.GetValue(avatarPreview) as Animator;
            previewAnimator.enabled = true;
            HybridAnimator = HybridEntity.CreatePlayableGraph(previewAnimator);

            previewRenderUtility = (PreviewRenderUtility) k_PreviewUtility.GetValue(avatarPreview);
            
            OnSetupFinished();
        }

        private void OnDetachFromPanelEvent(DetachFromPanelEvent evt)
        {
            Debug.Log("OnDetachFromPanelEvent");
            
            if (avatarPreview != null)
            {
                var disposeMethod = avatarPreview.GetType().GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                disposeMethod?.Invoke(avatarPreview, null);
            }
        }

        private void DrawGizmos(Rect r)
        {
            if (previewRenderUtility == null)
                return;
            
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            int width = (int) (r.width * (double) pixelsPerPoint);
            int height = (int) (r.height * (double) pixelsPerPoint);
            var renderTexture = new RenderTexture(width, height, GraphicsFormat.R16G16B16A16_SFloat, SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil));
          
            RenderTexture.active = renderTexture;
            GL.Clear(false, false, Color.clear);
            var mat = new Material(Shader.Find("Hidden/Internal-Colored"));
            mat.SetPass(0);
            
            GL.PushMatrix();
             
            GL.modelview = previewRenderUtility.camera.worldToCameraMatrix;
            GL.LoadProjectionMatrix(previewRenderUtility.camera.projectionMatrix);
               
            foreach (var deferredGizmo in DeferredGizmos)
            {
                switch (deferredGizmo.Type)
                {
                    case GizmoType.Sphere:
                        break;
                    case GizmoType.Capsule:
                        WireframeUtility.DrawWireframeCapsule(deferredGizmo.Position, deferredGizmo.Rotation, deferredGizmo.Radius, deferredGizmo.Length);
                        break;
                    case GizmoType.Box:
                        //WireframeUtility.DrawBox(deferredGizmo.Position, new Vector3(1, 1, 1));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            GL.PopMatrix();
            GL.Flush();
            
            RenderTexture.active = null;
            
            GUI.DrawTexture(r, renderTexture);
            
            Object.DestroyImmediate(renderTexture);
            
            DeferredGizmos.Clear();
        }

        public static void RenderTextureToFile(RenderTexture rt, string filepath)
        {
            var prevRt = RenderTexture.active;
            RenderTexture.active = rt;
            
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            
            RenderTexture.active = prevRt;

            byte[] bytes = tex.EncodeToPNG();
            
            System.IO.File.WriteAllBytes(filepath, bytes);
        }
        
        public void SetTimeRecursively(double time)
        {
            HybridAnimator?.SetTimeRecursively(time);
        }
    }
}