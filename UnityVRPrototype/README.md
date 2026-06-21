# Murillo Ureteroscopy VR Prototype

This Unity project is a first VR shell for the doctoral work: a pre-operative/training
visualizer for an automatically planned ureteroscopy path.

It does not run the AI pipeline on the headset. Python exports a lightweight case package,
and Unity displays the anatomy mesh, stone target, route, metrics, and camera-follow mode.

## 1. Prepare the Unity case

From the repository root:

```powershell
python -m pip install -r requirements-vr-export.txt
.\prepare_unity_case.ps1
```

This command generates the default export and copies the Unity-ready files into:

```text
Assets/StreamingAssets/vr_route_unity.json
Assets/Models/urinary_tract_unity.obj
```

The source export remains available at `exports/murillo_sample_case/`.

## 2. Open and test in Unity

1. Open `UnityVRPrototype` in Unity.
2. Wait for Unity to resolve the XR packages from `Packages/manifest.json` and import the OBJ mesh.
3. Use the Unity menu:

```text
Murillo VR > Setup Sample Scene
```

This creates or updates:
   - `Murillo XR Origin`
   - a head-tracked `Main Camera`
   - `VR Case Loader`
   - a directional light
   - `Assets/Scenes/MurilloQuestSample.unity`
   - the Build Settings scene list
4. Press Play for an editor smoke test.

The setup script also applies Quest-oriented Android player settings:

- package id: `br.edu.murillo.ureteroscopyvr`
- scripting backend: IL2CPP
- architecture: ARM64
- minimum Android SDK: 29
- graphics API: Vulkan

## 3. Enable VR for Meta Quest

The packages are already declared in `Packages/manifest.json`:

- `XR Plug-in Management`
- `OpenXR`
- `Unity OpenXR: Meta`
- `XR Interaction Toolkit`
- `Input System`

In Unity:

1. Open Project Settings > XR Plug-in Management.
2. Select the Android tab and enable OpenXR.
3. In the OpenXR Android settings, enable the Meta Quest feature group if Unity prompts for it.
4. Switch Build Target to Android.
5. Confirm `Assets/Scenes/MurilloQuestSample.unity` is enabled in Build Settings.
6. Build and run on the Quest headset.

## 4. Prototype controls

Quest controllers:

- `A` or `X`: toggle original A* path.
- `B` or `Y`: toggle B-Spline smoothed path.
- trigger: start/stop route-follow mode.
- thumbstick up/down: increase/decrease anatomy mesh opacity.

Editor keyboard fallback:

- `O`: toggle original A* path.
- `P`: toggle B-Spline smoothed path.
- `F`: follow the smoothed path.
- `+` / `-`: increase/decrease anatomy mesh opacity.

## 5. Notes

Unity batchmode may fail if this project is launched from a Windows path containing accented
characters. If command-line setup is needed, copy the project to an ASCII-only path such as
`C:\UnityWork\UnityVRPrototype` and run `MurilloVrSceneSetup.SetupSampleSceneBatch`.

## Clinical boundary

This is an academic/educational prototype for planning and training. It is not validated
for clinical diagnosis, intra-operative navigation, or patient care.
