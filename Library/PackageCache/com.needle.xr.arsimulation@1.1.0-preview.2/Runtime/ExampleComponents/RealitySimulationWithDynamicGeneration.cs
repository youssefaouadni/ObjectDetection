using Needle.XR.ARSimulation.Simulation;
using UnityEngine;

namespace Needle.XR.ARSimulation.ExampleComponents
{
    public class RealitySimulationWithDynamicGeneration : MonoBehaviour
    {
        private void Awake()
        {
            SimulatedAREnvironmentManager.SceneChangedOrRecreated += OnChangedOrRecreated;
            if(SimulatedAREnvironmentManager.Exists)
                RemoveARPlanes(SimulatedAREnvironmentManager.Instance);
        }

        private static void OnChangedOrRecreated(SimulatedAREnvironmentManager simulatedArEnvironmentManager)
        {
            RemoveARPlanes(simulatedArEnvironmentManager);
        }

        private static void RemoveARPlanes(SimulatedAREnvironmentManager manager)
        {
            if (manager.SceneInstances == null) return;
            foreach (var instance in manager.SceneInstances)
            {
                var simulatedPlane = instance.GetComponent<SimulatedARPlane>();
                if (simulatedPlane)
                {
                    Destroy(simulatedPlane);
                }
            }
        }
    }
}