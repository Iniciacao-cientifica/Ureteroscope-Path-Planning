using UnityEngine;
using UnityEngine.UI;

namespace NavegacaoRenal
{
    public sealed class KidneyMinimapPresenter : MonoBehaviour
    {
        [SerializeField] private KidneyGameManager gameManager;
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private GameObject minimapPanel;
        [SerializeField] private RawImage minimapImage;
        [SerializeField] private RectTransform marker;
        [SerializeField] private Text markerDistance;
        [SerializeField] private Transform realisticTarget;
        [SerializeField] private Transform explorationTarget;
        [SerializeField] private GameObject routeProxy;
        [SerializeField] private Transform activeKidneyCenter;
        [SerializeField] private float edgePadding = 0.08f;

        private bool markerClamped;

        public bool IsVisible => minimapPanel != null && minimapPanel.activeSelf;
        public bool IsMarkerClamped => markerClamped;
        public Camera MinimapCamera => minimapCamera;
        public RawImage MinimapImage => minimapImage;
        public GameObject RouteProxy => routeProxy;
        public Transform CurrentTarget => gameManager != null && gameManager.CurrentMode == KidneyGameMode.Exploration
            ? explorationTarget
            : realisticTarget;

        public void Configure(KidneyGameManager manager, Camera mapCamera, GameObject panel, RawImage image,
            RectTransform markerRect, Text distanceText, Transform realTarget, Transform freeTarget,
            GameObject mapRoute, Transform kidneyCenter)
        {
            gameManager = manager;
            minimapCamera = mapCamera;
            minimapPanel = panel;
            minimapImage = image;
            marker = markerRect;
            markerDistance = distanceText;
            realisticTarget = realTarget;
            explorationTarget = freeTarget;
            routeProxy = mapRoute;
            activeKidneyCenter = kidneyCenter;
        }

        private void LateUpdate() => RefreshMarker();

        public void SetVisible(bool visible)
        {
            if (minimapPanel != null) minimapPanel.SetActive(visible);
            if (minimapCamera != null) minimapCamera.gameObject.SetActive(visible);
            if (visible) RefreshMarker();
        }

        public void SetRouteVisible(bool visible)
        {
            if (routeProxy != null) routeProxy.SetActive(visible);
        }

        public void RefreshMarker()
        {
            Transform target = CurrentTarget;
            if (!IsVisible || target == null || minimapCamera == null || marker == null)
                return;

            Vector3 viewport = minimapCamera.WorldToViewportPoint(target.position);
            bool behind = viewport.z <= 0f;
            if (behind)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            markerClamped = behind || viewport.x < edgePadding || viewport.x > 1f - edgePadding ||
                            viewport.y < edgePadding || viewport.y > 1f - edgePadding;
            Vector2 unclamped = new Vector2(viewport.x, viewport.y);
            Vector2 clamped = new Vector2(
                Mathf.Clamp(unclamped.x, edgePadding, 1f - edgePadding),
                Mathf.Clamp(unclamped.y, edgePadding, 1f - edgePadding));
            marker.anchorMin = clamped;
            marker.anchorMax = clamped;
            marker.anchoredPosition = Vector2.zero;

            Vector3 forwardViewport = minimapCamera.WorldToViewportPoint(target.position + target.forward * 0.08f);
            Vector2 direction = markerClamped
                ? unclamped - new Vector2(0.5f, 0.5f)
                : new Vector2(forwardViewport.x - viewport.x, forwardViewport.y - viewport.y);
            if (behind) direction = -direction;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
            marker.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

            if (markerDistance != null)
            {
                markerDistance.gameObject.SetActive(markerClamped);
                if (markerClamped && activeKidneyCenter != null)
                    markerDistance.text = $"{Vector3.Distance(target.position, activeKidneyCenter.position):0.00} m";
            }
        }
    }
}
