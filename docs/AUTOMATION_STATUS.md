# Automation status and external blockers

## Implemented

- Anatomy mask loading from PNG stack, NPY, or NIfTI.
- Optional DICOM-to-NIfTI conversion preserving geometry.
- Optional 3D stone-mask component extraction.
- Automatic stone centroid, volume, diameter, and planning-target generation.
- One validated A*/B-Spline route per detected stone.
- Physical units, extrapolation checks, and versioned case validation.
- Unity case catalog, Android-safe asynchronous loading, runtime OBJ parsing, multiple routes/cases, VR manipulation, menu, and build/install command.

## Requires data or a person

- The anatomy mask and entry point remain human-reviewed.
- The stone-segmentation repository contains a TensorFlow checkpoint (`my_checkpoint`, about 16.8 MB) and several notebook-defined architectures, but no notebook or script references that checkpoint and no metadata identifies its architecture, input normalization, training split, or decision threshold. Loading it against a guessed network would be technically possible but scientifically invalid. A reproducible inference command therefore requires confirmation from the original author or retraining/exporting a named model with a patient-level split.
- Automatic collecting-system/ureter segmentation cannot be trained or validated from the current repositories because matching labeled 3D volumes are absent.
- Real DICOM geometry can only be validated after authorized anonymized examinations and corresponding masks are provided.
- Clinical correctness requires expert review and, for formal participant research, guidance about ethics approval.
- Quest comfort and performance require a physical headset test.

## Recommended IC boundary

The defensible primary result is a semi-automatic, human-in-the-loop planning and training prototype. Full anatomical segmentation, intra-operative tracking, clinical decision support, and robotics remain future work.
