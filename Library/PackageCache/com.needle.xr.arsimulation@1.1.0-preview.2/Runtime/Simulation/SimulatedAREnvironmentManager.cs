using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Needle.XR.ARSimulation.Compatibility;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.XR.ARFoundation;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;

#endif

namespace Needle.XR.ARSimulation.Simulation
{
    internal enum RealitySimulationSceneLoading
    {
        // Never = 0,
        EditAndPlayMode = 1,
        // OnlyPlayMode = 2,
    }

    /// <summary>
    /// Enables rendering part of your scene as the AR camera image
    /// </summary>
    [ExecuteAlways]
    public class SimulatedAREnvironmentManager : MonoBehaviour
    {
        /// <summary>
        /// Checks if a reality simulation manager exists in scene (accessing <see cref="Instance"/> will create a new instance if it does not exist yet)
        /// </summary>
        /// <returns>true if an instance of <see cref="SimulatedAREnvironmentManager"/> exists</returns>
        public static bool Exists => instance ? instance : FindObjectOfType<SimulatedAREnvironmentManager>();

        private static SimulatedAREnvironmentManager instance;

        /// <summary>
        /// Get or create a <see cref="SimulatedAREnvironmentManager"/> instance
        /// </summary>
        /// <returns>Instance of <see cref="SimulatedAREnvironmentManager"/></returns>
        public static SimulatedAREnvironmentManager Instance
        {
            get
            {
                if (instance) return instance;
                instance = FindObjectOfType<SimulatedAREnvironmentManager>();
                if (instance) return instance;
                var realityManager = new GameObject("Reality Simulation Manager");
                instance = realityManager.AddComponent<SimulatedAREnvironmentManager>();

                return instance;
            }
        }

#pragma warning disable 414
        public static event Action<SimulatedAREnvironmentManager> SceneChangedOrRecreated = null;
#pragma warning restore
        public IReadOnlyList<GameObject> SceneInstances => instantiatedGameObjects;

        /// <summary>
        /// Camera used in ARSessionOrigin
        /// </summary>
        private Camera ARCamera
        {
            get
            {
                if (!_arCamera)
                    SceneSetup.TryGetARCamera(out _arCamera);
                return _arCamera;
            }
        }

        private Camera _arCamera;


        /// <summary>
        /// Reference a <see cref="SceneAsset"/>, prefab or <see cref="GameObject"/> that is the simulated environment (the manager will create a instance of the assigned object for rendering)
        /// </summary>
        [Header("Environment"), Tooltip("Can be a scene asset, a prefab or a gameobject in the scene")] 
        public UnityEngine.Object SceneOrPrefab;

        /// <summary>
        /// If true the instantiated simulated environment <see cref="GameObject">GameObjects</see> will not be editable
        /// </summary>
        public bool NotEditable = true;

        /// <summary>
        /// If true the instantiated simulated environment <see cref="GameObject">GameObjects</see> will be hidden in the editor hierarchy
        /// </summary>
        public bool HideInHierarchy = true;

        public bool HideInSceneView = false;

        // /// <summary>
        // /// Factor to scale the rendered camera image resolution
        // /// </summary>
        // /// <returns>factor to scale the camera texture resolution</returns>
        // [Header("Environment Camera Image")] 
        // [Range(0.01f, 2)] public float Quality = 1;

        /// <summary>
        /// If set to none we use the Default RenderTextureFormat
        /// </summary>
        public GraphicsFormat Format = GraphicsFormat.None;

        /// <summary>
        /// The <see cref="RenderTexture"/> to render the simulated environment to
        /// </summary>
        // [FormerlySerializedAs("RealitySimulationRT")] 
        public RenderTexture EnvironmentCameraRT => internalRT;

        /// <summary>
        /// Experimental feature: dont save environment to the scene
        /// </summary>
        /// <returns>if true: simulated environment instances are not saved to the scene</returns>
        [Header("Experimental")] public bool NotSaved = true;

