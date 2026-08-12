# VR case format v2

Each case is self-contained:

```text
case_001/
├── manifest.json
├── anatomy.obj
├── stones.obj       # optional
├── routes.json
├── route.csv
└── thumbnail.png    # optional
```

`catalog.json` sits one directory above the cases and references each `manifest.json`.

## Coordinate contract

- Source mask array order: `z,y,x`.
- User-facing planning coordinates: voxel `x,y,z`.
- Spacing: millimetres in `x,y,z`.
- Exported OBJ vertices and route points: Unity `x,y-up,z` in metres.
- Conversion: apply the DICOM/NIfTI direction matrix to `index * spacing`, then map relative patient `(x,z,y)` to Unity metres.

Unity must not apply another voxel scale to schema-v2 cases.

## Required manifest fields

- `schema_version`: `2`.
- `case_id`: anonymous letters/numbers/underscore/hyphen identifier.
- `display_name`: non-identifying label.
- `volume_shape_zyx` and `spacing_mm_xyz`.
- `spacing_assumed`: warns when real spacing was unavailable.
- `files.anatomy` and `files.routes`.
- `stones` and `route_count`.
- academic `clinical_notice`.

Absolute source paths and patient identifiers are deliberately excluded.

## Routes

`routes.json` contains an array. Each route records:

- route and stone identifiers;
- start, requested target, and navigable planning target;
- original, smoothed, and dense visual paths;
- physical length and final error in millimetres;
- wall-risk and outside-mask checks;
- curvature, torsion, and processing time.

The dense route is only for rendering. Metrics remain based on the validated planning route.
