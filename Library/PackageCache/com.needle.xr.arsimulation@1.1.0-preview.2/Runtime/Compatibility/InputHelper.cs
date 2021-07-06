using System;
using System.Reflection;
using Needle.XR.ARSimulation.Extensions;
using UnityEngine;
using Needle.XR.ARSimulation.Simulation;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;

#endif

// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable once CheckNamespace
namespace Needle.XR.ARSimulation.Compatibility
{
	/// We need to do some trickery to get
	///   a) reliable key presses in both Game View and Device Simulation View
	///   b) reliable touch simulation in Game View (Device Simulation does its own)
	///   c) right mouse data in Device Simulation View (since UnityEngine.Input does not work there)
	/// 
	/// Basic idea is: OnGUI events survive everything (Input simulation, hacky touch workarounds, event grabbers)
	/// So they also survive
	///   a) the Device Simulator, which grabs all UnityEngine.Input data but forward all OnGUI.Event data
	///   b) the Input.SimulateTouch call, which creates some real weird cases of duplicate Input data
	///      and leads to Input.mousePosition stopping to be reliable
	///      (we're grabbing Input mouse data and simulating a touch with this
	///       which in turn seems to get sent as mouse data, even with Input.simulateMouseWithTouches being false)
	public class InputHelper : MonoBehaviour
	{
		public bool SimulateTouch = true;

		public Vector2 MousePosition { get; private set; }
		public bool LeftButtonDown { get; private set; }
		public bool LeftButtonUp { get; private set; }
		public bool LeftButtonPressed { get; private set; }
		public bool LeftButtonDownThisFrame { get; private set; }
		public bool RightButtonDown { get; private set; }
		public bool RightButtonUp { get; private set; }
		public bool RightButtonPressed { get; private set; }
		public bool RightButtonPressedLastFrame { get; private set; }
		public float Vertical { get; private set; }
		public float Horizontal { get; private set; }
		public float UpDown { get; set; }

		public bool FocusKeyDown { get; private set; }


		public static void GetInput(out float horizontal, out float vertical, out float up, out bool inputActive, out bool inputWasActive)
		{
			horizontal = Instance.Horizontal;
			vertical = Instance.Vertical;
			up = Instance.UpDown;
			inputActive = Instance.RightButtonPressed;
			inputWasActive = Instance.RightButtonPressedLastFrame;
		}

		public static void NormalizeSpeed(ref Vector2 pixelDelta)
		{
			pixelDelta *= (float) 1024f / (float) Mathf.Max(Screen.width, Screen.height); // speed normalization
		}

		private static InputHelper _instance;

		public static InputHelper Instance
		{
			get
			{
				if (!_instance)
				{
					_instance = FindObjectOfType<InputHelper>();
					if (!_instance)
					{
						if (SceneSetup.TryGetARCamera(out var cam))
							_instance = cam.gameObject.AddComponent<InputHelper>();
						else
						{
							var go = new GameObject("Input Helper");
							_instance = go.AddComponent<InputHelper>();
						}
					}
				}

				return _instance;
			}
		}

		private void ResetInputStates()
		{
			LeftButtonDown = false;
			LeftButtonPressed = false;
			LeftButtonUp = false;
			LeftButtonDownThisFrame = false;
			leftButtonWasDown = false;
			RightButtonDown = false;
			RightButtonPressed = false;
			RightButtonUp = false;
		}

#if UNITY_EDITOR
		private EditorWindow previouslyFocusedWindow;
		private bool isValidWindow = true;
		private bool IsDeviceSimulatorWindow(Type type) => type != null && !string.IsNullOrEmpty(type.FullName) && type.FullName.Contains("DeviceSimulator");
#endif

		private static bool AllowKeyboardInput(ARSimulationLoaderSettings.DeviceInputSettings settings, bool preventIfAnythingIsSelected = false)
		{
			if (!EventSystem.current) return true;
			var ev = EventSystem.current;
			if (!ev.currentSelectedGameObject) return true;
			if (preventIfAnythingIsSelected && ev.currentSelectedGameObject) return false;
			var go = ev.currentSelectedGameObject;
			if (go.TryGetComponent(out InputField _)) return false;
#if UNITY_TEXTMESHPRO
			if (go.TryGetComponent(out TMP_InputField _)) return false;
#endif
			
			if (settings == null) return true;
			
			if (settings.DeactivateWithSubsystem)
			{
				if(!ARSimulationCameraSubsystem.IsRunning || !ARSimulationSessionSubsystem.IsRunning)  
					return false;
			}
			
			return true;
		}