        /// <summary>
        /// Experimental feature: allows swapping the environment at runtime
        /// </summary>
        /// <returns>allow swapping the simulated environment during runtime</returns>
        public bool AllowRuntimeUpdates = false;


        /// <summary>
        /// The layer mask used for rendering the simulated environment in
        /// </summary>
        /// <returns>mask layer number</returns>
        public static int RealityLayerMask = 31;


        [SerializeField, HideInInspector] private UnityEngine.Object previousRealitySceneOrPrefab;
        [SerializeField, HideInInspector] private bool isValidRealityScene;

        [FormerlySerializedAs("realityCamera")] [SerializeField, HideInInspector] private Camera environmentCamera;
        private RenderTexture internalRT;
        private RealitySimulationSceneLoading previousLoading;

        [SerializeField, HideInInspector] private string currentRealityScenePath;
        [SerializeField, HideInInspector] private Scene currentRealitySceneInstance;
        [SerializeField, HideInInspector] private UnityEngine.Object currentRealityPrefabInstance;

        [SerializeField, HideInInspector] private List<GameObject> instantiatedGameObjects = new List<GameObject>();


#if (!UNITY_ANDROID && !UNITY_IOS) || UNITY_EDITOR

        /// <summary>
        /// Check if a <see cref="GameObject"/> is part of a simulated reality instance
        /// </summary>
        /// <param name="go">The <see cref="GameObject"/> to check</param>
        /// <returns>true if part of reality instance</returns>
        public bool IsPartOfSimulationInstance(GameObject go)
        {
            if (!go) return false;
            return instantiatedGameObjects != null && instantiatedGameObjects.Contains(go);
        }

        public bool TryGetARCameraClearFlags(out CameraClearFlags flags)
        {
            if (ARCamera)
            {
                flags = ARCamera.clearFlags;
                return true;
            }

            flags = 0;
            return false;
        }

        public void FixMainCameraClearFlags()
        {
            if (ARCamera && ARCamera.clearFlags == CameraClearFlags.Skybox)
            {
                ARCamera.clearFlags = CameraClearFlags.Color;
            }
        }

        /// <summary>
        /// Force to destroy and create new instances of simulated environment <see cref="GameObject"/>s
        /// </summary>
        [ContextMenu(nameof(ForceRecreate))]
        internal async void ForceRecreate()
        {
            await Task.Delay(1);
            DetectIfRealitySceneHasChanged(true);
        }

        internal async void UpdateIfChanged()
        {
            await Task.Delay(1);
            DetectIfRealitySceneHasChanged(true);
        }

        [ContextMenu(nameof(Disable))]
        private void Disable()
        {
            this.enabled = false;
            this.UnloadPreviousInstance(true);
        }

        private RealitySimulationSceneLoading RealitySimulationLoading = RealitySimulationSceneLoading.EditAndPlayMode;


        private async void OnEnable()
        {
            ARSimulationProjectInfo.RenderPipelineChanged += OnPipelineChanged;
#if UNITY_EDITOR
            EditorSceneManager.sceneSaving += OnSaving;
            EditorSceneManager.sceneSaved += OnSaved;
#if UNITY_URP
            SetupRendererURPCameraImageFeature();
#endif
#endif
            SetupCommandBuffer(true);

            if (!Application.isPlaying)
                await Task.Delay(1);
            if (this && enabled)
            {
                if (!Application.isPlaying && AllowRuntimeUpdates)
                    DetectIfRealitySceneHasChanged(true);
            }

            if (Application.isPlaying)
            {
                var background = FindObjectOfType<ARCameraBackground>();
                if (background && background.enabled)
                {
                    Debug.Log("Disabling AR Camera background", this);
                    background.enabled = false;
                }
            }
        }

        private void OnDisable()
        {
            ARSimulationProjectInfo.RenderPipelineChanged -= OnPipelineChanged;
#if UNITY_EDITOR
            EditorSceneManager.sceneSaving -= OnSaving;
            EditorSceneManager.sceneSaved -= OnSaved;
#endif
            if (!Application.isPlaying)
            {
                RemoveCommandBuffer();
                this.UnloadPreviousInstance(true);
            }
        }

