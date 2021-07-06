using System;
using System.Collections.Generic;
using Needle.XR.ARSimulation.Compatibility;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;
using TouchPhase = UnityEngine.TouchPhase;

#if UNITY_NEW_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Needle.XR.ARSimulation.ExampleComponents
{
    /// <summary>
    /// Helper class to perform AR raycasts from mouse position
    /// </summary>
    public static class ARInput
    {
        public enum InputType
        {
            Any = 0,
            Mouse = 1,
            Touch = 2,
        }
        
        private static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        public static ARRaycastManager RaycastManager => new Lazy<ARRaycastManager>(Object.FindObjectOfType<ARRaycastManager>).Value;

        public static bool TryGetHit(InputType type, out ARRaycastHit hit)
        {
            if (!TryGetInputPosition(type, out var inputPosition))
            {
                hit = new ARRaycastHit();
                return false;
            }

            if (!RaycastManager.Raycast(inputPosition, s_Hits, TrackableType.PlaneWithinPolygon))
            {
                hit = new ARRaycastHit();
                return false;
            }

            hit = s_Hits[0];
            return true;
        }

#if UNITY_NEW_INPUT_SYSTEM
        static bool[] touchBlocked = new bool[10];
#endif

        // ReSharper disable once MemberCanBePrivate.Global
        public static bool TryGetInputPosition(InputType type, out Vector2 inputPos)
        {
            if (!Ensure.CorrectInputSystemConfiguration())
            {
                inputPos = Vector2.zero;
                return false;
            }

#if UNITY_NEW_INPUT_SYSTEM

            if (type == InputType.Mouse || type == InputType.Any)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.enabled)
                {
                    // Debug.Log(mouse.leftButton.ReadValue() + ", " + mouse.leftButton.wasPressedThisFrame + ", " + mouse.leftButton.wasReleasedThisFrame + ", " + mouse.leftButton.ReadValueFromPreviousFrame() + ", " + mouse.leftButton.device.wasUpdatedThisFrame);
                    mouse.leftButton.pressPoint = .5f;
                    if (mouse.leftButton.wasPressedThisFrame || mouse.leftButton.ReadValue() > mouse.leftButton.ReadValueFromPreviousFrame())
                    {
                        inputPos = mouse.position.ReadValue();
                        return true;
                    }
                }
            }

            if (type == InputType.Touch || type == InputType.Any)
            {
                var touchScreen = Touchscreen.current;
                if (touchScreen != null && touchScreen.enabled)
                {
                    foreach (var touch in touchScreen.touches)
                    {
                        if (!touchBlocked[touch.touchId.ReadValue()] && touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                        {
                            touchBlocked[touch.touchId.ReadValue()] = true;
                            
                            // Debug.Log(Time.realtimeSinceStartup + ", " + touch.startTime.ReadValue());
                            // (Time.realtimeSinceStartup - touch.startTime.ReadValue()) == 0) // not useful, does not happen in the same frame

                            inputPos = touch.ReadValue().position;
                            return true;
                        }
                        else if(touch.phase.ReadValue().IsEndedOrCanceled()) {
                            touchBlocked[touch.touchId.ReadValue()] = false;
                        }
                    }
                }
            }
            
#else

            if (type == InputType.Mouse || type == InputType.Any)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    var mousePosition = Input.mousePosition;
                    inputPos = new Vector2(mousePosition.x, mousePosition.y);
                    return true;
                }
            }

            if (type == InputType.Touch || type == InputType.Any)
            {
                for (var i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase != TouchPhase.Began) continue;
                    inputPos = t.position;
                    return true;
                }
            }
#endif
            
            inputPos = default;
            return false;
        }
    }
}