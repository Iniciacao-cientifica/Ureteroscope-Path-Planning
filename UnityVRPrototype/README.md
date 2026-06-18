# Murillo Ureteroscopy VR Prototype

This Unity project is a first VR shell for the doctoral work: a pre-operative/training
visualizer for an automatically planned ureteroscopy path.

It does not run the AI pipeline on the headset. Python exports a lightweight case package,
and Unity displays the anatomy mesh, stone target, route, metrics, and camera-follow mode.

## 1. Generate a VR case from Python

From the repository root:

```powershell
python -m pip install -r requirements-vr-export.txt
python vr_export_pipeline.py
```

The default export is created at:

```text
exports/murillo_sample_case/
```

Important files:

- `urinary_tract.obj`: 3D anatomy mesh generated from the binary mask stack.
- `vr_route_unity.json`: route, points, metrics, and clinical notice for Unity.
- `route.csv`: route points for analysis or spreadsheet inspection.
- `case_manifest.json`: complete export metadata.

## 2. Bring the exported case into Unity

1. Open `UnityVRPrototype` in Unity 6 or a recent Unity version with OpenXR support.
2. Copy `exports/murillo_sample_case/vr_route_unity.json` to `Assets/StreamingAssets/`.
3. Copy `exports/murillo_sample_case/urinary_tract.obj` to `Assets/Models/`.
4. Create an empty GameObject named `VR Case Loader`.
5. Add the `VrCaseLoader` component.
6. Drag the imported urinary tract mesh object into `Urinary Tract Mesh`.
7. Assign your XR Origin or camera rig to `Camera Rig`.

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
