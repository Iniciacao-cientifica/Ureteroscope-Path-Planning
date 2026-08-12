"""Convert one anonymized DICOM series to NIfTI while preserving geometry.

The generated JSON deliberately contains geometry only.  Patient tags are never
copied.  The operator remains responsible for verifying that the input folder is
authorized and de-identified before running this tool.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def convert_series(input_dir, output_image, geometry_json, series_id=None):
    try:
        import SimpleITK as sitk
    except ImportError as exc:
        raise RuntimeError("Install requirements-medical-io.txt before importing DICOM.") from exc

    input_dir = Path(input_dir).resolve()
    output_image = Path(output_image).resolve()
    geometry_json = Path(geometry_json).resolve()
    series_ids = list(sitk.ImageSeriesReader.GetGDCMSeriesIDs(str(input_dir)) or [])
    if not series_ids:
        raise ValueError(f"No DICOM series found in {input_dir}.")
    if series_id is None:
        if len(series_ids) != 1:
            raise ValueError(
                "Multiple DICOM series were found. Re-run with --series-id using one of: "
                + ", ".join(series_ids)
            )
        series_id = series_ids[0]
    if series_id not in series_ids:
        raise ValueError(f"Series {series_id} was not found. Available: {', '.join(series_ids)}")

    file_names = sitk.ImageSeriesReader.GetGDCMSeriesFileNames(str(input_dir), series_id)
    reader = sitk.ImageSeriesReader()
    reader.SetFileNames(file_names)
    image = reader.Execute()
    output_image.parent.mkdir(parents=True, exist_ok=True)
    geometry_json.parent.mkdir(parents=True, exist_ok=True)
    sitk.WriteImage(image, str(output_image), useCompression=True)

    geometry = {
        "schema_version": 1,
        "source": "anonymized_dicom_series",
        "size_xyz": list(image.GetSize()),
        "spacing_mm_xyz": list(image.GetSpacing()),
        "origin_mm_xyz": list(image.GetOrigin()),
        "direction": list(image.GetDirection()),
        "pixel_type": image.GetPixelIDTypeAsString(),
        "slice_count": len(file_names),
    }
    with geometry_json.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(geometry, handle, ensure_ascii=False, indent=2)
        handle.write("\n")
    return geometry


def main():
    parser = argparse.ArgumentParser(description="Convert an anonymized DICOM series to NIfTI.")
    parser.add_argument("input_dir", type=Path)
    parser.add_argument("output_image", type=Path)
    parser.add_argument("--geometry-json", type=Path)
    parser.add_argument("--series-id")
    args = parser.parse_args()
    geometry_path = args.geometry_json or args.output_image.with_suffix(".geometry.json")
    geometry = convert_series(args.input_dir, args.output_image, geometry_path, args.series_id)
    print(f"NIfTI written to: {args.output_image}")
    print(f"Geometry written to: {geometry_path}")
    print(f"Size xyz: {geometry['size_xyz']} | spacing mm xyz: {geometry['spacing_mm_xyz']}")


if __name__ == "__main__":
    main()
