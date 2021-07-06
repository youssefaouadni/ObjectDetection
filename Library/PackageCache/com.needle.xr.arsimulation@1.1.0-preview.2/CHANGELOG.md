# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).


## [1.1.0-preview.2] - 2020-12-23
### Fixed
- Automatically adding basic ``ARPlane`` GameObject with the necessary components at runtime if none is assigned to the ``ARPlaneManager``. These are necessary to generate colliders for raycasting/interacting with planes

## [1.1.0-preview.1] - 2020-12-21
### Fixed
- Removed false error logs in CameraSubsystem and ProbeSubsystem registration
- Fixed automatic setup in cases of disabled domain reloads by changing the attribute from ``OnInitializeOnLoad`` to ``RuntimeInitializeOnLoadMethod``

## [1.1.0-preview] - 2020-12-13
### Added
- Initial support for CPU Image API. Conversion Params are not fully supported yet.

### Fixed
- Fixed CameraBackgroundRendererFeature support for URP 10.1 or newer
- Fix ShadowCatcher ``global/local`` shader warning
- CameraBackgroundRendererFeature should only warn if actually used
- Support for ARFoundation 4.1.0 new ARAnchor API

### Changed
- Moved ARMeshing sample scenes in ``Getting Started`` sample


## [1.0.5] - 2020-11-17
### Fixed
- The camera should not move anymore if any UI Text InputFields are selected (can be disabled in settings)
- The camera should not move if session subsystem or camera subsystem are disabled (can be disabled in settings)
- Clamp movement delta times in case of editor freezes

## [1.0.5-preview.2] - 2020-10-09
### Fixed
- SimulatedARTrackedImage inspector should update options for runtime added images

## [1.0.5-preview.1] - 2020-10-05
### Fixed
- Issue with creating SimulatedComponents in Editor even if XR Plugin was disabled
- Issue on osx with input events leaking in scene

### Changed
- Removed ``Attatching Anchors is not supported yet`` debug log
- SimulateTouch restricted to GameView window

### Added
- Movement option to allow using arrow keys for moving around (see ``ProjectSettings/AR Simulation/Input Settings``)
- Movement option to allow disabling right mouse button for moving around (see ``ProjectSettings/AR Simulation/Input Settings``)

## [1.0.4] - 2020-09-29
### Fixed
- NullReferenceException caused by SimulatedARTrackedImage component
- Issue with using URP with SimulatedARTrackedImage Gizmos set to render as mesh/image
- Startup placement of SimulatedARTrackedImage in default empty scene

## [1.0.4-preview.2] - 2020-09-27
### Added
- Initial support for Object Tracking

## [1.0.3] - 2020-08-27
### Fixed
- NullReferenceException in InputSettings
- PlacementHelper throws Exception if no ARSessionOrigin found in scene

## [1.0.4-preview.1] - 2020-09-21
### Added
- Initial support for ARMeshManager on OSX

### Fixed
- Fixed NullReferenceException in InputHelper when querying InputSettings that have not been created after updating the package

## [1.0.4-preview] - 2020-09-18
### Added
- Initial support for ARMeshManager on Windows!

