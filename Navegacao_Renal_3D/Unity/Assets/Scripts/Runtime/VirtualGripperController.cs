using UnityEngine;

namespace NavegacaoRenal
{
    public sealed class VirtualGripperController : MonoBehaviour
    {
        [SerializeField] private Transform leftJawPivot;
        [SerializeField] private Transform rightJawPivot;
        [SerializeField] private Transform leftJaw;
        [SerializeField] private Transform rightJaw;
        [SerializeField] private Transform shaft;
        [SerializeField] private Transform captureAnchor;
        [SerializeField] private float captureRadius = 0.018f;
        [SerializeField] private Vector3 leftOpenEuler = new Vector3(0f, -14f, 0f);
        [SerializeField] private Vector3 leftClosedEuler = new Vector3(0f, -3f, 0f);
        [SerializeField] private Vector3 rightOpenEuler = new Vector3(0f, 14f, 0f);
        [SerializeField] private Vector3 rightClosedEuler = new Vector3(0f, 3f, 0f);

        private float closure;

        public Transform CaptureAnchor => captureAnchor;
        public float CaptureRadius => captureRadius;
        public float Closure => closure;
        public Transform LeftJaw => leftJaw;
        public Transform RightJaw => rightJaw;
        public Transform Shaft => shaft;
        public bool IsConfigured => leftJawPivot != null && rightJawPivot != null && leftJaw != null &&
                                    rightJaw != null && shaft != null && captureAnchor != null;

        public void Configure(Transform leftPivot, Transform rightPivot, Transform stoneAnchor,
            Transform leftJawTransform, Transform rightJawTransform, Transform shaftTransform,
            float stoneCaptureRadius = 0.018f)
        {
            leftJawPivot = leftPivot;
            rightJawPivot = rightPivot;
            leftJaw = leftJawTransform;
            rightJaw = rightJawTransform;
            shaft = shaftTransform;
            captureAnchor = stoneAnchor;
            captureRadius = Mathf.Max(0.001f, stoneCaptureRadius);
            SetClosure(0f);
        }

        public void SetCaptureRadius(float value) => captureRadius = Mathf.Max(0.001f, value);

        public void SetClosure(float normalizedClosure)
        {
            closure = Mathf.Clamp01(normalizedClosure);
            if (leftJawPivot != null)
                leftJawPivot.localRotation = Quaternion.Euler(Vector3.Lerp(leftOpenEuler, leftClosedEuler, closure));
            if (rightJawPivot != null)
                rightJawPivot.localRotation = Quaternion.Euler(Vector3.Lerp(rightOpenEuler, rightClosedEuler, closure));
        }

        public void ResetGripper() => SetClosure(0f);

        public bool IsPoseOverlapping(Transform probe, Vector3 probePosition, Quaternion probeRotation,
            LayerMask collisionMask, float margin, out Vector3 contactPoint)
        {
            contactPoint = probePosition;
            if (!IsConfigured || probe == null || collisionMask.value == 0)
                return false;

            if (TryGetBox(leftJaw, probe, probePosition, probeRotation, margin,
                    out Vector3 leftCenter, out Vector3 leftExtents, out Quaternion leftRotation) &&
                TryFindBoxOverlap(leftCenter, leftExtents, leftRotation, collisionMask, out contactPoint))
                return true;

            if (TryGetBox(rightJaw, probe, probePosition, probeRotation, margin,
                    out Vector3 rightCenter, out Vector3 rightExtents, out Quaternion rightRotation) &&
                TryFindBoxOverlap(rightCenter, rightExtents, rightRotation, collisionMask, out contactPoint))
                return true;

            if (TryGetCapsule(shaft, probe, probePosition, probeRotation, margin,
                    out Vector3 pointA, out Vector3 pointB, out float radius) &&
                TryFindCapsuleOverlap(pointA, pointB, radius, collisionMask, out contactPoint))
                return true;

            return false;
        }

        public bool TrySweep(Transform probe, Vector3 probePosition, Quaternion probeRotation,
            Vector3 direction, float distance, LayerMask collisionMask, float margin, out RaycastHit nearestHit)
        {
            nearestHit = default;
            if (!IsConfigured || probe == null || collisionMask.value == 0 || distance <= Mathf.Epsilon)
                return false;

            bool blocked = false;
            float nearestDistance = float.PositiveInfinity;

            if (TryGetBox(leftJaw, probe, probePosition, probeRotation, margin,
                    out Vector3 leftCenter, out Vector3 leftExtents, out Quaternion leftRotation) &&
                Physics.BoxCast(leftCenter, leftExtents, direction, out RaycastHit leftHit, leftRotation,
                    distance, collisionMask, QueryTriggerInteraction.Ignore))
                ConsiderHit(leftHit, ref blocked, ref nearestDistance, ref nearestHit);

            if (TryGetBox(rightJaw, probe, probePosition, probeRotation, margin,
                    out Vector3 rightCenter, out Vector3 rightExtents, out Quaternion rightRotation) &&
                Physics.BoxCast(rightCenter, rightExtents, direction, out RaycastHit rightHit, rightRotation,
                    distance, collisionMask, QueryTriggerInteraction.Ignore))
                ConsiderHit(rightHit, ref blocked, ref nearestDistance, ref nearestHit);

            if (TryGetCapsule(shaft, probe, probePosition, probeRotation, margin,
                    out Vector3 pointA, out Vector3 pointB, out float radius) &&
                Physics.CapsuleCast(pointA, pointB, radius, direction, out RaycastHit shaftHit,
                    distance, collisionMask, QueryTriggerInteraction.Ignore))
                ConsiderHit(shaftHit, ref blocked, ref nearestDistance, ref nearestHit);

            return blocked;
        }

