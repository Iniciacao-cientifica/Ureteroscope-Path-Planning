Import generated anatomy meshes here.

Expected first mesh:

`../exports/murillo_sample_case/urinary_tract_unity.obj`

The repository's `prepare_unity_case.ps1` script copies the sample mesh here automatically.
After Unity imports the OBJ, use `Murillo VR > Setup Sample Scene`; the setup script assigns
the mesh to the `VrCaseLoader` component.
