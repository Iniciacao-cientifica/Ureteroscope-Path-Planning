using UnityEngine;

public interface ITrainingCourseView
{
    Transform ContentRoot { get; }
    GameObject SmoothedPathObject { get; }
    GameObject CurrentTargetObject { get; }
    float CurrentStoneDiameterMeters { get; }
    float RouteLengthMeters { get; }
    Color RouteColor { get; }
    Vector3 SampleRouteLocal(float distanceMeters);
}