        private bool wasActiveAndEnabled;

        private async void OnValidate()
        {
            if (IsPartOfSimulationInstance(SceneOrPrefab as GameObject) || (environmentCamera && SceneOrPrefab == environmentCamera.gameObject))
            {
                Debug.Log("Referencing an object in simulation instance or the environment camera is not allowed", this);
                SceneOrPrefab = previousRealitySceneOrPrefab;
                return;
            }
            
            SetupCommandBuffer(true);
            await Task.Delay(1);
            if (this && isActiveAndEnabled)
            {
                DetectIfRealitySceneHasChanged();
            }

            ApplySettings();
        }

#if UNITY_EDITOR
        private void OnSaving(Scene scene, string path)
        {
            if (NotSaved)
                UnloadPreviousInstance(true);
        }

        private void OnSaved(Scene scene)
        {
            if (NotSaved)
                DetectIfRealitySceneHasChanged(true);
        }

#if UNITY_URP
        [SerializeField, HideInInspector] private bool _rendererFeatureAdded;
        [ContextMenu(nameof(SetupRendererURPCameraImageFeature))]
        private void SetupRendererURPCameraImageFeature()
        {
            _rendererFeatureAdded = ARSimulationCameraBackgroundRendererFeature.AutomaticSupport.Run();
        }
#endif
#endif

        private void LateUpdate()
        {
            if (AllowRuntimeUpdates && RealityReferenceChanged())
            {
                DetectIfRealitySceneHasChanged(false);
                SetupCommandBuffer(true);
            }

            SyncReality();
        }

        private void OnPipelineChanged((CurrentRenderPipelineType previous, CurrentRenderPipelineType current) obj)
        {
            SetupCommandBuffer(true);
            DetectIfRealitySceneHasChanged();
            SyncReality();
        }

        private bool RealityReferenceChanged() => previousRealitySceneOrPrefab != SceneOrPrefab;

        private void UnloadPreviousInstance(bool destroyInstances)
        {
            RemoveCommandBuffer();
            
            previousLoading = RealitySimulationLoading;

            isValidRealityScene = SceneOrPrefab && (
#if UNITY_EDITOR
                SceneOrPrefab is SceneAsset ||
#endif
                SceneOrPrefab is GameObject);

            if (currentRealitySceneInstance.IsValid()) // && currentRealitySceneInstance.isLoaded)
            {
                try
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying && !EditorSceneManager.CloseScene(currentRealitySceneInstance, true))
                        Debug.Log("Failed removing " + currentRealitySceneInstance.path);
                    else if (Application.isPlaying && currentRealitySceneInstance.isLoaded) SceneManager.UnloadSceneAsync(currentRealitySceneInstance);
#else
                    SceneManager.UnloadSceneAsync(currentRealitySceneInstance);
#endif
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
                finally
                {
                    currentRealityScenePath = null;
                    currentRealitySceneInstance = new Scene();
                }
            }

            if (previousRealitySceneOrPrefab && previousRealitySceneOrPrefab is GameObject previousGameObject)
            {
#if UNITY_EDITOR
                var isAsset = EditorUtility.IsPersistent(previousGameObject);
                if (!isAsset)
#endif
                    previousGameObject.SetActive(true);
            }

            if (currentRealityPrefabInstance && currentRealityPrefabInstance != SceneOrPrefab)
            {
                if (Application.isPlaying)
                    Destroy(currentRealityPrefabInstance);
                else DestroyImmediate(currentRealityPrefabInstance);
                currentRealityPrefabInstance = null;
            }

