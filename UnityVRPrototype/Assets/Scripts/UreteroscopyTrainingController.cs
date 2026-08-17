using System;
using UnityEngine;

public enum TrainingDifficulty
{
    Tutorial,
    Intermediate,
    Advanced
}

public enum TrainingInputMode
{
    Keyboard,
    SerialUsb
}

public enum TrainingExperienceMode
{
    Training,
    Exploration
}

public enum TrainingSessionState
{
    LoadingCase,
    Ready,
    Calibrating,
    Running,
    SensorPaused,
    Finished
}

public class UreteroscopyTrainingController : MonoBehaviour
{
    public const float ActiveHudWidth = 340f;
    public const float ActiveMinimapMaximumSize = 210f;

    private const int InteriorLayer = 29;
    private const int MinimapOnlyLayer = 30;
    private const string EncoderScalePreference = "UreteroscopyTraining.MillimetersPerEncoderTick";
    private const string MouseSensitivityPreference = "UreteroscopyTraining.MouseSensitivity";

    [Header("Case and cameras")]
    public VrCaseLoader caseLoader;
    public Camera endoscopeCamera;
    public Camera minimapCamera;
    public TrainingDifficulty difficulty = TrainingDifficulty.Tutorial;
    public TrainingExperienceMode experienceMode = TrainingExperienceMode.Training;

    [Header("Physical probe")]
    public float tipRadiusMeters = 0.002f;
    public float millimetersPerEncoderTick = 0.785f;
    public float rotationSmoothing = 18f;
    public float targetExtraToleranceMillimeters = 5f;
    public float minimumTargetToleranceMillimeters = 8f;
    public float targetAngleToleranceDegrees = 15f;
    public float targetStableSeconds = 0.5f;

    [Header("Training safety")]
    public float maximumRouteDeviationMillimeters = 15f;
    public int maximumCollisionEvents = 5;
    public float collisionFlashSeconds = 0.4f;

    [Header("Input")]
    public TrainingInputMode inputMode = TrainingInputMode.Keyboard;
    public string serialPort = "AUTO";
    [Range(0.5f, 4f)] public float mouseSensitivity = 2f;

    public TrainingSessionState State { get; private set; } = TrainingSessionState.LoadingCase;
    public float ElapsedSeconds => elapsedSeconds;
    public int CollisionEvents => collisionEvents;
    public float CurrentDeviationMillimeters => currentDeviationMeters * 1000f;

    private ITrainingInputSource inputSource;
    private Transform probe;
    private GameObject interiorVisualRoot;
    private RenderTexture minimapTexture;
    private TrainingNavigationVisuals navigationVisuals;
    private Vector3[] routePositions = Array.Empty<Vector3>();
    private Quaternion neutralOrientation = Quaternion.identity;
    private Quaternion initialProbeRotation = Quaternion.identity;
    private TrainingInputFrame latestFrame;
    private bool hasLatestFrame;
    private bool calibrateRequested;
    private bool previousCalibrate;
    private bool previousAction;
    private bool wasContacting;
    private long lastEncoderTicks;
    private float elapsedSeconds;
    private float wallContactSeconds;
    private float traveledMeters;
    private float squaredDeviationSum;
    private int deviationSamples;
    private int collisionEvents;
    private float currentDeviationMeters;
    private float targetStableTimer;
    private float collisionFlashUntil;
    private bool showGiveUpConfirmation;
    private string incompleteHeading = "SESSÃO INCOMPLETA — DNF";
    private string participantCode = "ANON";
    private string feedbackMessage = "";
    private float feedbackUntil;
    private string lastCsvPath = "";
    private TrainingSessionResult lastResult;
    private bool encoderCalibrationMode;
    private bool encoderCalibrationHasZero;
    private long encoderCalibrationZeroTicks;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle centeredStyle;
    private GUIStyle hudTitleStyle;
    private GUIStyle hudLabelStyle;
    private GUIStyle hudCenteredStyle;
    private GUIStyle hudButtonStyle;
    private Font runtimeUiFont;

