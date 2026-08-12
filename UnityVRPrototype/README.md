# Ureteroscopy Planning VR — Unity project

Unity 6000.5/OpenXR application for loading versioned ureteroscopy cases from `Assets/StreamingAssets/Cases` on Desktop and Android.

## Prepare

From the repository root:

```powershell
.\build_vr_case.cmd -MaskDir map -CaseId murillo_sample_case -SpacingMm "2,2,2"
```

Open this project, wait for package resolution, and run:

```text
Murillo VR > Setup Sample Scene
Murillo VR > Configure OpenXR Android
Murillo VR > Validate Project
```

The setup creates a real XR Origin hierarchy, tracked head/controllers, controller rays, a world-space menu, the asynchronous case loader, and the build scene.

## Android requirements

Use Unity Hub to add the following modules to Unity `6000.5.0f1`:

- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

The automated build configures IL2CPP, ARM64, Vulkan, minimum API 29, OpenXR for Android, Oculus Touch, and Meta Quest Touch Plus profiles.

## Runtime design

- JSON and OBJ files are loaded with `UnityWebRequest`, including Android `jar:` StreamingAssets paths.
- OBJ is parsed at runtime, so cases are not tied to scene asset GUIDs.
- Route points and meshes are already exported in Unity metres with Y up.
- The camera remains head-tracked while the route marker moves, reducing artificial camera motion.
- One/two-hand grips manipulate the case root.
- Catalog and route changes dispose the previous case objects before loading the next.

## Build

Use the repository command rather than building manually:

```powershell
.\build_vr_case.cmd -MaskDir map -CaseId murillo_sample_case -SpacingMm "2,2,2" -BuildApk
```

The build fails early when the catalog, case files, OpenXR loader, Android module, or OBJ parser is unavailable.

## Clinical boundary

Academic/educational prototype only. Not validated for clinical diagnosis, intra-operative navigation, treatment, or patient care.