		private static bool AllowMouseInput(ARSimulationLoaderSettings.DeviceInputSettings settings)
		{
			if (settings == null) return true;
			
			if (settings.DeactivateWithSubsystem)
			{
				if(!ARSimulationCameraSubsystem.IsRunning || !ARSimulationSessionSubsystem.IsRunning) 
					return false;
			}
			return true;
		}

		private void OnGUI()
		{
			if (Application.isPlaying == false) return;

			if (Event.current == null) return;
			var t = Event.current.type;
			if (t == EventType.Layout || t == EventType.Used || t == EventType.Repaint || t == EventType.Ignore) return;


#if UNITY_EDITOR
			if (EditorWindow.focusedWindow != previouslyFocusedWindow)
			{
				if (EditorWindow.focusedWindow)
				{
					var typeName = EditorWindow.focusedWindow.GetType().FullName;
					isValidWindow = string.IsNullOrEmpty(typeName) || typeName.Contains("GameView") ||
					                typeName.Contains("DeviceSimulator");
					// Debug.Log("focused window: " + typeName + " is valid?: " + isValidWindow);
				}
				else isValidWindow = false;

				OnEditorWindowFocusChanged();
				previouslyFocusedWindow = EditorWindow.focusedWindow;
#if !UNITY_DEVICESIMULATOR_2_2_2_OR_NEWER
				if (EditorWindow.focusedWindow)
				{
					// device simulator prior 2.2.2-preview emits left and right mouse events
					// if we detect usage of device simulator window with an old version 
					// we better warn that the user experience is flawed (left and right button both rotate the camera here)
					var type = EditorWindow.focusedWindow.GetType();
					if (IsDeviceSimulatorWindow(type))
						Debug.LogWarning(
							"You're using an old version of Device Simulator package. Please consider upgrading for a better experience. In versions prior to 2.2.2-preview we receive left and right button events and camera rotation is flawed. Please refer to https://github.com/needle-mirror/com.unity.device-simulator/commit/bab810647d5fa76fb89391d5fed08c12757a484b for more information.");
				}
#endif
			}

			if (!isValidWindow)
			{
				// Debug.Log("Receive Input from invalid window");
				return;
			}
#endif
			
			var settings = ARSimulationLoader.GetSettings();

			// Grab key presses - this works in both Game View and Device Simulator
			if (t == EventType.KeyDown || t == EventType.KeyUp)
			{
				var isDown = t == EventType.KeyDown;
				// if any UI is selected make sure keys are up and prevent input being stuck at key down
				if (settings.InputSettings.DisableInputWithSelectedUI && !AllowKeyboardInput(settings.InputSettings))
					isDown = false;
				var arrow = ARSimulationLoaderSettings.DeviceInputSettings.MovementKeys.ArrowKeys;
				var wasd = ARSimulationLoaderSettings.DeviceInputSettings.MovementKeys.WASD;
				var movementKeys = settings
					? settings.InputSettings.Keys
					: arrow | wasd;

				bool KeyIsAllowed(ARSimulationLoaderSettings.DeviceInputSettings.MovementKeys required)
				{
					if (!settings) return true;
					return (movementKeys & required) != 0;
				}

				switch (Event.current.keyCode)
				{
					// check arrow keys
					case KeyCode.UpArrow:
						Vertical = isDown && KeyIsAllowed(arrow) ? 1 : 0;
						break;
					case KeyCode.DownArrow:
						Vertical = isDown && KeyIsAllowed(arrow) ? -1 : 0;
						break;
					case KeyCode.LeftArrow:
						Horizontal = isDown && KeyIsAllowed(arrow) ? -1 : 0;
						break;
					case KeyCode.RightArrow:
						Horizontal = isDown && KeyIsAllowed(arrow) ? 1 : 0;
						break;
					// check wasd
					case KeyCode.W:
						Vertical = isDown && KeyIsAllowed(wasd) ? 1 : 0;
						break;
					case KeyCode.S:
						Vertical = isDown && KeyIsAllowed(wasd) ? -1 : 0;
						break;
					case KeyCode.A:
						Horizontal = isDown && KeyIsAllowed(wasd) ? -1 : 0;
						break;
					case KeyCode.D:
						Horizontal = isDown && KeyIsAllowed(wasd) ? 1 : 0;
						break;
					case KeyCode.Q:
						UpDown = isDown && KeyIsAllowed(wasd) ? -1 : 0;
						break;
					case KeyCode.E:
						UpDown = isDown && KeyIsAllowed(wasd) ? 1 : 0;
						break;
				}

				if (settings && settings.InputSettings != null)
				{
					var focusKey = settings.InputSettings.FocusKey;
					if (Event.current.keyCode == focusKey)
						FocusKeyDown = isDown;
				}
				else
					FocusKeyDown = false;
			}

			var prev = MousePosition;
			MousePosition = new Vector2(Event.current.mousePosition.x, Screen.height - Event.current.mousePosition.y);
			if (MousePosition.HasInfinity() || float.IsInfinity(MousePosition.magnitude))
				MousePosition = prev;
#if !UNITY_EDITOR_OSX
			// these are broken in editor on mac 2019.3.15
			if (Event.current.pointerType == PointerType.Mouse)
#endif
			{
				if (t == EventType.MouseDown || t == EventType.MouseUp || t == EventType.MouseDrag)
				{
					if (!AllowMouseInput(settings.InputSettings)) t = EventType.MouseUp;
					
					RightButtonPressedLastFrame = RightButtonPressed;

					// for DeviceSimulator usage -
					// intercept RMB and use for camera navigation
					if (Event.current.button == 1)
					{
						// when simulating touches we receive a couple of more events
						// touch events seem to emit right mouse button events 
						// so we ignore right button events if we have left button events
						// for example: we simulate a touch with the left mouse button
						// and then receive right mouse drag events
						switch (t)
						{
							case EventType.MouseDown:
								RightButtonDown = true;
								RightButtonUp = false;
								RightButtonPressed = false;
								break;
							case EventType.MouseDrag:
								// in device simulator we receive events for left and right mouse button
								// please refer to https://github.com/needle-tools/ar-simulation/issues/24#issuecomment-646573846
								// for more information about the following code
#if UNITY_DEVICESIMULATOR_2_2_2_OR_NEWER
                                RightButtonPressed = RightButtonDown && !LeftButtonPressed;
#else
								// left button is always pressed in 2.2.1-preview, we receive no mouse up events
								RightButtonPressed = RightButtonDown;
#endif
								break;
							case EventType.MouseUp:
								RightButtonUp = true;
								RightButtonDown = false;
								RightButtonPressed = false;
								break;
						}

						Event.current.Use();
					}
					// for GameView usage - intercept LMB and use for touch simulation
					else if (Event.current.button == 0)
					{
						// This is needed for touch simulation in the Game View - 
						// turns out Event.current.mousePosition still is the original value with SimulateTouch, 
						// while Input.mousePosition does weird things
						switch (t)
						{
							case EventType.MouseDown:
								LeftButtonDown = true;
								LeftButtonUp = false;
								LeftButtonPressed = false;
								break;
							case EventType.MouseDrag:
								LeftButtonPressed = LeftButtonDown;
								break;
							case EventType.MouseUp:
								LeftButtonUp = true;
								LeftButtonDown = false;
								LeftButtonPressed = false;
								break;
						}
					}
				}
			}
		}

#if UNITY_EDITOR
		private void OnEditorWindowFocusChanged()
		{
			ResetInputStates();
		}
#endif