    private void Awake()
    {
        millimetersPerEncoderTick = PlayerPrefs.GetFloat(EncoderScalePreference, millimetersPerEncoderTick);
        mouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityPreference, mouseSensitivity), 0.5f, 4f);
        if (caseLoader == null) caseLoader = FindAnyObjectByType<VrCaseLoader>();
        navigationVisuals = GetComponent<TrainingNavigationVisuals>();
        if (navigationVisuals == null) navigationVisuals = gameObject.AddComponent<TrainingNavigationVisuals>();
        EnsureCameras();
    }

    private void OnEnable()
    {
        if (caseLoader != null)
        {
            caseLoader.CaseReady += HandleCaseReady;
            caseLoader.RouteChanged += HandleRouteChanged;
        }
    }

    private void Start()
    {
        if (caseLoader != null && caseLoader.IsReady) HandleCaseReady();
    }

    private void Update()
    {
        bool receivedFrame = false;
        inputSource?.Tick();
        if (inputSource != null && inputSource.TryGetLatestFrame(out TrainingInputFrame frame))
        {
            receivedFrame = true;
            latestFrame = frame;
            hasLatestFrame = true;
            bool calibrateEdge = frame.calibratePressed && !previousCalibrate;
            if (!encoderCalibrationMode && (calibrateRequested || calibrateEdge))
            {
                Calibrate(frame);
                calibrateRequested = false;
            }
            previousCalibrate = frame.calibratePressed;
        }

        if (State == TrainingSessionState.Running)
        {
            if (inputSource == null || !inputSource.IsConnected)
            {
                State = TrainingSessionState.SensorPaused;
                feedbackMessage = "Sensor desconectado. Movimento pausado.";
                return;
            }
            if (showGiveUpConfirmation)
            {
                if (receivedFrame)
                {
                    lastEncoderTicks = latestFrame.encoderTicks;
                    previousAction = latestFrame.actionPressed;
                }
                return;
            }
            if (receivedFrame) ProcessFrame(latestFrame);
            if (experienceMode == TrainingExperienceMode.Training && State == TrainingSessionState.Running)
            {
                elapsedSeconds += Time.deltaTime;
                if (wasContacting) wallContactSeconds += Time.deltaTime;
                UpdateTargetAlignment();
            }
        }
        else if (State == TrainingSessionState.SensorPaused && inputSource != null && inputSource.IsConnected)
        {
            State = TrainingSessionState.Calibrating;
            feedbackMessage = "Controle reconectado. Recalibre antes de continuar.";
        }
    }

    private void LateUpdate()
    {
        if (State == TrainingSessionState.Running) navigationVisuals?.TickArrow();
    }

    private void HandleCaseReady()
    {
        PrepareLoadedCase();
        State = TrainingSessionState.Ready;
    }

    private void HandleRouteChanged()
    {
        if (caseLoader != null && caseLoader.IsReady)
        {
            StopInput();
            PrepareLoadedCase();
            State = TrainingSessionState.Ready;
        }
    }

    private void PrepareLoadedCase()
    {
        if (caseLoader == null || !caseLoader.IsReady || caseLoader.ContentRoot == null) return;
        if (probe != null)
        {
            PreserveEndoscopeCamera();
            Destroy(probe.gameObject);
            probe = null;
        }
        if (interiorVisualRoot != null) Destroy(interiorVisualRoot);
        EnsureCameras();
        CreateInteriorVisual();
        CreateProbe();
        ConfigureMinimap();
        ApplyDifficultyVisuals();
        routePositions = caseLoader.CopyCurrentRoutePositions();
        navigationVisuals?.Configure(endoscopeCamera, caseLoader, probe, routePositions);
    }

    private void PreserveEndoscopeCamera()
    {
        if (endoscopeCamera == null) return;
        endoscopeCamera.transform.SetParent(transform, false);
    }

    private void EnsureCameras()
    {
        if (endoscopeCamera == null)
        {
            Camera existing = Camera.main;
            GameObject cameraObject = existing != null ? existing.gameObject : new GameObject("Endoscopic Camera");
            endoscopeCamera = existing != null ? existing : cameraObject.AddComponent<Camera>();
        }
        endoscopeCamera.gameObject.tag = "MainCamera";
        endoscopeCamera.gameObject.SetActive(true);
        endoscopeCamera.enabled = true;
        endoscopeCamera.targetTexture = null;
        endoscopeCamera.nearClipPlane = 0.001f;
        endoscopeCamera.farClipPlane = experienceMode == TrainingExperienceMode.Exploration ? 6f : 3f;
        endoscopeCamera.fieldOfView = 78f;
        endoscopeCamera.clearFlags = CameraClearFlags.SolidColor;
        endoscopeCamera.backgroundColor = new Color(0.025f, 0.005f, 0.008f);
        endoscopeCamera.cullingMask &= ~(1 << MinimapOnlyLayer);

        if (minimapCamera == null)
        {
            GameObject minimapObject = new GameObject("Training Minimap Camera");
            minimapCamera = minimapObject.AddComponent<Camera>();
        }
        minimapCamera.gameObject.SetActive(true);
        minimapCamera.enabled = true;
        minimapCamera.orthographic = true;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.015f, 0.035f, 0.055f);
        minimapCamera.cullingMask &= ~((1 << InteriorLayer) | (1 << TrainingNavigationVisuals.GuidanceLayer));
        minimapCamera.depth = -10f;
        if (minimapTexture == null)
        {
            minimapTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
            {
                name = "Training Minimap"
            };
        }
        if (!minimapTexture.IsCreated()) minimapTexture.Create();
        minimapCamera.targetTexture = minimapTexture;
    }

    private void CreateInteriorVisual()
    {
        GameObject anatomy = caseLoader.AnatomyObject;
        if (anatomy == null) return;
        SetLayerRecursively(anatomy.transform, MinimapOnlyLayer);
        interiorVisualRoot = new GameObject("Training Interior Visual");
        interiorVisualRoot.transform.SetParent(caseLoader.ContentRoot, false);
        interiorVisualRoot.layer = InteriorLayer;
        Material tissue = BuildInteriorMaterial();
        foreach (MeshFilter source in anatomy.GetComponentsInChildren<MeshFilter>(true))
        {
            GameObject copy = new GameObject(source.gameObject.name + " Interior");
            copy.layer = InteriorLayer;
            copy.transform.SetParent(interiorVisualRoot.transform, false);
            copy.transform.position = source.transform.position;
            copy.transform.rotation = source.transform.rotation;
            copy.transform.localScale = source.transform.lossyScale;
            copy.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            MeshRenderer renderer = copy.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = tissue;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static Material BuildInteriorMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { color = new Color(0.62f, 0.085f, 0.11f, 1f) };
        material.doubleSidedGI = true;
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.65f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.65f);
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", new Color(0.08f, 0.005f, 0.008f));
            material.EnableKeyword("_EMISSION");
        }
        return material;
    }

    private void CreateProbe()
    {
        GameObject probeObject = new GameObject("Training Ureteroscope Tip");
        probeObject.layer = MinimapOnlyLayer;
        probe = probeObject.transform;
        probe.SetParent(caseLoader.ContentRoot, false);
        probe.localPosition = caseLoader.GetCurrentStartLocal();
        Vector3 next = caseLoader.SampleCurrentRouteLocal(Mathf.Min(0.01f, caseLoader.CurrentRouteLengthMeters));
        Vector3 forward = next - probe.localPosition;
        initialProbeRotation = forward.sqrMagnitude > 0.000001f
            ? Quaternion.LookRotation(forward.normalized, Vector3.up)
            : Quaternion.identity;
        probe.localRotation = initialProbeRotation;

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Minimap Probe Marker";
        marker.layer = MinimapOnlyLayer;
        marker.transform.SetParent(probe, false);
        marker.transform.localScale = Vector3.one * tipRadiusMeters * 3.5f;
        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null) Destroy(markerCollider);
        Renderer markerRenderer = marker.GetComponent<Renderer>();
        markerRenderer.material.color = new Color(0.1f, 1f, 0.95f);

        endoscopeCamera.transform.SetParent(probe, false);
        endoscopeCamera.transform.localPosition = Vector3.zero;
        endoscopeCamera.transform.localRotation = Quaternion.identity;
        GameObject lightObject = new GameObject("Endoscope Light");
        lightObject.transform.SetParent(probe, false);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.range = 0.12f;
        light.spotAngle = 95f;
        light.intensity = 2.2f;
        light.color = new Color(1f, 0.83f, 0.76f);
        light.shadows = LightShadows.None;
    }

    private void ConfigureMinimap()
    {
        Renderer[] renderers = caseLoader.ContentRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
        float size = Mathf.Max(bounds.extents.x, bounds.extents.z, 0.04f);
        minimapCamera.orthographicSize = size * 1.25f;
        minimapCamera.transform.position = bounds.center + Vector3.up * Mathf.Max(0.25f, bounds.size.y + 0.15f);
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    public void BeginSession()
    {
        if (State != TrainingSessionState.Ready && State != TrainingSessionState.Finished) return;
        ResetMetrics();
        PlayerPrefs.SetFloat(MouseSensitivityPreference, Mathf.Clamp(mouseSensitivity, 0.5f, 4f));
        PlayerPrefs.Save();
        PrepareLoadedCase();
        inputSource = inputMode == TrainingInputMode.SerialUsb
            ? new SerialControllerInput(serialPort)
            : new KeyboardTrainingInput(millimetersPerEncoderTick, mouseSensitivity);
        State = TrainingSessionState.Calibrating;
        calibrateRequested = inputMode == TrainingInputMode.Keyboard;
        navigationVisuals?.SetPresentation(false, experienceMode == TrainingExperienceMode.Exploration);
        if (inputMode == TrainingInputMode.Keyboard)
        {
            feedbackMessage = "Ativando teclado e mouse...";
        }
        else
        {
            feedbackMessage = experienceMode == TrainingExperienceMode.Exploration
                ? "Mantenha a vareta imóvel e calibre para iniciar a exploração livre."
                : "Mantenha a vareta imóvel e pressione Calibrar.";
        }
    }

    public void RequestCalibration()
    {
        calibrateRequested = true;
    }

    public void BeginEncoderCalibration()
    {
        if (State != TrainingSessionState.Ready) return;
        StopInput();
        inputSource = new SerialControllerInput(serialPort);
        encoderCalibrationMode = true;
        encoderCalibrationHasZero = false;
        State = TrainingSessionState.Calibrating;
        feedbackMessage = "Conecte a vareta, posicione no zero e marque o início.";
    }

    private void MarkEncoderCalibrationZero()
    {
        if (!hasLatestFrame || inputSource == null || !inputSource.IsConnected)
        {
            feedbackMessage = "Aguardando pacotes válidos da vareta USB.";
            return;
        }
        encoderCalibrationZeroTicks = latestFrame.encoderTicks;
        encoderCalibrationHasZero = true;
        feedbackMessage = "Zero marcado. Avance exatamente 100 mm e conclua.";
    }

    private void FinishEncoderCalibration()
    {
        if (!encoderCalibrationHasZero || !hasLatestFrame)
        {
            feedbackMessage = "Marque o zero antes de concluir.";
            return;
        }
        long delta = Math.Abs(latestFrame.encoderTicks - encoderCalibrationZeroTicks);
        if (delta < 10)
        {
            feedbackMessage = "Poucos pulsos detectados. Verifique a roda e repita.";
            return;
        }
        millimetersPerEncoderTick = 100f / delta;
        PlayerPrefs.SetFloat(EncoderScalePreference, millimetersPerEncoderTick);
        PlayerPrefs.Save();
        feedbackMessage = $"Encoder calibrado: {millimetersPerEncoderTick:F4} mm/tick.";
        encoderCalibrationMode = false;
        StopInput();
        State = TrainingSessionState.Ready;
    }

    private void Calibrate(TrainingInputFrame frame)
    {
        if (!frame.imuOk)
        {
            feedbackMessage = "A IMU ainda não está pronta.";
            return;
        }
        neutralOrientation = frame.orientation;
        lastEncoderTicks = frame.encoderTicks;
        previousAction = frame.actionPressed;
        State = TrainingSessionState.Running;
        navigationVisuals?.SetPresentation(true, experienceMode == TrainingExperienceMode.Exploration);
        feedbackMessage = experienceMode == TrainingExperienceMode.Exploration
            ? "Exploração livre ativa. A seta indica a rota planejada."
            : "Calibrado. Navegue até a pedra.";
        feedbackUntil = Time.unscaledTime + 2f;
    }

    private void ProcessFrame(TrainingInputFrame frame)
    {
        if (!frame.imuOk) return;
        Quaternion relativeOrientation = TrainingInputMath.RelativeOrientation(neutralOrientation, frame.orientation);
        Quaternion desired = initialProbeRotation * relativeOrientation;
        probe.localRotation = Quaternion.Slerp(probe.localRotation, desired, 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime));

        long tickDelta = frame.encoderTicks - lastEncoderTicks;
        lastEncoderTicks = frame.encoderTicks;
        float deltaMeters = Mathf.Clamp(TrainingInputMath.TicksToMeters(tickDelta, millimetersPerEncoderTick), -0.02f, 0.02f);
        bool enforceSafety = experienceMode == TrainingExperienceMode.Training;
        bool contacting = Mathf.Abs(deltaMeters) > 0.000001f && !TryMoveProbe(deltaMeters, enforceSafety);
        UpdateCollisionState(enforceSafety && contacting);
        if (State == TrainingSessionState.Finished) return;

        currentDeviationMeters = TrainingMetrics.DistanceToPolyline(probe.localPosition, routePositions);
        if (enforceSafety)
        {
            squaredDeviationSum += currentDeviationMeters * currentDeviationMeters;
            deviationSamples++;
        }

        bool actionEdge = frame.actionPressed && !previousAction;
        if (enforceSafety && actionEdge)
        {
            if (targetStableTimer >= targetStableSeconds) FinishSession(true);
            else
            {
                feedbackMessage = "Aproxime e estabilize a mira sobre a pedra antes de acionar.";
                feedbackUntil = Time.unscaledTime + 1.8f;
            }
        }
        previousAction = frame.actionPressed;
    }

    private void UpdateCollisionState(bool contacting)
    {
        if (contacting && !wasContacting)
        {
            collisionEvents++;
            collisionFlashUntil = Time.unscaledTime + Mathf.Max(0.1f, collisionFlashSeconds);
            feedbackMessage = $"Contato bloqueado — colisão {collisionEvents}/{Mathf.Max(1, maximumCollisionEvents)}.";
            feedbackUntil = Time.unscaledTime + 1.5f;
            if (collisionEvents >= Mathf.Max(1, maximumCollisionEvents))
            {
                FinishSession(false, "LIMITE DE COLISÕES — DNF");
                return;
            }
        }
        wasContacting = contacting;
    }

    private bool TryMoveProbe(float deltaMeters, bool enforceSafety)
    {
        if (probe == null || Mathf.Abs(deltaMeters) < 0.000001f) return true;
        Vector3 direction = deltaMeters >= 0f ? probe.forward : -probe.forward;
        float distance = Mathf.Abs(deltaMeters);
        if (!enforceSafety)
        {
            probe.position += direction * distance;
            return true;
        }

        float permittedDistance = distance;
        bool hitWall;
        bool previousBackfaceSetting = Physics.queriesHitBackfaces;
        try
        {
            Physics.queriesHitBackfaces = true;
            hitWall = Physics.SphereCast(
                probe.position,
                tipRadiusMeters,
                direction,
                out RaycastHit hit,
                distance + 0.0005f,
                1 << MinimapOnlyLayer,
                QueryTriggerInteraction.Ignore
            );
            if (hitWall) permittedDistance = Mathf.Clamp(hit.distance - 0.0005f, 0f, distance);
        }
        finally
        {
            Physics.queriesHitBackfaces = previousBackfaceSetting;
        }

        float corridorRadius = Mathf.Max(tipRadiusMeters * 2f, maximumRouteDeviationMillimeters * 0.001f);
        float safeDistance = FindSafeRouteDistance(direction, permittedDistance, corridorRadius);
        bool hitSafetyBoundary = safeDistance + 0.000001f < permittedDistance;
        probe.position += direction * safeDistance;
        traveledMeters += safeDistance;
        return !hitWall && !hitSafetyBoundary;
    }

    private float FindSafeRouteDistance(Vector3 direction, float requestedDistance, float corridorRadius)
    {
        if (requestedDistance <= 0f || routePositions == null || routePositions.Length < 2) return 0f;
        Vector3 requestedWorld = probe.position + direction * requestedDistance;
        Vector3 requestedLocal = caseLoader.ContentRoot.InverseTransformPoint(requestedWorld);
        if (TrainingMetrics.IsWithinRouteCorridor(requestedLocal, routePositions, corridorRadius)) return requestedDistance;

        float low = 0f;
        float high = requestedDistance;
        for (int iteration = 0; iteration < 8; iteration++)
        {
            float middle = (low + high) * 0.5f;
            Vector3 candidateLocal = caseLoader.ContentRoot.InverseTransformPoint(probe.position + direction * middle);
            if (TrainingMetrics.IsWithinRouteCorridor(candidateLocal, routePositions, corridorRadius)) low = middle;
            else high = middle;
        }
        return low;
    }

    private void UpdateTargetAlignment()
    {
        if (probe == null || caseLoader == null) return;
        Vector3 targetWorld = caseLoader.ContentRoot.TransformPoint(caseLoader.GetCurrentTargetLocal());
        Vector3 toTarget = targetWorld - probe.position;
        float targetRadius = caseLoader.GetCurrentStoneRadiusMeters();
        float tolerance = Mathf.Max(targetRadius + targetExtraToleranceMillimeters * 0.001f, minimumTargetToleranceMillimeters * 0.001f);
        float angle = toTarget.sqrMagnitude > 0.0000001f ? Vector3.Angle(probe.forward, toTarget.normalized) : 0f;
        if (toTarget.magnitude <= tolerance && angle <= targetAngleToleranceDegrees)
        {
            targetStableTimer += Time.deltaTime;
        }
        else
        {
            targetStableTimer = 0f;
        }
    }

    public void AbortSession()
    {
        if (State == TrainingSessionState.Running || State == TrainingSessionState.Calibrating || State == TrainingSessionState.SensorPaused)
        {
            if (experienceMode == TrainingExperienceMode.Exploration) ExitExploration();
            else FinishSession(false, "DESISTÊNCIA — DNF");
        }
    }

    public void RequestGiveUpConfirmation()
    {
        if (experienceMode == TrainingExperienceMode.Training &&
            (State == TrainingSessionState.Running || State == TrainingSessionState.Calibrating || State == TrainingSessionState.SensorPaused))
        {
            showGiveUpConfirmation = true;
        }
    }

    public void CancelGiveUpConfirmation()
    {
        showGiveUpConfirmation = false;
    }

    public void ConfirmGiveUp()
    {
        if (!showGiveUpConfirmation) return;
        showGiveUpConfirmation = false;
        FinishSession(false, "DESISTÊNCIA — DNF");
    }

    public void ExitExploration()
    {
        if (experienceMode != TrainingExperienceMode.Exploration) return;
        showGiveUpConfirmation = false;
        StopInput();
        navigationVisuals?.SetPresentation(false, false);
        ResetMetrics();
        PrepareLoadedCase();
        State = TrainingSessionState.Ready;
    }

    private void FinishSession(bool completed, string unfinishedTitle = "SESSÃO INCOMPLETA — DNF")
    {
        if (experienceMode == TrainingExperienceMode.Exploration)
        {
            ExitExploration();
            return;
        }
        State = TrainingSessionState.Finished;
        incompleteHeading = unfinishedTitle;
        showGiveUpConfirmation = false;
        navigationVisuals?.SetPresentation(false, false);
        float rmsMeters = deviationSamples > 0 ? Mathf.Sqrt(squaredDeviationSum / deviationSamples) : 0f;
        VrRouteData route = caseLoader.CurrentRoute;
        float plannedMillimeters = route?.metrics != null && route.metrics.smoothed_length_mm > 0f
            ? route.metrics.smoothed_length_mm
            : caseLoader.CurrentRouteLengthMeters * 1000f;
        lastResult = new TrainingSessionResult
        {
            participantCode = TrainingCsvLogger.SanitizeParticipantCode(participantCode),
            timestampUtc = DateTime.UtcNow.ToString("O"),
            caseId = caseLoader.CurrentManifest?.case_id ?? caseLoader.CurrentCaseName,
            routeId = route?.route_id ?? "legacy_route",
            difficulty = difficulty.ToString(),
            completed = completed,
            elapsedSeconds = elapsedSeconds,
            collisionEvents = collisionEvents,
            wallContactSeconds = wallContactSeconds,
            rmsDeviationMillimeters = rmsMeters * 1000f,
            traveledMillimeters = traveledMeters * 1000f,
            plannedMillimeters = plannedMillimeters,
            inputSource = inputSource?.DisplayName ?? inputMode.ToString(),
            firmwareVersion = inputSource?.FirmwareVersion ?? "unknown"
        };
        lastResult.score = TrainingMetrics.CalculateScore(lastResult);
        lastCsvPath = TrainingCsvLogger.Append(lastResult);
        StopInput();
    }

    private void ResetMetrics()
    {
        elapsedSeconds = 0f;
        wallContactSeconds = 0f;
        traveledMeters = 0f;
        squaredDeviationSum = 0f;
        deviationSamples = 0;
        collisionEvents = 0;
        currentDeviationMeters = 0f;
        targetStableTimer = 0f;
        collisionFlashUntil = 0f;
        showGiveUpConfirmation = false;
        incompleteHeading = "SESSÃO INCOMPLETA — DNF";
        calibrateRequested = false;
        previousAction = false;
        previousCalibrate = false;
        wasContacting = false;
        lastResult = null;
        lastCsvPath = "";
    }

    private void ApplyDifficultyVisuals()
    {
        if (caseLoader?.RouteRoot == null) return;
        caseLoader.RouteRoot.gameObject.SetActive(experienceMode == TrainingExperienceMode.Exploration || difficulty != TrainingDifficulty.Advanced);
        int routeLayer = difficulty == TrainingDifficulty.Intermediate ? MinimapOnlyLayer : 0;
        SetLayerRecursively(caseLoader.RouteRoot, routeLayer);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    private void StopInput()
    {
        inputSource?.Dispose();
        inputSource = null;
    }

    private void OnGUI()
    {
        EnsureGuiStyles();
        if (Time.unscaledTime < collisionFlashUntil)
        {
            float remaining = Mathf.Clamp01((collisionFlashUntil - Time.unscaledTime) / Mathf.Max(0.1f, collisionFlashSeconds));
            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.05f, 0.03f, 0.18f + remaining * 0.28f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        bool activeHud = State == TrainingSessionState.Running;
        float panelWidth = activeHud ? ActiveHudWidth : 430f;
        float panelHeight = State == TrainingSessionState.Ready
            ? 400f
            : activeHud
                ? experienceMode == TrainingExperienceMode.Training ? 180f : 160f
                : 220f;
        GUI.Box(new Rect(14, 14, panelWidth, panelHeight), "");
        if (activeHud)
        {
            string activeTitle = experienceMode == TrainingExperienceMode.Training
                ? "URETEROSCOPIA • TREINAMENTO"
                : "URETEROSCOPIA • EXPLORAÇÃO";
            GUI.Label(new Rect(26, 20, 310, 24), activeTitle, hudTitleStyle);
            GUI.Label(new Rect(26, 45, 305, 19), $"Caso: {caseLoader?.CurrentCaseName ?? "carregando"}", hudLabelStyle);
        }
        else
        {
            GUI.Label(new Rect(28, 24, 360, 34), "TREINAMENTO DE URETEROSCOPIA", titleStyle);
            GUI.Label(new Rect(28, 60, 400, 24), $"Caso: {caseLoader?.CurrentCaseName ?? "carregando"}", labelStyle);
        }

        if (State == TrainingSessionState.Ready)
        {
            GUI.Label(new Rect(28, 91, 90, 24), "Modo:", labelStyle);
            if (GUI.Toggle(new Rect(120, 88, 140, 28), experienceMode == TrainingExperienceMode.Training, "Treinamento", "Button")) experienceMode = TrainingExperienceMode.Training;
            if (GUI.Toggle(new Rect(264, 88, 164, 28), experienceMode == TrainingExperienceMode.Exploration, "Exploração livre", "Button")) experienceMode = TrainingExperienceMode.Exploration;

            if (experienceMode == TrainingExperienceMode.Training)
            {
                GUI.Label(new Rect(28, 128, 130, 24), "Código anônimo:", labelStyle);
                participantCode = GUI.TextField(new Rect(165, 126, 205, 27), participantCode, 16);
                GUI.Label(new Rect(28, 164, 100, 24), "Dificuldade:", labelStyle);
                if (GUI.Toggle(new Rect(130, 162, 75, 26), difficulty == TrainingDifficulty.Tutorial, "Tutorial", "Button")) difficulty = TrainingDifficulty.Tutorial;
                if (GUI.Toggle(new Rect(207, 162, 85, 26), difficulty == TrainingDifficulty.Intermediate, "Intermed.", "Button")) difficulty = TrainingDifficulty.Intermediate;
                if (GUI.Toggle(new Rect(294, 162, 75, 26), difficulty == TrainingDifficulty.Advanced, "Avançado", "Button")) difficulty = TrainingDifficulty.Advanced;
            }
            else
            {
                GUI.Label(new Rect(28, 126, 400, 62), "Explore dentro e fora do modelo sem pontuação ou CSV.\nA seta colorida indica o próximo trecho da rota.", labelStyle);
            }

            GUI.Label(new Rect(28, 200, 100, 24), "Controle:", labelStyle);
            if (GUI.Toggle(new Rect(130, 198, 110, 26), inputMode == TrainingInputMode.Keyboard, "Teclado", "Button")) inputMode = TrainingInputMode.Keyboard;
            if (GUI.Toggle(new Rect(242, 198, 127, 26), inputMode == TrainingInputMode.SerialUsb, "Vareta USB", "Button")) inputMode = TrainingInputMode.SerialUsb;
            if (inputMode == TrainingInputMode.SerialUsb)
            {
                GUI.Label(new Rect(28, 236, 100, 24), "Porta COM:", labelStyle);
                serialPort = GUI.TextField(new Rect(130, 234, 239, 27), serialPort, 16);
            }
            if (inputMode == TrainingInputMode.SerialUsb && GUI.Button(new Rect(28, 272, 400, 30), $"CALIBRAR ENCODER 100 mm ({millimetersPerEncoderTick:F4} mm/tick)"))
            {
                BeginEncoderCalibration();
                return;
            }
            else if (inputMode == TrainingInputMode.Keyboard)
            {
                GUI.Label(new Rect(28, 234, 135, 24), $"Sensibilidade: {mouseSensitivity:F1}", labelStyle);
                mouseSensitivity = GUI.HorizontalSlider(new Rect(165, 241, 203, 18), mouseSensitivity, 0.5f, 4f);
                GUI.Label(new Rect(28, 268, 400, 42), "Mouse: mirar | Esq./Dir.: avançar/recuar\nW/S e setas também funcionam", centeredStyle);
            }
            string startLabel = experienceMode == TrainingExperienceMode.Training ? "INICIAR TREINAMENTO" : "INICIAR EXPLORAÇÃO";
            if (GUI.Button(new Rect(28, 326, 400, 46), startLabel))
            {
                ApplyDifficultyVisuals();
                BeginSession();
                return;
            }
        }
        else if (State == TrainingSessionState.LoadingCase)
        {
            GUI.Label(new Rect(28, 98, 350, 30), "Carregando anatomia, pedra e rota...", labelStyle);
        }
        else if (State == TrainingSessionState.Calibrating || State == TrainingSessionState.SensorPaused)
        {
            GUI.Label(new Rect(28, 92, 350, 45), feedbackMessage, labelStyle);
            GUI.Label(new Rect(28, 132, 350, 24), inputSource?.DisplayName ?? "Sem controle", labelStyle);
            if (encoderCalibrationMode)
            {
                if (GUI.Button(new Rect(28, 160, 105, 32), "1. MARCAR ZERO")) MarkEncoderCalibrationZero();
                if (GUI.Button(new Rect(138, 160, 135, 32), "2. CONCLUIR 100 mm"))
                {
                    FinishEncoderCalibration();
                    return;
                }
                if (GUI.Button(new Rect(278, 160, 91, 32), "Cancelar"))
                {
                    encoderCalibrationMode = false;
                    StopInput();
                    State = TrainingSessionState.Ready;
                    return;
                }
            }
            else
            {
                if (State == TrainingSessionState.Calibrating && GUI.Button(new Rect(28, 160, 165, 32), "CALIBRAR AGORA")) RequestCalibration();
                string cancelLabel = experienceMode == TrainingExperienceMode.Training ? "Desistir" : "Sair";
                if (GUI.Button(new Rect(204, 160, 165, 32), cancelLabel))
                {
                    if (experienceMode == TrainingExperienceMode.Training) RequestGiveUpConfirmation();
                    else ExitExploration();
                    return;
                }
            }
        }
        else if (State == TrainingSessionState.Running)
        {
            if (experienceMode == TrainingExperienceMode.Training)
            {
                GUI.Label(new Rect(26, 68, 305, 20), $"Tempo {elapsedSeconds:F1}s  •  Colisões {collisionEvents}/{Mathf.Max(1, maximumCollisionEvents)}", hudLabelStyle);
                GUI.Label(new Rect(26, 90, 305, 20), $"Desvio da rota: {CurrentDeviationMillimeters:F1} mm", hudLabelStyle);
                string target = targetStableTimer > 0f ? $"ALVO ALINHADO {Mathf.Clamp01(targetStableTimer / targetStableSeconds) * 100f:F0}%" : "Procure e alinhe a pedra";
                GUI.Label(new Rect(26, 112, 305, 20), target, hudLabelStyle);
            }
            else
            {
                GUI.Label(new Rect(26, 68, 305, 20), "Livre • sem pontuação ou CSV", hudLabelStyle);
                GUI.Label(new Rect(26, 90, 305, 20), $"Distância da rota: {CurrentDeviationMillimeters:F1} mm", hudLabelStyle);
            }
            if (inputMode == TrainingInputMode.Keyboard)
            {
                string actionHelp = experienceMode == TrainingExperienceMode.Training ? "Centro/Espaço: confirmar" : "Seta: próximo trecho";
                float helpY = experienceMode == TrainingExperienceMode.Training ? 136f : 114f;
                GUI.Label(new Rect(26, helpY, 190, 34), $"Mouse Esq./Dir.: mover\n{actionHelp}", hudLabelStyle);
            }
            string endLabel = experienceMode == TrainingExperienceMode.Training ? "DESISTIR" : "SAIR";
            float buttonY = experienceMode == TrainingExperienceMode.Training ? 140f : 118f;
            if (GUI.Button(new Rect(222, buttonY, 118, 30), endLabel, hudButtonStyle))
            {
                if (experienceMode == TrainingExperienceMode.Training) RequestGiveUpConfirmation();
                else ExitExploration();
                return;
            }
        }

        if (State == TrainingSessionState.Finished && lastResult != null)
        {
            Rect panel = new Rect(Screen.width * 0.5f - 245f, Screen.height * 0.5f - 155f, 490f, 310f);
            GUI.Box(panel, "");
            string heading = lastResult.completed ? $"CONCLUÍDO — NOTA {lastResult.score:F0}/100" : incompleteHeading;
            GUI.Label(new Rect(panel.x + 25, panel.y + 22, 440, 38), heading, titleStyle);
            GUI.Label(new Rect(panel.x + 25, panel.y + 72, 440, 100),
                $"Tempo: {lastResult.elapsedSeconds:F1}s\nColisões: {lastResult.collisionEvents}\nDesvio RMS: {lastResult.rmsDeviationMillimeters:F1} mm\nPercurso: {lastResult.traveledMillimeters:F1} mm", labelStyle);
            GUI.Label(new Rect(panel.x + 25, panel.y + 180, 440, 42), "Resultado salvo localmente em CSV.", centeredStyle);
            if (GUI.Button(new Rect(panel.x + 80, panel.y + 238, 330, 44), "NOVO TREINAMENTO"))
            {
                PrepareLoadedCase();
                State = TrainingSessionState.Ready;
                return;
            }
        }

        if (minimapTexture != null && State != TrainingSessionState.LoadingCase && State != TrainingSessionState.Ready)
        {
            float maximum = activeHud ? ActiveMinimapMaximumSize : 300f;
            float proportion = activeHud ? 0.27f : 0.36f;
            float size = Mathf.Min(maximum, Screen.height * proportion);
            Rect container = new Rect(Screen.width - size - 18f, 14f, size + 8f, size + 28f);
            Rect map = new Rect(container.x + 4f, container.y + 22f, size, size);
            GUI.Box(container, "");
            GUI.Label(new Rect(container.x + 4f, container.y + 2f, size, 18f), "MINIMAPA", hudCenteredStyle);
            GUI.DrawTexture(map, minimapTexture, ScaleMode.ScaleToFit, false);
        }
        if (!string.IsNullOrEmpty(feedbackMessage) && (feedbackUntil <= 0f || Time.unscaledTime <= feedbackUntil) && State == TrainingSessionState.Running)
        {
            GUI.Label(new Rect(Screen.width * 0.5f - 250, Screen.height - 90, 500, 30), feedbackMessage, hudCenteredStyle);
        }

        if (showGiveUpConfirmation)
        {
            Rect confirmation = new Rect(Screen.width * 0.5f - 230f, Screen.height * 0.5f - 105f, 460f, 210f);
            GUI.Box(confirmation, "");
            GUI.Label(new Rect(confirmation.x + 24, confirmation.y + 22, 412, 35), "DESISTIR DO TREINAMENTO?", titleStyle);
            GUI.Label(new Rect(confirmation.x + 24, confirmation.y + 66, 412, 48), "A tentativa será registrada como DNF.", centeredStyle);
            if (GUI.Button(new Rect(confirmation.x + 24, confirmation.y + 138, 190, 44), "CONTINUAR"))
            {
                CancelGiveUpConfirmation();
                return;
            }
            if (GUI.Button(new Rect(confirmation.x + 246, confirmation.y + 138, 190, 44), "CONFIRMAR DNF"))
            {
                ConfirmGiveUp();
                return;
            }
        }
        GUI.Label(new Rect(12, Screen.height - 35, Screen.width - 24, 25),
            "PROTÓTIPO ACADÊMICO/EDUCACIONAL — NÃO UTILIZAR EM PACIENTES OU PROCEDIMENTOS CLÍNICOS", hudCenteredStyle);
    }

    private void EnsureGuiStyles()
    {
        if (titleStyle != null) return;
        runtimeUiFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 16);
        Font font = runtimeUiFont != null ? runtimeUiFont : GUI.skin.font;
        titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        labelStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, normal = { textColor = Color.white }, wordWrap = true };
        centeredStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        hudTitleStyle = new GUIStyle(labelStyle) { fontSize = 14, fontStyle = FontStyle.Bold };
        hudLabelStyle = new GUIStyle(labelStyle) { fontSize = 12, fontStyle = FontStyle.Normal };
        hudCenteredStyle = new GUIStyle(hudLabelStyle) { alignment = TextAnchor.MiddleCenter };
        hudButtonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 12, fontStyle = FontStyle.Bold };
    }

    private void OnDisable()
    {
        if (caseLoader != null)
        {
            caseLoader.CaseReady -= HandleCaseReady;
            caseLoader.RouteChanged -= HandleRouteChanged;
        }
        StopInput();
    }

    private void OnDestroy()
    {
        if (runtimeUiFont != null) Destroy(runtimeUiFont);
        if (minimapTexture != null)
        {
            minimapTexture.Release();
            Destroy(minimapTexture);
        }
    }
}
