import json
import tempfile
import unittest
from pathlib import Path

import numpy as np

from validate_vr_case import validate_case
from vr_case_pipeline import extract_stones, physical_length_mm, voxel_xyz_to_unity_m


class VrCasePipelineTests(unittest.TestCase):
    def test_voxel_to_unity_uses_spacing_and_y_up(self):
        result = voxel_xyz_to_unity_m((10, 20, 30), (0.5, 1.0, 2.0))
        self.assertEqual(result, (0.005, 0.06, 0.02))

    def test_physical_length_uses_anisotropic_spacing(self):
        length = physical_length_mm([(0, 0, 0), (2, 3, 4)], (0.5, 1.0, 2.0))
        self.assertAlmostEqual(length, np.linalg.norm([1.0, 3.0, 8.0]))

    def test_extract_stones_finds_3d_components(self):
        volume = np.zeros((8, 8, 8), dtype=np.uint8)
        volume[1:3, 1:3, 1:3] = 1
        volume[5:7, 5:7, 5:7] = 1
        stones = extract_stones(volume, (1.0, 1.0, 2.0), minimum_voxels=2, maximum_stones=8)
        self.assertEqual(len(stones), 2)
        self.assertEqual(stones[0]["voxel_count"], 8)
        self.assertEqual(stones[0]["volume_mm3"], 16.0)

    def test_validator_accepts_minimal_v2_case(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "anatomy.obj").write_text("v 0 0 0\n", encoding="utf-8")
            manifest = {
                "schema_version": 2,
                "case_id": "case_001",
                "files": {"anatomy": "anatomy.obj", "routes": "routes.json", "stones": ""},
                "route_count": 1,
            }
            route = {
                "start": {"x": 0.0, "y": 0.0, "z": 0.0},
                "target": {"x": 0.1, "y": 0.0, "z": 0.0},
                "path_original": [{"x": 0.0, "y": 0.0, "z": 0.0}, {"x": 0.1, "y": 0.0, "z": 0.0}],
                "path_smoothed": [{"x": 0.0, "y": 0.0, "z": 0.0}, {"x": 0.1, "y": 0.0, "z": 0.0}],
                "path_visual": [{"x": 0.0, "y": 0.0, "z": 0.0}, {"x": 0.1, "y": 0.0, "z": 0.0}],
                "metrics": {"outside_points": 0, "smoothed_length_mm": 100.0},
            }
            (root / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
            (root / "routes.json").write_text(
                json.dumps({"schema_version": 2, "case_id": "case_001", "routes": [route]}),
                encoding="utf-8",
            )
            self.assertEqual(validate_case(root), [])


if __name__ == "__main__":
    unittest.main()