            if (destroyInstances)
            {
                foreach (var go in instantiatedGameObjects)
                {
                    if (go)
                    {
                        if (Application.isPlaying) Destroy(go);
                        else DestroyImmediate(go);
                    }
                }

                instantiatedGameObjects.Clear();

                if (environmentCamera)
                {
                    if (Application.isPlaying) Destroy(environmentCamera.gameObject);
                    else DestroyImmediate(environmentCamera.gameObject);
                }
            }
        }

        private bool isLoadingScene;

        private async void DetectIfRealitySceneHasChanged(bool force = false)
        {
            if (!force && instantiatedGameObjects.Count > 0)
            {
                if (!RealityReferenceChanged())
                    return;
                if (!AllowRuntimeUpdates && Application.isPlaying)
                    return;
                if (previousLoading == RealitySimulationLoading && previousRealitySceneOrPrefab == SceneOrPrefab)
                    return;
            }

            if (SceneOrPrefab is GameObject prefab)
            {
                if (prefab.GetComponentsInChildren<SimulatedAREnvironmentManager>().Any(s => s && s == this))
                {
                    Debug.LogError(nameof(SimulatedAREnvironmentManager) + " can not be part of the assigned scene object", this);
                    return;
                }
            }

            UnloadPreviousInstance(true);

            if (this && !isActiveAndEnabled) return;
            if (isLoadingScene) return;
            
            previousRealitySceneOrPrefab = SceneOrPrefab;

            if (!SceneOrPrefab && SceneOrPrefab != null)
            {
                Debug.Log("Referenced object is destroyed or missing", this);
                return;
            }

#if UNITY_EDITOR
            if (SceneOrPrefab is SceneAsset sa)
            {
                currentRealityScenePath = AssetDatabase.GetAssetPath(sa);
            }
            else currentRealityScenePath = null;
#endif


            if (!string.IsNullOrWhiteSpace(currentRealityScenePath))
            {
                // Debug.Log("open scene " + currentRealityScenePath);
                Scene scene;
#if UNITY_EDITOR
                if (Application.isPlaying)
#endif
                {
#if UNITY_EDITOR
                    if (EditorBuildSettings.scenes.All(s => s.path != currentRealityScenePath))
                    {
                        Debug.Log("Please add " + currentRealityScenePath + " to your build settings to be loaded during playmode", SceneOrPrefab);
                        return;
                    }
#endif
                    var task = SceneManager.LoadSceneAsync(currentRealityScenePath, LoadSceneMode.Additive);
                    isLoadingScene = true;
                    while (!task.isDone) await Task.Delay(10);
                    isLoadingScene = false;
                    scene = SceneManager.GetSceneByPath(currentRealityScenePath);
                }
#if UNITY_EDITOR
                else
                    scene = EditorSceneManager.OpenScene(currentRealityScenePath, OpenSceneMode.Additive);
#endif


                currentRealitySceneInstance = scene;
                SetupRealityInstance(scene.GetRootGameObjects());
                UnloadPreviousInstance(false);
            }

            if (SceneOrPrefab is GameObject go)
            {
                if (!go)
                {
                    Debug.LogError("Referenced object is destroyed or missing", this);
                    return;
                }
                var inst = Instantiate(go);
                inst.SetActive(true);
                currentRealityPrefabInstance = inst;
                SetupRealityInstance(inst);

#if UNITY_EDITOR
                var isAsset = EditorUtility.IsPersistent(go);
                if (!isAsset)
#endif
                    go.SetActive(false);
            }
        }

