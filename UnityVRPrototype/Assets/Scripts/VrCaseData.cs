using System;

[Serializable]
public class VrPoint
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class VrRouteMetrics
{
    public int path_points;
    public int exported_path_points;
    public int smoothed_points;
    public float path_length_voxels;
    public float smoothed_length_voxels;
    public int risk_points;
    public float final_error_voxels;
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
    public string case_name;
    public string clinical_notice;
    public int[] volume_shape_zyx;
    public VrPoint start;
    public VrPoint target;
    public VrPoint[] path_original;
    public VrPoint[] path_smoothed;
    public VrRouteMetrics metrics;
}
