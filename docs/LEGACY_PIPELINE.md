# Pipeline legado Python e UnityVRPrototype

Esta página preserva as instruções que anteriormente ocupavam o README raiz.
O pipeline continua versionado e funcional, mas não é a base do projeto ativo
`Navegacao_Renal_3D`.

## Semi-automatic Ureteroscopy Planning VR

Academic prototype that converts a navigable urinary-tract mask into validated
routes and an interactive Meta Quest case. The clinical workflow remains
human-in-the-loop: anatomy and entry point are reviewed before route generation.

> Research/training prototype only. It is not validated for diagnosis, patient
> care, intra-operative navigation, or robotic control.

## Current workflow

```text
Reviewed anatomy mask + entry point
                 |
Optional stone mask -> automatic 3D components and targets
                 |
             A* routes
                 |
Safe B-Spline + route validation
                 |
manifest.json + OBJ meshes + routes.json
                 |
Unity/OpenXR -> Meta Quest
```

The original classification project is intentionally not required. A stone
segmentation mask can be supplied when a validated model is available;
otherwise the operator provides a target.

## Quick start

```powershell
python -m pip install -r requirements-vr-export.txt
.\build_vr_case.cmd -MaskDir map -CaseId murillo_sample_case -SpacingMm "2,2,2"
```

This creates `cases/murillo_sample_case/`, validates it, and synchronizes it
with `UnityVRPrototype/Assets/StreamingAssets/Cases/`.

With a 3D stone mask in the same geometry:

```powershell
.\build_vr_case.cmd `
  -MaskDir "data\case_001\lumen" `
  -StoneMask "data\case_001\stones.nii.gz" `
  -CaseId "case_001" `
  -Start "253,355,20" `
  -SpacingMm "0.72,0.72,1.0"
```

The stone mask is split into 3D connected components. The pipeline calculates
centroid, volume, equivalent diameter, and a route for each retained component.
If a centroid is outside the navigable mask, the planning target is snapped to
its nearest navigable voxel and both locations remain recorded.

## Build and install on Quest

```powershell
.\build_vr_case.cmd -MaskDir map -CaseId murillo_sample_case -SpacingMm "2,2,2" -BuildApk
.\build_vr_case.cmd -MaskDir map -CaseId murillo_sample_case -SpacingMm "2,2,2" -Install
```

The APK is written to `UnityVRPrototype/Builds/UreteroscopyVR.apk`. See
[Quest setup and testing](QUEST_TESTING.md).

## Unity interaction

- Grip with one controller: move and rotate the model.
- Grip with both controllers: move and scale the model.
- `X`: anatomy visibility.
- `Y`: route visibility.
- `A`: start/stop route marker.
- `B`: next route/stone.
- Left thumbstick up/down: anatomy opacity.
- Left thumbstick click: reset model.
- Right thumbstick left/right: previous/next case.
- Controller ray + trigger: use the world-space menu.

Editor keyboard fallback: `O`, `P`, `F`, `N`, `C`, `R`, `+`, and `-`.

## Desktop training game

The legacy Unity project includes a monitor-based academic training mode with
an endoscopic camera, minimap, three difficulty levels, wall collisions, score,
and anonymous local CSV results. It works with keyboard/mouse or the ESP32-S3
physical probe controller.

Open `Assets/Scenes/UreteroscopyDesktopTraining.unity` inside
`UnityVRPrototype` or use `Murillo VR > Setup Desktop Training Scene`. See
[desktop training](DESKTOP_TRAINING.md) and
[physical controller assembly](PHYSICAL_CONTROLLER.md).

## DICOM/NIfTI

```powershell
python -m pip install -r requirements-medical-io.txt
python dicom_to_nifti.py "anonymized_dicom" "case_001_ct.nii.gz"
```

The converter preserves spacing, origin, and direction in a geometry JSON but
copies no patient tags. The input must already be authorized and de-identified.

## Validation

```powershell
python -m unittest discover -s tests -v
python validate_vr_case.py cases\murillo_sample_case
```

The v2 case interface is documented in [CASE_FORMAT.md](CASE_FORMAT.md).
Remaining dependencies are documented in
[AUTOMATION_STATUS.md](AUTOMATION_STATUS.md).

## Practical Windows note

Unity batch mode can fail when the project is inside OneDrive or a path
containing accents. The legacy APK script stages an ASCII-only temporary copy.
For manual Unity work, a clean checkout such as
`C:\UnityWork\UreteroscopyVR` remains preferable.
