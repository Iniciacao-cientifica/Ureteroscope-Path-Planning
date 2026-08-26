using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace NavegacaoRenal
{
    public sealed class KidneyGameManager : MonoBehaviour
    {
        [Header("Modes")]
        [SerializeField] private GameObject realisticRig;
        [SerializeField] private GameObject explorationRig;
        [SerializeField] private Transform probe;
        [SerializeField] private Transform startAnchor;
        [SerializeField] private Transform targetStone;
        [SerializeField] private GameObject routeGuide;
        [SerializeField] private GameObject minimapCamera;
        [SerializeField] private KidneyGameMode initialMode = KidneyGameMode.Realistic;

        [Header("Easy level")]
        [SerializeField] private int maximumWallContacts = 5;
        [SerializeField] private float captureDistance = 0.10f;

        private KidneyGameMode currentMode;
        private int wallContacts;
        private bool won;
        private bool lost;
        private bool paused;
        private float startedAt;
        private float redFlashUntil;

        public bool CanNavigate => currentMode == KidneyGameMode.Realistic && !won && !lost && !paused;

        public void Configure(GameObject realRig, GameObject freeRig, Transform probeTransform, Transform start, Transform stone, GameObject route, GameObject minimap)
        {
            realisticRig = realRig;
            explorationRig = freeRig;
            probe = probeTransform;
            startAnchor = start;
            targetStone = stone;
            routeGuide = route;
            minimapCamera = minimap;
        }

        private void Start()
        {
            Application.targetFrameRate = 120;
            startedAt = Time.unscaledTime;
            if (routeGuide != null) routeGuide.SetActive(false);
            SetMode(initialMode, true);
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.f1Key.wasPressedThisFrame) SetMode(KidneyGameMode.Realistic);
            if (Keyboard.current.f2Key.wasPressedThisFrame) SetMode(KidneyGameMode.Exploration);
            if (Keyboard.current.pKey.wasPressedThisFrame) paused = !paused;
            if (Keyboard.current.rKey.wasPressedThisFrame) ResetAttempt();
            if (Keyboard.current.tKey.wasPressedThisFrame && routeGuide != null) routeGuide.SetActive(!routeGuide.activeSelf);
            if (Keyboard.current.mKey.wasPressedThisFrame && minimapCamera != null) minimapCamera.SetActive(!minimapCamera.activeSelf);

            if (CanNavigate && Keyboard.current.spaceKey.wasPressedThisFrame && targetStone != null && probe != null)
            {
                if (Vector3.Distance(probe.position, targetStone.position) <= captureDistance)
                    won = true;
            }
        }

        public void SetMode(KidneyGameMode mode, bool force = false)
        {
            if (!force && currentMode == mode)
                return;

            currentMode = mode;
            if (realisticRig != null) realisticRig.SetActive(mode == KidneyGameMode.Realistic);
            if (explorationRig != null) explorationRig.SetActive(mode == KidneyGameMode.Exploration);
            if (mode == KidneyGameMode.Realistic) ResetProbePosition();
        }

        public void ReportWallContact(Vector3 point)
        {
            if (!CanNavigate)
                return;

            wallContacts++;
            redFlashUntil = Time.unscaledTime + 0.4f;
            if (wallContacts >= maximumWallContacts)
                lost = true;
        }

        private void ResetAttempt()
        {
            wallContacts = 0;
            won = false;
            lost = false;
            paused = false;
            startedAt = Time.unscaledTime;
            ResetProbePosition();
        }

        private void ResetProbePosition()
        {
            if (probe == null || startAnchor == null)
                return;

            CharacterController controller = probe.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            probe.SetPositionAndRotation(startAnchor.position, startAnchor.rotation);
            if (controller != null) controller.enabled = true;
        }

        private void OnGUI()
        {
            GUIStyle box = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft };
            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };

            GUI.Box(new Rect(18, 18, 375, 225), GUIContent.none, box);
            GUI.Label(new Rect(34, 28, 340, 30), "Navegacao Renal 3D - Facil", title);
            GUI.Label(new Rect(34, 62, 340, 25), currentMode == KidneyGameMode.Realistic ? "Modo: Realista" : "Modo: Exploracao livre");
            GUI.Label(new Rect(34, 88, 340, 25), $"Tempo: {Time.unscaledTime - startedAt:0.0}s   Toques: {wallContacts}/{maximumWallContacts}");

            if (GUI.Button(new Rect(34, 120, 155, 32), "F1 - Realista")) SetMode(KidneyGameMode.Realistic);
            if (GUI.Button(new Rect(205, 120, 170, 32), "F2 - Exploracao")) SetMode(KidneyGameMode.Exploration);
            GUI.Label(new Rect(34, 160, 340, 25), currentMode == KidneyGameMode.Realistic
                ? "Mouse: direcao | W/S: mover | Q/E: girar | Espaco: pegar"
                : "Botao direito + mouse | WASD/QE | Shift: acelerar");
            if (GUI.Button(new Rect(34, 192, 155, 30), "T - Mostrar rota"))
                if (routeGuide != null) routeGuide.SetActive(!routeGuide.activeSelf);
            if (GUI.Button(new Rect(205, 192, 170, 30), "M - Minimap"))
                if (minimapCamera != null) minimapCamera.SetActive(!minimapCamera.activeSelf);

            if (won || lost || paused)
            {
                string message = won ? "PEDRA CAPTURADA!" : lost ? "LIMITE DE TOQUES ATINGIDO" : "PAUSADO";
                GUI.Box(new Rect(Screen.width * 0.5f - 180, Screen.height * 0.5f - 55, 360, 110), message);
                if (GUI.Button(new Rect(Screen.width * 0.5f - 90, Screen.height * 0.5f, 180, 34), "R - Reiniciar")) ResetAttempt();
            }

            if (Time.unscaledTime < redFlashUntil)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.05f, 0.05f, 0.72f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, 12), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, Screen.height - 12, Screen.width, 12), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, 0, 12, Screen.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(Screen.width - 12, 0, 12, Screen.height), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }
    }
}
