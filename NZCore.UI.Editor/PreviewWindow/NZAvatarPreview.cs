// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Reflection;
using NZCore.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NZCore.UI.Editor
{
    public class NZAvatarPreview
    {
        #region Reflection Access
        private static readonly Type t_GameObjectInspectorType =  typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameObjectInspector");
        private static readonly MethodInfo m_GameObjectInspector_GetRenderableCenterRecurse = t_GameObjectInspectorType.GetMethod("GetRenderableCenterRecurse", BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo m_GameObjectInspector_HasRenderableParts = t_GameObjectInspectorType.GetMethod("HasRenderableParts", BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo m_GameObjectInspector_GetRenderableBounds = t_GameObjectInspectorType.GetMethod("GetRenderableBounds", BindingFlags.Public | BindingFlags.Static);
        
        private static readonly Type t_BlendTree =  typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Animations.BlendTree");
        private static readonly MethodInfo m_BlendTree_GetAnimationClipsFlattened = t_BlendTree.GetMethod("GetAnimationClipsFlattened", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly Type t_ModelImporter =  typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ModelImporter");
        private static readonly MethodInfo m_ModelImporter_CalculateBestFittingPreviewGameObject = t_ModelImporter.GetMethod("CalculateBestFittingPreviewGameObject", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly Type t_AvatarPreviewSelection =  typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AvatarPreviewSelection");
        private static readonly MethodInfo m_AvatarPreviewSelection_GetPreview = t_AvatarPreviewSelection.GetMethod("GetPreview", BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo m_AvatarPreviewSelection_SetPreview = t_AvatarPreviewSelection.GetMethod("SetPreview", BindingFlags.Public | BindingFlags.Static);
        
        private static readonly Type t_EditorUtility=  typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.EditorUtility");
        private static readonly MethodInfo m_EditorUtility_InstantiateForAnimatorPreview = t_EditorUtility.GetMethod("InstantiateForAnimatorPreview", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo m_EditorUtility_InitInstantiatedPreviewRecursive = t_EditorUtility.GetMethod("InstantiateForAnimatorPreview", BindingFlags.NonPublic | BindingFlags.Static);
        
        private static readonly Type t_IAnimationPreviewable =  typeof(Animator).Assembly.GetType("UnityEngine.Animations.IAnimationPreviewable");
        private static readonly MethodInfo m_IAnimationPreviewable_OnPreviewUpdate = t_IAnimationPreviewable.GetMethod("OnPreviewUpdate", BindingFlags.Public | BindingFlags.Instance);
        
        private static readonly MethodInfo getComponentsMethod = typeof(GameObject).GetMethod("GetComponentsInChildren", new[] { typeof(Type) });
        #endregion

        #region Constants

        private const string kDefaultAvatarPreviewOption = "DefaultAvatarPreviewOption";
        private const string kIkPref = "AvatarpreviewShowIK";
        private const string k2DPref = "Avatarpreview2D";
        private const string kReferencePref = "AvatarpreviewShowReference";
        private const string kSpeedPref = "AvatarpreviewSpeed";
        private const float kTimeControlRectHeight = 20;

        #endregion

        #region Fields

        public int fps = 60;

        private Material     m_FloorMaterial;
        private Material     m_FloorMaterialSmall;
        private Material     m_ShadowMaskMaterial;
        private Material     m_ShadowPlaneMaterial;

        public PreviewRenderUtility        m_PreviewUtility;
        private GameObject                  m_PreviewInstance;
        private GameObject                  m_ReferenceInstance;
        private GameObject                  m_DirectionInstance;
        private GameObject                  m_PivotInstance;
        private GameObject                  m_RootInstance;
        //private IAnimationPreviewable[]     m_Previewables;
        private object[]                    m_Previewables;
        private float                       m_BoundingVolumeScale;
        private Motion                      m_SourcePreviewMotion;
        private Animator                    m_SourceScenePreviewAnimator;

        private const string                s_PreviewStr = "Preview";
        private int                         m_PreviewHint = s_PreviewStr.GetHashCode();

        private const string                s_PreviewSceneStr = "PreviewSene";
        private int                         m_PreviewSceneHint = s_PreviewSceneStr.GetHashCode();

        private Texture2D                   m_FloorTexture;
        private Mesh                        m_FloorPlane;

        private bool                        m_ShowReference = false;
        private bool                        m_IKOnFeet = false;
        private bool                        m_ShowIKOnFeetButton = true;
        private bool                        m_2D;
        private bool                        m_IsValid;

        private const float kFloorFadeDuration = 0.2f;
        private const float kFloorScale = 5;
        private const float kFloorScaleSmall = 0.2f;
        private const float kFloorTextureScale = 4;
        private const float kFloorAlpha = 0.5f;
        private const float kFloorShadowAlpha = 0.3f;
        private const float kDefaultIntensity = 1.4f;

        private const int kDefaultLayer = 0; // Must match kDefaultLayer in TagTypes.h

        private float m_PrevFloorHeight = 0;
        private float m_NextFloorHeight = 0;

        public Vector2 m_PreviewDir = new Vector2(120, -20);
        public float m_AvatarScale = 1.0f;
        public float m_ZoomFactor = 1.0f;
        public Vector3 m_PivotPositionOffset = Vector3.zero;
        
        //private float m_LastNormalizedTime = -1000;
        //private float m_LastStartTime = -1000;
        //private float m_LastStopTime = -1000;
        private bool m_NextTargetIsForward = true;

        private static NZAvatarPreview instance;

        #endregion
        
        #region Properties

        public static NZAvatarPreview Instance
        {
            get => instance;
        }
        
        public OnAvatarChange OnAvatarChangeFunc
        {
            set => m_OnAvatarChangeFunc = value;
        }

        public bool IKOnFeet => m_IKOnFeet;

        public bool ShowIKOnFeetButton
        {
            get => m_ShowIKOnFeetButton;
            set => m_ShowIKOnFeetButton = value;
        }

        public bool is2D
        {
            get => m_2D;
            set
            {
                m_2D = value;
                if (m_2D)
                {
                    m_PreviewDir = new Vector2();
                }
            }
        }

        public Animator Animator => m_PreviewInstance != null ? m_PreviewInstance.GetComponent(typeof(Animator)) as Animator : null;

        public GameObject PreviewObject => m_PreviewInstance;

        public ModelImporterAnimationType animationClipType => GetAnimationType(m_SourcePreviewMotion);

        public Vector3 bodyPosition
        {
            get
            {
                if (Animator && Animator.isHuman)
                    return Animator.bodyPosition;

                if (m_PreviewInstance != null)
                {
                    return (Vector3)m_GameObjectInspector_GetRenderableCenterRecurse.Invoke(null, new object[] { m_PreviewInstance, 1, 8 });
                    //return GameObjectInspector.GetRenderableCenterRecurse(m_PreviewInstance, 1, 8);
                }

                return Vector3.zero;
            }
        }
        
        public PreviewRenderUtility PreviewUtility
        {
            get
            {
                if (m_PreviewUtility != null)
                {
                    return m_PreviewUtility;
                }

                m_PreviewUtility = new PreviewRenderUtility
                {
                    camera =
                    {
                        fieldOfView = 30.0f,
                        allowHDR = false,
                        allowMSAA = false
                    },
                    ambientColor = new Color(.1f, .1f, .1f, 0)
                };
                    
                m_PreviewUtility.lights[0].intensity = kDefaultIntensity;
                m_PreviewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0);
                m_PreviewUtility.lights[1].intensity = kDefaultIntensity;
                
                return m_PreviewUtility;
            }
        }

        public Vector3 rootPosition => m_PreviewInstance ? m_PreviewInstance.transform.position : Vector3.zero;
        #endregion
        
        #region GUIStyles
        private class Styles
        {
            public readonly GUIContent pivot = EditorGUIUtility.TrIconContent("AvatarPivot", "Displays avatar's pivot and mass center");
            public readonly GUIContent ik = EditorGUIUtility.TrTextContent("IK", "Toggles feet IK preview");
            public readonly GUIContent is2D = EditorGUIUtility.TrIconContent("SceneView2D", "Toggles 2D preview mode");
            public readonly GUIContent avatarIcon = EditorGUIUtility.TrIconContent("AvatarSelector", "Changes the model to use for previewing.");

            public readonly GUIStyle preButton = "toolbarbutton";
            public readonly GUIStyle preSlider = "preSlider";
            public readonly GUIStyle preSliderThumb = "preSliderThumb";
        }
        private static Styles s_Styles;
        #endregion
        
        public delegate void OnAvatarChange();

        private OnAvatarChange m_OnAvatarChangeFunc;
        
        public NZAvatarPreview(Animator previewObjectInScene, Motion objectOnSameAsset)
        {
            instance = this;
            
            InitInstance(previewObjectInScene, objectOnSameAsset);
        }
        
        private void Init()
        {
            if (s_Styles == null)
                s_Styles = new Styles();

            if (m_FloorPlane == null)
            {
                m_FloorPlane = Resources.GetBuiltinResource(typeof(Mesh), "New-Plane.fbx") as Mesh;
            }

            if (m_FloorTexture == null)
            {
                m_FloorTexture = (Texture2D)EditorGUIUtility.Load("Avatar/Textures/AvatarFloor.png");
            }

            if (m_FloorMaterial == null)
            {
                Shader shader = EditorGUIUtility.LoadRequired("Previews/PreviewPlaneWithShadow.shader") as Shader;
                m_FloorMaterial = new Material(shader)
                {
                    mainTexture = m_FloorTexture,
                    mainTextureScale = Vector2.one * kFloorScale * kFloorTextureScale
                };
                m_FloorMaterial.SetVector("_Alphas", new Vector4(kFloorAlpha, kFloorShadowAlpha, 0, 0));
                m_FloorMaterial.hideFlags = HideFlags.HideAndDontSave;

                m_FloorMaterialSmall = new Material(m_FloorMaterial)
                {
                    mainTextureScale = Vector2.one * kFloorScaleSmall * kFloorTextureScale,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (m_ShadowMaskMaterial == null)
            {
                Shader shader = EditorGUIUtility.LoadRequired("Previews/PreviewShadowMask.shader") as Shader;
                m_ShadowMaskMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (m_ShadowPlaneMaterial == null)
            {
                Shader shader = EditorGUIUtility.LoadRequired("Previews/PreviewShadowPlaneClip.shader") as Shader;
                m_ShadowPlaneMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }
        
        private void InitInstance(Animator scenePreviewObject, Motion motion)
        {
            m_SourcePreviewMotion = motion;
            m_SourceScenePreviewAnimator = scenePreviewObject;

            if (m_PreviewInstance == null)
            {
                GameObject go = CalculatePreviewGameObject(scenePreviewObject, motion, animationClipType);
                SetupBounds(go);
            }

            if (m_ReferenceInstance == null)
            {
                GameObject referenceGO = (GameObject)EditorGUIUtility.Load("Avatar/dial_flat.prefab");
                m_ReferenceInstance = (GameObject)Object.Instantiate(referenceGO, Vector3.zero, Quaternion.identity);
                m_EditorUtility_InitInstantiatedPreviewRecursive.Invoke(null, new object[] { m_ReferenceInstance });
                PreviewUtility.AddSingleGO(m_ReferenceInstance);
            }

            if (m_DirectionInstance == null)
            {
                GameObject directionGO = (GameObject)EditorGUIUtility.Load("Avatar/arrow.fbx");
                m_DirectionInstance = (GameObject)Object.Instantiate(directionGO, Vector3.zero, Quaternion.identity);
                m_EditorUtility_InitInstantiatedPreviewRecursive.Invoke(null, new object[] { m_DirectionInstance });
                PreviewUtility.AddSingleGO(m_DirectionInstance);
            }

            if (m_PivotInstance == null)
            {
                GameObject pivotGO = (GameObject)EditorGUIUtility.Load("Avatar/root.fbx");
                m_PivotInstance = (GameObject)Object.Instantiate(pivotGO, Vector3.zero, Quaternion.identity);
                m_EditorUtility_InitInstantiatedPreviewRecursive.Invoke(null, new object[] { m_PivotInstance });
                PreviewUtility.AddSingleGO(m_PivotInstance);
            }

            if (m_RootInstance == null)
            {
                GameObject rootGO = (GameObject)EditorGUIUtility.Load("Avatar/root.fbx");
                m_RootInstance = (GameObject)Object.Instantiate(rootGO, Vector3.zero, Quaternion.identity);
                m_EditorUtility_InitInstantiatedPreviewRecursive.Invoke(null, new object[] { m_RootInstance });
                PreviewUtility.AddSingleGO(m_RootInstance);
            }

            // Load preview settings from prefs
            m_IKOnFeet = EditorPrefs.GetBool(kIkPref, false);
            m_ShowReference = EditorPrefs.GetBool(kReferencePref, true);
            is2D = EditorPrefs.GetBool(k2DPref, EditorSettings.defaultBehaviorMode == EditorBehaviorMode.Mode2D);

            SetPreviewCharacterEnabled(false, false);

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(motion)) as ModelImporter;
            if (importer && importer.bakeAxisConversion)
            {
                m_PreviewDir += new Vector2(180,0);
            }

            m_PivotPositionOffset = Vector3.zero;
        }
        
        private void SetupBounds(GameObject go)
        {
            m_IsValid = go != null && go != GetGenericAnimationFallback();

            if (go != null)
            {
                m_PreviewInstance = InstantiatePreviewPrefab(go);
                PreviewUtility.AddSingleGO(m_PreviewInstance);

                m_Previewables = (object[]) getComponentsMethod.Invoke(m_PreviewInstance, new object[] { t_IAnimationPreviewable });
                var bounds = (Bounds) m_GameObjectInspector_GetRenderableBounds.Invoke(null, new object[] { m_PreviewInstance });

                m_BoundingVolumeScale = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));


                if (Animator && Animator.isHuman)
                    m_AvatarScale = m_ZoomFactor = Animator.humanScale;
                else
                    m_AvatarScale = m_ZoomFactor = m_BoundingVolumeScale / 2;
            }
        }
        
        private GameObject InstantiatePreviewPrefab(GameObject original)
        {
            if (original == null)
                throw new ArgumentException("The Prefab you want to instantiate is null.");
            
            //GameObject go = EditorUtility.InstantiateRemoveAllNonAnimationComponents(original, Vector3.zero, Quaternion.identity) as GameObject;
            GameObject go = Object.Instantiate(original);
            go.name += "AnimatorPreview";
            go.tag = "Untagged";
            //EditorUtility.InitInstantiatedPreviewRecursive(go);
            m_EditorUtility_InitInstantiatedPreviewRecursive.Invoke(null, new object[] { go });
            
            Animator[] componentsInChildren = go.GetComponentsInChildren<Animator>();
            foreach (var animator in componentsInChildren)
            {
                animator.enabled = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.logWarnings = false;
                animator.fireEvents = false;
            }
            
            if (componentsInChildren.Length == 0)
            {
                Animator animator = go.AddComponent<Animator>();
                animator.enabled = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.logWarnings = false;
                animator.fireEvents = false;
            }
            
            return go;
        }
        
        public void OnDisable()
        {
            if (m_PreviewUtility == null)
            {
                return;
            }

            m_PreviewUtility.Cleanup();
            m_PreviewUtility = null;
        }

        private void SetPreviewCharacterEnabled(bool enabled, bool showReference)
        {
            if (m_PreviewInstance != null)
                SetEnabledRecursive(m_PreviewInstance, enabled);
            
            SetEnabledRecursive(m_ReferenceInstance, showReference && enabled);
            SetEnabledRecursive(m_DirectionInstance, showReference && enabled);
            SetEnabledRecursive(m_PivotInstance, showReference && enabled);
            SetEnabledRecursive(m_RootInstance, showReference && enabled);
        }

        private static void SetEnabledRecursive(GameObject go, bool enabled)
        {
            foreach (Renderer componentsInChild in go.GetComponentsInChildren<Renderer>())
                componentsInChild.enabled = enabled;
        }

        private static AnimationClip GetFirstAnimationClipFromMotion(Motion motion)
        {
            AnimationClip clip = motion as AnimationClip;
            if (clip)
                return clip;
           
            UnityEditor.Animations.BlendTree blendTree = motion as UnityEditor.Animations.BlendTree;
            if (blendTree)
            {
                //AnimationClip[] clips = blendTree.GetAnimationClipsFlattened();
                AnimationClip[] clips = (AnimationClip[]) m_BlendTree_GetAnimationClipsFlattened.Invoke(blendTree, null);
                if (clips.Length > 0)
                    return clips[0];
            }

            return null;
        }

        private static ModelImporterAnimationType GetAnimationType(GameObject go)
        {
            Animator animator = go.GetComponent<Animator>();
            if (animator)
            {
                Avatar avatar = animator.avatar;
                if (avatar && avatar.isHuman)
                    return ModelImporterAnimationType.Human;
                return ModelImporterAnimationType.Generic;
            }

            if (go.GetComponent<Animation>() != null)
            {
                return ModelImporterAnimationType.Legacy;
            }

            return ModelImporterAnimationType.None;
        }

        private static ModelImporterAnimationType GetAnimationType(Motion motion)
        {
            AnimationClip clip = GetFirstAnimationClipFromMotion(motion);
            if (clip)
            {
                if (clip.legacy)
                    return ModelImporterAnimationType.Legacy;
                
                if (clip.humanMotion)
                    return ModelImporterAnimationType.Human;
                
                return ModelImporterAnimationType.Generic;
            }

            return ModelImporterAnimationType.None;
        }

        private static bool IsValidPreviewGameObject(GameObject target, ModelImporterAnimationType requiredClipType)
        {
            if (target != null && !target.activeSelf)
                Debug.LogWarning("Can't preview inactive object, using fallback object");

            return target != null && 
                   target.activeSelf &&
                   //GameObjectInspector.HasRenderableParts(target) &&
                   (bool) m_GameObjectInspector_HasRenderableParts.Invoke(null, new object[] { target }) &&
                !(requiredClipType != ModelImporterAnimationType.None && 
                  GetAnimationType(target) != requiredClipType);
        }

        private static GameObject FindBestFittingRenderableGameObjectFromModelAsset(Object asset, ModelImporterAnimationType animationType)
        {
            if (asset == null)
                return null;

            ModelImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(asset)) as ModelImporter;
            if (importer == null)
                return null;

            //string assetPath = importer.CalculateBestFittingPreviewGameObject();
            string assetPath = (string)m_ModelImporter_CalculateBestFittingPreviewGameObject.Invoke(importer, null);
            GameObject tempGO = AssetDatabase.LoadMainAssetAtPath(assetPath) as GameObject;

            // We should also check for isHumanClip matching the animationclip requiremenets...
            if (IsValidPreviewGameObject(tempGO, ModelImporterAnimationType.None))
                return tempGO;
            else
                return null;
        }

        private static GameObject CalculatePreviewGameObject(Animator selectedAnimator, Motion motion, ModelImporterAnimationType animationType)
        {
            AnimationClip sourceClip = GetFirstAnimationClipFromMotion(motion);

            // Use selected preview
            //GameObject selected = AvatarPreviewSelection.GetPreview(animationType);
            GameObject selected = (GameObject)m_AvatarPreviewSelection_GetPreview.Invoke(null, new object[] { animationType });
            if (IsValidPreviewGameObject(selected, ModelImporterAnimationType.None))
                return selected;

            if (selectedAnimator != null && IsValidPreviewGameObject(selectedAnimator.gameObject, animationType))
                return selectedAnimator.gameObject;

            // Find the best fitting preview game object for the asset we are viewing (Handles @ convention, will pick base path for you)
            selected = FindBestFittingRenderableGameObjectFromModelAsset(sourceClip, animationType);
            if (selected != null)
                return selected;

            return animationType switch
            {
                ModelImporterAnimationType.Human => GetHumanoidFallback(),
                ModelImporterAnimationType.Generic => GetGenericAnimationFallback(),
                _ => null
            };
        }

        private static GameObject GetGenericAnimationFallback()
        {
            return (GameObject)EditorGUIUtility.Load("Avatar/DefaultGeneric.fbx");
        }

        private static GameObject GetHumanoidFallback()
        {
            return (GameObject)EditorGUIUtility.Load("Avatar/DefaultAvatar.fbx");
        }

        public void ResetPreviewInstance()
        {
            Object.DestroyImmediate(m_PreviewInstance);
            GameObject go = CalculatePreviewGameObject(m_SourceScenePreviewAnimator, m_SourcePreviewMotion, animationClipType);
            SetupBounds(go);
        }

        public void DoSelectionChange()
        {
            m_OnAvatarChangeFunc();
        }

        float PreviewSlider(Rect rect, float val, float snapThreshold)
        {
            val = GUI.HorizontalSlider(rect, val, 0.1f, 2.0f, s_Styles.preSlider, s_Styles.preSliderThumb);//, GUILayout.MaxWidth(64));
            if (val > 0.25f - snapThreshold && val < 0.25f + snapThreshold)
                val = 0.25f;
            else if (val > 0.5f - snapThreshold && val < 0.5f + snapThreshold)
                val = 0.5f;
            else if (val > 0.75f - snapThreshold && val < 0.75f + snapThreshold)
                val = 0.75f;
            else if (val > 1.0f - snapThreshold && val < 1.0f + snapThreshold)
                val = 1.0f;
            else if (val > 1.25f - snapThreshold && val < 1.25f + snapThreshold)
                val = 1.25f;
            else if (val > 1.5f - snapThreshold && val < 1.5f + snapThreshold)
                val = 1.5f;
            else if (val > 1.75f - snapThreshold && val < 1.75f + snapThreshold)
                val = 1.75f;

            return val;
        }

        public void DoPreviewSettings()
        {
            Init();

            if (m_ShowIKOnFeetButton)
            {
                EditorGUI.BeginChangeCheck();
                m_IKOnFeet = GUILayout.Toggle(m_IKOnFeet, s_Styles.ik, s_Styles.preButton);
                if (EditorGUI.EndChangeCheck())
                    EditorPrefs.SetBool(kIkPref, m_IKOnFeet);
            }

            EditorGUI.BeginChangeCheck();
            GUILayout.Toggle(is2D, s_Styles.is2D, s_Styles.preButton);
            if (EditorGUI.EndChangeCheck())
            {
                is2D = !is2D;
                EditorPrefs.SetBool(k2DPref, is2D);
            }

            EditorGUI.BeginChangeCheck();
            m_ShowReference = GUILayout.Toggle(m_ShowReference, s_Styles.pivot, s_Styles.preButton);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetBool(kReferencePref, m_ShowReference);

            // if (EditorGUILayout.DropdownButton(s_Styles.avatarIcon, FocusType.Passive, EditorStyles.toolbarDropDownRight))
            // {
            //     GenericMenu menu = new GenericMenu();
            //     menu.AddItem(EditorGUIUtility.TrTextContent("Auto"), false, SetPreviewAvatarOption, PreviewPopupOptions.Auto);
            //     menu.AddItem(EditorGUIUtility.TrTextContent("Unity Model"), false, SetPreviewAvatarOption, PreviewPopupOptions.DefaultModel);
            //     menu.AddItem(EditorGUIUtility.TrTextContent("Other..."), false, SetPreviewAvatarOption, PreviewPopupOptions.Other);
            //     menu.ShowAsContext();
            // }
        }

        private void DoRenderPreview(Rect previewRect, GUIStyle background)
        {
            var probe = RenderSettings.ambientProbe;
            PreviewUtility.BeginPreview(previewRect, background);

            Quaternion bodyRot;
            Quaternion rootRot;
            Vector3 rootPos;
            Vector3 bodyPos = rootPosition;
            Vector3 pivotPos;

            if (Animator && Animator.isHuman)
            {
                rootRot = Animator.rootRotation;
                rootPos = Animator.rootPosition;

                bodyRot = Animator.bodyRotation;

                pivotPos = Animator.pivotPosition;
            }
            else if (Animator && Animator.hasRootMotion)
            {
                rootRot = Animator.rootRotation;
                rootPos = Animator.rootPosition;

                bodyRot = Quaternion.identity;

                pivotPos = Vector3.zero;
            }
            else
            {
                rootRot = Quaternion.identity;
                rootPos = Vector3.zero;

                bodyRot = Quaternion.identity;

                pivotPos = Vector3.zero;
            }

            SetupPreviewLightingAndFx(probe);

            Vector3 direction = bodyRot * Vector3.forward;
            direction[1] = 0;
            Quaternion directionRot = Quaternion.LookRotation(direction);
            Vector3 directionPos = rootPos;

            Quaternion pivotRot = rootRot;

            // Scale all Preview Objects to fit avatar size.
            PositionPreviewObjects(pivotRot, pivotPos, bodyRot, bodyPosition, directionRot, rootRot, rootPos, directionPos, m_AvatarScale);

            bool dynamicFloorHeight = !is2D && Mathf.Abs(m_NextFloorHeight - m_PrevFloorHeight) > m_ZoomFactor * 0.01f;

            // Calculate floor height and alpha
            float mainFloorHeight, mainFloorAlpha;
            if (dynamicFloorHeight)
            {
                float fadeMoment = m_NextFloorHeight < m_PrevFloorHeight ? kFloorFadeDuration : (1 - kFloorFadeDuration);
                //mainFloorHeight = timeControl.normalizedTime < fadeMoment ? m_PrevFloorHeight : m_NextFloorHeight;
                //mainFloorAlpha = Mathf.Clamp01(Mathf.Abs(timeControl.normalizedTime - fadeMoment) / kFloorFadeDuration);
                mainFloorHeight = m_PrevFloorHeight;
                mainFloorAlpha = 1;
            }
            else
            {
                mainFloorHeight = m_PrevFloorHeight;
                mainFloorAlpha = is2D ? 0.5f : 1;
            }

            Quaternion floorRot = is2D ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
            Vector3 floorPos = m_ReferenceInstance.transform.position;
            floorPos.y = mainFloorHeight;

            // Render shadow map
            Matrix4x4 shadowMatrix;
            RenderTexture shadowMap = RenderPreviewShadowmap(PreviewUtility.lights[0], m_BoundingVolumeScale / 2, bodyPosition, floorPos, out shadowMatrix);

            // SRP might initialize the light settings during the first frame of rendering
            // (e.g HDRP is overriding the intensity value during 'InitDefaultHDAdditionalLightData').
            // So this call is necessary to avoid a flickering when selecting an animation clip.
            if (PreviewUtility.lights[0].intensity != kDefaultIntensity || PreviewUtility.lights[0].intensity != kDefaultIntensity)
            {
                SetupPreviewLightingAndFx(probe);
            }

            float tempZoomFactor = (is2D ? 1.0f : m_ZoomFactor);
            // Position camera
            PreviewUtility.camera.orthographic = is2D;
            if (is2D)
                PreviewUtility.camera.orthographicSize = 2.0f * m_ZoomFactor;
            PreviewUtility.camera.nearClipPlane = 0.5f * tempZoomFactor;
            PreviewUtility.camera.farClipPlane = 100.0f * m_AvatarScale;
            Quaternion camRot = Quaternion.Euler(-m_PreviewDir.y, -m_PreviewDir.x, 0);

            // Add panning offset
            Vector3 camPos = camRot * (Vector3.forward * -5.5f * tempZoomFactor) + bodyPos + m_PivotPositionOffset;
            PreviewUtility.camera.transform.position = camPos;
            PreviewUtility.camera.transform.rotation = camRot;
            
            SetPreviewCharacterEnabled(true, m_ShowReference);
            foreach (var previewable in m_Previewables)
            {
                //previewable.OnPreviewUpdate();
                m_IAnimationPreviewable_OnPreviewUpdate.Invoke(previewable, null);
            }

            PreviewUtility.Render(option != PreviewPopupOptions.DefaultModel);
            SetPreviewCharacterEnabled(false, false);

            // Texture offset - negative in order to compensate the floor movement.
            Vector2 textureOffset = -new Vector2(floorPos.x, is2D ? floorPos.y : floorPos.z);

            // Render main floor
            {
                Material mat = m_FloorMaterial;
                Matrix4x4 matrix = Matrix4x4.TRS(floorPos, floorRot, Vector3.one * kFloorScale * m_AvatarScale);

                mat.mainTextureOffset = textureOffset * kFloorScale * 0.08f * (1.0f / m_AvatarScale);
                mat.SetTexture("_ShadowTexture", shadowMap);
                mat.SetMatrix("_ShadowTextureMatrix", shadowMatrix);
                mat.SetVector("_Alphas", new Vector4(kFloorAlpha * mainFloorAlpha, kFloorShadowAlpha * mainFloorAlpha, 0, 0));
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;

                Graphics.DrawMesh(m_FloorPlane, matrix, mat, kDefaultLayer, PreviewUtility.camera, 0);
            }

            // Render small floor
            if (dynamicFloorHeight)
            {
                bool topIsNext = m_NextFloorHeight > m_PrevFloorHeight;
                float floorHeight = topIsNext ? m_NextFloorHeight : m_PrevFloorHeight;
                float otherFloorHeight = topIsNext ? m_PrevFloorHeight : m_NextFloorHeight;
                float floorAlpha = (floorHeight == mainFloorHeight ? 1 - mainFloorAlpha : 1) * Mathf.InverseLerp(otherFloorHeight, floorHeight, rootPos.y);
                floorPos.y = floorHeight;

                Material mat = m_FloorMaterialSmall;
                mat.mainTextureOffset = textureOffset * kFloorScaleSmall * 0.08f;
                mat.SetTexture("_ShadowTexture", shadowMap);
                mat.SetMatrix("_ShadowTextureMatrix", shadowMatrix);
                mat.SetVector("_Alphas", new Vector4(kFloorAlpha * floorAlpha, 0, 0, 0));
                Matrix4x4 matrix = Matrix4x4.TRS(floorPos, floorRot, Vector3.one * kFloorScaleSmall * m_AvatarScale);
                Graphics.DrawMesh(m_FloorPlane, matrix, mat, kDefaultLayer, PreviewUtility.camera, 0);
            }

            var clearMode = PreviewUtility.camera.clearFlags;
            PreviewUtility.camera.clearFlags = CameraClearFlags.Nothing;
            PreviewUtility.Render(false);
            PreviewUtility.camera.clearFlags = clearMode;
            RenderTexture.ReleaseTemporary(shadowMap);
        }
        
        private RenderTexture RenderPreviewShadowmap(Light light, float scale, Vector3 center, Vector3 floorPos, out Matrix4x4 outShadowMatrix)
        {
            Assert.IsTrue(Event.current.type == EventType.Repaint);

            // Set ortho camera and position it
            var cam = PreviewUtility.camera;
            cam.orthographic = true;
            cam.orthographicSize = scale * 2.0f;
            cam.nearClipPlane = 1 * scale;
            cam.farClipPlane = 25 * scale;
            cam.transform.rotation = is2D ? Quaternion.identity : light.transform.rotation;
            cam.transform.position = center - light.transform.forward * (scale * 5.5f);

            // Clear to black
            CameraClearFlags oldFlags = cam.clearFlags;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Color oldColor = cam.backgroundColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);

            // Create render target for shadow map
            const int kShadowSize = 256;
            RenderTexture oldRT = cam.targetTexture;
            RenderTexture rt = RenderTexture.GetTemporary(kShadowSize, kShadowSize, 16);
            rt.isPowerOfTwo = true;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = FilterMode.Bilinear;
            cam.targetTexture = rt;

            // Enable character and render with camera into the shadowmap
            SetPreviewCharacterEnabled(true, false);
            m_PreviewUtility.camera.Render();

            // Draw a quad, with shader that will produce white color everywhere
            // where something was rendered (via inverted depth test)
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadOrtho();
            m_ShadowMaskMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Vertex3(0, 0, -99.0f);
            GL.Vertex3(1, 0, -99.0f);
            GL.Vertex3(1, 1, -99.0f);
            GL.Vertex3(0, 1, -99.0f);
            GL.End();

            // Render floor with black color, to mask out any shadow from character
            // parts that are under the preview plane
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.LoadIdentity();
            GL.MultMatrix(cam.worldToCameraMatrix);
            m_ShadowPlaneMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            float sc = kFloorScale * scale;
            GL.Vertex(floorPos + new Vector3(-sc, 0, -sc));
            GL.Vertex(floorPos + new Vector3(sc, 0, -sc));
            GL.Vertex(floorPos + new Vector3(sc, 0, sc));
            GL.Vertex(floorPos + new Vector3(-sc, 0, sc));
            GL.End();

            GL.PopMatrix();

            // Shadowmap sampling matrix, from world space into shadowmap space
            Matrix4x4 texMatrix = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity,
                new Vector3(0.5f, 0.5f, 0.5f));
            outShadowMatrix = texMatrix * cam.projectionMatrix * cam.worldToCameraMatrix;

            // Restore previous camera parameters
            cam.orthographic = false;
            cam.clearFlags = oldFlags;
            cam.backgroundColor = oldColor;
            cam.targetTexture = oldRT;

            return rt;
        }

        private void SetupPreviewLightingAndFx(SphericalHarmonicsL2 probe)
        {
            PreviewUtility.lights[0].intensity = kDefaultIntensity;
            PreviewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0);
            PreviewUtility.lights[1].intensity = kDefaultIntensity;
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientLight = new Color(0.1f, 0.1f, 0.1f, 1.0f);
            RenderSettings.ambientProbe = probe;
        }
       
        private void PositionPreviewObjects(Quaternion pivotRot, Vector3 pivotPos, Quaternion bodyRot, Vector3 bodyPos,
            Quaternion directionRot, Quaternion rootRot, Vector3 rootPos, Vector3 directionPos,
            float scale)
        {
            m_ReferenceInstance.transform.position = rootPos;
            m_ReferenceInstance.transform.rotation = rootRot;
            m_ReferenceInstance.transform.localScale = Vector3.one * scale * 1.25f;

            m_DirectionInstance.transform.position = directionPos;
            m_DirectionInstance.transform.rotation = directionRot;
            m_DirectionInstance.transform.localScale = Vector3.one * scale * 2;

            m_PivotInstance.transform.position = pivotPos;
            m_PivotInstance.transform.rotation = pivotRot;
            m_PivotInstance.transform.localScale = Vector3.one * scale * 0.1f;

            m_RootInstance.transform.position = bodyPos;
            m_RootInstance.transform.rotation = bodyRot;
            m_RootInstance.transform.localScale = Vector3.one * scale * 0.25f;

            if (Animator)
            {
                //float normalizedTime = timeControl.normalizedTime;
                //float normalizedDelta = timeControl.deltaTime / (timeControl.stopTime - timeControl.startTime);

                // Always set last height to next height after wrapping the time.
                //if (normalizedTime - normalizedDelta < 0 || normalizedTime - normalizedDelta >= 1)
                    m_PrevFloorHeight = m_NextFloorHeight;

                // Check that AvatarPreview is getting reliable info about time and deltaTime.
                // if (m_LastNormalizedTime != -1000 && timeControl.startTime == m_LastStartTime && timeControl.stopTime == m_LastStopTime)
                // {
                //     float difference = normalizedTime - normalizedDelta - m_LastNormalizedTime;
                //     if (difference > 0.5f)
                //         difference -= 1;
                //     else if (difference < -0.5f)
                //         difference += 1;
                // }
                // m_LastNormalizedTime = normalizedTime;
                // m_LastStartTime = timeControl.startTime;
                // m_LastStopTime = timeControl.stopTime;

                // Alternate getting the height for next time and previous time.
                if (m_NextTargetIsForward)
                    m_NextFloorHeight = Animator.targetPosition.y;
                else
                    m_PrevFloorHeight = Animator.targetPosition.y;

                // Flip next target time.
                m_NextTargetIsForward = !m_NextTargetIsForward;
                Animator.SetTarget(AvatarTarget.Root, m_NextTargetIsForward ? 1 : 0);
            }
        }

        public void AvatarTimeControlGUI(Rect rect)
        {
            //const float kSliderWidth = 150f;
            //const float kSpacing = 4f;
            //Rect timeControlRect = rect;

            // background
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);

            //timeControlRect.height = kTimeControlRectHeight;
            //timeControlRect.xMax -= kSliderWidth;

            // Rect sliderControlRect = rect;
            // sliderControlRect.height = kTimeControlRectHeight;
            // sliderControlRect.yMin += 1;
            // sliderControlRect.yMax -= 1;
            // sliderControlRect.xMin = sliderControlRect.xMax - kSliderWidth + kSpacing;
            //
            // //timeControl.DoTimeControl(timeControlRect);
            // Rect labelRect = new Rect(new Vector2(rect.x, rect.y), EditorStyles.toolbarLabel.CalcSize(EditorGUIUtility.TrTempContent("xxxxxx")));;
            // labelRect.x = rect.xMax - labelRect.width;
            // labelRect.yMin = rect.yMin;
            // labelRect.yMax = rect.yMax;
            //
            // sliderControlRect.xMax = labelRect.xMin;

            // EditorGUI.BeginChangeCheck();
            // timeControl.playbackSpeed = PreviewSlider(sliderControlRect, timeControl.playbackSpeed, 0.03f);
            // if (EditorGUI.EndChangeCheck())
            //     EditorPrefs.SetFloat(kSpeedPref, timeControl.playbackSpeed);
            // GUI.Label(labelRect, timeControl.playbackSpeed.ToString("f2", CultureInfo.InvariantCulture.NumberFormat) + "x", EditorStyles.toolbarLabel);
            //
            // // Show current time in seconds:frame and in percentage
            // rect.y = rect.yMax - 24;
            // float time = timeControl.currentTime - timeControl.startTime;
            // EditorGUI.DropShadowLabel(new Rect(rect.x, rect.y, rect.width, 20),
            //     UnityString.Format("{0,2}:{1:00} ({2:000.0%}) Frame {3}", (int)time, Repeat(Mathf.FloorToInt(time * fps), fps), timeControl.normalizedTime, Mathf.FloorToInt(timeControl.currentTime * fps))
            // );
        }

        enum PreviewPopupOptions : int
        {
            Auto = 0,
            DefaultModel = 1,
            Other = 2
        }

        protected enum ViewTool { None, Pan, Zoom, Orbit }
        protected ViewTool m_ViewTool = ViewTool.None;
        protected ViewTool viewTool
        {
            get
            {
                Event evt = Event.current;
                if (m_ViewTool == ViewTool.None)
                {
                    bool controlKeyOnMac = (evt.control && Application.platform == RuntimePlatform.OSXEditor);

                    // actionKey could be command key on mac or ctrl on windows
                    bool actionKey = EditorGUI.actionKey;

                    bool noModifiers = (!actionKey && !controlKeyOnMac && !evt.alt);

                    if ((evt.button <= 0 && noModifiers) || (evt.button <= 0 && actionKey) || evt.button == 2)
                        m_ViewTool = ViewTool.Pan;
                    else if ((evt.button <= 0 && controlKeyOnMac) || (evt.button == 1 && evt.alt))
                        m_ViewTool = ViewTool.Zoom;
                    else if (evt.button <= 0 && evt.alt || evt.button == 1)
                        m_ViewTool = ViewTool.Orbit;
                }
                return m_ViewTool;
            }
        }

        protected MouseCursor currentCursor
        {
            get
            {
                switch (m_ViewTool)
                {
                    case ViewTool.Orbit: return MouseCursor.Orbit;
                    case ViewTool.Pan: return MouseCursor.Pan;
                    case ViewTool.Zoom: return MouseCursor.Zoom;
                    default: return MouseCursor.Arrow;
                }
            }
        }


        protected void HandleMouseDown(Event evt, int id, Rect previewRect)
        {
            if (viewTool != ViewTool.None && previewRect.Contains(evt.mousePosition))
            {
                EditorGUIUtility.SetWantsMouseJumping(1);
                evt.Use();
                GUIUtility.hotControl = id;
            }
        }

        protected void HandleMouseUp(Event evt, int id)
        {
            if (GUIUtility.hotControl == id)
            {
                m_ViewTool = ViewTool.None;

                GUIUtility.hotControl = 0;
                EditorGUIUtility.SetWantsMouseJumping(0);
                evt.Use();
            }
        }

        protected void HandleMouseDrag(Event evt, int id, Rect previewRect)
        {
            if (m_PreviewInstance == null)
                return;

            if (GUIUtility.hotControl == id)
            {
                switch (m_ViewTool)
                {
                    case ViewTool.Orbit:    DoAvatarPreviewOrbit(evt, previewRect); break;
                    case ViewTool.Pan:      DoAvatarPreviewPan(evt); break;

                    // case 605415 invert zoom delta to match scene view zooming
                    case ViewTool.Zoom:     DoAvatarPreviewZoom(evt, -HandleUtility.niceMouseDeltaZoom * (evt.shift ? 2.0f : 0.5f)); break;
                    default:                Debug.Log("Enum value not handled"); break;
                }
            }
        }

        protected void HandleViewTool(Event evt, EventType eventType, int id, Rect previewRect)
        {
            switch (eventType)
            {
                case EventType.ScrollWheel: DoAvatarPreviewZoom(evt, HandleUtility.niceMouseDeltaZoom * (evt.shift ? 2.0f : 0.5f)); break;
                case EventType.MouseDown:   HandleMouseDown(evt, id, previewRect); break;
                case EventType.MouseUp:     HandleMouseUp(evt, id); break;
                case EventType.MouseDrag:   HandleMouseDrag(evt, id, previewRect); break;
            }
        }

        public void DoAvatarPreviewDrag(Event evt, EventType type)
        {
            if (type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                evt.Use();
            }
            else if (type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                GameObject newPreviewObject = DragAndDrop.objectReferences[0] as GameObject;

                if (newPreviewObject)
                {
                    DragAndDrop.AcceptDrag();
                    SetPreview(newPreviewObject);
                }

                evt.Use();
            }
        }

        public void DoAvatarPreviewOrbit(Event evt, Rect previewRect)
        {
            //Reset 2D on Orbit
            if (is2D)
            {
                is2D = false;
            }
            m_PreviewDir -= evt.delta * (evt.shift ? 3 : 1) / Mathf.Min(previewRect.width, previewRect.height) * 140.0f;
            m_PreviewDir.y = Mathf.Clamp(m_PreviewDir.y, -90, 90);
            evt.Use();
        }

        public void DoAvatarPreviewPan(Event evt)
        {
            Camera cam = PreviewUtility.camera;
            Vector3 screenPos = cam.WorldToScreenPoint(bodyPosition + m_PivotPositionOffset);
            Vector3 delta = new Vector3(-evt.delta.x, evt.delta.y, 0);
            // delta panning is scale with the zoom factor to allow fine tuning when user is zooming closely.
            screenPos += delta * Mathf.Lerp(0.25f, 2.0f, m_ZoomFactor * 0.5f);
            Vector3 worldDelta = cam.ScreenToWorldPoint(screenPos) - (bodyPosition + m_PivotPositionOffset);
            m_PivotPositionOffset += worldDelta;
            evt.Use();
        }

        public void ResetPreviewFocus()
        {
            m_PivotPositionOffset = bodyPosition - rootPosition;
        }

        public void DoAvatarPreviewFrame(Event evt, EventType type, Rect previewRect)
        {
            if (type == EventType.KeyDown && evt.keyCode == KeyCode.F)
            {
                ResetPreviewFocus();
                m_ZoomFactor = m_AvatarScale;
                evt.Use();
            }

            if (type == EventType.KeyDown && Event.current.keyCode == KeyCode.G)
            {
                m_PivotPositionOffset = GetCurrentMouseWorldPosition(evt, previewRect) - bodyPosition;
                evt.Use();
            }
        }

        protected Vector3 GetCurrentMouseWorldPosition(Event evt, Rect previewRect)
        {
            Camera cam = PreviewUtility.camera;

            float scaleFactor = PreviewUtility.GetScaleFactor(previewRect.width, previewRect.height);
            Vector3 mouseLocal = new Vector3((evt.mousePosition.x - previewRect.x) * scaleFactor, (previewRect.height - (evt.mousePosition.y - previewRect.y)) * scaleFactor, 0);
            mouseLocal.z = Vector3.Distance(bodyPosition, cam.transform.position);
            return cam.ScreenToWorldPoint(mouseLocal);
        }

        public void DoAvatarPreviewZoom(Event evt, float delta)
        {
            float zoomDelta = -delta * 0.05f;
            m_ZoomFactor += m_ZoomFactor * zoomDelta;

            // zoom is clamp too 10 time closer than the original zoom
            m_ZoomFactor = Mathf.Max(m_ZoomFactor, m_AvatarScale / 10.0f);
            evt.Use();
        }

        public void DoAvatarPreview(Rect rect, GUIStyle background)
        {
            Init();

            Rect choserRect = new Rect(rect.xMax - 16, rect.yMax - 16, 16, 16);
            if (EditorGUI.DropdownButton(choserRect, GUIContent.none, FocusType.Passive, GUIStyle.none))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(EditorGUIUtility.TrTextContent("Auto"), false, SetPreviewAvatarOption, PreviewPopupOptions.Auto);
                menu.AddItem(EditorGUIUtility.TrTextContent("Unity Model"), false, SetPreviewAvatarOption, PreviewPopupOptions.DefaultModel);
                menu.AddItem(EditorGUIUtility.TrTextContent("Other..."), false, SetPreviewAvatarOption, PreviewPopupOptions.Other);
                menu.ShowAsContext();
            }

            Rect previewRect = rect;
            previewRect.yMin += kTimeControlRectHeight;
            previewRect.height = Mathf.Max(previewRect.height, 64f);

            int previewID = GUIUtility.GetControlID(m_PreviewHint, FocusType.Passive, previewRect);
            Event evt = Event.current;
            EventType type = evt.GetTypeForControl(previewID);

            if (type == EventType.Repaint && m_IsValid)
            {
                DoRenderPreview(previewRect, background);
                PreviewUtility.EndAndDrawPreview(previewRect);
            }

            AvatarTimeControlGUI(rect);


            int previewSceneID = GUIUtility.GetControlID(m_PreviewSceneHint, FocusType.Passive);
            type = evt.GetTypeForControl(previewSceneID);

            DoAvatarPreviewDrag(evt, type);
            HandleViewTool(evt, type, previewSceneID, previewRect);
            DoAvatarPreviewFrame(evt, type, previewRect);

            if (!m_IsValid)
            {
                Rect warningRect = previewRect;
                warningRect.yMax -= warningRect.height / 2 - 16;
                EditorGUI.DropShadowLabel(
                    warningRect,
                    "No model is available for preview.\nPlease drag a model into this Preview Area.");
            }

            // Apply the current cursor
            if (evt.type == EventType.Repaint)
                EditorGUIUtility.AddCursorRect(previewRect, currentCursor);
        }

        private PreviewPopupOptions option
        {
            get => (PreviewPopupOptions) EditorPrefs.GetInt(kDefaultAvatarPreviewOption);
            set => EditorPrefs.SetInt(kDefaultAvatarPreviewOption, (int)value);
        }

        void SetPreviewAvatarOption(object obj)
        {
            var newSelectedOption = (PreviewPopupOptions)obj;

            if (option != newSelectedOption)
            {
                option = newSelectedOption;

                switch (option)
                {
                    case PreviewPopupOptions.Auto:
                        SetPreview(null);
                        break;
                    case PreviewPopupOptions.DefaultModel:
                        SetPreview(GetHumanoidFallback());
                        break;
                    //case PreviewPopupOptions.Other:
                    //     ObjectSelectorOperation.Start(this);
                    //    break;
                }
            }
        }

        void SetPreview(GameObject gameObject)
        {
            //AvatarPreviewSelection.SetPreview(animationClipType, gameObject);
            m_AvatarPreviewSelection_SetPreview.Invoke(null, new object[] { animationClipType, gameObject });

            Object.DestroyImmediate(m_PreviewInstance);
            InitInstance(m_SourceScenePreviewAnimator, m_SourcePreviewMotion);

            if (m_OnAvatarChangeFunc != null)
                m_OnAvatarChangeFunc();
        }

        int Repeat(int t, int length)
        {
            // Have to do double modulo in order to work for negative numbers.
            // This is quicker than a branch to test for negative number.
            return ((t % length) + length) % length;
        }
    }
}
