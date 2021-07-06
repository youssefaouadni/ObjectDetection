using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Needle.XR.ARSimulation.Simulation
{
    /// <summary>
    /// Used to mark a <see cref="GameObject">GameObject</see>
    /// to be used as a simulated reality scene provider for <see cref="SimulatedAREnvironmentManager"/>
    /// <para>
    /// Add this component to a gameobject and set <see cref="IsActive"/> to true
    /// to render the object including children as camera image
    /// </para>
    /// <para>
    /// NOTE: if another scene or gameobject is currently rendered as a camera image
    /// it will be swapped with the new one
    /// </para>
    /// </summary>
    [ExecuteAlways]
    public class SimulatedAREnvironment : MonoBehaviour
    {
        /// <summary>
        /// Set to true to render this object including children as a camera image
        /// </summary>
        public bool IsActive = false;


#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
        private void OnValidate()
        {
            if (Application.isPlaying) return;

#if UNITY_EDITOR
            if (EditorUtility.IsPersistent(this.gameObject))
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            AsyncValidate();
        }

        private bool evaluating;
        private async void AsyncValidate()
        {
            evaluating = true;
            await Task.Delay(1);
            evaluating = false;
            if (!this || !this.gameObject) return;
            if (SimulatedAREnvironmentManager.Exists && SimulatedAREnvironmentManager.Instance.IsPartOfSimulationInstance(this.gameObject))
            {
                if (Application.isPlaying) Destroy(this);
                else DestroyImmediate(this);
                return;
            }

            if (IsActive)
            {
                SimulatedAREnvironmentManager.Instance.SceneOrPrefab = this.gameObject;
                SimulatedAREnvironmentManager.Instance.UpdateIfChanged();
                SimulatedAREnvironmentManager.Instance.enabled = true;
                SimulatedAREnvironmentManager.Instance.FixMainCameraClearFlags();
            }
            else if (SimulatedAREnvironmentManager.Exists && SimulatedAREnvironmentManager.Instance.SceneOrPrefab == this.gameObject)
            {
                SimulatedAREnvironmentManager.Instance.SceneOrPrefab = null;
                SimulatedAREnvironmentManager.Instance.UpdateIfChanged();
            }
        }

        private void Update()
        {
            if (SimulatedAREnvironmentManager.Exists && !evaluating)
                IsActive = SimulatedAREnvironmentManager.Instance.SceneOrPrefab == this.gameObject;
        }
#endif
    }
}