		// dont warn about never used values
#pragma warning disable CS0414

		/// <summary>
		/// Cached mouse position to check for TouchPhase.Moved vs. TouchPhase.Stationary
		/// </summary>
		private Vector2 lastMousePosition;

		/// <summary>
		/// We have weird double-frame events (send data from one frame is received in the next as simulated input)
		/// so we need to wait a frame before we know if a pointer is actually stationary
		/// TODO: maybe we can also just call SimulateTouch every 2nd frame? Not sure if we get that to be reliable
		/// </summary>
		private int pointerStationaryCount = 0;

		// TODO: figure out why incrementing id does result in input not working at all anymore (sometimes even breaking editor input completely!!!)
		/// <summary>
		/// This is incremented to be similar in behaviour to actual devices
		/// </summary>
		private const int simulatedTouchId = 0;

		private TouchPhase simulatedTouchPhase = TouchPhase.Ended;
		private bool leftButtonWasDown;
		private bool previousSimulateTouchState;

#pragma warning restore CS0414

#if UNITY_EDITOR

		private void OnEnable()
		{
			// Cancel touches - not sure when these get stuck
			// for (var i = 0; i < 100; i++)
			{
				SimulateTouchInput(0, Vector2.zero, TouchPhase.Canceled);
			}
		}

