
using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class RallyController : MonoBehaviour
{
    [Serializable]
    public class RallySettingsData
    {
        public int version = 6;
        public int startPhaseIndex;
        public int endPhaseIndex;
        public int courtSideIndex;

        public int serveStartIndex;
        public int serveTargetIndex;

        public Vector3 serveTargetLeftOffset;
        public Vector3 serveTargetCenterOffset;
        public Vector3 serveTargetRightOffset;

        public float receiveApexHeight;
        public float receiveBackspinRpm;
        public int receiveContactFixedFrames;

        public Vector3 setterLocalPosition;

        public int tossSideIndex;
        public float tossTargetZOffset;
        public float setApexHeight;
        public float setSpinRpm;
        public int setContactFixedFrames;

        public int spikeCourseIndex;
        public float spikeSpeedKmh;

        public float spikeTargetLeftDistanceOffsetMeters;
        public float spikeTargetRightDistanceOffsetMeters;
        public Vector3 spikeTargetLeftOffset;
        public Vector3 spikeTargetRightOffset;

        public int blockContactFixedFrames;
        public float blockExitSpeedKmh;
        public float blockSpinRpm;

        // Version 6: Dig用に動かしたBlock通過点を保存
        public Vector3 blockContactPointLeftLocalPosition;
        public Vector3 blockContactPointRightLocalPosition;

        // Version 5: Block着地点
        public Vector3 blockLandingPointLeftLocalPosition;
        public Vector3 blockLandingPointRightLocalPosition;

        // 旧JSON互換
        public Vector3 blockLandingPointLeftOffset;
        public Vector3 blockLandingPointRightOffset;

        public float tossHeight;
        public float serveHitHeight;
        public float tossForwardDistance;
        public float serveSpeedKmh;
        public float spikeServeLaunchAngle;

        public bool enableExperimentFade;
        public float serveFadeDelay;
        public float receiveFadeDelay;
        public float tossFadeDelay;
        public float spikeFadeDelay;
        public float blockFadeDelay;
        public float fadeDuration;

        public bool hasDhitSettings;
        public float baseShiftPixels;
        public bool dynamicShiftEnabled;
        public DhitSettings dhitSettings = new DhitSettings();
    }

    public enum PlayPhase
    {
        Serve = 0,
        Receive = 1,
        Toss = 2,
        Spike = 3,
        Block = 4
    }

    public enum CourtSide
    {
        Left = 0,
        Right = 1
    }

    public enum ServeStartPosition
    {
        Left = 0,
        Center = 1,
        Right = 2
    }

    public enum ServeTargetPosition
    {
        Left = 0,
        Center = 1,
        Right = 2
    }

    public enum TossSide
    {
        Left = 0,
        Right = 1
    }

    public enum SpikeCourse
    {
        Straight = 0,
        Cross = 1
    }

    public enum BlockMode
    {
        None = 0,
        Block = 1
    }

    [Header("Simulation Range")]
    [SerializeField] private PlayPhase startPhase = PlayPhase.Serve;
    [SerializeField] private PlayPhase endPhase = PlayPhase.Receive;

    [Header("Court Side")]
    [SerializeField] private Transform playRoot;
    [SerializeField] private CourtSide currentCourtSide = CourtSide.Left;

    [Header("Ball Spawner")]
    [SerializeField] private BallSpawner ballSpawner;

    [Header("Serve Start Points")]
    [SerializeField] private Transform serveStartLeft;
    [SerializeField] private Transform serveStartCenter;
    [SerializeField] private Transform serveStartRight;

    [Header("Selected Serve Start")]
    [SerializeField] private ServeStartPosition selectedServeStart =
        ServeStartPosition.Center;

    [Header("Serve Target - Receiver Transforms")]
    [SerializeField] private Transform serveTargetLeft;
    [SerializeField] private Transform serveTargetCenter;
    [SerializeField] private Transform serveTargetRight;

    [Header("Selected Serve Target")]
    [SerializeField] private ServeTargetPosition selectedServeTarget =
        ServeTargetPosition.Center;

    [Header("Serve Target Offset")]
    [SerializeField] private Vector3 serveTargetLeftOffset = Vector3.zero;
    [SerializeField] private Vector3 serveTargetCenterOffset = Vector3.zero;
    [SerializeField] private Vector3 serveTargetRightOffset = Vector3.zero;

    private Vector3 serveTargetLeftBaseLocalPosition;
    private Vector3 serveTargetCenterBaseLocalPosition;
    private Vector3 serveTargetRightBaseLocalPosition;
    private bool serveTargetBasePositionsCached = false;

    [Header("Serve Receive / Cut")]
    [SerializeField] private Transform setterPosition;
    [SerializeField] private float receiveApexHeight = 4.0f;
    [SerializeField] private float receiveBackspinRpm = 180.0f;
    [Min(0)]
    [SerializeField] private int receiveContactFixedFrames = 1;

    [Header("Setter Toss Target")]
    [SerializeField] private Transform leftTossTarget;
    [SerializeField] private Transform rightTossTarget;

    [Header("Selected Toss")]
    [SerializeField] private TossSide selectedTossSide = TossSide.Left;
    [SerializeField] private float tossTargetZOffset = 0.0f;

    [Header("Setter Toss")]
    [SerializeField] private float setApexHeight = 4.5f;
    [SerializeField] private float setSpinRpm = 60.0f;
    [Min(0)]
    [SerializeField] private int setContactFixedFrames = 1;

    [Header("Spike")]
    [SerializeField] private Transform spikeTargetLeft;
    [SerializeField] private Transform spikeTargetRight;
    [SerializeField] private SpikeCourse selectedSpikeCourse =
        SpikeCourse.Cross;
    [SerializeField] private float spikeSpeedKmh = 100.0f;

    [Header("Spike Target Distance Offset")]
    [SerializeField] private float spikeTargetLeftDistanceOffsetMeters = 0.0f;
    [SerializeField] private float spikeTargetRightDistanceOffsetMeters = 0.0f;

    [HideInInspector]
    [SerializeField] private Vector3 spikeTargetLeftOffset = Vector3.zero;
    [HideInInspector]
    [SerializeField] private Vector3 spikeTargetRightOffset = Vector3.zero;

    private Vector3 spikeTargetLeftBaseLocalPosition;
    private Vector3 spikeTargetRightBaseLocalPosition;
    private bool spikeTargetBasePositionsCached = false;

    [Header("Block Trajectory")]
    [SerializeField] private Transform blockContactPointLeft;
    [SerializeField] private Transform blockLandingPointLeft;
    [SerializeField] private Transform blockContactPointRight;
    [SerializeField] private Transform blockLandingPointRight;

    [HideInInspector]
    [SerializeField] private Vector3 blockLandingPointLeftOffset = Vector3.zero;
    [HideInInspector]
    [SerializeField] private Vector3 blockLandingPointRightOffset = Vector3.zero;

    private Vector3 blockLandingPointLeftBaseLocalPosition;
    private Vector3 blockLandingPointRightBaseLocalPosition;
    private bool blockLandingPointBasePositionsCached = false;

    [Min(0)]
    [SerializeField] private int blockContactFixedFrames = 1;

    [Min(0.1f)]
    [SerializeField] private float blockExitSpeedKmh = 45.0f;

    [SerializeField] private float blockSpinRpm = 600.0f;

    [Header("Serve Toss")]
    [SerializeField] private float tossHeight = 1.5f;
    [SerializeField] private float serveHitHeight = 3.0f;
    [SerializeField] private float tossForwardDistance = 1.0f;

    [Header("Spike Serve")]
    [SerializeField] private float serveSpeedKmh = 130.0f;
    [SerializeField] private float spikeServeLaunchAngle = 2.0f;

    [Header("DHIT / Image Shift")]
    [SerializeField] private DHITController dhitController;

    [Header("Experiment Fade")]
    [SerializeField] private bool enableExperimentFade = true;
    [SerializeField] private ImageController imageController;

    [Min(0.0f)]
    [SerializeField] private float serveFadeDelay = 0.80f;
    [Min(0.0f)]
    [SerializeField] private float receiveFadeDelay = 0.50f;
    [Min(0.0f)]
    [SerializeField] private float tossFadeDelay = 0.50f;
    [Min(0.0f)]
    [SerializeField] private float spikeFadeDelay = 0.20f;
    [Min(0.0f)]
    [SerializeField] private float blockFadeDelay = 0.20f;
    [Min(0.0f)]
    [SerializeField] private float fadeDuration = 0.03f;

    [Header("Rally JSON")]
    [SerializeField] private string defaultRallyJsonFileName =
        "rally_settings.json";
    [SerializeField] private string rallyJsonRelativeFolder =
        "Experiment/Rallies";

    [Header("Startup")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("Keyboard Debug")]
    [SerializeField] private bool enableKeyboardDebug = true;

    // ============================================================
    // First landing / IN-OUT judgement
    // World origin is the centre of an 18 m (Z) x 9 m (X) court.
    // ============================================================
    [Header("Court IN / OUT")]
    [SerializeField] private float courtWidthMeters = 9.0f;
    [SerializeField] private float courtLengthMeters = 18.0f;

    [Header("Last Landing Result (runtime)")]
    [SerializeField] private bool hasLandingResult;
    [SerializeField] private bool lastLandingIsIn;
    [SerializeField] private Vector3 lastLandingPointWorld;
    [SerializeField] private float lastLandingBallRadiusMeters;

    public bool HasLandingResult => hasLandingResult;
    public bool LastLandingIsIn => lastLandingIsIn;
    public Vector3 LastLandingPointWorld => lastLandingPointWorld;
    public float LastLandingBallRadiusMeters => lastLandingBallRadiusMeters;

    public event Action<bool, Vector3> OnCourtLandingJudged;

    private VolleyballBallPhysics currentBallPhysics;
    private Coroutine experimentFadeTimerCoroutine;

    private Quaternion basePlayRootRotation = Quaternion.identity;
    private bool playRootRotationCached = false;

    // ============================================================
    // Properties
    // ============================================================

    public CourtSide CurrentCourtSide => currentCourtSide;
    public PlayPhase StartPhase => startPhase;
    public PlayPhase EndPhase => endPhase;
    public TossSide CurrentTossSide => selectedTossSide;
    public SpikeCourse CurrentSpikeCourse => selectedSpikeCourse;
    public float SpikeSpeedKmh => spikeSpeedKmh;
    public float SpikeFadeDelay => spikeFadeDelay;
    public float BlockExitSpeedKmh => blockExitSpeedKmh;
    public float BlockSpinRpm => blockSpinRpm;
    public float BlockFadeDelay => blockFadeDelay;

    public BlockMode CurrentBlockMode =>
        endPhase == PlayPhase.Block ? BlockMode.Block : BlockMode.None;

    public float TossTargetZOffset => tossTargetZOffset;
    public float SetterTossApexHeight => setApexHeight;
    public ServeStartPosition CurrentServeStart => selectedServeStart;
    public ServeTargetPosition CurrentServeTarget => selectedServeTarget;
    public float ServeTossHeight => tossHeight;
    public float ServeHitHeight => serveHitHeight;
    public float ServeTossForwardDistance => tossForwardDistance;
    public float ServeSpeedKmh => serveSpeedKmh;
    public float SpikeServeLaunchAngle => spikeServeLaunchAngle;
    public float ServeFadeDelay => serveFadeDelay;
    public bool ExperimentFadeEnabled => enableExperimentFade;

    public bool DynamicShiftEnabled =>
        dhitController != null && dhitController.IsDynamic;

    public float BaseShiftPixels =>
        dhitController != null
            ? dhitController.BaseShiftPixels
            : imageController != null
                ? imageController.ShiftPixels
                : 0.0f;

    public float ReceiveApexHeight => receiveApexHeight;
    public float ReceiveBackspinRpm => receiveBackspinRpm;
    public int ReceiveContactFixedFrames => receiveContactFixedFrames;

    public Vector3 SetterLocalPosition =>
        setterPosition != null ? setterPosition.localPosition : Vector3.zero;

    public float SetSpinRpm => setSpinRpm;
    public int SetContactFixedFrames => setContactFixedFrames;
    public int BlockContactFixedFrames => blockContactFixedFrames;
    public float ReceiveFadeDelay => receiveFadeDelay;
    public float TossFadeDelay => tossFadeDelay;
    public float FadeDuration => fadeDuration;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Awake()
    {
        CachePlayRootRotation();
        CacheServeTargetBasePositions();
        ApplyAllServeTargetOffsets();
        CacheSpikeTargetBasePositions();
        ApplyAllSpikeTargetOffsets();
        CacheBlockLandingPointBasePositions();
    }

    private void Start()
    {
        ApplyCourtSide();
        ValidateSimulationRange();

        if (spawnOnStart)
            ResetSelectedSimulation();
    }

    private void Update()
    {
        if (!enableKeyboardDebug)
            return;

        if (EventSystem.current != null)
        {
            GameObject selectedObject =
                EventSystem.current.currentSelectedGameObject;

            if (selectedObject != null &&
                selectedObject.GetComponent<TMP_InputField>() != null)
                return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
            SelectSimulationRange(PlayPhase.Serve, PlayPhase.Serve);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            SelectSimulationRange(PlayPhase.Receive, PlayPhase.Receive);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            SelectSimulationRange(PlayPhase.Serve, PlayPhase.Receive);

        if (Input.GetKeyDown(KeyCode.Alpha4))
            SelectSimulationRange(PlayPhase.Toss, PlayPhase.Spike);

        if (Input.GetKeyDown(KeyCode.Alpha5))
            SelectSimulationRange(PlayPhase.Serve, PlayPhase.Spike);

        if (Input.GetKeyDown(KeyCode.Alpha6))
            SelectSimulationRange(PlayPhase.Serve, PlayPhase.Block);

        if (Input.GetKeyDown(KeyCode.V))
            StartSelectedSimulation();

        if (Input.GetKeyDown(KeyCode.Z))
            ResetSelectedSimulation();
    }

    // ============================================================
    // Simulation Control
    // ============================================================

    public void SelectSimulationRange(
        PlayPhase newStartPhase,
        PlayPhase newEndPhase)
    {
        if ((int)newStartPhase > (int)newEndPhase)
        {
            Debug.LogError("[RallyController] Start PhaseはEnd Phase以前にしてください。");
            return;
        }

        startPhase = newStartPhase;
        endPhase = newEndPhase;
        ResetSelectedSimulation();
    }

    public void SetStartPhase(int index)
    {
        startPhase = (PlayPhase)Mathf.Clamp(index, 0, 4);
        ValidateSimulationRange();
    }

    public void SetEndPhase(int index)
    {
        endPhase = (PlayPhase)Mathf.Clamp(index, 0, 4);
        ValidateSimulationRange();
    }

    private void ValidateSimulationRange()
    {
        if ((int)startPhase <= (int)endPhase)
            return;

        Debug.LogWarning(
            "[RallyController] Start PhaseがEnd Phaseより後なので補正します。");

        endPhase = startPhase;
    }

    private bool ShouldContinueAfter(PlayPhase completedPhase)
    {
        return (int)completedPhase < (int)endPhase;
    }

    public void StartSelectedSimulation()
    {
        ResetLandingResult();
        ValidateSimulationRange();
        ResetExperimentFade();

        if (dhitController != null)
            dhitController.BeginTrial();

        switch (startPhase)
        {
            case PlayPhase.Serve:
                SpawnBallAtSelectedStart();
                StartSpikeServeSequence();
                break;

            case PlayPhase.Receive:
                SpawnBallAtSelectedReceiver();
                StartReceiveSequence();
                break;

            case PlayPhase.Toss:
                SpawnBallAtSetter();
                StartSetterTossSequence();
                break;

            case PlayPhase.Spike:
            case PlayPhase.Block:
                SpawnBallAtSelectedTossTarget();
                StartSpikeSequence();
                break;
        }
    }

    public void ResetSelectedSimulation()
    {
        ResetLandingResult();
        ValidateSimulationRange();
        ResetExperimentFade();

        if (dhitController != null)
            dhitController.EndTrial();

        switch (startPhase)
        {
            case PlayPhase.Serve:
                SpawnBallAtSelectedStart();
                break;

            case PlayPhase.Receive:
                SpawnBallAtSelectedReceiver();
                break;

            case PlayPhase.Toss:
                SpawnBallAtSetter();
                break;

            case PlayPhase.Spike:
            case PlayPhase.Block:
                SpawnBallAtSelectedTossTarget();
                break;
        }
    }

    // ============================================================
    // Court
    // ============================================================

    private void CachePlayRootRotation()
    {
        if (playRoot == null)
        {
            Debug.LogWarning("[RallyController] PlayRootが設定されていません。");
            return;
        }

        basePlayRootRotation = playRoot.localRotation;
        playRootRotationCached = true;
    }

    private void ApplyCourtSide()
    {
        if (playRoot == null)
            return;

        if (!playRootRotationCached)
            CachePlayRootRotation();

        if (!playRootRotationCached)
            return;

        if (currentCourtSide == CourtSide.Left)
        {
            playRoot.localRotation = basePlayRootRotation;
        }
        else
        {
            playRoot.localRotation =
                basePlayRootRotation *
                Quaternion.Euler(0.0f, 180.0f, 0.0f);
        }
    }

    public void ToggleCourt()
    {
        if (playRoot == null)
        {
            Debug.LogError("[RallyController] PlayRootが設定されていません。");
            return;
        }

        currentCourtSide =
            currentCourtSide == CourtSide.Left
                ? CourtSide.Right
                : CourtSide.Left;

        ApplyCourtSide();
        ResetSelectedSimulation();
    }

    // ============================================================
    // Ball Spawn
    // ============================================================

    private GameObject SpawnBallAtPoint(Transform spawnPoint, string label)
    {
        if (ballSpawner == null)
        {
            Debug.LogError("[RallyController] BallSpawnerが設定されていません。");
            return null;
        }

        if (spawnPoint == null)
        {
            Debug.LogError("[RallyController] Spawn Pointがnullです。");
            return null;
        }

        UnbindCurrentBall();

        GameObject newBall = ballSpawner.RespawnBall(spawnPoint);

        if (newBall == null)
            return null;

        currentBallPhysics =
            newBall.GetComponent<VolleyballBallPhysics>();

        if (currentBallPhysics == null)
        {
            Debug.LogError(
                "[RallyController] VolleyballBallPhysicsがありません。");
            return null;
        }

        BindCurrentBallEvents();

        Debug.Log(
            $"[RallyController] Ball Spawn: {label}, {spawnPoint.position}");

        return newBall;
    }

    private void BindCurrentBallEvents()
    {
        if (currentBallPhysics == null)
            return;

        currentBallPhysics.OnTossApex += HandleTossApex;
        currentBallPhysics.OnServeHitPoint += HandleServeHitPoint;
        currentBallPhysics.OnServeReachedTarget += HandleServeReachedTarget;
        currentBallPhysics.OnReceiveReachedTarget += HandleReceiveReachedTarget;
        currentBallPhysics.OnSetReachedTarget += HandleSetReachedTarget;
        currentBallPhysics.OnSpikeReachedTarget += HandleSpikeReachedTarget;
        currentBallPhysics.OnFirstGroundContact += HandleFirstGroundContact;
    }

    public void SpawnBallAtSelectedStart()
    {
        Transform point = GetSelectedServeStart();

        if (point == null)
        {
            Debug.LogError("[RallyController] Serve Startがありません。");
            return;
        }

        SpawnBallAtPoint(point, "Serve Start");
    }

    public void SpawnBallAtSelectedReceiver()
    {
        Transform point = GetSelectedServeTarget();

        if (point == null)
        {
            Debug.LogError("[RallyController] Receiverがありません。");
            return;
        }

        SpawnBallAtPoint(point, "Receive Start");
    }

    private void SpawnBallAtSetter()
    {
        if (setterPosition == null)
        {
            Debug.LogError("[RallyController] Setter Positionがありません。");
            return;
        }

        SpawnBallAtPoint(setterPosition, "Setter Toss Start");
    }

    private void SpawnBallAtSelectedTossTarget()
    {
        Transform target = GetSelectedTossTarget();

        if (target == null)
        {
            Debug.LogError("[RallyController] Toss Targetがありません。");
            return;
        }

        GameObject ball = SpawnBallAtPoint(
            target,
            "Spike Contact Point");

        if (ball == null || currentBallPhysics == null)
            return;

        currentBallPhysics.PlaceAt(GetSelectedTossTargetPosition());
    }

    // ============================================================
    // Serve / Receive / Toss
    // ============================================================

    public void StartSpikeServeSequence()
    {
        if (currentBallPhysics == null)
        {
            Debug.LogWarning("[RallyController] Ballがありません。");
            return;
        }

        Transform target = GetSelectedServeTarget();

        if (target == null)
        {
            Debug.LogError("[RallyController] Receiver Targetがありません。");
            return;
        }

        Vector3 tossDirection =
            target.position - currentBallPhysics.transform.position;

        tossDirection.y = 0.0f;

        if (tossDirection.sqrMagnitude <= 0.0001f)
        {
            Debug.LogError("[RallyController] Toss Directionが無効です。");
            return;
        }

        tossDirection.Normalize();
        NotifyPhaseStarted(PlayPhase.Serve);

        currentBallPhysics.TossUp(
            tossHeight,
            serveHitHeight,
            tossForwardDistance,
            tossDirection);
    }

    private void StartReceiveSequence()
    {
        if (currentBallPhysics == null)
            return;

        if (setterPosition == null)
        {
            Debug.LogError("[RallyController] Setter Positionがありません。");
            return;
        }

        NotifyPhaseStarted(PlayPhase.Receive);

        currentBallPhysics.ReceiveToSetter(
            setterPosition,
            receiveApexHeight,
            receiveBackspinRpm,
            receiveContactFixedFrames);
    }

    private void HandleTossApex()
    {
        if (currentBallPhysics == null)
            return;

        Debug.Log(
            "[RallyController] Serve Toss Apex: " +
            currentBallPhysics.transform.position);
    }

    private void HandleServeHitPoint()
    {
        if (currentBallPhysics == null)
            return;

        Transform target = GetSelectedServeTarget();

        if (target == null)
        {
            Debug.LogError("[RallyController] Receiver Targetがありません。");
            return;
        }

        currentBallPhysics.SpikeServe(
            target,
            serveSpeedKmh,
            spikeServeLaunchAngle);
    }

    private void HandleServeReachedTarget()
    {
        if (currentBallPhysics == null)
            return;

        if (!ShouldContinueAfter(PlayPhase.Serve))
        {
            Debug.Log("[RallyController] Finished at Serve.");
            return;
        }

        StartReceiveSequence();
    }

    private void HandleReceiveReachedTarget()
    {
        if (currentBallPhysics == null)
            return;

        if (!ShouldContinueAfter(PlayPhase.Receive))
        {
            Debug.Log("[RallyController] Finished at Receive.");
            return;
        }

        StartSetterTossSequence();
    }

    private void StartSetterTossSequence()
    {
        if (currentBallPhysics == null)
            return;

        Transform target = GetSelectedTossTarget();

        if (target == null)
        {
            Debug.LogError("[RallyController] Toss Targetがありません。");
            return;
        }

        NotifyPhaseStarted(PlayPhase.Toss);

        currentBallPhysics.SetToTarget(
            GetSelectedTossTargetPosition(),
            setApexHeight,
            setSpinRpm,
            setContactFixedFrames);
    }

    private void HandleSetReachedTarget()
    {
        if (currentBallPhysics == null)
            return;

        if (!ShouldContinueAfter(PlayPhase.Toss))
        {
            Debug.Log("[RallyController] Finished at Toss.");
            return;
        }

        StartSpikeSequence();
    }

    // ============================================================
    // Spike / Block
    // ============================================================

    private void StartSpikeSequence()
    {
        if (currentBallPhysics == null)
            return;

        if (endPhase == PlayPhase.Block)
        {
            Transform contact = GetSelectedBlockContactPoint();
            Transform landing = GetSelectedBlockLandingPoint();

            if (contact == null || landing == null)
            {
                Debug.LogError(
                    "[RallyController] Block Contact/Landing Pointがありません。");
                return;
            }

            Debug.Log(
                "[RallyController] Block Sequence Start\n" +
                $"Contact = {contact.position}\n" +
                $"Landing = {landing.position}");

            NotifyPhaseStarted(PlayPhase.Block);

            currentBallPhysics.SpikeWithBlock(
                contact,
                landing,
                spikeSpeedKmh,
                blockExitSpeedKmh,
                blockSpinRpm,
                blockContactFixedFrames);

            return;
        }

        Transform spikeTarget = GetSelectedSpikeTarget();

        if (spikeTarget == null)
        {
            Debug.LogError("[RallyController] Spike Targetがありません。");
            return;
        }

        NotifyPhaseStarted(PlayPhase.Spike);

        currentBallPhysics.Spike(
            spikeTarget,
            spikeSpeedKmh);
    }

    private void HandleSpikeReachedTarget()
    {
        if (currentBallPhysics == null)
            return;

        Debug.Log(
            endPhase == PlayPhase.Block
                ? "[RallyController] Finished at Block."
                : "[RallyController] Finished at Spike.");
    }

    // ============================================================
    // First Landing Judgement
    // ============================================================

    private void ResetLandingResult()
    {
        hasLandingResult = false;
        lastLandingIsIn = false;
        lastLandingPointWorld = Vector3.zero;
        lastLandingBallRadiusMeters = 0.0f;
    }

    private void HandleFirstGroundContact(Vector3 groundPoint, float radius)
    {
        if (hasLandingResult) return;

        hasLandingResult = true;
        lastLandingPointWorld = groundPoint;
        lastLandingBallRadiusMeters = Mathf.Max(0.0f, radius);
        lastLandingIsIn = IsGroundContactInCourt(groundPoint, radius);

        Debug.Log(
            $"[RallyController] First landing: " +
            $"{(lastLandingIsIn ? "IN" : "OUT")} | " +
            $"XZ=({groundPoint.x:F3}, {groundPoint.z:F3}) m | " +
            $"radius={lastLandingBallRadiusMeters:F3} m");

        OnCourtLandingJudged?.Invoke(lastLandingIsIn, groundPoint);
    }

    /// <summary>
    /// A circle of radius r (the ball silhouette projected onto XZ) is
    /// IN if it overlaps the court rectangle even at a single boundary
    /// point. Includes the corners; no Collider is needed for this test.
    /// </summary>
    public bool IsGroundContactInCourt(Vector3 groundPoint, float radius)
    {
        float halfWidth = Mathf.Max(0.0f, courtWidthMeters) * 0.5f;
        float halfLength = Mathf.Max(0.0f, courtLengthMeters) * 0.5f;
        float r = Mathf.Max(0.0f, radius);

        float distanceOutsideX =
            Mathf.Max(Mathf.Abs(groundPoint.x) - halfWidth, 0.0f);
        float distanceOutsideZ =
            Mathf.Max(Mathf.Abs(groundPoint.z) - halfLength, 0.0f);

        return distanceOutsideX * distanceOutsideX +
               distanceOutsideZ * distanceOutsideZ <= r * r + 0.0000001f;
    }

    // ============================================================
    // JSON Capture
    // ============================================================

    public RallySettingsData CreateCurrentRallySettings()
    {
        DhitSettings snapshot =
            dhitController != null
                ? dhitController.CreateSettingsSnapshot()
                : null;

        return new RallySettingsData
        {
            version = 6,

            startPhaseIndex = (int)startPhase,
            endPhaseIndex = (int)endPhase,
            courtSideIndex = (int)currentCourtSide,

            serveStartIndex = (int)selectedServeStart,
            serveTargetIndex = (int)selectedServeTarget,

            serveTargetLeftOffset = serveTargetLeftOffset,
            serveTargetCenterOffset = serveTargetCenterOffset,
            serveTargetRightOffset = serveTargetRightOffset,

            receiveApexHeight = receiveApexHeight,
            receiveBackspinRpm = receiveBackspinRpm,
            receiveContactFixedFrames = receiveContactFixedFrames,

            setterLocalPosition = SetterLocalPosition,

            tossSideIndex = (int)selectedTossSide,
            tossTargetZOffset = tossTargetZOffset,
            setApexHeight = setApexHeight,
            setSpinRpm = setSpinRpm,
            setContactFixedFrames = setContactFixedFrames,

            spikeCourseIndex = (int)selectedSpikeCourse,
            spikeSpeedKmh = spikeSpeedKmh,

            spikeTargetLeftDistanceOffsetMeters =
                spikeTargetLeftDistanceOffsetMeters,

            spikeTargetRightDistanceOffsetMeters =
                spikeTargetRightDistanceOffsetMeters,

            spikeTargetLeftOffset = spikeTargetLeftOffset,
            spikeTargetRightOffset = spikeTargetRightOffset,

            blockContactFixedFrames = blockContactFixedFrames,
            blockExitSpeedKmh = blockExitSpeedKmh,
            blockSpinRpm = blockSpinRpm,

            // Version 6: 通過点も保存
            blockContactPointLeftLocalPosition =
                GetBlockContactPointLocalPosition(0),

            blockContactPointRightLocalPosition =
                GetBlockContactPointLocalPosition(1),

            blockLandingPointLeftLocalPosition =
                GetBlockLandingPointLocalPosition(0),

            blockLandingPointRightLocalPosition =
                GetBlockLandingPointLocalPosition(1),

            blockLandingPointLeftOffset = Vector3.zero,
            blockLandingPointRightOffset = Vector3.zero,

            tossHeight = tossHeight,
            serveHitHeight = serveHitHeight,
            tossForwardDistance = tossForwardDistance,
            serveSpeedKmh = serveSpeedKmh,
            spikeServeLaunchAngle = spikeServeLaunchAngle,

            enableExperimentFade = enableExperimentFade,
            serveFadeDelay = serveFadeDelay,
            receiveFadeDelay = receiveFadeDelay,
            tossFadeDelay = tossFadeDelay,
            spikeFadeDelay = spikeFadeDelay,
            blockFadeDelay = blockFadeDelay,
            fadeDuration = fadeDuration,

            hasDhitSettings = dhitController != null,
            baseShiftPixels = BaseShiftPixels,

            dynamicShiftEnabled =
                dhitController != null && dhitController.IsDynamic,

            dhitSettings = snapshot
        };
    }

    // ============================================================
    // JSON Apply
    // ============================================================

    public void ApplyRallySettings(
        RallySettingsData data,
        bool resetSimulation = true)
    {
        if (data == null)
        {
            Debug.LogError("[RallyController] RallySettingsDataがnullです。");
            return;
        }

        startPhase =
            (PlayPhase)Mathf.Clamp(data.startPhaseIndex, 0, 4);

        endPhase =
            (PlayPhase)Mathf.Clamp(data.endPhaseIndex, 0, 4);

        currentCourtSide =
            (CourtSide)Mathf.Clamp(data.courtSideIndex, 0, 1);

        selectedServeStart =
            (ServeStartPosition)Mathf.Clamp(data.serveStartIndex, 0, 2);

        selectedServeTarget =
            (ServeTargetPosition)Mathf.Clamp(data.serveTargetIndex, 0, 2);

        serveTargetLeftOffset = data.serveTargetLeftOffset;
        serveTargetCenterOffset = data.serveTargetCenterOffset;
        serveTargetRightOffset = data.serveTargetRightOffset;

        receiveApexHeight = data.receiveApexHeight;
        receiveBackspinRpm = data.receiveBackspinRpm;
        receiveContactFixedFrames =
            Mathf.Max(0, data.receiveContactFixedFrames);

        if (data.version >= 3 && setterPosition != null)
            setterPosition.localPosition = data.setterLocalPosition;

        selectedTossSide =
            (TossSide)Mathf.Clamp(data.tossSideIndex, 0, 1);

        tossTargetZOffset = data.tossTargetZOffset;
        setApexHeight = data.setApexHeight;
        setSpinRpm = data.setSpinRpm;
        setContactFixedFrames =
            Mathf.Max(0, data.setContactFixedFrames);

        selectedSpikeCourse =
            (SpikeCourse)Mathf.Clamp(data.spikeCourseIndex, 0, 1);

        spikeSpeedKmh = Mathf.Max(0.0f, data.spikeSpeedKmh);

        if (data.version >= 4)
        {
            spikeTargetLeftDistanceOffsetMeters =
                data.spikeTargetLeftDistanceOffsetMeters;

            spikeTargetRightDistanceOffsetMeters =
                data.spikeTargetRightDistanceOffsetMeters;

            spikeTargetLeftOffset = Vector3.zero;
            spikeTargetRightOffset = Vector3.zero;
        }
        else
        {
            spikeTargetLeftDistanceOffsetMeters = 0.0f;
            spikeTargetRightDistanceOffsetMeters = 0.0f;
            spikeTargetLeftOffset = data.spikeTargetLeftOffset;
            spikeTargetRightOffset = data.spikeTargetRightOffset;
        }

        blockContactFixedFrames =
            Mathf.Max(0, data.blockContactFixedFrames);

        blockExitSpeedKmh =
            Mathf.Max(0.1f, data.blockExitSpeedKmh);

        blockSpinRpm = data.blockSpinRpm;

        // Version 6: Block Contact Pointを復元
        if (data.version >= 6)
        {
            SetBlockContactPointLocalPosition(
                0,
                data.blockContactPointLeftLocalPosition);

            SetBlockContactPointLocalPosition(
                1,
                data.blockContactPointRightLocalPosition);
        }

        // Version 5以降: 着地点を復元
        if (data.version >= 5)
        {
            blockLandingPointLeftOffset = Vector3.zero;
            blockLandingPointRightOffset = Vector3.zero;

            SetBlockLandingPointLocalPosition(
                0,
                data.blockLandingPointLeftLocalPosition);

            SetBlockLandingPointLocalPosition(
                1,
                data.blockLandingPointRightLocalPosition);
        }
        else
        {
            blockLandingPointLeftOffset =
                data.blockLandingPointLeftOffset;

            blockLandingPointRightOffset =
                data.blockLandingPointRightOffset;
        }

        tossHeight = Mathf.Max(0.0f, data.tossHeight);
        serveHitHeight = data.serveHitHeight;

        tossForwardDistance =
            Mathf.Max(0.0f, data.tossForwardDistance);

        serveSpeedKmh = Mathf.Max(0.0f, data.serveSpeedKmh);
        spikeServeLaunchAngle = data.spikeServeLaunchAngle;

        enableExperimentFade = data.enableExperimentFade;
        serveFadeDelay = Mathf.Max(0.0f, data.serveFadeDelay);
        receiveFadeDelay = Mathf.Max(0.0f, data.receiveFadeDelay);
        tossFadeDelay = Mathf.Max(0.0f, data.tossFadeDelay);
        spikeFadeDelay = Mathf.Max(0.0f, data.spikeFadeDelay);
        blockFadeDelay = Mathf.Max(0.0f, data.blockFadeDelay);
        fadeDuration = Mathf.Max(0.0f, data.fadeDuration);

        if (data.hasDhitSettings)
        {
            if (dhitController != null)
            {
                dhitController.ApplySavedState(
                    data.baseShiftPixels,
                    data.dynamicShiftEnabled,
                    data.dhitSettings);
            }
            else if (imageController != null)
            {
                imageController.SetShiftPixels(data.baseShiftPixels);
            }
        }

        ValidateSimulationRange();
        ApplyCourtSide();
        ApplyAllServeTargetOffsets();
        ApplyAllSpikeTargetOffsets();

        if (data.version < 5)
            ApplyAllBlockLandingPointOffsets();

        ResetExperimentFade();

        if (resetSimulation)
            ResetSelectedSimulation();
    }

    // ============================================================
    // JSON Save / Load
    // ============================================================

    public bool SaveRallyJson()
    {
        string path = RequestRallySavePath();

        if (string.IsNullOrWhiteSpace(path))
            return false;

        return SaveRallyJsonToPath(path);
    }

    public bool LoadRallyJson()
    {
        string path = RequestRallyLoadPath();

        if (string.IsNullOrWhiteSpace(path))
            return false;

        return LoadRallyJsonFromPath(path, false);
    }

    public bool LoadRallyJsonAndPlay()
    {
        string path = RequestRallyLoadPath();

        if (string.IsNullOrWhiteSpace(path))
            return false;

        return LoadRallyJsonFromPath(path, true);
    }

    public bool SaveRallyJsonToPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            RallySettingsData data = CreateCurrentRallySettings();
            string json = JsonUtility.ToJson(data, true);

            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, json);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif

            Debug.Log("[RallyController] Rally Settings Saved: " + path);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[RallyController] JSON Save Failed: " + ex);
            return false;
        }
    }

    public bool LoadRallyJsonFromPath(
        string path,
        bool playAfterLoad = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (!File.Exists(path))
        {
            Debug.LogError("[RallyController] JSON File Not Found: " + path);
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);

            RallySettingsData data =
                JsonUtility.FromJson<RallySettingsData>(json);

            if (data == null)
            {
                Debug.LogError("[RallyController] Invalid JSON.");
                return false;
            }

            ApplyRallySettings(data, true);

            Debug.Log("[RallyController] Rally Settings Loaded: " + path);

            if (playAfterLoad)
                StartSelectedSimulation();

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[RallyController] JSON Load Failed: " + ex);
            return false;
        }
    }

    // ============================================================
    // UI Button Methods
    // ============================================================

    public void OnClickSaveRallyJson()
    {
        Debug.Log("[RallyController] Save button clicked.", this);
        SaveRallyJson();
    }

    public void OnClickLoadRallyJson()
    {
        Debug.Log("[RallyController] Load button clicked.", this);
        LoadRallyJson();
    }

    public void OnClickLoadRallyJsonAndPlay()
    {
        Debug.Log("[RallyController] Load & Play clicked.", this);
        LoadRallyJsonAndPlay();
    }

    // ============================================================
    // Standalone File Browser
    // ============================================================

    private string RequestRallySavePath()
    {
        Debug.Log("[RallyController] Save dialog requested.", this);

        string path = SFB.StandaloneFileBrowser.SaveFilePanel(
            "Save Rally Settings",
            GetRallyJsonDirectory(),
            Path.GetFileNameWithoutExtension(
                GetDefaultRallyJsonFileName()),
            "json");

        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return path.EndsWith(
            ".json",
            StringComparison.OrdinalIgnoreCase)
            ? path
            : path + ".json";
    }

    private string RequestRallyLoadPath()
    {
        Debug.Log("[RallyController] Load dialog requested.", this);

        string[] paths = SFB.StandaloneFileBrowser.OpenFilePanel(
            "Load Rally Settings",
            GetRallyJsonDirectory(),
            "json",
            false);

        if (paths == null || paths.Length == 0)
            return string.Empty;

        return paths[0];
    }

    public string GetRallyJsonDirectory()
    {
        string relativeFolder =
            string.IsNullOrWhiteSpace(rallyJsonRelativeFolder)
                ? Path.Combine("Experiment", "Rallies")
                : rallyJsonRelativeFolder.Trim()
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);