        private void SetupRealityInstance(params GameObject[] gameObjects)
        {
            ARCamera.cullingMask = RealityLayerMask;

            var lazyCamProvider = new Lazy<SimulatedARCameraFrameDataProvider>(FindObjectOfType<SimulatedARCameraFrameDataProvider>);

            void SetupLayerMaskRecursively(GameObject go)
            {
                instantiatedGameObjects.Add(go);
                ApplySettings(go);

                foreach (var component in go.GetComponents<Component>())
                {
#if UNITY_EDITOR
                    if (component is Light lightComponent)
                    {
                        // try set directional light on cam data provider
                        if (lightComponent.type == LightType.Directional)
                        {
                            var prov = lazyCamProvider.Value;
                            if (prov && !prov.InputLight)
                            {
                                prov.InputLight = lightComponent;
                            }
                        }
                    }

                    if (component is Collider)
                        InternalEditorUtility.SetIsInspectorExpanded(component, false);
#endif
                }

                for (var i = 0; i < go.transform.childCount; i++)
                    SetupLayerMaskRecursively(go.transform.GetChild(i).gameObject);
            }

            // var sessionOrigin = FindObjectOfType<ARSessionOrigin>();

            var root = new GameObject("Simulated AR Environment Instance");
            if (NotEditable)
                root.hideFlags = HideFlags.NotEditable;
            else root.hideFlags &= ~HideFlags.NotEditable;

            root.layer = RealityLayerMask;
            instantiatedGameObjects.Add(root);
            ApplySettings(root);

            foreach (var go in gameObjects)
            {
                var scene = SceneManager.GetSceneAt(0);
                SceneManager.MoveGameObjectToScene(go, scene);
                var simParent = root.transform; // .camera.transform.parent;
                go.transform.SetParent(simParent, true);
                if (!environmentCamera)
                    environmentCamera = go.GetComponentInChildren<Camera>();
                SetupLayerMaskRecursively(go);
            }

            if (!environmentCamera) environmentCamera = new GameObject("Simulated AR Environment Camera").AddComponent<Camera>();
            ApplySettings(environmentCamera.gameObject);


            SetupCommandBuffer(true);
            SyncReality();
            SceneChangedOrRecreated?.Invoke(this);

            if (ARCamera.renderingPath == RenderingPath.DeferredLighting)
                Debug.LogWarning("Camera Image with deferred rendering is currently not supported", this);

#if UNITY_EDITOR
            InternalEditorUtility.RepaintAllViews();
#endif
        }

        private void ApplySettings()
        {
            if (ARCamera.clearFlags == CameraClearFlags.Skybox)
                Debug.LogWarning("Main camera clear flags must not be set to " + CameraClearFlags.Skybox + " for AR reality simulation", ARCamera);

            foreach (var inst in instantiatedGameObjects)
                ApplySettings(inst);
            if (environmentCamera)
                ApplySettings(environmentCamera.gameObject);

#if UNITY_EDITOR
            InternalEditorUtility.RepaintAllViews();
#endif
        }

        private void ApplySettings(GameObject go)
        {
            if (!go) return;

            go.layer = RealityLayerMask;
            go.tag = "EditorOnly";

            if (HideInHierarchy)
                go.hideFlags |= HideFlags.HideInHierarchy;
            else
                go.hideFlags &= ~HideFlags.HideInHierarchy;

            // settings HideFlags to HideAndDontSave makes onenable and ondisable being called multiple times the same frame 
            if (NotEditable)
                go.hideFlags |= HideFlags.NotEditable;
            else
                go.hideFlags &= ~HideFlags.NotEditable;

#if UNITY_EDITOR
#if UNITY_2019_3_OR_NEWER
            SceneVisibilityManager.instance.DisablePicking(go, false);
#endif
            if (HideInSceneView)
                SceneVisibilityManager.instance.Hide(go, false);
            else SceneVisibilityManager.instance.Show(go, false);
#endif
        }

        private CommandBuffer renderCameraBackground;
        private CameraEvent renderCameraEvent = CameraEvent.BeforeForwardOpaque;
        private CameraEvent previousCameraEvent; // for debugging

