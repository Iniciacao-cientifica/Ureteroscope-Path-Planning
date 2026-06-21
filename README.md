# Ureteroscope-Path-Planning
 Path Planning of an flexible ureteroscope using search techniques, filters, and meta-heuristic optimization

## Recommended local demo: PyVista

The original research workflow is Python-first: it builds a 3D urinary tract model,
computes the A* route, smooths the path, and opens an interactive PyVista viewer.
This is the fastest way to demonstrate the project before turning it into a full VR app.

Install the viewer dependencies:

```powershell
python -m pip install -r requirements-pyvista.txt
```

Run a computation-only test:

```powershell
python -B pyvista_demo.py --no-view
```

Run the interactive 3D viewer:

```powershell
python -B pyvista_demo.py
```

PyVista controls:

- `A`: show/hide the original A* path.
- `Z`: show/hide the smoothed path.
- `D`: show/hide curve points.
- `C`: show/hide extrapolation check.
- `M`: animate the camera along the route.
- `R`: reset camera.
- `S`: save screenshot.

Generated files are written to `outputs/pyvista_demo/`, including route CSV, summary JSON,
and an OBJ surface export.

## VR prototype export

This repository also includes an offline export pipeline for a Unity/OpenXR VR prototype.
The intended use is academic planning/training: Python computes the ureteroscopy route and
Unity displays the anatomy, target stone, route, metrics, and camera-follow simulation.

Generate the default sample case:

```powershell
python -m pip install -r requirements-vr-export.txt
.\prepare_unity_case.ps1
```

Outputs are written to `exports/murillo_sample_case/` and copied into `UnityVRPrototype/Assets/`:

- `urinary_tract_unity.obj`: mesh generated from the binary mask stack in `map/`.
- `vr_route_unity.json`: Unity-readable route, start/target points, and metrics.
- `route.csv`: A* and B-Spline route points for analysis.
- `case_manifest.json`: export metadata and clinical/coordinate notes.

Open `UnityVRPrototype/` in Unity and wait for package/asset import. The Unity project now
declares the Quest/OpenXR packages in `Packages/manifest.json`; use
`Murillo VR > Setup Sample Scene` to create the `Murillo XR Origin`, tracked camera,
case loader, sample scene, and Build Settings entry. Then enable OpenXR for Android in
Project Settings > XR Plug-in Management, switch the build target to Android, and build
to the Meta Quest.

Quest controller controls:

- `A` or `X`: show/hide the original A* path.
- `B` or `Y`: show/hide the smoothed path.
- trigger: start/stop camera-follow mode.
- thumbstick up/down: adjust anatomy mesh opacity.

This is not a validated clinical tool and must not be used for diagnosis, treatment, or
intra-operative navigation.
