Copy the generated `vr_route_unity.json` file into this folder.

Example source after running the Python exporter:

`../exports/murillo_sample_case/vr_route_unity.json`

The repository's `prepare_unity_case.ps1` script copies the sample route here automatically.
After the file exists, use `Murillo VR > Setup Sample Scene` in Unity to create the Quest
sample scene and wire the loader.

The generated `urinary_tract.obj` should be imported into `Assets/Models/` and assigned to
the `Urinary Tract Mesh` field on the `VrCaseLoader` component.
