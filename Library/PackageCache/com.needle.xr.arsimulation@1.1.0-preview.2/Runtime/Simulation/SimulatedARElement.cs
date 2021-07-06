using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Needle.XR.ARSimulation.Simulation
{
    /// <summary>
    /// Base AR Desktop simulation component that provides handy utility methods
    /// </summary>
    public abstract class SimulatedARElement : MonoBehaviour
    {
        private bool _triedGettingOrigin; 
        private ARSessionOrigin _arSessionOrigin;
        private ARSessionOrigin ArSessionOrigin
        {
            get
            {
                if (_triedGettingOrigin) return _arSessionOrigin;
                _triedGettingOrigin = true;
                if (!_arSessionOrigin)
                {
                    _arSessionOrigin = GetComponentInParent<ARSessionOrigin>();
                }
                return _arSessionOrigin;
            }
        }

        /// <summary>
        /// If the component is part of the <see cref="ArSessionOrigin"/> hierarchy this method transforms the provided world space in session space
        /// </summary>
        /// <param name="poseInWorldSpace">The Pose in world space to be passed to AR Foundation</param>
        /// <returns>The Pose transformed in session space if the component is part of the AR Session Origin hierarchy</returns>
        protected Pose TransformPoseToSessionSpaceIfNecessary(Pose poseInWorldSpace)
        {
            if (ArSessionOrigin && ArSessionOrigin.trackablesParent)
            {
                return ArSessionOrigin.trackablesParent.InverseTransformPose(poseInWorldSpace);
            }
            return poseInWorldSpace;
        }

        protected void Reset()
        {
            _triedGettingOrigin = false;
        }
    }
}