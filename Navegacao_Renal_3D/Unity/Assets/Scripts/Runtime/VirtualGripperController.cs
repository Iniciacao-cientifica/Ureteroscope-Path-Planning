using UnityEngine;

namespace NavegacaoRenal
{
    public sealed class VirtualGripperController : MonoBehaviour
    {
        [SerializeField] private Transform leftJawPivot;
        [SerializeField] private Transform rightJawPivot;
        [SerializeField] private Transform captureAnchor;
        [SerializeField] private Vector3 leftOpenEuler = new Vector3(0f, -14f, 0f);
        [SerializeField] private Vector3 leftClosedEuler = new Vector3(0f, -3f, 0f);
        [SerializeField] private Vector3 rightOpenEuler = new Vector3(0f, 14f, 0f);
        [SerializeField] private Vector3 rightClosedEuler = new Vector3(0f, 3f, 0f);

        private float closure;

        public Transform CaptureAnchor => captureAnchor;
        public float Closure => closure;
        public bool IsConfigured => leftJawPivot != null && rightJawPivot != null && captureAnchor != null;

        public void Configure(Transform leftPivot, Transform rightPivot, Transform stoneAnchor)
        {
            leftJawPivot = leftPivot;
            rightJawPivot = rightPivot;
            captureAnchor = stoneAnchor;
            SetClosure(0f);
        }

        public void SetClosure(float normalizedClosure)
        {
            closure = Mathf.Clamp01(normalizedClosure);
            if (leftJawPivot != null)
                leftJawPivot.localRotation = Quaternion.Euler(Vector3.Lerp(leftOpenEuler, leftClosedEuler, closure));
            if (rightJawPivot != null)
                rightJawPivot.localRotation = Quaternion.Euler(Vector3.Lerp(rightOpenEuler, rightClosedEuler, closure));
        }

        public void ResetGripper() => SetClosure(0f);
    }
}
