using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.XR.ARSubsystems;

namespace Needle.XR.ARSimulation.Simulation
{
    /// <summary>
    /// Used to pass camera data to AR Foundation like <see cref="AverageBrightness"/> or <see cref="SphericalHarmonicsL2"/>
    /// </summary>
    [ExecuteAlways]
    public class SimulatedARCameraFrameDataProvider : MonoBehaviour
    {
        
        /// <summary>
        /// The AR camera tracking state
        /// </summary>
        public TrackingState TrackingState = TrackingState.Tracking;
        
        [Header("Input Light")]
        [FormerlySerializedAs("MainLight")]
        public Light InputLight;
        public bool ConvertIntensityToLumen = true;

        // BUG: this makes VisualElement Popup go beyond scope and not render the component anymore
        //public XRCameraFrameProperties Properties = (XRCameraFrameProperties) ~0;

        [Header("Environment Estimation / Spherical Harmonics")]
        public bool UseSphericalHarmonics = true;
        [Range(0,2)]
        public float AverageBrightness = 0.1f;
        [Range(1000, 10000)]
        public float ColorTemperature = 6500;
        public Color ColorCorrection = Color.white;
        public double ExposureDuration;
        public float ExposureOffset;

        [Header("Custom Camera Matrices")]
        /// <summary>
        /// Editor AR camera projection matrix passed to ARFoundation
        /// </summary>
        public Matrix4x4 ProjectionMatrix = Matrix4x4.identity;

        public Matrix4x4 DisplayMatrix = Matrix4x4.identity;
        
#if (UNITY_ARSUBSYSTEMS_3_1_0_PREVIEW_2_OR_NEWER && !UNITY_ARSUBSYSTEMS_3_1_3_OR_NEWER) || UNITY_ARSUBSYSTEMS_4_OR_NEWER
        [Header("Generated from Input Light")]
        // added in 3.1.0-preview2, removed in 3.1.3 https://github.com/needle-mirror/com.unity.xr.arsubsystems/commit/928aca5600531b8a4eb7562c6a5f46ec3b580ba7
        public float MainLightIntensityLumen;
        public Color MainLightColor;
        public Vector3 MainLightDirection = Vector3.down;

        /// <summary>
        /// Spherical Harmonics to be passed to ARFoundation.
        /// They get automatically calculated from the light assigned to <see cref="DirectionalLightToSetSphericalHarmonicsFrom"/>.
        /// If no light is assigned it will use the first directional light it finds in the scene
        /// </summary>
        public SphericalHarmonicsL2 SphericalHarmonicsL2;

        [Header("Generation Settings")]
        /// <summary>
        /// Set to true to recalculate <see cref="SphericalHarmonicsL2"/> every frame
        /// </summary>
        public bool SphericalHarmonicsContinuousUpdate = true;
#endif

        private Camera arCamera;


        // public Texture2D CameraImage { get; private set; }

        /// <summary>
        /// Directional light used to calculate spherical harmonics from. 
        /// If you do not assign a light here it uses the first directional light it finds in the scene
        /// </summary>
        [FormerlySerializedAs("LightToSetSphericalHarmonicsFrom")]
        public Light DirectionalLightToSetSphericalHarmonicsFrom;


#if (!UNITY_ANDROID && !UNITY_IOS) || UNITY_EDITOR
        [ContextMenu(nameof(TryFindSphericalHarmonicsDirectionalLight))]
        private void TryFindSphericalHarmonicsDirectionalLight()
        {
            if (!DirectionalLightToSetSphericalHarmonicsFrom)
                DirectionalLightToSetSphericalHarmonicsFrom = Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
        }

        private void EnsureARCamera()
        {
            if (!arCamera)
            {
                if (!SceneSetup.TryGetARCamera(out arCamera))
                    arCamera = Camera.main;
            }
        }

        private void Start()
        {
            EnsureARCamera();
#if (UNITY_ARSUBSYSTEMS_3_1_0_PREVIEW_2_OR_NEWER && !UNITY_ARSUBSYSTEMS_3_1_3_OR_NEWER) || UNITY_ARSUBSYSTEMS_4_OR_NEWER
            UpdateSphericalHarmonics(true);
#endif
        }

        private void LateUpdate()
        {
            EnsureARCamera();

            if (arCamera)
            {
                ProjectionMatrix = arCamera.projectionMatrix;
            }


#if (UNITY_ARSUBSYSTEMS_3_1_0_PREVIEW_2_OR_NEWER && !UNITY_ARSUBSYSTEMS_3_1_3_OR_NEWER) || UNITY_ARSUBSYSTEMS_4_OR_NEWER
            UpdateSphericalHarmonics();

            if (InputLight != null)
            {
                MainLightColor = InputLight.color;
                MainLightDirection = InputLight.transform.forward;
                MainLightIntensityLumen = ConvertIntensityToLumen ? ConvertBrightnessToLumen(InputLight.intensity) : InputLight.intensity;
            }
#endif
        }

#if (UNITY_ARSUBSYSTEMS_3_1_0_PREVIEW_2_OR_NEWER && !UNITY_ARSUBSYSTEMS_3_1_3_OR_NEWER) || UNITY_ARSUBSYSTEMS_4_OR_NEWER
        private void UpdateSphericalHarmonics(bool force = false)
        {
            if (force || SphericalHarmonicsContinuousUpdate)
            {
                TryFindSphericalHarmonicsDirectionalLight();
                if (DirectionalLightToSetSphericalHarmonicsFrom)
                {
                    SphericalHarmonicsL2.Clear();

                    if(UseSphericalHarmonics)
                    {
                        var lightIntensity = DirectionalLightToSetSphericalHarmonicsFrom.intensity;
                        var intensity = ConvertIntensityToLumen ? lightIntensity : ConvertBrightnessFromLumen(lightIntensity);
                        SphericalHarmonicsL2.AddDirectionalLight(
                            -DirectionalLightToSetSphericalHarmonicsFrom.transform.forward, 
                            DirectionalLightToSetSphericalHarmonicsFrom.color,
                            intensity
                        );
                        // DirectionalLightToSetSphericalHarmonicsFrom.colorTemperature
                        SphericalHarmonicsL2.AddAmbientLight(ColorTemperatureToRGB(ColorTemperature) * AverageBrightness);
                    }
                }
            }
        }
#endif

        private static float ConvertBrightnessToLumen(float brightness)
        {
            const float kMaxLuminosity = 2000.0f;
            return Mathf.Clamp(brightness * kMaxLuminosity, 0f, kMaxLuminosity);
        }

        private static float ConvertBrightnessFromLumen(float brightness)
        {
            const float kMaxLuminosity = 2000.0f;
            return Mathf.Clamp(brightness / kMaxLuminosity, 0f, 1);
        }

    Color ColorTemperatureToRGB (float kelvin) {
        var temp = kelvin / 100;
        float red, green, blue;

        if( temp <= 66 ) { 
            red = 255; 
            green = temp;
            green = 99.4708025861f * Mathf.Log(green) - 161.1195681661f;
            
            if( temp <= 19){
                blue = 0;
            } else {
                blue = temp-10;
                blue = 138.5177312231f * Mathf.Log(blue) - 305.0447927307f;
            }
        } else {
            red = temp - 60;
            red = 329.698727446f * Mathf.Pow(red, -0.1332047592f);
    
            green = temp - 60;
            green = 288.1221695283f * Mathf.Pow(green, -0.0755148492f );

            blue = 255;
        }

        return new Color(Mathf.Clamp01(red / 255f), Mathf.Clamp01(green / 255f), Mathf.Clamp01(blue / 255f));
    }
#endif
    }
}