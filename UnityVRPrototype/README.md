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
2. Wait for Unity to import the OBJ mesh.
3. The editor setup script should automatically create:
   - `VR Case Loader`
   - a basic camera
   - a directional light
4. Press Play.

If the scene is not created automatically, use the Unity menu:

```text
Murillo VR > Setup Sample Scene
```

## 3. Enable VR for Meta Quest

1. Install or confirm `XR Plug-in Management`, `OpenXR`, and `XR Interaction Toolkit`.
2. In Project Settings, enable OpenXR for Android.
3. Enable the Meta Quest/OpenXR feature group when Unity prompts for it.
4. Switch Build Target to Android.
5. Build and run on the Quest headset.

## 4. Prototype controls

- `O`: toggle original A* path.
- `P`: toggle B-Spline smoothed path.
- `F`: follow the smoothed path with the assigned camera rig.
- `+` / `-`: increase or decrease anatomy mesh opacity.

## Clinical boundary

This is an academic/educational prototype for planning and training. It is not validated
for clinical diagnosis, intra-operative navigation, or patient care.