        private static void ConsiderHit(RaycastHit hit, ref bool blocked, ref float nearestDistance,
            ref RaycastHit nearestHit)
        {
            if (hit.distance >= nearestDistance)
                return;
            blocked = true;
            nearestDistance = hit.distance;
            nearestHit = hit;
        }

        private static bool TryGetBox(Transform shape, Transform probe, Vector3 probePosition,
            Quaternion probeRotation, float margin, out Vector3 center, out Vector3 halfExtents,
            out Quaternion rotation)
        {
            center = probePosition;
            halfExtents = Vector3.zero;
            rotation = probeRotation;
            MeshFilter filter = shape != null ? shape.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
                return false;

            Bounds bounds = filter.sharedMesh.bounds;
            Quaternion inverseProbeRotation = Quaternion.Inverse(probe.rotation);
            Vector3 worldCenter = shape.TransformPoint(bounds.center);
            Vector3 relativeCenter = inverseProbeRotation * (worldCenter - probe.position);
            center = probePosition + probeRotation * relativeCenter;
            rotation = probeRotation * inverseProbeRotation * shape.rotation;
            Vector3 scale = Abs(shape.lossyScale);
            halfExtents = Vector3.Scale(bounds.extents, scale) + Vector3.one * Mathf.Max(0f, margin);
            return true;
        }

        private static bool TryGetCapsule(Transform shape, Transform probe, Vector3 probePosition,
            Quaternion probeRotation, float margin, out Vector3 pointA, out Vector3 pointB, out float radius)
        {
            pointA = probePosition;
            pointB = probePosition;
            radius = 0f;
            MeshFilter filter = shape != null ? shape.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
                return false;

            Bounds bounds = filter.sharedMesh.bounds;
            Quaternion inverseProbeRotation = Quaternion.Inverse(probe.rotation);
            Vector3 worldCenter = shape.TransformPoint(bounds.center);
            Vector3 relativeCenter = inverseProbeRotation * (worldCenter - probe.position);
            Vector3 center = probePosition + probeRotation * relativeCenter;
            Quaternion shapeRotation = probeRotation * inverseProbeRotation * shape.rotation;
            Vector3 scale = Abs(shape.lossyScale);
            radius = Mathf.Max(bounds.extents.x * scale.x, bounds.extents.z * scale.z) + Mathf.Max(0f, margin);
            float halfHeight = bounds.extents.y * scale.y + Mathf.Max(0f, margin);
            Vector3 axisOffset = shapeRotation * Vector3.up * Mathf.Max(0f, halfHeight - radius);
            pointA = center + axisOffset;
            pointB = center - axisOffset;
            return true;
        }

        private static bool TryFindBoxOverlap(Vector3 center, Vector3 halfExtents, Quaternion rotation,
            LayerMask collisionMask, out Vector3 contactPoint)
        {
            Collider[] overlaps = Physics.OverlapBox(center, halfExtents, rotation, collisionMask,
                QueryTriggerInteraction.Ignore);
            return TryFindNearestPoint(overlaps, center, out contactPoint);
        }

        private static bool TryFindCapsuleOverlap(Vector3 pointA, Vector3 pointB, float radius,
            LayerMask collisionMask, out Vector3 contactPoint)
        {
            Vector3 center = (pointA + pointB) * 0.5f;
            Collider[] overlaps = Physics.OverlapCapsule(pointA, pointB, radius, collisionMask,
                QueryTriggerInteraction.Ignore);
            return TryFindNearestPoint(overlaps, center, out contactPoint);
        }

        private static bool TryFindNearestPoint(Collider[] overlaps, Vector3 center, out Vector3 nearest)
        {
            nearest = center;
            if (overlaps == null || overlaps.Length == 0)
                return false;

            nearest = overlaps[0].ClosestPoint(center);
            float nearestSquaredDistance = (nearest - center).sqrMagnitude;
            for (int index = 1; index < overlaps.Length; index++)
            {
                Vector3 candidate = overlaps[index].ClosestPoint(center);
                float squaredDistance = (candidate - center).sqrMagnitude;
                if (squaredDistance >= nearestSquaredDistance)
                    continue;
                nearest = candidate;
                nearestSquaredDistance = squaredDistance;
            }
            return true;
        }

        private static Vector3 Abs(Vector3 value) => new Vector3(
            Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
