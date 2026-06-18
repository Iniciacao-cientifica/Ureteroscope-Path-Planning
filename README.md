# Ureteroscope-Path-Planning
 Path Planning of an flexible ureteroscope using search techniques, filters, and meta-heuristic optimization

## VR prototype export

This repository now includes an offline export pipeline for a Unity/OpenXR VR prototype.
The intended use is academic planning/training: Python computes the ureteroscopy route and
Unity displays the anatomy, target stone, route, metrics, and camera-follow simulation.

Generate the default sample case:

```powershell
python -m pip install -r requirements-vr-export.txt
python vr_export_pipeline.py
```

Outputs are written to `exports/murillo_sample_case/`:

- `urinary_tract.obj`: mesh generated from the binary mask stack in `map/`.
- `vr_route_unity.json`: Unity-readable route, start/target points, and metrics.
- `route.csv`: A* and B-Spline route points for analysis.
- `case_manifest.json`: export metadata and clinical/coordinate notes.

Open `UnityVRPrototype/` in Unity to load the exported case into a Meta Quest/OpenXR scene.

This is not a validated clinical tool and must not be used for diagnosis, treatment, or
intra-operative navigation.