#if UNITY_EDITOR
        string directory =
            Path.Combine(Application.dataPath, relativeFolder);
#else
        string directory =
            Path.Combine(Application.persistentDataPath, relativeFolder);
#endif

        Directory.CreateDirectory(directory);
        return Path.GetFullPath(directory);
    }

    public string GetDefaultRallyJsonPath()
    {
        return Path.Combine(
            GetRallyJsonDirectory(),
            GetDefaultRallyJsonFileName());
    }

    private static bool IsPathInsideDirectory(
        string path,
        string directory)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            string.IsNullOrWhiteSpace(directory))
            return false;

        string fullPath = Path.GetFullPath(path);

        string fullDirectory =
            Path.GetFullPath(directory).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        return fullPath.StartsWith(
            fullDirectory,
            StringComparison.OrdinalIgnoreCase);
    }

    private string GetDefaultRallyJsonFileName()
    {
        string fileName =
            string.IsNullOrWhiteSpace(defaultRallyJsonFileName)
                ? "rally_settings.json"
                : defaultRallyJsonFileName.Trim();

        if (!fileName.EndsWith(
            ".json",
            StringComparison.OrdinalIgnoreCase))
            fileName += ".json";

        return fileName;
    }

    // ============================================================
    // External Control: Serve
    // ============================================================

    public void SetServeStart(int index)
    {
        selectedServeStart =
            (ServeStartPosition)Mathf.Clamp(index, 0, 2);
    }

    public void SetServeStartAndRespawn(int index)
    {
        SetServeStart(index);
        SpawnBallAtSelectedStart();
    }

    public void SetServeTarget(int index)
    {
        selectedServeTarget =
            (ServeTargetPosition)Mathf.Clamp(index, 0, 2);
    }

    public void SetServeTargetOffset(int index, Vector3 offset)
    {
        index = Mathf.Clamp(index, 0, 2);

        ServeTargetPosition target =
            (ServeTargetPosition)index;

        switch (target)
        {
            case ServeTargetPosition.Left:
                serveTargetLeftOffset = offset;
                break;

            case ServeTargetPosition.Center:
                serveTargetCenterOffset = offset;
                break;

            case ServeTargetPosition.Right:
                serveTargetRightOffset = offset;
                break;
        }

        ApplyServeTargetOffset(target);
    }

    public Vector3 GetServeTargetOffset(int index)
    {
        switch ((ServeTargetPosition)Mathf.Clamp(index, 0, 2))
        {
            case ServeTargetPosition.Left:
                return serveTargetLeftOffset;

            case ServeTargetPosition.Center:
                return serveTargetCenterOffset;

            case ServeTargetPosition.Right:
                return serveTargetRightOffset;

            default:
                return Vector3.zero;
        }
    }

    public void ResetServeTargetOffsets()
    {
        serveTargetLeftOffset = Vector3.zero;
        serveTargetCenterOffset = Vector3.zero;
        serveTargetRightOffset = Vector3.zero;

        ApplyAllServeTargetOffsets();
    }

    // ============================================================
    // External Control: Setter / Toss
    // ============================================================

    public void SetSetterLocalPosition(Vector3 localPosition)
    {
        if (setterPosition == null)
        {
            Debug.LogWarning(
                "[RallyController] Setter Positionがありません。", this);
            return;
        }

        setterPosition.localPosition = localPosition;
    }

    public void SetTossSide(int index)
    {
        selectedTossSide =
            (TossSide)Mathf.Clamp(index, 0, 1);

        ApplyAllSpikeTargetOffsets();
    }

    public void SetTossTargetZOffset(float offset)
    {
        tossTargetZOffset = offset;
        ApplyAllSpikeTargetOffsets();
    }

    public void SetSetApexHeight(float height)
    {
        setApexHeight = height;
    }

    public void SetSetSpinRpm(float rpm)
    {
        setSpinRpm = rpm;
    }

    public void SetSetContactFixedFrames(int frames)
    {
        setContactFixedFrames = Mathf.Max(0, frames);
    }

    // ============================================================
    // External Control: Spike
    // ============================================================

    public void SetSpikeCourse(int index)
    {
        selectedSpikeCourse =
            (SpikeCourse)Mathf.Clamp(index, 0, 1);
    }

    public void SetSpikeTargetDistanceOffset(
        int index,
        float distanceMeters)
    {
        index = Mathf.Clamp(index, 0, 1);

        if (index == 0)
        {
            spikeTargetLeftDistanceOffsetMeters = distanceMeters;
            spikeTargetLeftOffset = Vector3.zero;
        }
        else
        {
            spikeTargetRightDistanceOffsetMeters = distanceMeters;
            spikeTargetRightOffset = Vector3.zero;
        }

        ApplySpikeTargetOffset(index);
    }

    public float GetSpikeTargetDistanceOffset(int index)
    {
        return Mathf.Clamp(index, 0, 1) == 0
            ? spikeTargetLeftDistanceOffsetMeters
            : spikeTargetRightDistanceOffsetMeters;
    }

    public void SetSpikeTargetOffset(int index, Vector3 offset)
    {
        index = Mathf.Clamp(index, 0, 1);

        if (index == 0)
        {
            spikeTargetLeftDistanceOffsetMeters = 0.0f;
            spikeTargetLeftOffset = offset;
        }
        else
        {
            spikeTargetRightDistanceOffsetMeters = 0.0f;
            spikeTargetRightOffset = offset;
        }

        ApplySpikeTargetOffset(index);
    }

    public Vector3 GetSpikeTargetOffset(int index)
    {
        return Mathf.Clamp(index, 0, 1) == 0
            ? spikeTargetLeftOffset
            : spikeTargetRightOffset;
    }

    public void ResetSpikeTargetOffsets()
    {
        spikeTargetLeftDistanceOffsetMeters = 0.0f;
        spikeTargetRightDistanceOffsetMeters = 0.0f;
        spikeTargetLeftOffset = Vector3.zero;
        spikeTargetRightOffset = Vector3.zero;

        ApplyAllSpikeTargetOffsets();
    }

    // ============================================================
    // External Control: Block / Dig
    // ============================================================

    public void SetBlockMode(int index)
    {
        endPhase = Mathf.Clamp(index, 0, 1) == 1
            ? PlayPhase.Block
            : PlayPhase.Spike;

        ValidateSimulationRange();
    }

    public void SetBlockEnabled(bool enabled)
    {
        endPhase = enabled
            ? PlayPhase.Block
            : PlayPhase.Spike;

        ValidateSimulationRange();
    }

    public void SetBlockContactFixedFrames(int frames)
    {
        blockContactFixedFrames = Mathf.Max(0, frames);
    }

    public void SetBlockExitSpeed(float speedKmh)
    {
        blockExitSpeedKmh = Mathf.Max(0.1f, speedKmh);
    }

    public void SetBlockSpinRpm(float rpm)
    {
        blockSpinRpm = rpm;
    }

    // ============================================================
    // NEW: Block Contact Point Local Position
    // 0 = Left, 1 = Right
    // ============================================================

    public void SetBlockContactPointLocalPosition(
        int index,
        Vector3 localPosition)
    {
        index = Mathf.Clamp(index, 0, 1);

        Transform target = index == 0
            ? blockContactPointLeft
            : blockContactPointRight;

        if (target == null)
        {
            Debug.LogWarning(
                "[RallyController] Block Contact Point " +
                (index == 0 ? "Left" : "Right") +
                " が設定されていません。",
                this);
            return;
        }

        target.localPosition = localPosition;
    }

    public Vector3 GetBlockContactPointLocalPosition(int index)
    {
        index = Mathf.Clamp(index, 0, 1);

        Transform target = index == 0
            ? blockContactPointLeft
            : blockContactPointRight;

        return target != null
            ? target.localPosition
            : Vector3.zero;
    }

    // ============================================================
    // Block Landing Point Local Position
    // ============================================================

    public void SetBlockLandingPointLocalPosition(
        int index,
        Vector3 localPosition)
    {
        index = Mathf.Clamp(index, 0, 1);

        Transform target = index == 0
            ? blockLandingPointLeft
            : blockLandingPointRight;

        if (target == null)
        {
            Debug.LogWarning(
                "[RallyController] Block Landing Point " +
                (index == 0 ? "Left" : "Right") +
                " が設定されていません。",
                this);
            return;
        }

        target.localPosition = localPosition;

        if (index == 0)
            blockLandingPointLeftOffset = Vector3.zero;
        else
            blockLandingPointRightOffset = Vector3.zero;
    }

    public Vector3 GetBlockLandingPointLocalPosition(int index)
    {
        index = Mathf.Clamp(index, 0, 1);

        Transform target = index == 0
            ? blockLandingPointLeft
            : blockLandingPointRight;

        return target != null
            ? target.localPosition
            : Vector3.zero;
    }

    public void SetBlockLandingPointOffset(
        int index,
        Vector3 offset)
    {
        index = Mathf.Clamp(index, 0, 1);

        if (index == 0)
            blockLandingPointLeftOffset = offset;
        else
            blockLandingPointRightOffset = offset;

        ApplyBlockLandingPointOffset(index);
    }

    public Vector3 GetBlockLandingPointOffset(int index)
    {
        return Mathf.Clamp(index, 0, 1) == 0
            ? blockLandingPointLeftOffset
            : blockLandingPointRightOffset;
    }

    public void ResetBlockLandingPointOffsets()
    {
        blockLandingPointLeftOffset = Vector3.zero;
        blockLandingPointRightOffset = Vector3.zero;

        ApplyAllBlockLandingPointOffsets();
    }

    // ============================================================
    // Other Parameters
    // ============================================================

    public void SetSpikeSpeed(float speedKmh)
    {
        spikeSpeedKmh = Mathf.Max(0.0f, speedKmh);
    }

    public void SetServeSpeed(float speedKmh)
    {
        serveSpeedKmh = Mathf.Max(0.0f, speedKmh);
    }

    public void SetSpikeServeLaunchAngle(float angleDeg)
    {
        spikeServeLaunchAngle = angleDeg;
    }

    public void SetTossHeight(float height)
    {
        tossHeight = Mathf.Max(0.0f, height);
    }

    public void SetServeHitHeight(float height)
    {
        serveHitHeight = height;
    }

    public void SetTossForwardDistance(float distance)
    {
        tossForwardDistance = Mathf.Max(0.0f, distance);
    }

    public void SetReceiveApexHeight(float height)
    {
        receiveApexHeight = height;
    }

    public void SetReceiveBackspinRpm(float rpm)
    {
        receiveBackspinRpm = rpm;
    }

    public void SetReceiveContactFixedFrames(int frames)
    {
        receiveContactFixedFrames = Mathf.Max(0, frames);
    }

    // ============================================================
    // Experiment Fade
    // ============================================================

    public void SetExperimentFadeEnabled(bool enabled)
    {
        enableExperimentFade = enabled;

        if (!enableExperimentFade)
            ResetExperimentFade();
    }

    public void SetServeFadeDelay(float seconds)
    {
        serveFadeDelay = Mathf.Max(0.0f, seconds);
    }

    public void SetReceiveFadeDelay(float seconds)
    {
        receiveFadeDelay = Mathf.Max(0.0f, seconds);
    }

    public void SetTossFadeDelay(float seconds)
    {
        tossFadeDelay = Mathf.Max(0.0f, seconds);
    }

    public void SetSpikeFadeDelay(float seconds)
    {
        spikeFadeDelay = Mathf.Max(0.0f, seconds);
    }

    public void SetBlockFadeDelay(float seconds)
    {
        blockFadeDelay = Mathf.Max(0.0f, seconds);
    }

    public void SetFadeDuration(float seconds)
    {
        fadeDuration = Mathf.Max(0.0f, seconds);
    }

    private void NotifyPhaseStarted(PlayPhase phase)
    {
        if (!enableExperimentFade || phase != endPhase)
            return;

        if (imageController == null)
        {
            Debug.LogWarning(
                "[RallyController] Fade用ImageControllerがありません。");
            return;
        }

        CancelExperimentFadeTimer();

        float delay = GetFadeDelay(phase);

        experimentFadeTimerCoroutine =
            StartCoroutine(FadeAfterDelay(phase, delay));
    }

    private IEnumerator FadeAfterDelay(PlayPhase phase, float delay)
    {
        if (delay > 0.0f)
            yield return new WaitForSecondsRealtime(delay);

        experimentFadeTimerCoroutine = null;

        if (!enableExperimentFade ||
            imageController == null ||
            phase != endPhase)
            yield break;

        imageController.FadeOut(fadeDuration);
    }

    private float GetFadeDelay(PlayPhase phase)
    {
        switch (phase)
        {
            case PlayPhase.Serve:
                return Mathf.Max(0.0f, serveFadeDelay);

            case PlayPhase.Receive:
                return Mathf.Max(0.0f, receiveFadeDelay);

            case PlayPhase.Toss:
                return Mathf.Max(0.0f, tossFadeDelay);

            case PlayPhase.Spike:
                return Mathf.Max(0.0f, spikeFadeDelay);

            case PlayPhase.Block:
                return Mathf.Max(0.0f, blockFadeDelay);

            default:
                return 0.0f;
        }
    }

    private void CancelExperimentFadeTimer()
    {
        if (experimentFadeTimerCoroutine == null)
            return;

        StopCoroutine(experimentFadeTimerCoroutine);
        experimentFadeTimerCoroutine = null;
    }

    public void ResetExperimentFade()
    {
        CancelExperimentFadeTimer();

        if (imageController != null)
            imageController.ResetFade();
    }

    // ============================================================
    // Serve Start Selection
    // ============================================================

    private Transform GetSelectedServeStart()
    {
        switch (selectedServeStart)
        {
            case ServeStartPosition.Left:
                return serveStartLeft;

            case ServeStartPosition.Center:
                return serveStartCenter;

            case ServeStartPosition.Right:
                return serveStartRight;

            default:
                return serveStartCenter;
        }
    }

    // ============================================================
    // Serve Target Positions
    // ============================================================

    private void CacheServeTargetBasePositions()
    {
        if (serveTargetLeft != null)
            serveTargetLeftBaseLocalPosition = serveTargetLeft.localPosition;

        if (serveTargetCenter != null)
            serveTargetCenterBaseLocalPosition = serveTargetCenter.localPosition;

        if (serveTargetRight != null)
            serveTargetRightBaseLocalPosition = serveTargetRight.localPosition;

        serveTargetBasePositionsCached = true;
    }

    private void ApplyAllServeTargetOffsets()
    {
        ApplyServeTargetOffset(ServeTargetPosition.Left);
        ApplyServeTargetOffset(ServeTargetPosition.Center);
        ApplyServeTargetOffset(ServeTargetPosition.Right);
    }

    private void ApplyServeTargetOffset(ServeTargetPosition target)
    {
        if (!serveTargetBasePositionsCached)
            CacheServeTargetBasePositions();

        switch (target)
        {
            case ServeTargetPosition.Left:
                if (serveTargetLeft != null)
                    serveTargetLeft.localPosition =
                        serveTargetLeftBaseLocalPosition +
                        serveTargetLeftOffset;
                break;

            case ServeTargetPosition.Center:
                if (serveTargetCenter != null)
                    serveTargetCenter.localPosition =
                        serveTargetCenterBaseLocalPosition +
                        serveTargetCenterOffset;
                break;

            case ServeTargetPosition.Right:
                if (serveTargetRight != null)
                    serveTargetRight.localPosition =
                        serveTargetRightBaseLocalPosition +
                        serveTargetRightOffset;
                break;
        }
    }

    private Transform GetSelectedServeTarget()
    {
        switch (selectedServeTarget)
        {
            case ServeTargetPosition.Left:
                return serveTargetLeft;

            case ServeTargetPosition.Center:
                return serveTargetCenter;

            case ServeTargetPosition.Right:
                return serveTargetRight;

            default:
                return serveTargetCenter;
        }
    }

    // ============================================================
    // Toss Target Selection
    // ============================================================

    private Transform GetSelectedTossTarget()
    {
        return selectedTossSide == TossSide.Left
            ? leftTossTarget
            : rightTossTarget;
    }

    private Vector3 GetSelectedTossTargetPosition()
    {
        Transform target = GetSelectedTossTarget();

        if (target == null)
            return Vector3.zero;

        Vector3 zDirection = Vector3.forward;

        if (playRoot != null)
            zDirection = playRoot.TransformDirection(Vector3.forward);

        return target.position + zDirection * tossTargetZOffset;
    }

    // ============================================================
    // Spike Target Positions
    // ============================================================

    private void CacheSpikeTargetBasePositions()
    {
        if (spikeTargetLeft != null)
            spikeTargetLeftBaseLocalPosition = spikeTargetLeft.localPosition;

        if (spikeTargetRight != null)
            spikeTargetRightBaseLocalPosition = spikeTargetRight.localPosition;

        spikeTargetBasePositionsCached = true;
    }

    private void ApplyAllSpikeTargetOffsets()
    {
        ApplySpikeTargetOffset(0);
        ApplySpikeTargetOffset(1);
    }

    private void ApplySpikeTargetOffset(int index)
    {
        if (!spikeTargetBasePositionsCached)
            CacheSpikeTargetBasePositions();

        index = Mathf.Clamp(index, 0, 1);

        Transform target = index == 0
            ? spikeTargetLeft
            : spikeTargetRight;

        if (target == null)
            return;

        Vector3 baseLocal = index == 0
            ? spikeTargetLeftBaseLocalPosition
            : spikeTargetRightBaseLocalPosition;

        Vector3 legacyOffset = index == 0
            ? spikeTargetLeftOffset
            : spikeTargetRightOffset;

        float distanceOffset = index == 0
            ? spikeTargetLeftDistanceOffsetMeters
            : spikeTargetRightDistanceOffsetMeters;

        Vector3 localPosition = baseLocal + legacyOffset;
        Transform parent = target.parent;

        Vector3 worldPosition = parent != null
            ? parent.TransformPoint(localPosition)
            : localPosition;

        if (Mathf.Abs(distanceOffset) <= 0.000001f)
        {
            target.localPosition = localPosition;
            return;
        }

        Vector3 contactPosition = GetSelectedTossTargetPosition();
        Vector3 direction = worldPosition - contactPosition;
        direction.y = 0.0f;

        if (direction.sqrMagnitude <= 0.000001f)
        {
            Debug.LogWarning(
                "[RallyController] Invalid Spike Target direction.", this);

            target.localPosition = localPosition;
            return;
        }

        direction.Normalize();

        Vector3 finalWorld =
            worldPosition + direction * distanceOffset;

        finalWorld.y = worldPosition.y;

        if (parent != null)
            target.localPosition = parent.InverseTransformPoint(finalWorld);
        else
            target.position = finalWorld;
    }

    private Transform GetSelectedSpikeTarget()
    {
        if (selectedTossSide == TossSide.Left)
        {
            return selectedSpikeCourse == SpikeCourse.Straight
                ? spikeTargetRight
                : spikeTargetLeft;
        }

        return selectedSpikeCourse == SpikeCourse.Straight
            ? spikeTargetLeft
            : spikeTargetRight;
    }

    // ============================================================
    // Block Landing Point Positions
    // ============================================================

    private void CacheBlockLandingPointBasePositions()
    {
        if (blockLandingPointLeft != null)
            blockLandingPointLeftBaseLocalPosition =
                blockLandingPointLeft.localPosition;

        if (blockLandingPointRight != null)
            blockLandingPointRightBaseLocalPosition =
                blockLandingPointRight.localPosition;

        blockLandingPointBasePositionsCached = true;
    }

    private void ApplyAllBlockLandingPointOffsets()
    {
        ApplyBlockLandingPointOffset(0);
        ApplyBlockLandingPointOffset(1);
    }

    private void ApplyBlockLandingPointOffset(int index)
    {
        if (!blockLandingPointBasePositionsCached)
            CacheBlockLandingPointBasePositions();

        index = Mathf.Clamp(index, 0, 1);

        if (index == 0)
        {
            if (blockLandingPointLeft != null)
            {
                blockLandingPointLeft.localPosition =
                    blockLandingPointLeftBaseLocalPosition +
                    blockLandingPointLeftOffset;
            }

            return;
        }

        if (blockLandingPointRight != null)
        {
            blockLandingPointRight.localPosition =
                blockLandingPointRightBaseLocalPosition +
                blockLandingPointRightOffset;
        }
    }

    private Transform GetSelectedBlockContactPoint()
    {
        return selectedTossSide == TossSide.Left
            ? blockContactPointLeft
            : blockContactPointRight;
    }

    private Transform GetSelectedBlockLandingPoint()
    {
        return selectedTossSide == TossSide.Left
            ? blockLandingPointLeft
            : blockLandingPointRight;
    }

    // ============================================================
    // Cleanup
    // ============================================================

    private void UnbindCurrentBall()
    {
        if (currentBallPhysics == null)
            return;

        currentBallPhysics.OnTossApex -= HandleTossApex;
        currentBallPhysics.OnServeHitPoint -= HandleServeHitPoint;
        currentBallPhysics.OnServeReachedTarget -= HandleServeReachedTarget;
        currentBallPhysics.OnReceiveReachedTarget -= HandleReceiveReachedTarget;
        currentBallPhysics.OnSetReachedTarget -= HandleSetReachedTarget;
        currentBallPhysics.OnSpikeReachedTarget -= HandleSpikeReachedTarget;
        currentBallPhysics.OnFirstGroundContact -= HandleFirstGroundContact;

        currentBallPhysics = null;
    }

    private void OnDestroy()
    {
        CancelExperimentFadeTimer();
        UnbindCurrentBall();
    }
}
