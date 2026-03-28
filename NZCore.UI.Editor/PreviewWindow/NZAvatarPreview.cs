// <copyright project="NZCore.UI.Editor" file="NZAvatarPreview.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Reflection;
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

        private static readonly Type GameObjectInspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameObjectInspector");

        private static readonly MethodInfo GetRenderableCenterRecurseMethodInfo =
            GameObjectInspectorType.GetMethod("GetRenderableCenterRecurse", BindingFlags.Public | BindingFlags.Static);

        private static readonly MethodInfo HasRenderablePartsMethodInfo =
            GameObjectInspectorType.GetMethod("HasRenderableParts", BindingFlags.Public | BindingFlags.Static);

        private static readonly MethodInfo GetRenderableBoundsMethodInfo =
            GameObjectInspectorType.GetMethod("GetRenderableBounds", BindingFlags.Public | BindingFlags.Static);

        private static readonly Type BlendTreeType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Animations.BlendTree");

        private static readonly MethodInfo GetAnimationClipsFlattenedMethodInfo =
            BlendTreeType.GetMethod("GetAnimationClipsFlattened", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Type ModelImporterType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ModelImporter");

        private static readonly MethodInfo CalculateBestFittingPreviewGameObjectMethodInfo =
            ModelImporterType.GetMethod("CalculateBestFittingPreviewGameObject", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Type AvatarPreviewSelectionType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AvatarPreviewSelection");

        private static readonly MethodInfo GetPreviewMethodInfo =
            AvatarPreviewSelectionType.GetMethod("GetPreview", BindingFlags.Public | BindingFlags.Static);

        private static readonly MethodInfo SetPreviewMethodInfo =
            AvatarPreviewSelectionType.GetMethod("SetPreview", BindingFlags.Public | BindingFlags.Static);

        private static readonly Type EditorUtilityType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.EditorUtility");

        private static readonly MethodInfo InstantiateForAnimatorPreviewMethodInfo =
            EditorUtilityType.GetMethod("InstantiateForAnimatorPreview", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo InitInstantiatedPreviewRecursiveMethodInfo =
            EditorUtilityType.GetMethod("InstantiateForAnimatorPreview", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly Type IAnimationPreviewableType = typeof(Animator).Assembly.GetType("UnityEngine.Animations.IAnimationPreviewable");

        private static readonly MethodInfo OnPreviewUpdateMethodInfo =
            IAnimationPreviewableType.GetMethod("OnPreviewUpdate", BindingFlags.Public | BindingFlags.Instance);

        private static readonly MethodInfo GetComponentsInChildrenMethodInfo = typeof(GameObject).GetMethod("GetComponentsInChildren", new[] { typeof(Type) });

#endregion

#region Constants

        private const string DefaultAvatarPreviewOption = "DefaultAvatarPreviewOption";
        private const string IkPref = "AvatarpreviewShowIK";
        private const string Avatar2DPref = "Avatarpreview2D";
        private const string ReferencePref = "AvatarpreviewShowReference";
        private const string SpeedPref = "AvatarpreviewSpeed";
        private const float TimeControlRectHeight = 20;

#endregion

#region Fields

        public int FPS = 60;

        private Material _floorMaterial;
        private Material _floorMaterialSmall;
        private Material _shadowMaskMaterial;
        private Material _shadowPlaneMaterial;

        public PreviewRenderUtility _previewUtility;
        private GameObject _referenceInstance;
        private GameObject _directionInstance;
        private GameObject _pivotInstance;

        private GameObject _rootInstance;

        //private IAnimationPreviewable[]     m_Previewables;
        private object[] _previewables;
        private float _boundingVolumeScale;
        private Motion _sourcePreviewMotion;
        private Animator _sourceScenePreviewAnimator;

        private const string PreviewStr = "Preview";
        private int _previewHint = PreviewStr.GetHashCode();

        private const string PreviewSceneStr = "PreviewSene";
        private int _previewSceneHint = PreviewSceneStr.GetHashCode();

        private Texture2D _floorTexture;
        private Mesh _floorPlane;

        private bool _showReference = false;
        private bool _iKOnFeet = false;
        private bool _showIKOnFeetButton = true;
        private bool _2D;
        private bool _isValid;

        private const float FloorFadeDuration = 0.2f;
        private const float FloorScale = 5;
        private const float FloorScaleSmall = 0.2f;
        private const float FloorTextureScale = 4;
        private const float FloorAlpha = 0.5f;
        private const float FloorShadowAlpha = 0.3f;
        private const float DefaultIntensity = 1.4f;

        private const int DefaultLayer = 0; // Must match kDefaultLayer in TagTypes.h

        private float _prevFloorHeight = 0;
        private float _nextFloorHeight = 0;

        public Vector2 PreviewDir = new(120, -20);
        public float AvatarScale = 1.0f;
        public float ZoomFactor = 1.0f;
        public Vector3 PivotPositionOffset = Vector3.zero;

        //private float m_LastNormalizedTime = -1000;
        //private float m_LastStartTime = -1000;
        //private float m_LastStopTime = -1000;
        private bool _nextTargetIsForward = true;

        private static NZAvatarPreview instance;
        private static readonly int Alphas = Shader.PropertyToID("_Alphas");

#endregion

#region Properties

        public static NZAvatarPreview Instance => instance;

        public OnAvatarChange OnAvatarChangeFunc
        {
            set => _onAvatarChangeFunc = value;
        }

        public bool IKOnFeet => _iKOnFeet;

        public bool ShowIKOnFeetButton
        {
            get => _showIKOnFeetButton;
            set => _showIKOnFeetButton = value;
        }

        public bool Is2D
        {
            get => _2D;
            set
            {
                _2D = value;
                if (_2D)
                {
                    PreviewDir = new Vector2();
                }
            }
        }

        public Animator Animator => PreviewObject != null ? PreviewObject.GetComponent(typeof(Animator)) as Animator : null;

        public GameObject PreviewObject { get; private set; }

        public ModelImporterAnimationType AnimationClipType => GetAnimationType(_sourcePreviewMotion);

        public Vector3 BodyPosition
        {
            get
            {
                if (Animator && Animator.isHuman)
                {
                    return Animator.bodyPosition;
                }

                if (PreviewObject != null)
                {
                    return (Vector3)GetRenderableCenterRecurseMethodInfo.Invoke(null, new object[] { PreviewObject, 1, 8 });
                    //return GameObjectInspector.GetRenderableCenterRecurse(m_PreviewInstance, 1, 8);
                }

                return Vector3.zero;
            }
        }

        public PreviewRenderUtility PreviewUtility
        {
            get
            {
                if (_previewUtility != null)
                {
                    return _previewUtility;
                }

                _previewUtility = new PreviewRenderUtility
                {
                    camera =
                    {
                        fieldOfView = 30.0f,
                        allowHDR = false,
                        allowMSAA = false
                    },
                    ambientColor = new Color(.1f, .1f, .1f, 0)
                };

                _previewUtility.lights[0].intensity = DefaultIntensity;
                _previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0);
                _previewUtility.lights[1].intensity = DefaultIntensity;

                return _previewUtility;
            }
        }

        public Vector3 RootPosition => PreviewObject ? PreviewObject.transform.position : Vector3.zero;

#endregion

#region GUIStyles

        private class Styles
        {
            public readonly GUIContent Pivot = EditorGUIUtility.TrIconContent("AvatarPivot", "Displays avatar's pivot and mass center");
            public readonly GUIContent IK = EditorGUIUtility.TrTextContent("IK", "Toggles feet IK preview");
            public readonly GUIContent Is2D = EditorGUIUtility.TrIconContent("SceneView2D", "Toggles 2D preview mode");
            public readonly GUIContent AvatarIcon = EditorGUIUtility.TrIconContent("AvatarSelector", "Changes the model to use for previewing.");

            public readonly GUIStyle PreButton = "toolbarbutton";
            public readonly GUIStyle PreSlider = "preSlider";
            public readonly GUIStyle PreSliderThumb = "preSliderThumb";
        }

        private Styles _styles = new();

#endregion

        public delegate void OnAvatarChange();

        private OnAvatarChange _onAvatarChangeFunc;

        public NZAvatarPreview(Animator previewObjectInScene, Motion objectOnSameAsset)
        {
            instance = this;

            InitInstance(previewObjectInScene, objectOnSameAsset);
        }

        private void Init()
        {
            if (_styles == null)
            {
                _styles = new Styles();
            }

            if (_floorPlane == null)
            {
                _floorPlane = Resources.GetBuiltinResource(typeof(Mesh), "New-Plane.fbx") as Mesh;
            }

            if (_floorTexture == null)
            {
                _floorTexture = (Texture2D)EditorGUIUtility.Load("Avatar/Textures/AvatarFloor.png");
            }

            if (_floorMaterial == null)
            {
                var shader = EditorGUIUtility.LoadRequired("Previews/PreviewPlaneWithShadow.shader") as Shader;
                _floorMaterial = new Material(shader)
                {
                    mainTexture = _floorTexture,
                    mainTextureScale = Vector2.one * FloorScale * FloorTextureScale
                };
                _floorMaterial.SetVector(Alphas, new Vector4(FloorAlpha, FloorShadowAlpha, 0, 0));
                _floorMaterial.hideFlags = HideFlags.HideAndDontSave;

                _floorMaterialSmall = new Material(_floorMaterial)
                {
                    mainTextureScale = Vector2.one * FloorScaleSmall * FloorTextureScale,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (_shadowMaskMaterial == null)
            {
                var shader = EditorGUIUtility.LoadRequired("Previews/PreviewShadowMask.shader") as Shader;
                _shadowMaskMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (_shadowPlaneMaterial == null)
            {
                var shader = EditorGUIUtility.LoadRequired("Previews/PreviewShadowPlaneClip.shader") as Shader;
                _shadowPlaneMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        private void InitInstance(Animator scenePreviewObject, Motion motion)
        {
            _sourcePreviewMotion = motion;
            _sourceScenePreviewAnimator = scenePreviewObject;

            if (PreviewObject == null)
            {
                var go = CalculatePreviewGameObject(scenePreviewObject, motion, AnimationClipType);
                SetupBounds(go);
            }

            if (_referenceInstance == null)
            {
                var referenceGo = (GameObject)EditorGUIUtility.Load("Avatar/dial_flat.prefab");
                _referenceInstance = (GameObject)Object.Instantiate(referenceGo, Vector3.zero, Quaternion.identity);
                InitInstantiatedPreviewRecursiveMethodInfo.Invoke(null, new object[] { _referenceInstance });
                PreviewUtility.AddSingleGO(_referenceInstance);
            }

            if (_directionInstance == null)
            {
                var directionGo = (GameObject)EditorGUIUtility.Load("Avatar/arrow.fbx");
                _directionInstance = (GameObject)Object.Instantiate(directionGo, Vector3.zero, Quaternion.identity);
                InitInstantiatedPreviewRecursiveMethodInfo.Invoke(null, new object[] { _directionInstance });
                PreviewUtility.AddSingleGO(_directionInstance);
            }

            if (_pivotInstance == null)
            {
                var pivotGo = (GameObject)EditorGUIUtility.Load("Avatar/root.fbx");
                _pivotInstance = (GameObject)Object.Instantiate(pivotGo, Vector3.zero, Quaternion.identity);
                InitInstantiatedPreviewRecursiveMethodInfo.Invoke(null, new object[] { _pivotInstance });
                PreviewUtility.AddSingleGO(_pivotInstance);
            }

            if (_rootInstance == null)
            {
                var rootGo = (GameObject)EditorGUIUtility.Load("Avatar/root.fbx");
                _rootInstance = (GameObject)Object.Instantiate(rootGo, Vector3.zero, Quaternion.identity);
                InitInstantiatedPreviewRecursiveMethodInfo.Invoke(null, new object[] { _rootInstance });
                PreviewUtility.AddSingleGO(_rootInstance);
            }

            // Load preview settings from prefs
            _iKOnFeet = EditorPrefs.GetBool(IkPref, false);
            _showReference = EditorPrefs.GetBool(ReferencePref, true);
            Is2D = EditorPrefs.GetBool(Avatar2DPref, EditorSettings.defaultBehaviorMode == EditorBehaviorMode.Mode2D);

            SetPreviewCharacterEnabled(false, false);

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(motion)) as ModelImporter;
            if (importer && importer.bakeAxisConversion)
            {
                PreviewDir += new Vector2(180, 0);
            }

            PivotPositionOffset = Vector3.zero;
        }

        private void SetupBounds(GameObject go)
        {
            _isValid = go != null && go != GetGenericAnimationFallback();

            if (go != null)
            {
                PreviewObject = InstantiatePreviewPrefab(go);
                PreviewUtility.AddSingleGO(PreviewObject);

                _previewables = (object[])GetComponentsInChildrenMethodInfo.Invoke(PreviewObject, new object[] { IAnimationPreviewableType });
                var bounds = (Bounds)GetRenderableBoundsMethodInfo.Invoke(null, new object[] { PreviewObject });

                _boundingVolumeScale = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));


                if (Animator && Animator.isHuman)
                {
                    AvatarScale = ZoomFactor = Animator.humanScale;
                }
                else
                {
                    AvatarScale = ZoomFactor = _boundingVolumeScale / 2;
                }
            }
        }

        private GameObject InstantiatePreviewPrefab(GameObject original)
        {
            if (original == null)
            {
                throw new ArgumentException("The Prefab you want to instantiate is null.");
            }

            //GameObject go = EditorUtility.InstantiateRemoveAllNonAnimationComponents(original, Vector3.zero, Quaternion.identity) as GameObject;
            var go = Object.Instantiate(original);
            go.name += "AnimatorPreview";
            go.tag = "Untagged";
            //EditorUtility.InitInstantiatedPreviewRecursive(go);
            InitInstantiatedPreviewRecursiveMethodInfo.Invoke(null, new object[] { go });

            var componentsInChildren = go.GetComponentsInChildren<Animator>();
            foreach (var animator in componentsInChildren)
            {
                animator.enabled = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.logWarnings = false;
                animator.fireEvents = false;
            }

            if (componentsInChildren.Length == 0)
            {
                var animator = go.AddComponent<Animator>();
                animator.enabled = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.logWarnings = false;
                animator.fireEvents = false;
            }

            return go;
        }

        public void OnDisable()
        {
            if (_previewUtility == null)
            {
                return;
            }

            _previewUtility.Cleanup();
            _previewUtility = null;
        }

        private void SetPreviewCharacterEnabled(bool enabled, bool showReference)
        {
            if (PreviewObject != null)
            {
                SetEnabledRecursive(PreviewObject, enabled);
            }

            SetEnabledRecursive(_referenceInstance, showReference && enabled);
            SetEnabledRecursive(_directionInstance, showReference && enabled);
            SetEnabledRecursive(_pivotInstance, showReference && enabled);
            SetEnabledRecursive(_rootInstance, showReference && enabled);
        }

        private static void SetEnabledRecursive(GameObject go, bool enabled)
        {
            foreach (var componentsInChild in go.GetComponentsInChildren<Renderer>())
            {
                componentsInChild.enabled = enabled;
            }
        }

        private static AnimationClip GetFirstAnimationClipFromMotion(Motion motion)
        {
            var clip = motion as AnimationClip;
            if (clip)
            {
                return clip;
            }

            var blendTree = motion as UnityEditor.Animations.BlendTree;
            if (blendTree)
            {
                //AnimationClip[] clips = blendTree.GetAnimationClipsFlattened();
                var clips = (AnimationClip[])GetAnimationClipsFlattenedMethodInfo.Invoke(blendTree, null);
                if (clips.Length > 0)
                {
                    return clips[0];
                }
            }

            return null;
        }

        private static ModelImporterAnimationType GetAnimationType(GameObject go)
        {
            var animator = go.GetComponent<Animator>();
            if (animator)
            {
                var avatar = animator.avatar;
                if (avatar && avatar.isHuman)
                {
                    return ModelImporterAnimationType.Human;
                }

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
            var clip = GetFirstAnimationClipFromMotion(motion);
            if (clip)
            {
                if (clip.legacy)
                {
                    return ModelImporterAnimationType.Legacy;
                }

                if (clip.humanMotion)
                {
                    return ModelImporterAnimationType.Human;
                }

                return ModelImporterAnimationType.Generic;
            }

            return ModelImporterAnimationType.None;
        }

        private static bool IsValidPreviewGameObject(GameObject target, ModelImporterAnimationType requiredClipType)
        {
            if (target != null && !target.activeSelf)
            {
                Debug.LogWarning("Can't preview inactive object, using fallback object");
            }

            return target != null &&
                   target.activeSelf &&
                   //GameObjectInspector.HasRenderableParts(target) &&
                   (bool)HasRenderablePartsMethodInfo.Invoke(null, new object[] { target }) &&
                   !(requiredClipType != ModelImporterAnimationType.None &&
                     GetAnimationType(target) != requiredClipType);
        }

        private static GameObject FindBestFittingRenderableGameObjectFromModelAsset(Object asset, ModelImporterAnimationType animationType)
        {
            if (asset == null)
            {
                return null;
            }

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(asset)) as ModelImporter;
            if (importer == null)
            {
                return null;
            }

            //string assetPath = importer.CalculateBestFittingPreviewGameObject();
            var assetPath = (string)CalculateBestFittingPreviewGameObjectMethodInfo.Invoke(importer, null);
            var tempGo = AssetDatabase.LoadMainAssetAtPath(assetPath) as GameObject;

            // We should also check for isHumanClip matching the animationclip requiremenets...
            if (IsValidPreviewGameObject(tempGo, ModelImporterAnimationType.None))
            {
                return tempGo;
            }
            else
            {
                return null;
            }
        }

        private static GameObject CalculatePreviewGameObject(Animator selectedAnimator, Motion motion, ModelImporterAnimationType animationType)
        {
            var sourceClip = GetFirstAnimationClipFromMotion(motion);

            // Use selected preview
            //GameObject selected = AvatarPreviewSelection.GetPreview(animationType);
            var selected = (GameObject)GetPreviewMethodInfo.Invoke(null, new object[] { animationType });
            if (IsValidPreviewGameObject(selected, ModelImporterAnimationType.None))
            {
                return selected;
            }

            if (selectedAnimator != null && IsValidPreviewGameObject(selectedAnimator.gameObject, animationType))
            {
                return selectedAnimator.gameObject;
            }

            // Find the best fitting preview game object for the asset we are viewing (Handles @ convention, will pick base path for you)
            selected = FindBestFittingRenderableGameObjectFromModelAsset(sourceClip, animationType);
            if (selected != null)
            {
                return selected;
            }

            return animationType switch
            {
                ModelImporterAnimationType.Human => GetHumanoidFallback(),
                ModelImporterAnimationType.Generic => GetGenericAnimationFallback(),
                _ => null
            };
        }

        private static GameObject GetGenericAnimationFallback() => (GameObject)EditorGUIUtility.Load("Avatar/DefaultGeneric.fbx");

        private static GameObject GetHumanoidFallback() => (GameObject)EditorGUIUtility.Load("Avatar/DefaultAvatar.fbx");

        public void ResetPreviewInstance()
        {
            Object.DestroyImmediate(PreviewObject);
            var go = CalculatePreviewGameObject(_sourceScenePreviewAnimator, _sourcePreviewMotion, AnimationClipType);
            SetupBounds(go);
        }

        public void DoSelectionChange()
        {
            _onAvatarChangeFunc();
        }

        private float PreviewSlider(Rect rect, float val, float snapThreshold)
        {
            val = GUI.HorizontalSlider(rect, val, 0.1f, 2.0f, _styles.PreSlider, _styles.PreSliderThumb); //, GUILayout.MaxWidth(64));
            if (val > 0.25f - snapThreshold && val < 0.25f + snapThreshold)
            {
                val = 0.25f;
            }
            else if (val > 0.5f - snapThreshold && val < 0.5f + snapThreshold)
            {
                val = 0.5f;
            }
            else if (val > 0.75f - snapThreshold && val < 0.75f + snapThreshold)
            {
                val = 0.75f;
            }
            else if (val > 1.0f - snapThreshold && val < 1.0f + snapThreshold)
            {
                val = 1.0f;
            }
            else if (val > 1.25f - snapThreshold && val < 1.25f + snapThreshold)
            {
                val = 1.25f;
            }
            else if (val > 1.5f - snapThreshold && val < 1.5f + snapThreshold)
            {
                val = 1.5f;
            }
            else if (val > 1.75f - snapThreshold && val < 1.75f + snapThreshold)
            {
                val = 1.75f;
            }

            return val;
        }

        public void DoPreviewSettings()
        {
            Init();

            if (_showIKOnFeetButton)
            {
                EditorGUI.BeginChangeCheck();
                _iKOnFeet = GUILayout.Toggle(_iKOnFeet, _styles.IK, _styles.PreButton);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(IkPref, _iKOnFeet);
                }
            }

            EditorGUI.BeginChangeCheck();
            GUILayout.Toggle(Is2D, _styles.Is2D, _styles.PreButton);
            if (EditorGUI.EndChangeCheck())
            {
                Is2D = !Is2D;
                EditorPrefs.SetBool(Avatar2DPref, Is2D);
            }

            EditorGUI.BeginChangeCheck();
            _showReference = GUILayout.Toggle(_showReference, _styles.Pivot, _styles.PreButton);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(ReferencePref, _showReference);
            }

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
            var bodyPos = RootPosition;
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

            var direction = bodyRot * Vector3.forward;
            direction[1] = 0;
            var directionRot = Quaternion.LookRotation(direction);
            var directionPos = rootPos;

            var pivotRot = rootRot;

            // Scale all Preview Objects to fit avatar size.
            PositionPreviewObjects(pivotRot, pivotPos, bodyRot, BodyPosition, directionRot, rootRot, rootPos, directionPos, AvatarScale);

            var dynamicFloorHeight = !Is2D && Mathf.Abs(_nextFloorHeight - _prevFloorHeight) > ZoomFactor * 0.01f;

            // Calculate floor height and alpha
            float mainFloorHeight, mainFloorAlpha;
            if (dynamicFloorHeight)
            {
                var fadeMoment = _nextFloorHeight < _prevFloorHeight ? FloorFadeDuration : 1 - FloorFadeDuration;
                //mainFloorHeight = timeControl.normalizedTime < fadeMoment ? m_PrevFloorHeight : m_NextFloorHeight;
                //mainFloorAlpha = Mathf.Clamp01(Mathf.Abs(timeControl.normalizedTime - fadeMoment) / kFloorFadeDuration);
                mainFloorHeight = _prevFloorHeight;
                mainFloorAlpha = 1;
            }
            else
            {
                mainFloorHeight = _prevFloorHeight;
                mainFloorAlpha = Is2D ? 0.5f : 1;
            }

            var floorRot = Is2D ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
            var floorPos = _referenceInstance.transform.position;
            floorPos.y = mainFloorHeight;

            // Render shadow map
            Matrix4x4 shadowMatrix;
            var shadowMap = RenderPreviewShadowmap(PreviewUtility.lights[0], _boundingVolumeScale / 2, BodyPosition, floorPos, out shadowMatrix);

            // SRP might initialize the light settings during the first frame of rendering
            // (e.g HDRP is overriding the intensity value during 'InitDefaultHDAdditionalLightData').
            // So this call is necessary to avoid a flickering when selecting an animation clip.
            if (PreviewUtility.lights[0].intensity != DefaultIntensity || PreviewUtility.lights[0].intensity != DefaultIntensity)
            {
                SetupPreviewLightingAndFx(probe);
            }

            var tempZoomFactor = Is2D ? 1.0f : ZoomFactor;
            // Position camera
            PreviewUtility.camera.orthographic = Is2D;
            if (Is2D)
            {
                PreviewUtility.camera.orthographicSize = 2.0f * ZoomFactor;
            }

            PreviewUtility.camera.nearClipPlane = 0.5f * tempZoomFactor;
            PreviewUtility.camera.farClipPlane = 100.0f * AvatarScale;
            var camRot = Quaternion.Euler(-PreviewDir.y, -PreviewDir.x, 0);

            // Add panning offset
            var camPos = camRot * (Vector3.forward * -5.5f * tempZoomFactor) + bodyPos + PivotPositionOffset;
            PreviewUtility.camera.transform.position = camPos;
            PreviewUtility.camera.transform.rotation = camRot;

            SetPreviewCharacterEnabled(true, _showReference);
            foreach (var previewable in _previewables)
            {
                //previewable.OnPreviewUpdate();
                OnPreviewUpdateMethodInfo.Invoke(previewable, null);
            }

            PreviewUtility.Render(Option != PreviewPopupOptions.DefaultModel);
            SetPreviewCharacterEnabled(false, false);

            // Texture offset - negative in order to compensate the floor movement.
            var textureOffset = -new Vector2(floorPos.x, Is2D ? floorPos.y : floorPos.z);

            // Render main floor
            {
                var mat = _floorMaterial;
                var matrix = Matrix4x4.TRS(floorPos, floorRot, Vector3.one * FloorScale * AvatarScale);

                mat.mainTextureOffset = textureOffset * FloorScale * 0.08f * (1.0f / AvatarScale);
                mat.SetTexture("_ShadowTexture", shadowMap);
                mat.SetMatrix("_ShadowTextureMatrix", shadowMatrix);
                mat.SetVector("_Alphas", new Vector4(FloorAlpha * mainFloorAlpha, FloorShadowAlpha * mainFloorAlpha, 0, 0));
                mat.renderQueue = (int)RenderQueue.Background;

                Graphics.DrawMesh(_floorPlane, matrix, mat, DefaultLayer, PreviewUtility.camera, 0);
            }

            // Render small floor
            if (dynamicFloorHeight)
            {
                var topIsNext = _nextFloorHeight > _prevFloorHeight;
                var floorHeight = topIsNext ? _nextFloorHeight : _prevFloorHeight;
                var otherFloorHeight = topIsNext ? _prevFloorHeight : _nextFloorHeight;
                var floorAlpha = (floorHeight == mainFloorHeight ? 1 - mainFloorAlpha : 1) * Mathf.InverseLerp(otherFloorHeight, floorHeight, rootPos.y);
                floorPos.y = floorHeight;

                var mat = _floorMaterialSmall;
                mat.mainTextureOffset = textureOffset * FloorScaleSmall * 0.08f;
                mat.SetTexture("_ShadowTexture", shadowMap);
                mat.SetMatrix("_ShadowTextureMatrix", shadowMatrix);
                mat.SetVector("_Alphas", new Vector4(FloorAlpha * floorAlpha, 0, 0, 0));
                var matrix = Matrix4x4.TRS(floorPos, floorRot, Vector3.one * FloorScaleSmall * AvatarScale);
                Graphics.DrawMesh(_floorPlane, matrix, mat, DefaultLayer, PreviewUtility.camera, 0);
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
            cam.transform.rotation = Is2D ? Quaternion.identity : light.transform.rotation;
            cam.transform.position = center - light.transform.forward * (scale * 5.5f);

            // Clear to black
            var oldFlags = cam.clearFlags;
            cam.clearFlags = CameraClearFlags.SolidColor;
            var oldColor = cam.backgroundColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);

            // Create render target for shadow map
            const int kShadowSize = 256;
            var oldRT = cam.targetTexture;
            var rt = RenderTexture.GetTemporary(kShadowSize, kShadowSize, 16);
            rt.isPowerOfTwo = true;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = FilterMode.Bilinear;
            cam.targetTexture = rt;

            // Enable character and render with camera into the shadowmap
            SetPreviewCharacterEnabled(true, false);
            _previewUtility.camera.Render();

            // Draw a quad, with shader that will produce white color everywhere
            // where something was rendered (via inverted depth test)
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadOrtho();
            _shadowMaskMaterial.SetPass(0);
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
            _shadowPlaneMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            var sc = FloorScale * scale;
            GL.Vertex(floorPos + new Vector3(-sc, 0, -sc));
            GL.Vertex(floorPos + new Vector3(sc, 0, -sc));
            GL.Vertex(floorPos + new Vector3(sc, 0, sc));
            GL.Vertex(floorPos + new Vector3(-sc, 0, sc));
            GL.End();

            GL.PopMatrix();

            // Shadowmap sampling matrix, from world space into shadowmap space
            var texMatrix = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity,
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
            PreviewUtility.lights[0].intensity = DefaultIntensity;
            PreviewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0);
            PreviewUtility.lights[1].intensity = DefaultIntensity;
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientLight = new Color(0.1f, 0.1f, 0.1f, 1.0f);
            RenderSettings.ambientProbe = probe;
        }

        private void PositionPreviewObjects(Quaternion pivotRot, Vector3 pivotPos, Quaternion bodyRot, Vector3 bodyPos,
            Quaternion directionRot, Quaternion rootRot, Vector3 rootPos, Vector3 directionPos,
            float scale)
        {
            _referenceInstance.transform.position = rootPos;
            _referenceInstance.transform.rotation = rootRot;
            _referenceInstance.transform.localScale = Vector3.one * scale * 1.25f;

            _directionInstance.transform.position = directionPos;
            _directionInstance.transform.rotation = directionRot;
            _directionInstance.transform.localScale = Vector3.one * scale * 2;

            _pivotInstance.transform.position = pivotPos;
            _pivotInstance.transform.rotation = pivotRot;
            _pivotInstance.transform.localScale = Vector3.one * scale * 0.1f;

            _rootInstance.transform.position = bodyPos;
            _rootInstance.transform.rotation = bodyRot;
            _rootInstance.transform.localScale = Vector3.one * scale * 0.25f;

            if (Animator)
            {
                //float normalizedTime = timeControl.normalizedTime;
                //float normalizedDelta = timeControl.deltaTime / (timeControl.stopTime - timeControl.startTime);

                // Always set last height to next height after wrapping the time.
                //if (normalizedTime - normalizedDelta < 0 || normalizedTime - normalizedDelta >= 1)
                _prevFloorHeight = _nextFloorHeight;

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
                if (_nextTargetIsForward)
                {
                    _nextFloorHeight = Animator.targetPosition.y;
                }
                else
                {
                    _prevFloorHeight = Animator.targetPosition.y;
                }

                // Flip next target time.
                _nextTargetIsForward = !_nextTargetIsForward;
                Animator.SetTarget(AvatarTarget.Root, _nextTargetIsForward ? 1 : 0);
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

        private enum PreviewPopupOptions : byte
        {
            Auto = 0,
            DefaultModel = 1,
            Other = 2
        }

        private ViewTool _viewTool = ViewTool.None;

        protected ViewTool ViewTool
        {
            get
            {
                var evt = Event.current;
                if (_viewTool == ViewTool.None)
                {
                    var controlKeyOnMac = evt.control && Application.platform == RuntimePlatform.OSXEditor;

                    // actionKey could be command key on mac or ctrl on windows
                    var actionKey = EditorGUI.actionKey;

                    var noModifiers = !actionKey && !controlKeyOnMac && !evt.alt;

                    if ((evt.button <= 0 && noModifiers) || (evt.button <= 0 && actionKey) || evt.button == 2)
                    {
                        _viewTool = ViewTool.Pan;
                    }
                    else if ((evt.button <= 0 && controlKeyOnMac) || (evt.button == 1 && evt.alt))
                    {
                        _viewTool = ViewTool.Zoom;
                    }
                    else if ((evt.button <= 0 && evt.alt) || evt.button == 1)
                    {
                        _viewTool = ViewTool.Orbit;
                    }
                }

                return _viewTool;
            }
        }

        protected MouseCursor CurrentCursor
        {
            get
            {
                switch (_viewTool)
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
            if (ViewTool != ViewTool.None && previewRect.Contains(evt.mousePosition))
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
                _viewTool = ViewTool.None;

                GUIUtility.hotControl = 0;
                EditorGUIUtility.SetWantsMouseJumping(0);
                evt.Use();
            }
        }

        protected void HandleMouseDrag(Event evt, int id, Rect previewRect)
        {
            if (PreviewObject == null)
            {
                return;
            }

            if (GUIUtility.hotControl == id)
            {
                switch (_viewTool)
                {
                    case ViewTool.Orbit: DoAvatarPreviewOrbit(evt, previewRect); break;
                    case ViewTool.Pan: DoAvatarPreviewPan(evt); break;

                    // case 605415 invert zoom delta to match scene view zooming
                    case ViewTool.Zoom: DoAvatarPreviewZoom(evt, -HandleUtility.niceMouseDeltaZoom * (evt.shift ? 2.0f : 0.5f)); break;
                    default: Debug.Log("Enum value not handled"); break;
                }
            }
        }

        protected void HandleViewTool(Event evt, EventType eventType, int id, Rect previewRect)
        {
            switch (eventType)
            {
                case EventType.ScrollWheel: DoAvatarPreviewZoom(evt, HandleUtility.niceMouseDeltaZoom * (evt.shift ? 2.0f : 0.5f)); break;
                case EventType.MouseDown: HandleMouseDown(evt, id, previewRect); break;
                case EventType.MouseUp: HandleMouseUp(evt, id); break;
                case EventType.MouseDrag: HandleMouseDrag(evt, id, previewRect); break;
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
                var newPreviewObject = DragAndDrop.objectReferences[0] as GameObject;

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
            if (Is2D)
            {
                Is2D = false;
            }

            PreviewDir -= evt.delta * (evt.shift ? 3 : 1) / Mathf.Min(previewRect.width, previewRect.height) * 140.0f;
            PreviewDir.y = Mathf.Clamp(PreviewDir.y, -90, 90);
            evt.Use();
        }

        public void DoAvatarPreviewPan(Event evt)
        {
            var cam = PreviewUtility.camera;
            var screenPos = cam.WorldToScreenPoint(BodyPosition + PivotPositionOffset);
            var delta = new Vector3(-evt.delta.x, evt.delta.y, 0);
            // delta panning is scale with the zoom factor to allow fine tuning when user is zooming closely.
            screenPos += delta * Mathf.Lerp(0.25f, 2.0f, ZoomFactor * 0.5f);
            var worldDelta = cam.ScreenToWorldPoint(screenPos) - (BodyPosition + PivotPositionOffset);
            PivotPositionOffset += worldDelta;
            evt.Use();
        }

        public void ResetPreviewFocus()
        {
            PivotPositionOffset = BodyPosition - RootPosition;
        }

        public void DoAvatarPreviewFrame(Event evt, EventType type, Rect previewRect)
        {
            if (type == EventType.KeyDown && evt.keyCode == KeyCode.F)
            {
                ResetPreviewFocus();
                ZoomFactor = AvatarScale;
                evt.Use();
            }

            if (type == EventType.KeyDown && Event.current.keyCode == KeyCode.G)
            {
                PivotPositionOffset = GetCurrentMouseWorldPosition(evt, previewRect) - BodyPosition;
                evt.Use();
            }
        }

        protected Vector3 GetCurrentMouseWorldPosition(Event evt, Rect previewRect)
        {
            var cam = PreviewUtility.camera;

            var scaleFactor = PreviewUtility.GetScaleFactor(previewRect.width, previewRect.height);
            var mouseLocal = new Vector3((evt.mousePosition.x - previewRect.x) * scaleFactor,
                (previewRect.height - (evt.mousePosition.y - previewRect.y)) * scaleFactor, 0);
            mouseLocal.z = Vector3.Distance(BodyPosition, cam.transform.position);
            return cam.ScreenToWorldPoint(mouseLocal);
        }

        public void DoAvatarPreviewZoom(Event evt, float delta)
        {
            var zoomDelta = -delta * 0.05f;
            ZoomFactor += ZoomFactor * zoomDelta;

            // zoom is clamp too 10 time closer than the original zoom
            ZoomFactor = Mathf.Max(ZoomFactor, AvatarScale / 10.0f);
            evt.Use();
        }

        public void DoAvatarPreview(Rect rect, GUIStyle background)
        {
            Init();

            var choserRect = new Rect(rect.xMax - 16, rect.yMax - 16, 16, 16);
            if (EditorGUI.DropdownButton(choserRect, GUIContent.none, FocusType.Passive, GUIStyle.none))
            {
                var menu = new GenericMenu();
                menu.AddItem(EditorGUIUtility.TrTextContent("Auto"), false, SetPreviewAvatarOption, PreviewPopupOptions.Auto);
                menu.AddItem(EditorGUIUtility.TrTextContent("Unity Model"), false, SetPreviewAvatarOption, PreviewPopupOptions.DefaultModel);
                menu.AddItem(EditorGUIUtility.TrTextContent("Other..."), false, SetPreviewAvatarOption, PreviewPopupOptions.Other);
                menu.ShowAsContext();
            }

            var previewRect = rect;
            previewRect.yMin += TimeControlRectHeight;
            previewRect.height = Mathf.Max(previewRect.height, 64f);

            var previewID = GUIUtility.GetControlID(_previewHint, FocusType.Passive, previewRect);
            var evt = Event.current;
            var type = evt.GetTypeForControl(previewID);

            if (type == EventType.Repaint && _isValid)
            {
                DoRenderPreview(previewRect, background);
                PreviewUtility.EndAndDrawPreview(previewRect);
            }

            AvatarTimeControlGUI(rect);


            var previewSceneID = GUIUtility.GetControlID(_previewSceneHint, FocusType.Passive);
            type = evt.GetTypeForControl(previewSceneID);

            DoAvatarPreviewDrag(evt, type);
            HandleViewTool(evt, type, previewSceneID, previewRect);
            DoAvatarPreviewFrame(evt, type, previewRect);

            if (!_isValid)
            {
                var warningRect = previewRect;
                warningRect.yMax -= warningRect.height / 2 - 16;
                EditorGUI.DropShadowLabel(
                    warningRect,
                    "No model is available for preview.\nPlease drag a model into this Preview Area.");
            }

            // Apply the current cursor
            if (evt.type == EventType.Repaint)
            {
                EditorGUIUtility.AddCursorRect(previewRect, CurrentCursor);
            }
        }

        private PreviewPopupOptions Option
        {
            get => (PreviewPopupOptions)EditorPrefs.GetInt(DefaultAvatarPreviewOption);
            set => EditorPrefs.SetInt(DefaultAvatarPreviewOption, (int)value);
        }

        private void SetPreviewAvatarOption(object obj)
        {
            var newSelectedOption = (PreviewPopupOptions)obj;

            if (Option != newSelectedOption)
            {
                Option = newSelectedOption;

                switch (Option)
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

        private void SetPreview(GameObject gameObject)
        {
            //AvatarPreviewSelection.SetPreview(animationClipType, gameObject);
            SetPreviewMethodInfo.Invoke(null, new object[] { AnimationClipType, gameObject });

            Object.DestroyImmediate(PreviewObject);
            InitInstance(_sourceScenePreviewAnimator, _sourcePreviewMotion);

            if (_onAvatarChangeFunc != null)
            {
                _onAvatarChangeFunc();
            }
        }

        private int Repeat(int t, int length) =>
            // Have to do double modulo in order to work for negative numbers.
            // This is quicker than a branch to test for negative number.
            (t % length + length) % length;
    }
}