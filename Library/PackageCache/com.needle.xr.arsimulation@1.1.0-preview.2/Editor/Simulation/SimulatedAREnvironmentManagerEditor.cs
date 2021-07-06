using UnityEditor;
using UnityEngine;

namespace Needle.XR.ARSimulation.Simulation
{
    [CustomEditor(typeof(SimulatedAREnvironmentManager))]
    public class SimulatedAREnvironmentManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            if (target is SimulatedAREnvironmentManager rm)
            {
                if (rm.TryGetARCameraClearFlags(out var flags))
                {
                    if (flags == CameraClearFlags.Skybox)
                    {
                        EditorGUILayout.HelpBox("Main Camera clear flags must not be set to Skybox", MessageType.Warning, true);
                        if (GUILayout.Button("Fix Camera"))
                        {
                            rm.FixMainCameraClearFlags();
                        }
                    }
                }
            }
        }
    }
}