		private void LateUpdate()
		{
			if (!Application.isPlaying) return;

			LeftButtonDownThisFrame = LeftButtonDown && !leftButtonWasDown;
			leftButtonWasDown = LeftButtonDown;

			if (SimulateTouch)
			{
				MockTouchInput();
			}
			else if (previousSimulateTouchState)
			{
				EndTouch();
			}

			previousSimulateTouchState = SimulateTouch;
		}
#endif

		private void MockTouchInput()
		{
#if UNITY_EDITOR
			Input.simulateMouseWithTouches = false;

			var focusedWindow = EditorWindow.focusedWindow;
			var allowUseTouch = focusedWindow && !IsDeviceSimulatorWindow(focusedWindow.GetType());

			if (allowUseTouch && (LeftButtonPressed || LeftButtonDown))
			{
				switch (simulatedTouchPhase)
				{
					// update touch
					case TouchPhase.Began:
					case TouchPhase.Moved:
					case TouchPhase.Stationary:
						if (lastMousePosition != MousePosition)
						{
							simulatedTouchPhase = TouchPhase.Moved;
							pointerStationaryCount = 0;
							SimulateTouchInput(simulatedTouchId, MousePosition, TouchPhase.Moved);
						}
						else if (pointerStationaryCount > 2)
						{
							simulatedTouchPhase = TouchPhase.Stationary;
							SimulateTouchInput(simulatedTouchId, MousePosition, TouchPhase.Stationary);
						}
						else
							pointerStationaryCount++;

						break;

					// begin touch
					// this is the previous touch state, we change it to began here
					// so its not actually a canceled event
					case TouchPhase.Ended:
					case TouchPhase.Canceled:
						simulatedTouchPhase = TouchPhase.Began;
						// simulatedTouchId++;
						// begin touch!
						SimulateTouchInput(simulatedTouchId, MousePosition, TouchPhase.Began);
						pointerStationaryCount = 0;
						break;
				}
			}
			else
			{
				EndTouch();
			}

			lastMousePosition = MousePosition;
#endif
		}

		private void EndTouch()
		{
#if UNITY_EDITOR
			if (simulatedTouchPhase != TouchPhase.Ended)
			{
				simulatedTouchPhase = TouchPhase.Ended;
				SimulateTouchInput(simulatedTouchId, MousePosition, TouchPhase.Ended);
				pointerStationaryCount = 0;
			}
#endif
		}

		private static MethodInfo _simulateTouchMethod;
		private static readonly object[] _simulateTouchArguments = new object[3];

		// Input.SimulateTouch is internal; needs reflection.
		// https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/InputLegacy/Input.bindings.cs#L333
		private void SimulateTouchInput(int id, Vector2 position, TouchPhase action)
		{
#if UNITY_EDITOR
			// check if another touch has the exact same position - in this case, we need to abort here
			for (var i = 0; i < Input.touchCount; i++)
			{
				var existingTouch = Input.GetTouch(i);
				if (existingTouch.fingerId == id) continue;
				if (Vector2.SqrMagnitude(existingTouch.position - position) < 0.1f)
				{
					return;
				}
			}

			if (_simulateTouchMethod == null)
			{
				_simulateTouchMethod = typeof(Input).GetMethod("SimulateTouch", (BindingFlags) (-1));
			}

			if (_simulateTouchMethod != null)
			{
				_simulateTouchArguments[0] = id % 10;
				_simulateTouchArguments[1] = position;
				_simulateTouchArguments[2] = action;
				// Debug.Log(Time.renderedFrameCount + ", " + _simulateTouchArguments[0] + ", " + action);
				_simulateTouchMethod.Invoke(null, _simulateTouchArguments);
			}
			else
			{
				Debug.LogWarning("Failed finding SimulateTouch method", this);
				SimulateTouch = false;
			}
#endif
		}
	}
}