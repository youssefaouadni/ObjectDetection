#if UNITY_LEGACY_INPUT_2_OR_NEWER
using UnityEngine.SpatialTracking;
#endif
using System;
using Needle.XR.ARSimulation.Compatibility;
using Needle.XR.ARSimulation.Extensions;
using Needle.XR.ARSimulation.Interfaces;
using UnityEngine;
using UnityEngine.Experimental.XR.Interaction;

namespace Needle.XR.ARSimulation.Simulation
{
    /// <summary>
    /// supporting the "old/legacy" input system
    /// </summary>
    public class SimulatedARPoseProvider : BasePoseProvider, IOverrideablePositionRotation, IInputDevice
    {
#if UNITY_LEGACY_INPUT_2_OR_NEWER
        public override PoseDataFlags GetPoseFromProvider(out Pose output)
        {
#if (!UNITY_IOS && !UNITY_ANDROID) || UNITY_EDITOR
            output = new Pose(currentPosition, currentRotation);
            return PoseDataFlags.Position | PoseDataFlags.Rotation;
#else
            output = Pose.identity;
            return PoseDataFlags.NoData;
#endif
        }
#else
        public override bool TryGetPoseFromProvider(out Pose output)
        {
#if (!UNITY_IOS && !UNITY_ANDROID) || UNITY_EDITOR
            output = new Pose(currentPosition, currentRotation);
            return true;
#else
            output = Pose.identity;
            return false;
#endif
        }
#endif

        public float moveSensibility = 1;
        public float rotationSensibility = 5;
        [Range(0.01f, 1)] public float interpolation = .1f;

        [Tooltip("This is only used for legacy input to override usage with ARPoseDriver")]
        public bool setTransformDirectly = false;

        private Vector3 currentPosition;
        private Quaternion currentRotation;

        private Quaternion _targetRotation;
        private Vector3 _targetPosition;
        private Vector2 lastMousePosition;

#if (!UNITY_IOS && !UNITY_ANDROID) || UNITY_EDITOR
        private bool didOverride = false;
#endif

        public Vector3 Forward => transform.forward;

        public void SetTargetPosition(Vector3 position)
        {
#if (!UNITY_IOS && !UNITY_ANDROID) || UNITY_EDITOR
            _targetPosition = position;
#endif
        }
        
        public void SetTargetRotation(Quaternion rot)
        {
#if (!UNITY_IOS && !UNITY_ANDROID) || UNITY_EDITOR
            _targetRotation = rot;
#endif
        }
        
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
#if (!UNITY_IOS && !UNITY_ANDROID) || UNITY_EDITOR
            currentPosition = position;
            currentRotation = rotation;
            var t = transform;
            t.position = position;
            t.rotation = rotation;
            _targetPosition = position;
            _targetRotation = rotation;
            didOverride = true;
#endif
        }


#if (!UNITY_IOS && !UNITY_ANDROID) || UNITY_EDITOR
        private void Start()
        {
            if (didOverride) return;
            var t = transform;
            currentPosition = t.position;
            currentRotation = t.localRotation;
            _targetRotation = currentRotation;
            _targetPosition = currentPosition;
#if !UNITY_NEW_INPUT_SYSTEM
            if (!Ensure.CorrectInputSystemConfiguration())
                return;
            lastMousePosition = Input.mousePosition;
#endif
        }

        private void OnDestroy()
        {
            
        }


        private void LateUpdate()
        {
            // ReSharper disable once Unity.NoNullPropagation
            InputHelper.GetInput(out var h, out var v, out var u, out var active, out var wasActive);
            var settings =  ARSimulationLoaderSettings.Instance ? ARSimulationLoaderSettings.Instance.InputSettings : new ARSimulationLoaderSettings.DeviceInputSettings();
            var dt = Time.deltaTime;
            dt = Mathf.Clamp(dt, 0, .1f);
            h *= moveSensibility * dt * settings.MovementSensitivity;
            v *= moveSensibility * dt * settings.MovementSensitivity;
            u *= moveSensibility * dt * settings.MovementSensitivity;

            var requireRightMouseToMove = (settings.Mouse & ARSimulationLoaderSettings.DeviceInputSettings.MouseMovement.RequireRightMousePressedToMove) != 0;

            if ((active && wasActive) || !requireRightMouseToMove)
            {
                var t = transform;
                var forward = Quaternion.Inverse(t.parent.rotation) * t.forward;
                Vector3 up;
                if (settings.Mode == ARSimulationLoaderSettings.DeviceInputSettings.MovementMode.Walk)
                {
                    forward.y = 0;
                    forward.Normalize();
                    up = Vector3.up;
                }
                else
                {
                    forward = t.forward;
                    up = t.up;
                }

                var right = Quaternion.Inverse(t.parent.rotation) * t.right;
                _targetPosition += (v * forward + h * right + u * up);
            }

            if(active && wasActive)
            {
                var mouseDelta = InputHelper.Instance.MousePosition - lastMousePosition;
                InputHelper.NormalizeSpeed(ref mouseDelta);
                var delta = new Vector2(mouseDelta.x, mouseDelta.y);
                delta = Vector2.ClampMagnitude(delta, 200);
                var d = new Vector2(delta.x, delta.y);
                d *= rotationSensibility * Time.deltaTime * settings.RotationSensitivity;
                if (!d.HasNaN())
                {
                    var wr = new Vector3(0, d.x, 0);
                    var lr = new Vector3(-d.y, 0, 0);
                    _targetRotation.Normalize();
                    _targetRotation = Quaternion.Euler(wr) * (_targetRotation * Quaternion.Euler(lr));
                }
            }

            lastMousePosition = InputHelper.Instance.MousePosition;
            
            if (InputHelper.Instance.FocusKeyDown) InputDeviceNavigationUtil.SetFocus(this);

            var step = interpolation <= 0.001f ? 1 : Mathf.Clamp01(dt / interpolation);
            var newPosition = Vector3.Slerp(currentPosition, _targetPosition, step);
            currentPosition = newPosition;
            var newRotation = Quaternion.Slerp(currentRotation, _targetRotation, step);
            currentRotation = newRotation;

            if (setTransformDirectly) // we need to do this in LateUpdate to override ARPoseDriver because there is no official API 
            {
                var t = transform;
                t.position = currentPosition;
                t.rotation = currentRotation;
            }
        }
#else
            private void Awake()
            {
                enabled = false;
            }
#endif
    }
}