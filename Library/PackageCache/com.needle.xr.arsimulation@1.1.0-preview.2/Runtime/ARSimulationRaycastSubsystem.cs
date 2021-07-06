using System.Collections.Generic;
using Needle.XR.ARSimulation.Simulation;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

// ReSharper disable InconsistentNaming

namespace Needle.XR.ARSimulation
{
    /// <summary>
    /// ARDesktop implementation of the <c>XRRaycastSubsystem</c>. Do not create this directly. Use the <c>SubsystemManager</c> instead.
    /// </summary>
    [Preserve]
    public sealed class ARSimulationRaycastSubsystem : XRRaycastSubsystem
    {
        // ReSharper disable once FieldCanBeMadeReadOnly.Global
        // ReSharper disable once ConvertToConstant.Global
        public static bool CallPhysicsSyncTransformsBeforeRaycasting = false;

#if !UNITY_2020_2_OR_NEWER || !UNITY_ARSUBSYSTEMS_4_0_1_OR_NEWER
        protected override Provider CreateProvider() => new ARSimulationProvider();
#endif

        private class ARSimulationProvider : Provider
        {
            private readonly List<XRRaycastHit> _hits = new List<XRRaycastHit>();
            private readonly RaycastHit[] _raycastHits = new RaycastHit[1024];
            private ARSessionOrigin _sessionOrigin;

            public override NativeArray<XRRaycastHit> Raycast(
                XRRaycastHit defaultRaycastHit,
                Ray ray,
                TrackableType trackableTypeMask,
                Allocator allocator)
            {
                if (!SceneSetup.TryGetARCamera(out var arCamera)) return new NativeArray<XRRaycastHit>();
                var arCameraTransform = arCamera.transform;

                // ray = arCameraTransform.TransformRay(ray);

                // var hitCount = Physics.RaycastNonAlloc(ray, _raycastHits);
                if (CallPhysicsSyncTransformsBeforeRaycasting)
                    Physics.SyncTransforms();
                var hitCount = Physics.RaycastNonAlloc(ray, _raycastHits);
                var hits = _raycastHits;
                if (hitCount <= 0) return new NativeArray<XRRaycastHit>();
                _hits.Clear();
                // if we use MakeContentAppear another Transform is mixed in and offset so that breaks raycasting pose transformation
                // we get the session from the parent of the camera because that way we either get the session origin transform directly 
                // if MakeContentAppear was never used OR we get the Content offset transform
                // either way: InverseTransformPoint puts us then in correct session space
                var cameraParent = arCameraTransform.parent;

                if (!_sessionOrigin) _sessionOrigin = arCameraTransform.GetComponentInParent<ARSessionOrigin>();
                var sessionTransform = _sessionOrigin.transform;
                
                var origin = ray.origin; //session.InverseTransformPoint(ray.origin);

                for (var i = 0; i < hitCount; i++)
                {
                    var hit = hits[i];
                    // try get id from hit plane
                    if (!hit.collider.TryGetComponent<ARPlane>(out var plane)) continue;

                    // make stuff session relative
                    hit.distance = Vector3.Distance(hit.point, origin) / sessionTransform.localScale.x;
                    hit.point = cameraParent.InverseTransformPoint(hit.point);
                    hit.normal = cameraParent.InverseTransformDirection(hit.normal);

                    // var planeForward = Vector3.Cross(right, hit.normal);
                    var normalRotation = Quaternion.LookRotation(cameraParent.forward, hit.normal);

                    var pose = new Pose(hit.point, normalRotation);
                    var arHit = new XRRaycastHit(plane.trackableId, pose, hit.distance, trackableTypeMask);
                    _hits.Add(arHit);
                }

                var arr = new NativeArray<XRRaycastHit>(_hits.Count, allocator);
                for (var k = 0; k < arr.Length; k++)
                    arr[k] = _hits[k];
                return arr;
            }

            public override NativeArray<XRRaycastHit> Raycast(
                XRRaycastHit defaultRaycastHit,
                Vector2 screenPoint,
                TrackableType trackableTypeMask,
                Allocator allocator)
            {
                SceneSetup.TryGetARCamera(out var cam);
                if (cam == null) return new NativeArray<XRRaycastHit>();
                var sp = (Vector3) (screenPoint * new Vector2(Screen.width, Screen.height));
                var ray = cam.ScreenPointToRay(sp);
                // ignore the near plane
                ray.origin = cam.transform.position;
                return Raycast(defaultRaycastHit, ray, trackableTypeMask, allocator);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDescriptor()
        {
            XRRaycastSubsystemDescriptor.RegisterDescriptor(new XRRaycastSubsystemDescriptor.Cinfo
            {
                id = "ARSimulation-Raycast",
#if UNITY_2020_2_OR_NEWER && UNITY_ARSUBSYSTEMS_4_0_1_OR_NEWER
                providerType = typeof(ARSimulationProvider),
                subsystemTypeOverride = typeof(ARSimulationRaycastSubsystem),
#else
                subsystemImplementationType = typeof(ARSimulationRaycastSubsystem),
#endif
                supportsViewportBasedRaycast = true,
                supportsWorldBasedRaycast = true,
                supportedTrackableTypes =
                    (TrackableType.Planes & ~TrackableType.PlaneWithinInfinity) |
                    TrackableType.FeaturePoint
            });
        }
    }
}