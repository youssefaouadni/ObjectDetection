
#if UNITY_XR_MANAGEMENT_3_2_10_OR_ABOVE

using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

using UnityEngine.XR.Management;

using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;

namespace Needle.XR.ARSimulation
{
    public class ARSimulationXRLoaderSetup
    {
        public static string LoaderType => typeof(ARSimulationLoader).FullName;

        [InitializeOnLoadMethod]
        // ReSharper disable once UnusedMember.Local
        private static async void EnableXRLoader()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Application.isPlaying) return;

            ARSimulationLoader.RequestLoaderActivation += () => EnsureXRLoaderEnabled(true);

            while (true)
            {
                // wait a moment for XRGeneralSettings.Instance to be available
                // if we run immediately the instance will be either overwritten 
                // or our settings will be gone the next frame
                await Task.Delay(500);
                // check if user allowed setting the xr loader
                var settings = ARSimulationLoader.GetSettings();
                if (settings != null && settings && !settings.allowAddXRLoader)
                {
                    await Task.Delay(10000);
                    continue;
                }

                if (EnsureXRLoaderEnabled(false))
                    Debug.Log(
                        "<b>[AR Simulation]</b> Enabled XR Management Loader ✔\nIf you dont want automatic XR Loader activation go to " +
                        "\"Project Settings/XR Plug-In Management/AR Simulation\" and uncheck \"" +
                        ObjectNames.NicifyVariableName(nameof(settings.allowAddXRLoader)) + "\"",
                        XRGeneralSettings.Instance);

                await Task.Delay(2000);
            }
        }

        private static bool didTryOpenProjectWindow;

        /// <summary>
        /// Make sure xr loader is added to xr plug in management
        /// </summary>
        /// <returns>true if added to plug in management</returns>
        private static bool EnsureXRLoaderEnabled(bool viaEvent)
        {
            var inst = Resources.FindObjectsOfTypeAll<ARSimulationLoader>().LastOrDefault();
            if (!inst)
            {
                inst = ScriptableObject.CreateInstance<ARSimulationLoader>();
            }
            XRGeneralSettings.Instance = XRGeneralSettings.Instance
                ? XRGeneralSettings.Instance
                : Resources.FindObjectsOfTypeAll<XRGeneralSettings>().FirstOrDefault();
            if (!inst || XRGeneralSettings.Instance == null || !XRGeneralSettings.Instance)
            {
                // see issue https://github.com/needle-tools/ar-simulation/issues/28
                if (!didTryOpenProjectWindow)
                {
                    // Debug.Log("XR Management is not initialized. Please open \"Project Settings/XR Plug-in Management\" and enable AR Simulation");
                    didTryOpenProjectWindow = true;
                    SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
                    return EnsureXRLoaderEnabled(viaEvent);   
                }

                return false; 
                // in a new editor/project without having used XR management there might be no settings asset
                // xr management seems to create it when opening and selecting the preferences window
                // we want to set it up automatically here though on install so if there is no instance already
                // if(!CreateSettingsIfNonExist() || XRGeneralSettings.Instance == null)
                //     return false;
            }

            if (!XRGeneralSettings.Instance.Manager) return false;
            var loaders = XRGeneralSettings.Instance.Manager.loaders;
            if (loaders == null) return false;
            // if not in list already
            if (loaders.Any(l => l is ARSimulationLoader)) return false;
            loaders.Add(inst);
            XRPackageMetadataStore.AssignLoader(XRGeneralSettings.Instance.Manager, LoaderType, BuildTargetGroup.Standalone);
            EditorUtility.SetDirty(XRGeneralSettings.Instance.Manager);
            EditorUtility.SetDirty(XRGeneralSettings.Instance);
            AssetDatabase.SaveAssets();
            return true;
        }

        // the following code is unfortunately not enough. an instance might be created but the manager is still missing
        // private static XRGeneralSettings CreateSettingsIfNonExist()
        // {
        //     if (XRGeneralSettings.Instance) return XRGeneralSettings.Instance;
        //     // more or less how XRSettingsManager creates the instance
        //     var generalSettings = ScriptableObject.CreateInstance(typeof(XRGeneralSettings)) as XRGeneralSettings;
        //     var assetPath = "Assets/XR/";
        //     if (!string.IsNullOrEmpty(assetPath))
        //     {
        //         assetPath += "/XRGeneralSettings.asset";
        //         AssetDatabase.CreateFolder("Assets", "XR");
        //         AssetDatabase.CreateAsset(generalSettings, assetPath);
        //     }
        //     EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, generalSettings, true);
        //     XRGeneralSettings.Instance = generalSettings;
        //     return XRGeneralSettings.Instance;
        // }
    }
}


#endif