        private void SetupCommandBuffer(bool force)
        {
            // if (!EnvironmentCameraRT) return;
            if (force || renderCameraBackground == null || renderCameraEvent != previousCameraEvent)
            {
                if (!ARCamera) return;
                if (renderCameraBackground != null) ARCamera.RemoveCommandBuffer(previousCameraEvent, renderCameraBackground);
                renderCameraEvent = CameraEvent.BeforeForwardOpaque;
                previousCameraEvent = renderCameraEvent;
                if (ARSimulationProjectInfo.CurrentRenderPipeline != CurrentRenderPipelineType.Builtin) return;
                renderCameraBackground = new CommandBuffer() {name = "Render Simulated AR Environment"};
                renderCameraBackground.ClearRenderTarget(true, true, Color.black);
                var mat = ARSimulationProjectInfo.CreateRenderCameraImageMaterial();
                // renderCameraBackground.Blit(internalRT, RealitySimulationRT);
                renderCameraBackground.Blit(internalRT, BuiltinRenderTextureType.CurrentActive, mat);
                ARCamera.AddCommandBuffer(renderCameraEvent, renderCameraBackground);
            }
        }

        private void RemoveCommandBuffer()
        {
            if (renderCameraBackground != null && ARCamera) ARCamera.RemoveCommandBuffer(previousCameraEvent, renderCameraBackground);
            renderCameraBackground = null;
        }

        private GraphicsFormat format;
        private void SyncReality()
        {
            if (!environmentCamera) return;

            // if (EnvironmentCameraRT)
            {
                // Quality = Mathf.Abs(Quality);
                // if (Quality < 0.00001f)
                // {
                //     Debug.LogWarning("A value of zero for " + nameof(Quality) + " is not allowed");
                //     Quality = 0.00001f;
                // }
                
                if (!internalRT || internalRT.width != Screen.width || internalRT.height != Screen.height || format != Format)
                {
                    if (internalRT && internalRT.IsCreated())
                        internalRT.Release();
                    if (Format != GraphicsFormat.None && !SystemInfo.IsFormatSupported(Format, FormatUsage.Render))
                    {
                        Debug.LogError("Format is not supported: " + Format, this);
                        Format = GraphicsFormat.None;
                    }
                    format = Format;
                    // if format is set to None use the default RenderTexture format
                    internalRT = Format == GraphicsFormat.None 
                        ? new RenderTexture(Screen.width, Screen.height, 1, RenderTextureFormat.Default) 
                        : new RenderTexture(Screen.width, Screen.height, 1, Format);
                    internalRT.Create();
                    SetupCommandBuffer(true);
                }

                environmentCamera.targetTexture = internalRT;
                environmentCamera.cullingMask = ~ RealityLayerMask;
                // Graphics.Blit(internalRT, EnvironmentCameraRT);
            }

            var t = environmentCamera.transform;
            var mc = ARCamera.transform;
            var camPose = new Pose(mc.position, mc.rotation);
            var sessionSpace = mc.parent;
            bool hasSessionSpace = sessionSpace;

            // if you dont have a scene setup for AR but setup the reality sim
            // it's possible for the camera to not be in session space
            // in that case we dont need to transform our simulation camera
            // at runtime tho a session is expected to be present
            if (!Application.isPlaying)
                hasSessionSpace &= mc.GetComponentInParent<ARSessionOrigin>();
            else if (!hasSessionSpace && Time.frameCount == 300)
                Debug.LogWarning("The main camera is expected to be in ARSessionOrigin hierarchy", this);

            if (hasSessionSpace)
                camPose = sessionSpace.InverseTransformPose(camPose);
            t.position = camPose.position;
            t.rotation = camPose.rotation;
            environmentCamera.fieldOfView = ARCamera.fieldOfView;
            environmentCamera.focalLength = ARCamera.focalLength;
            environmentCamera.nearClipPlane = ARCamera.nearClipPlane;
            environmentCamera.farClipPlane = ARCamera.farClipPlane;
            environmentCamera.usePhysicalProperties = ARCamera.usePhysicalProperties;
            if (ARSimulationProjectInfo.CurrentRenderPipeline == CurrentRenderPipelineType.Builtin)
            {
                environmentCamera.clearFlags = CameraClearFlags.Color;
                environmentCamera.backgroundColor = ARCamera.backgroundColor;
            }
        }
#endif
    }
}