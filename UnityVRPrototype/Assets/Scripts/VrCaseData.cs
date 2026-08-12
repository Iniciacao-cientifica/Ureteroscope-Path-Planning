using System;

[Serializable]
public class VrPoint
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class VrCaseCatalog
{
    public int schema_version;
    public VrCaseCatalogEntry[] cases;
}

[Serializable]
public class VrCaseCatalogEntry
{
    public string case_id;
    public string display_name;
    public string manifest_file;
}

[Serializable]
public class VrCaseFiles
{
    public string anatomy;
    public string stones;
    public string routes;
    public string thumbnail;
}

[Serializable]
public class VrStoneData
{
    public string stone_id;
    public VrPoint centroid;
    public int voxel_count;
    public float volume_mm3;
    public float equivalent_diameter_mm;
    public string source;
}

[Serializable]
public class VrCaseManifest
{
    public int schema_version;
    public string case_id;
    public string display_name;
    public string generated_at;
    public string clinical_notice;
    public int[] volume_shape_zyx;
    public VrPoint spacing_mm_xyz;
    public bool spacing_assumed;
    public VrCaseFiles files;
    public VrStoneData[] stones;
    public int route_count;
}

[Serializable]
public class VrRouteMetrics
{
    public int path_points;
    public int exported_path_points;
    public int smoothed_points;
    public int visual_points;
    public float path_length_voxels;
    public float smoothed_length_voxels;
    public float path_length_mm;
    public float smoothed_length_mm;
    public int risk_points;
    public float final_error_voxels;
    public float final_error_mm;
    public float processing_seconds;
    public float curvature_mean;
    public float curvature_max;
    public float torsion_mean;
    public float torsion_max;
    public int outside_points;
    public float outside_percent;
    public int[] outside_indices;
    public float outside_max_distance;
    public float outside_mean_distance;
}

[Serializable]
public class VrRouteData
{
    public int schema_version;
    public string route_id;
    public string stone_id;
    public string coordinate_space;
    public string case_name;
    public string clinical_notice;
    public int[] volume_shape_zyx;
    public VrPoint start;
    public VrPoint target;
    public VrPoint requested_target;
    public VrPoint[] path_original;
    public VrPoint[] path_smoothed;
    public VrPoint[] path_visual;
    public VrRouteMetrics metrics;
}

[Serializable]
public class VrRoutesDocument
{
    public int schema_version;
    public string case_id;
    public string coordinate_space;
    public VrRouteData[] routes;
}