## [1.0.3-preview.1] - 2020-08-24
### Added
- Quad Rendering support for SimulatedARTrackedImage: assign a material to the ``InstanceMaterial`` field in the ``SimulatedARTrackedImage`` component to have ARSimulation create an instance with the image and a quad mesh at runtime
- Focus Mode Feature ([#33])(https://github.com/needle-tools/ar-simulation/issues/33). Press ``F`` to focus on a point at mouse position at runtime (Configuration for key, focus target and focus distance can be found in ``ARSimulationSettings/Input Settings``

### Fixed
- Samples issue on iOS where assigned image in ``ReferenceImageLibary`` was missing size setting which caused the build to stop ([#34])(https://github.com/needle-tools/ar-simulation/issues/34)
- Added workaround for AnchorManager throwing an exception when exiting playmode due to AnchorSubsystem being stopped (case #1268386)

## [1.0.3-preview] - 2020-08-05
### Fixed
- Touch input stops working ([#31](https://github.com/needle-tools/ar-simulation/issues/31))

## [1.0.2] - 2020-07-27
### Fixed
- Image Tracking Pose is not session relative anymore
- Added Editor #ifdef to plane- and pointcloud generators to remove cg alloc in builds
- Fixed issue with ``MakeContentAppear``
- Improved New Input System experience: first frame when clicking in game window is ignored and thus we remove potential huge mouse deltas
- Fixed issue with New Input System where ``device was updated this frame`` was false every frame

## [1.0.1] - 2020-07-01
### Fixed
- Oopsi, how embarrassing.

## [1.0.0] - 2020-07-01
### Added
- Getting Started window buttons for ``Settings`` and ``Asset Store``
- Getting Started window with more :)
- ``Simulated AR Image``
  - added support to change image at runtime 
  - added custom popup for currently available images referenced by ``AR Tracked Image Manager``
  - added warning if assigned image is not used by any available library or library is missing or empty
  - changed gizmo color if assigned image is missing in library
- Added circle icon to ``Editor Only`` GameObjects
- Added random rotation and scale to sample component ``InstantiateAtRaycastHitExample``

### Fixed
- Getting Started window ``Import Samples`` should now correctly update disabled scope. Previously installing samples didn't enable the other "Open Sample XYZ" buttons.
- Fixed camera movement on Mac: ``Event.current.pointerType`` is broken in 2019.3.15 (case 1259249)
- Getting started window placement on 4k monitors
- Tagged sample environment with ``Editor Only``
- Fixed Getting Started window center position only on first open to avoid flicker
- URP shadow catcher shader fixed (caused by failed rename of local shader feature when changing package name)
- Compiler error with old ``XR Management`` package prior 3.2.10
- Getting Started window now opens Package Manager once, to fix issue with not finding samples

### Changed
- Getting Started window pings ``Documentation.pdf``
- Adding package into project that has never set up ``XR Plug-in Management`` will now open ``Project Window/XR Plug-in Management`` window once. That will trigger initialization and in turn will enable AR Simulation to register its loader ([#28](https://github.com/needle-tools/ar-simulation/issues/28))
- Instantiate sample component now uses a list of prefabs to random select from

## [1.0.0-preview.7] - 2020-06-27
### Added
- `Documentation.pdf` added to Getting Started Sample
- Samples now get pinged after installation to make them easier to discover

### Fixed
- Touch simulation issues with double touches ([#21](https://github.com/needle-tools/ar-simulation/issues/21))

## [1.0.0-preview.6] - 2020-06-23
### Added
- Global settings for movement and rotation sensibility in AR Simulation Loader Settings, see XR Plug-in Management.
- Default ``Walk`` movement mode similar to how Mars movement works

### Fixed
- Automatic plugin enabling should work correctly now
- Minor Simulated AR Pointcloud fixes
- Fixed issue with zero look vector when game view was not visible

### Changed
- Renamed input devices to match naming conventions.
- Moved most of the example assets and prefabs in resources to editor assembly to avoid including them in builds if not used

## [1.0.0-preview.5] - 2020-06-20
### Added
- Support for AR Foundation 4.0.2 (latest)
- Getting Started Window
- Automatic AR Simulation XR loader activation in XR Plug-in Management

### Fixed
- Fixed NaN errors that happened occasionally by not handling ``Ignore`` events [[issue 26]](https://github.com/needle-tools/ar-simulation/issues/26)
- Fixed issue with Device Simulator prior version 2.2.2-preview where left button pressed getting stuck lead to not being able to move around or rotate anymore. [[issue 24]](https://github.com/needle-tools/ar-simulation/issues/24)

## [1.0.0-preview.4] - 2020-06-15
### Added
- AR Simulation now automatically adds its loader to XR Plug-in Management on first installation. This removes the need for users to enable it manually.

### Fixed
- Planes being added and updated in the same frame are now being updated correctly

## [1.0.0-preview.3] - 2020-06-13

### Added
- support for camera background rendering
  - either add an ``ARSimulatedEnvironmentManager`` component to a GameObject in your scene. You can reference a prefab, a gameobject in your scene, or even a complete scene asset as "simulated background".
  - or add an ``ARSimulatedEnvironment`` component to the root gameObject of the scene and set ``IsActive`` to true. The component will assign itself to an ``ARSimulatedEnvironmentManager``. This way it's easy to toggle multiple different environments (currently only at edit time).
  - if you're using URP, when you press play for the first time a RenderFeature will be added to your ForwardRenderer for background rendering. This won't do anything in a build.
- flat assets sample for better in-editor simulation
- ``Tools/ARSimulation/Setup Scene`` for quickly setting up an empty scene for AR
- implicit setup on playmode enter if the scene has a ``ARSession`` component  
- support for device simulator
- new ``SimulatedARTrackedImage`` to support image tracking feature
- print error when user sets ``Use New Input System`` in player settings without having the package installed

### Changed
- changed project name to ARSimulation
- support for all input systems
- basic one touch support in-editor
- improved sample quality
- ``SimulatedARPointCloud`` has mode field for supporting spherical and planar pointclouds when using random points

### Known Issues
- when using URP, background rendering only works after pressing Play one time [#18](https://github.com/needle-tools/ar-simulation/issues/18)
- Device Simulator has no multitouch (Unity issue)
- Simulated Environment probe are currently not supported due to issue with copying a cubemap texture on Dx11 (Unity issue [1215635](https://issuetracker.unity3d.com/issues/cubemap-dot-createexternaltexture-does-not-produce-correct-cubemap-when-using-getnativetextureptr-from-an-existing-one))

## [0.0.3-preview] - 2020-02-10
- remove unnecessary files (arimg exe / Docs which were for ARCore)
- add npmignore for unsupported~ folder

## [0.0.2-preview] - 2020-02-10
- fixed dependencies
- fixed usings (Rider!!)

## [0.0.1-preview] - 2020-02-10

- Initial release for ar foundation editor testing/development
- Support includes AR plane generation, trackables, pointclouds, anchors, handheld camera 
