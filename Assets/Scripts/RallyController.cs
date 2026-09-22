using System.Collections;
using UnityEngine;

/// <summary>
/// ラリー全体の進行を管理する。
///
/// Phase:
/// Serve
/// ↓
/// Receive
/// ↓
/// Toss
/// ↓
/// Spike
/// ↓
/// Block (End Phase = Block のときのみ)
///
/// Toss Target:
/// LeftTossTarget / RightTossTarget
///
/// 選択したTossTargetが
/// Setter Tossの到達点
/// = Spike Contact Point
/// になる。
/// </summary>
public class RallyController : MonoBehaviour
{
    // ============================================================
    // Play Phase
    // ============================================================

    public enum PlayPhase
    {
        Serve = 0,
        Receive = 1,
        Toss = 2,
        Spike = 3,
        Block = 4
    }

    [Header("Simulation Range")]

    [Tooltip("シミュレーション開始Phase")]
    [SerializeField]
    private PlayPhase startPhase =
        PlayPhase.Serve;

    [Tooltip("シミュレーション終了Phase")]
    [SerializeField]
    private PlayPhase endPhase =
        PlayPhase.Receive;


    // ============================================================
    // Court Side
    // ============================================================

    public enum CourtSide
    {
        Left = 0,
        Right = 1
    }

    [Header("Court Side")]

    [Tooltip(
        "プレー関係のオブジェクトをまとめた親Transform。" +
        "コート中央をPivotにしてください。"
    )]
    [SerializeField]
    private Transform playRoot;

    [SerializeField]
    private CourtSide currentCourtSide =
        CourtSide.Left;


    // ============================================================
    // Serve Start
    // ============================================================

    public enum ServeStartPosition
    {
        Left = 0,
        Center = 1,
        Right = 2
    }


    // ============================================================
    // Serve Target
    // ============================================================

    public enum ServeTargetPosition
    {
        Left = 0,
        Center = 1,
        Right = 2
    }


    // ============================================================
    // Toss Side
    // ============================================================

    public enum TossSide
    {
        Left = 0,
        Right = 1
    }


    // ============================================================
    // Spike Course
    // ============================================================

    public enum SpikeCourse
    {
        Straight = 0,
        Cross = 1
    }


    // ============================================================
    // Block Mode (Legacy Compatibility)
    // ============================================================

    /// <summary>
    /// 旧UI / 既存コードとの互換用。
    /// 現在は独立したBlock状態を保持せず、
    /// End Phase == Block かどうかから導出する。
    /// </summary>
    public enum BlockMode
    {
        None = 0,
        Block = 1
    }


    // ============================================================
    // Ball Spawner
    // ============================================================

    [Header("Ball Spawner")]

    [SerializeField]
    private BallSpawner ballSpawner;


    // ============================================================
    // Serve Start Points
    // ============================================================

    [Header("Serve Start Points")]

    [SerializeField]
    private Transform serveStartLeft;

    [SerializeField]
    private Transform serveStartCenter;

    [SerializeField]
    private Transform serveStartRight;

    [Header("Selected Serve Start")]

    [SerializeField]
    private ServeStartPosition selectedServeStart =
        ServeStartPosition.Center;


    // ============================================================
    // Receiver / Serve Target
    // ============================================================

    [Header("Serve Target - Receiver Transforms")]

    [SerializeField]
    private Transform serveTargetLeft;

    [SerializeField]
    private Transform serveTargetCenter;

    [SerializeField]
    private Transform serveTargetRight;

    [Header("Selected Serve Target")]

    [SerializeField]
    private ServeTargetPosition selectedServeTarget =
        ServeTargetPosition.Center;


    // ============================================================
    // Serve Target Offset
    // ============================================================

    [Header("Serve Target Offset")]

    [Tooltip("Serve Target Left の基準位置からのLocal Offset [m]")]
    [SerializeField]
    private Vector3 serveTargetLeftOffset =
        Vector3.zero;

    [Tooltip("Serve Target Center の基準位置からのLocal Offset [m]")]
    [SerializeField]
    private Vector3 serveTargetCenterOffset =
        Vector3.zero;

    [Tooltip("Serve Target Right の基準位置からのLocal Offset [m]")]
    [SerializeField]
    private Vector3 serveTargetRightOffset =
        Vector3.zero;

    private Vector3 serveTargetLeftBaseLocalPosition;
    private Vector3 serveTargetCenterBaseLocalPosition;
    private Vector3 serveTargetRightBaseLocalPosition;

    private bool serveTargetBasePositionsCached =
        false;


    // ============================================================
    // Receive
    // ============================================================

    [Header("Serve Receive / Cut")]

    [Tooltip(
        "サーブカット後にボールを返すSetter位置。" +
        "PlayRootの子に置く。"
    )]
    [SerializeField]
    private Transform setterPosition;

    [Tooltip(
        "サーブカット軌道の最高到達World Y [m]。"
    )]
    [SerializeField]
    private float receiveApexHeight =
        4.0f;

    [Tooltip(
        "サーブカット時のBackspin [rpm]。" +
        "現段階では見た目の回転のみ。"
    )]
    [SerializeField]
    private float receiveBackspinRpm =
        180.0f;

    [Tooltip(
        "Receiver位置で停止するFixedUpdate数。" +
        "0なら即座にカット。"
    )]
    [Min(0)]
    [SerializeField]
    private int receiveContactFixedFrames =
        1;


    // ============================================================
    // Setter Toss Target
    // ============================================================

    [Header("Setter Toss Target")]

    [Tooltip(
        "Left Tossの到達点。" +
        "同時にSpike Contact Pointとして扱う。"
    )]
    [SerializeField]
    private Transform leftTossTarget;

    [Tooltip(
        "Right Tossの到達点。" +
        "同時にSpike Contact Pointとして扱う。"
    )]
    [SerializeField]
    private Transform rightTossTarget;

    [Header("Selected Toss")]

    [Tooltip("Left / Rightのどちらへトスするか")]
    [SerializeField]
    private TossSide selectedTossSide =
        TossSide.Left;

    [Tooltip(
        "選択したToss Targetに加えるZ方向Offset [m]。" +
        "初期値は0。"
    )]
    [SerializeField]
    private float tossTargetZOffset =
        0.0f;


    // ============================================================
    // Setter Toss
    // ============================================================

    [Header("Setter Toss")]

    [Tooltip(
        "Setter Tossの最高到達World Y [m]。" +
        "Toss TargetのYと同じならTargetがApexになる。"
    )]
    [SerializeField]
    private float setApexHeight =
        4.5f;

    [Tooltip(
        "Setter Toss時のボール回転 [rpm]。" +
        "現段階では見た目のみ。"
    )]
    [SerializeField]
    private float setSpinRpm =
        60.0f;

    [Tooltip(
        "Setter位置で停止するFixedUpdate数。" +
        "0なら即座にToss。"
    )]
    [Min(0)]
    [SerializeField]
    private int setContactFixedFrames =
        1;


    // ============================================================
    // Spike
    // ============================================================

    [Header("Spike")]

    [Tooltip(
        "既存のSpike Target Left。" +
        "現在の実装ではLeft Toss Cross / Right Toss Straightで使用する。"
    )]
    [SerializeField]
    private Transform spikeTargetLeft;

    [Tooltip(
        "既存のSpike Target Right。" +
        "現在の実装ではLeft Toss Straight / Right Toss Crossで使用する。"
    )]
    [SerializeField]
    private Transform spikeTargetRight;

    [SerializeField]
    private SpikeCourse selectedSpikeCourse =
        SpikeCourse.Cross;

    [Tooltip("スパイク打球直後の速度 [km/h]")]
    [SerializeField]
    private float spikeSpeedKmh =
        100.0f;


    // ============================================================
    // Block
    // ============================================================

    /*
     * Blockは独立したON/OFF状態を持たない。
     * End Phase == PlayPhase.Block のときだけ、
     * Spike Contact -> Block Contact -> Block Landing の軌道を使用する。
     */

    [Header("Block Trajectory")]

    [Tooltip(
        "Left Toss時のBlock Contact Point。" +
        "Block ONではSpike Contact Pointからこの点を必ず通る。"
    )]
    [SerializeField]
    private Transform blockContactPointLeft;

    [Tooltip(
        "Left Toss時のBlock Landing Point。" +
        "Block Contact後にボールが落ちる位置。"
    )]
    [SerializeField]
    private Transform blockLandingPointLeft;

    [Tooltip(
        "Right Toss時のBlock Contact Point。" +
        "Block ONではSpike Contact Pointからこの点を必ず通る。"
    )]
    [SerializeField]
    private Transform blockContactPointRight;

    [Tooltip(
        "Right Toss時のBlock Landing Point。" +
        "Block Contact後にボールが落ちる位置。"
    )]
    [SerializeField]
    private Transform blockLandingPointRight;

    [Tooltip(
        "Block Contact Pointで停止するFixedUpdate数。" +
        "0なら接触直後にBlock Landing Pointへ打ち出す。"
    )]
    [Min(0)]
    [SerializeField]
    private int blockContactFixedFrames =
        1;

    [Tooltip(
        "ブロック接触後のボール速度 [km/h]。" +
        "減速率ではなく速度を直接指定する。"
    )]
    [Min(0.1f)]
    [SerializeField]
    private float blockExitSpeedKmh =
        45.0f;

    [Tooltip(
        "ブロック接触後のボール回転 [rpm]。" +
        "正の値はトップスピン、負の値はバックスピン。"
    )]
    [SerializeField]
    private float blockSpinRpm =
        600.0f;


    // ============================================================
    // Serve Toss
    // ============================================================

    [Header("Serve Toss")]

    [Tooltip(
        "ボール現在位置からトス最高点までの上昇量 [m]"
    )]
    [SerializeField]
    private float tossHeight =
        1.5f;

    [Tooltip(
        "下降中にこのWorld Yへ到達した瞬間にサーブを打つ [m]"
    )]
    [SerializeField]
    private float serveHitHeight =
        3.0f;

    [Tooltip(
        "ServeStartからサーブ打点までにコート方向へ進む距離 [m]"
    )]
    [SerializeField]
    private float tossForwardDistance =
        1.0f;


    // ============================================================
    // Spike Serve
    // ============================================================

    [Header("Spike Serve")]

    [Tooltip("サーブ打球直後の速度 [km/h]")]
    [SerializeField]
    private float serveSpeedKmh =
        130.0f;

    [Tooltip(
        "打ち出し角度 [deg]。" +
        "0=水平、プラス=上向き、マイナス=下向き"
    )]
    [SerializeField]
    private float spikeServeLaunchAngle =
        2.0f;


    // ============================================================
    // Experiment Fade
    // ============================================================

    [Header("Experiment Fade")]

    [Tooltip(
        "実験用の映像遮蔽を有効にするかどうか。\n" +
        "End Phase開始から、そのPhaseに対応するDelay秒後にFade Outする。"
    )]
    [SerializeField]
    private bool enableExperimentFade =
        true;

    [Tooltip(
        "Shader Fadeを制御するImageController。"
    )]
    [SerializeField]
    private ImageController imageController;

    [Tooltip("End Phase = Serve のとき、Serve開始からFade Out開始までの時間 [s]")]
    [Min(0.0f)]
    [SerializeField]
    private float serveFadeDelay =
        0.80f;

    [Tooltip("End Phase = Receive のとき、Receive開始からFade Out開始までの時間 [s]")]
    [Min(0.0f)]
    [SerializeField]
    private float receiveFadeDelay =
        0.50f;

    [Tooltip("End Phase = Toss のとき、Toss開始からFade Out開始までの時間 [s]")]
    [Min(0.0f)]
    [SerializeField]
    private float tossFadeDelay =
        0.50f;

    [Tooltip("End Phase = Spike のとき、Spike開始からFade Out開始までの時間 [s]")]
    [Min(0.0f)]
    [SerializeField]
    private float spikeFadeDelay =
        0.20f;

    [Tooltip(
        "End Phase = Block のとき、ブロックありSpike開始から" +
        "Fade Out開始までの時間 [s]"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float blockFadeDelay =
        0.20f;

    [Tooltip(
        "Fade Outにかける時間 [s]。\n" +
        "0なら指定時刻で瞬時に完全遮蔽する。"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float fadeDuration =
        0.03f;


    // ============================================================
    // Startup
    // ============================================================

    [Header("Startup")]

    [SerializeField]
    private bool spawnOnStart =
        true;


    // ============================================================
    // Keyboard Debug
    // ============================================================

    [Header("Keyboard Debug")]

    [SerializeField]
    private bool enableKeyboardDebug =
        true;


    // ============================================================
    // Runtime
    // ============================================================

    private VolleyballBallPhysics currentBallPhysics;

    private Coroutine experimentFadeTimerCoroutine;

    private Quaternion basePlayRootRotation =
        Quaternion.identity;

    private bool playRootRotationCached =
        false;


    // ============================================================
    // Properties
    // ============================================================

    public CourtSide CurrentCourtSide =>
        currentCourtSide;

    public PlayPhase StartPhase =>
        startPhase;

    public PlayPhase EndPhase =>
        endPhase;

    public TossSide CurrentTossSide =>
        selectedTossSide;

    public SpikeCourse CurrentSpikeCourse =>
        selectedSpikeCourse;

    public BlockMode CurrentBlockMode =>
        endPhase == PlayPhase.Block
            ? BlockMode.Block
            : BlockMode.None;

    public float TossTargetZOffset =>
        tossTargetZOffset;

    public float SetterTossApexHeight =>
        setApexHeight;

    public ServeStartPosition CurrentServeStart =>
        selectedServeStart;

    public ServeTargetPosition CurrentServeTarget =>
        selectedServeTarget;

    public float ServeTossHeight =>
        tossHeight;

    public float ServeHitHeight =>
        serveHitHeight;

    public float ServeTossForwardDistance =>
        tossForwardDistance;

    public float ServeSpeedKmh =>
        serveSpeedKmh;

    public float SpikeServeLaunchAngle =>
        spikeServeLaunchAngle;

    public float ServeFadeDelay =>
        serveFadeDelay;


    // ============================================================
    // Unity
    // ============================================================

    private void Awake()
    {
        CachePlayRootRotation();

        CacheServeTargetBasePositions();
        ApplyAllServeTargetOffsets();
    }

    private void Start()
    {
        ApplyCourtSide();

        ValidateSimulationRange();

        if (spawnOnStart)
        {
            ResetSelectedSimulation();
        }
    }

    private void Update()
    {
        if (!enableKeyboardDebug)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectSimulationRange(
                PlayPhase.Serve,
                PlayPhase.Serve
            );
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectSimulationRange(
                PlayPhase.Receive,
                PlayPhase.Receive
            );
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectSimulationRange(
                PlayPhase.Serve,
                PlayPhase.Receive
            );
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SelectSimulationRange(
                PlayPhase.Toss,
                PlayPhase.Spike
            );
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            SelectSimulationRange(
                PlayPhase.Serve,
                PlayPhase.Spike
            );
        }

        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            SelectSimulationRange(
                PlayPhase.Serve,
                PlayPhase.Block
            );
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            StartSelectedSimulation();
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            ResetSelectedSimulation();
        }
    }


    // ============================================================
    // Simulation Selection
    // ============================================================

    public void SelectSimulationRange(
        PlayPhase newStartPhase,
        PlayPhase newEndPhase
    )
    {
        if (
            (int)newStartPhase >
            (int)newEndPhase
        )
        {
            Debug.LogError(
                "[RallyController] " +
                "Start PhaseはEnd Phase以前にしてください。"
            );

            return;
        }

        startPhase =
            newStartPhase;

        endPhase =
            newEndPhase;

        Debug.Log(
            "[RallyController] Simulation Selected\n" +
            $"Start = {startPhase}\n" +
            $"End = {endPhase}"
        );

        ResetSelectedSimulation();
    }

    public void SetStartPhase(
        int index
    )
    {
        index =
            Mathf.Clamp(
                index,
                0,
                4
            );

        startPhase =
            (PlayPhase)index;

        ValidateSimulationRange();
    }

    public void SetEndPhase(
        int index
    )
    {
        index =
            Mathf.Clamp(
                index,
                0,
                4
            );

        endPhase =
            (PlayPhase)index;

        ValidateSimulationRange();
    }

    private void ValidateSimulationRange()
    {
        if (
            (int)startPhase <=
            (int)endPhase
        )
        {
            return;
        }

        Debug.LogWarning(
            "[RallyController] " +
            "Start PhaseがEnd Phaseより後なので、" +
            "End PhaseをStart Phaseへ合わせます。"
        );

        endPhase =
            startPhase;
    }

    private bool ShouldContinueAfter(
        PlayPhase completedPhase
    )
    {
        return
            (int)completedPhase <
            (int)endPhase;
    }


    // ============================================================
    // Simulation Start
    // ============================================================

    public void StartSelectedSimulation()
    {
        ValidateSimulationRange();

        /*
         * 前回TrialのFade状態や、
         * 実行待ちのFade Timerが残っていても
         * 新しいTrial開始時には必ず通常表示へ戻す。
         */
        ResetExperimentFade();

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

                SpawnBallAtSelectedTossTarget();

                StartSpikeSequence();

                break;


            case PlayPhase.Block:

                SpawnBallAtSelectedTossTarget();

                StartSpikeSequence();

                break;
        }
    }


    // ============================================================
    // Simulation Reset
    // ============================================================

    public void ResetSelectedSimulation()
    {
        ValidateSimulationRange();

        /*
         * ZキーによるResetを含め、
         * Ball Reset時にはFadeも必ず解除する。
         */
        ResetExperimentFade();

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

                SpawnBallAtSelectedTossTarget();

                break;


            case PlayPhase.Block:

                SpawnBallAtSelectedTossTarget();

                break;
        }
    }


    // ============================================================
    // Court Side
    // ============================================================

    private void CachePlayRootRotation()
    {
        if (playRoot == null)
        {
            Debug.LogWarning(
                "[RallyController] PlayRoot が設定されていません。"
            );

            return;
        }

        basePlayRootRotation =
            playRoot.localRotation;

        playRootRotationCached =
            true;
    }

    private void ApplyCourtSide()
    {
        if (playRoot == null)
        {
            return;
        }

        if (!playRootRotationCached)
        {
            CachePlayRootRotation();
        }

        if (!playRootRotationCached)
        {
            return;
        }

        if (
            currentCourtSide ==
            CourtSide.Left
        )
        {
            playRoot.localRotation =
                basePlayRootRotation;
        }
        else
        {
            playRoot.localRotation =
                basePlayRootRotation *
                Quaternion.Euler(
                    0.0f,
                    180.0f,
                    0.0f
                );
        }
    }

    public void ToggleCourt()
    {
        if (playRoot == null)
        {
            Debug.LogError(
                "[RallyController] PlayRoot が設定されていません。"
            );

            return;
        }

        currentCourtSide =
            currentCourtSide ==
            CourtSide.Left
                ? CourtSide.Right
                : CourtSide.Left;

        ApplyCourtSide();

        Debug.Log(
            "[RallyController] Court Changed\n" +
            $"Court Side = {currentCourtSide}\n" +
            $"PlayRoot Position = {playRoot.position}\n" +
            $"PlayRoot Rotation = {playRoot.localEulerAngles}"
        );

        ResetSelectedSimulation();
    }


    // ============================================================
    // Generic Ball Spawn
    // ============================================================

    private GameObject SpawnBallAtPoint(
        Transform spawnPoint,
        string label
    )
    {
        if (ballSpawner == null)
        {
            Debug.LogError(
                "[RallyController] BallSpawner が設定されていません。"
            );

            return null;
        }

        if (spawnPoint == null)
        {
            Debug.LogError(
                "[RallyController] Spawn Point がnullです。"
            );

            return null;
        }

        UnbindCurrentBall();

        GameObject newBall =
            ballSpawner.RespawnBall(
                spawnPoint
            );

        if (newBall == null)
        {
            return null;
        }

        currentBallPhysics =
            newBall.GetComponent<VolleyballBallPhysics>();

        if (currentBallPhysics == null)
        {
            Debug.LogError(
                "[RallyController] " +
                "Ball PrefabにVolleyballBallPhysicsがありません。"
            );

            return null;
        }

        BindCurrentBallEvents();

        Debug.Log(
            "[RallyController] Ball Spawn\n" +
            $"Type = {label}\n" +
            $"Court = {currentCourtSide}\n" +
            $"Position = {spawnPoint.position}"
        );

        return newBall;
    }

    private void BindCurrentBallEvents()
    {
        if (currentBallPhysics == null)
        {
            return;
        }

        currentBallPhysics.OnTossApex +=
            HandleTossApex;

        currentBallPhysics.OnServeHitPoint +=
            HandleServeHitPoint;

        currentBallPhysics.OnServeReachedTarget +=
            HandleServeReachedTarget;

        currentBallPhysics.OnReceiveReachedTarget +=
            HandleReceiveReachedTarget;

        currentBallPhysics.OnSetReachedTarget +=
            HandleSetReachedTarget;

        currentBallPhysics.OnSpikeReachedTarget +=
            HandleSpikeReachedTarget;
    }


    // ============================================================
    // Spawn
    // ============================================================

    public void SpawnBallAtSelectedStart()
    {
        Transform spawnPoint =
            GetSelectedServeStart();

        if (spawnPoint == null)
        {
            Debug.LogError(
                "[RallyController] ServeStart が設定されていません。"
            );

            return;
        }

        SpawnBallAtPoint(
            spawnPoint,
            "Serve Start"
        );
    }

    public void SpawnBallAtSelectedReceiver()
    {
        Transform receiver =
            GetSelectedServeTarget();

        if (receiver == null)
        {
            Debug.LogError(
                "[RallyController] Receiver が設定されていません。"
            );

            return;
        }

        SpawnBallAtPoint(
            receiver,
            "Receive Start"
        );
    }

    private void SpawnBallAtSetter()
    {
        if (setterPosition == null)
        {
            Debug.LogError(
                "[RallyController] Setter Position が設定されていません。"
            );

            return;
        }

        SpawnBallAtPoint(
            setterPosition,
            "Setter Toss Start"
        );
    }

    private void SpawnBallAtSelectedTossTarget()
    {
        Transform tossTarget =
            GetSelectedTossTarget();

        if (tossTarget == null)
        {
            Debug.LogError(
                "[RallyController] Toss Target が設定されていません。"
            );

            return;
        }

        GameObject newBall =
            SpawnBallAtPoint(
                tossTarget,
                "Spike Contact Point"
            );

        if (
            newBall == null ||
            currentBallPhysics == null
        )
        {
            return;
        }

        currentBallPhysics.PlaceAt(
            GetSelectedTossTargetPosition()
        );
    }


    // ============================================================
    // Serve Sequence
    // ============================================================

    public void StartSpikeServeSequence()
    {
        if (currentBallPhysics == null)
        {
            Debug.LogWarning(
                "[RallyController] Ballがありません。"
            );

            return;
        }

        Transform target =
            GetSelectedServeTarget();

        if (target == null)
        {
            Debug.LogError(
                "[RallyController] Receiver Target が設定されていません。"
            );

            return;
        }

        Vector3 tossDirection =
            target.position -
            currentBallPhysics.transform.position;

        tossDirection.y =
            0.0f;

        if (
            tossDirection.sqrMagnitude <=
            0.0001f
        )
        {
            Debug.LogError(
                "[RallyController] Toss Directionを計算できません。"
            );

            return;
        }

        tossDirection.Normalize();

        Debug.Log(
            "[RallyController] Serve Toss Start\n" +
            $"Court = {currentCourtSide}\n" +
            $"Receiver = {target.name}\n" +
            $"Receiver Position = {target.position}\n" +
            $"Toss Height = {tossHeight:F2} m\n" +
            $"Serve Hit Height = {serveHitHeight:F2} m\n" +
            $"Forward Distance = {tossForwardDistance:F2} m"
        );

        NotifyPhaseStarted(
            PlayPhase.Serve
        );

        currentBallPhysics.TossUp(
            tossHeight,
            serveHitHeight,
            tossForwardDistance,
            tossDirection
        );
    }


    // ============================================================
    // Receive Sequence
    // ============================================================

    private void StartReceiveSequence()
    {
        if (currentBallPhysics == null)
        {
            return;
        }

        if (setterPosition == null)
        {
            Debug.LogError(
                "[RallyController] Setter Position が設定されていません。"
            );

            return;
        }

        NotifyPhaseStarted(
            PlayPhase.Receive
        );

        currentBallPhysics.ReceiveToSetter(
            setterPosition,
            receiveApexHeight,
            receiveBackspinRpm,
            receiveContactFixedFrames
        );
    }


    // ============================================================
    // Serve Events
    // ============================================================

    private void HandleTossApex()
    {
        if (currentBallPhysics == null)
        {
            return;
        }

        Debug.Log(
            "[RallyController] Serve Toss Apex\n" +
            $"Position = {currentBallPhysics.transform.position}"
        );
    }

    private void HandleServeHitPoint()
    {
        if (currentBallPhysics == null)
        {
            return;
        }

        Transform target =
            GetSelectedServeTarget();

        if (target == null)
        {
            Debug.LogError(
                "[RallyController] Receiver Target が設定されていません。"
            );

            return;
        }

        Debug.Log(
            "[RallyController] Spike Serve Impact\n" +
            $"Court = {currentCourtSide}\n" +
            $"Hit Position = {currentBallPhysics.transform.position}\n" +
            $"Receiver = {target.name}\n" +
            $"Receiver Position = {target.position}\n" +
            $"Speed = {serveSpeedKmh:F1} km/h\n" +
            $"Launch Angle = {spikeServeLaunchAngle:F2} deg"
        );

        currentBallPhysics.SpikeServe(
            target,
            serveSpeedKmh,
            spikeServeLaunchAngle
        );
    }

    private void HandleServeReachedTarget()
    {
        if (currentBallPhysics == null)
        {
            return;
        }

        Transform receiver =
            GetSelectedServeTarget();

        Debug.Log(
            "[RallyController] Serve Completed\n" +
            $"Receiver = " +
            $"{(receiver != null ? receiver.name : "null")}\n" +
            $"Ball Position = {currentBallPhysics.transform.position}"
        );

        if (
            !ShouldContinueAfter(
                PlayPhase.Serve
            )
        )
        {
            Debug.Log(
                "[RallyController] Simulation Finished at Serve."
            );

            return;
        }

        StartReceiveSequence();
    }


    // ============================================================
    // Receive Completed
    // ============================================================

    private void HandleReceiveReachedTarget()
    {
        if (currentBallPhysics == null)
        {
            return;
        }

        Debug.Log(
            "[RallyController] Receive Completed\n" +
            $"Setter = " +
            $"{(setterPosition != null ? setterPosition.name : "null")}\n" +
            $"Ball Position = {currentBallPhysics.transform.position}"
        );

        if (
            !ShouldContinueAfter(
                PlayPhase.Receive
            )
        )
        {
            Debug.Log(
                "[RallyController] Simulation Finished at Receive."
            );

            return;
        }

        StartSetterTossSequence();
    }


    // ============================================================
    // Setter Toss
    // ============================================================

    private void StartSetterTossSequence()
    {
        if (currentBallPhysics == null)
        {
            Debug.LogWarning(
                "[RallyController] Ballがありません。"
            );

            return;
        }

        Transform tossTarget =
            GetSelectedTossTarget();

        if (tossTarget == null)
        {
            Debug.LogError(
                "[RallyController] Toss Target が設定されていません。"
            );

            return;
        }

        Vector3 targetPosition =
            GetSelectedTossTargetPosition();

        Debug.Log(
            "[RallyController] Setter Toss Start\n" +
            $"Toss Side = {selectedTossSide}\n" +
            $"Target = {tossTarget.name}\n" +
            $"Base Target Position = {tossTarget.position}\n" +
            $"Z Offset = {tossTargetZOffset:F3} m\n" +
            $"Actual Target Position = {targetPosition}\n" +
            $"Apex Height = {setApexHeight:F3} m"
        );

        NotifyPhaseStarted(
            PlayPhase.Toss
        );

        currentBallPhysics.SetToTarget(
            targetPosition,
            setApexHeight,
            setSpinRpm,
            setContactFixedFrames
        );
    }

    private void HandleSetReachedTarget()
    {
        if (currentBallPhysics == null)
        {
            return;
        }

        Debug.Log(
            "[RallyController] Set Completed\n" +
            $"Toss Side = {selectedTossSide}\n" +
            $"Spike Contact Position = " +
            $"{currentBallPhysics.transform.position}"
        );

        if (
            !ShouldContinueAfter(
                PlayPhase.Toss
            )
        )
        {
            Debug.Log(
                "[RallyController] Simulation Finished at Toss."
            );

            return;
        }

        StartSpikeSequence();
    }


    // ============================================================
    // Spike
    // ============================================================

    private void StartSpikeSequence()
    {
        if (currentBallPhysics == null)
        {
            Debug.LogWarning(
                "[RallyController] Ballがありません。"
            );

            return;
        }

        /*
         * Blockの使用有無はEnd Phaseだけで決める。
         *
         * End = Spike
         *   -> 通常Spike
         *
         * End = Block
         *   -> Spike Contact -> Block Contact -> Block Landing
         *
         * 独立したBlock ON/OFF状態は持たない。
         */
        bool useBlock =
            endPhase ==
            PlayPhase.Block;

        if (useBlock)
        {
            Transform blockContactPoint =
                GetSelectedBlockContactPoint();

            Transform blockLandingPoint =
                GetSelectedBlockLandingPoint();

            if (
                blockContactPoint == null ||
                blockLandingPoint == null
            )
            {
                Debug.LogError(
                    "[RallyController] End PhaseがBlockですが、" +
                    "Block Contact Point / Block Landing Pointが設定されていません。\n" +
                    $"Toss Side = {selectedTossSide}"
                );

                return;
            }

            Debug.Log(
                "[RallyController] Block Sequence Start\n" +
                $"End Phase = {endPhase}\n" +
                $"Toss Side = {selectedTossSide}\n" +
                $"Course = {selectedSpikeCourse}\n" +
                $"Spike Contact Position = " +
                $"{currentBallPhysics.transform.position}\n" +
                $"Block Contact Point = {blockContactPoint.name}\n" +
                $"Block Contact Position = {blockContactPoint.position}\n" +
                $"Spike Speed = {spikeSpeedKmh:F1} km/h\n" +
                $"Block Contact Frames = {blockContactFixedFrames}\n" +
                $"Block Landing Point = {blockLandingPoint.name}\n" +
                $"Block Landing Position = {blockLandingPoint.position}\n" +
                $"Block Exit Speed = {blockExitSpeedKmh:F1} km/h\n" +
                $"Block Spin = {blockSpinRpm:F1} rpm"
            );

            /*
             * End PhaseがBlockなので、FadeもBlock条件として扱う。
             * blockFadeDelayはSpikeWithBlock開始時点から数える。
             */
            NotifyPhaseStarted(
                PlayPhase.Block
            );

            currentBallPhysics.SpikeWithBlock(
                blockContactPoint,
                blockLandingPoint,
                spikeSpeedKmh,
                blockExitSpeedKmh,
                blockSpinRpm,
                blockContactFixedFrames
            );

            return;
        }


        Transform spikeTarget =
            GetSelectedSpikeTarget();

        if (spikeTarget == null)
        {
            Debug.LogError(
                "[RallyController] Spike Target が設定されていません。"
            );

            return;
        }

        Debug.Log(
            "[RallyController] Spike Start\n" +
            $"End Phase = {endPhase}\n" +
            $"Toss Side = {selectedTossSide}\n" +
            $"Course = {selectedSpikeCourse}\n" +
            $"Contact Position = " +
            $"{currentBallPhysics.transform.position}\n" +
            $"Target = {spikeTarget.name}\n" +
            $"Target Position = {spikeTarget.position}\n" +
            $"Speed = {spikeSpeedKmh:F1} km/h"
        );

        NotifyPhaseStarted(
            PlayPhase.Spike
        );

        currentBallPhysics.Spike(
            spikeTarget,
            spikeSpeedKmh
        );
    }


    private void HandleSpikeReachedTarget()
    {
        if (currentBallPhysics == null)
        {
            return;
        }

        if (
            endPhase ==
            PlayPhase.Block
        )
        {
            Transform blockLandingPoint =
                GetSelectedBlockLandingPoint();

            Debug.Log(
                "[RallyController] Block Landing Completed\n" +
                $"Toss Side = {selectedTossSide}\n" +
                $"Course = {selectedSpikeCourse}\n" +
                $"Landing Point = " +
                $"{(blockLandingPoint != null ? blockLandingPoint.name : "null")}\n" +
                $"Ball Position = " +
                $"{currentBallPhysics.transform.position}"
            );

            Debug.Log(
                "[RallyController] Simulation Finished at Block."
            );

            return;
        }

        Transform spikeTarget =
            GetSelectedSpikeTarget();

        Debug.Log(
            "[RallyController] Spike Target Reached\n" +
            $"Toss Side = {selectedTossSide}\n" +
            $"Course = {selectedSpikeCourse}\n" +
            $"Target = " +
            $"{(spikeTarget != null ? spikeTarget.name : "null")}\n" +
            $"Ball Position = " +
            $"{currentBallPhysics.transform.position}"
        );

        Debug.Log(
            "[RallyController] Simulation Finished at Spike."
        );
    }


    // ============================================================
    // External Control
    // ============================================================

    public void SetServeStart(
        int index
    )
    {
        index =
            Mathf.Clamp(
                index,
                0,
                2
            );

        selectedServeStart =
            (ServeStartPosition)index;
    }

    public void SetServeStartAndRespawn(
        int index
    )
    {
        SetServeStart(
            index
        );

        SpawnBallAtSelectedStart();
    }

    public void SetServeTarget(
        int index
    )
    {
        index =
            Mathf.Clamp(
                index,
                0,
                2
            );

        selectedServeTarget =
            (ServeTargetPosition)index;
    }

    public void SetServeTargetOffset(
        int index,
        Vector3 offset
    )
    {
        index =
            Mathf.Clamp(
                index,
                0,
                2
            );

        ServeTargetPosition targetPosition =
            (ServeTargetPosition)index;

        switch (targetPosition)
        {
            case ServeTargetPosition.Left:

                serveTargetLeftOffset =
                    offset;

                break;

            case ServeTargetPosition.Center:

                serveTargetCenterOffset =
                    offset;

                break;

            case ServeTargetPosition.Right:

                serveTargetRightOffset =
                    offset;

                break;
        }

        ApplyServeTargetOffset(
            targetPosition
        );
    }

    public Vector3 GetServeTargetOffset(
        int index
    )
    {
        index =
            Mathf.Clamp(
                index,
                0,
                2
            );

        switch ((ServeTargetPosition)index)
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
        serveTargetLeftOffset =
            Vector3.zero;

        serveTargetCenterOffset =
            Vector3.zero;

        serveTargetRightOffset =
            Vector3.zero;

        ApplyAllServeTargetOffsets();
    }

    public void SetTossSide(
        int index
    )
    {
        index =
            Mathf.Clamp(
                index,
                0,
                1
            );

        selectedTossSide =
            (TossSide)index;
    }

    public void SetTossTargetZOffset(
        float offset
    )
    {
        tossTargetZOffset =
            offset;
    }

    public void SetSetApexHeight(
        float height
    )
    {
        setApexHeight =
            height;
    }

    public void SetSetSpinRpm(
        float rpm
    )
    {
        setSpinRpm =
            rpm;
    }

    public void SetSetContactFixedFrames(
        int frames
    )
    {
        setContactFixedFrames =
            Mathf.Max(
                0,
                frames
            );
    }

    public void SetSpikeCourse(
        int index
    )
    {
        index =
            Mathf.Clamp(
                index,
                0,
                1
            );

        selectedSpikeCourse =
            (SpikeCourse)index;
    }

    /// <summary>
    /// 旧UI互換用。
    /// Blockの独立状態は持たず、End PhaseをSpike / Blockへ切り替える。
    /// </summary>
    public void SetBlockMode(
        int index
    )
    {
        index =
            Mathf.Clamp(
                index,
                0,
                1
            );

        endPhase =
            index == 1
                ? PlayPhase.Block
                : PlayPhase.Spike;

        ValidateSimulationRange();
    }

    /// <summary>
    /// 旧Toggle互換用。
    /// true = End Phase Block
    /// false = End Phase Spike
    /// </summary>
    public void SetBlockEnabled(
        bool enabled
    )
    {
        endPhase =
            enabled
                ? PlayPhase.Block
                : PlayPhase.Spike;

        ValidateSimulationRange();
    }

    public void SetBlockContactFixedFrames(
        int frames
    )
    {
        blockContactFixedFrames =
            Mathf.Max(
                0,
                frames
            );
    }

    public void SetBlockExitSpeed(
        float speedKmh
    )
    {
        blockExitSpeedKmh =
            Mathf.Max(
                0.1f,
                speedKmh
            );
    }

    public void SetBlockSpinRpm(
        float rpm
    )
    {
        blockSpinRpm =
            rpm;
    }

    public void SetSpikeSpeed(
        float speedKmh
    )
    {
        spikeSpeedKmh =
            Mathf.Max(
                0.0f,
                speedKmh
            );
    }

    public void SetServeSpeed(
        float speedKmh
    )
    {
        serveSpeedKmh =
            Mathf.Max(
                0.0f,
                speedKmh
            );
    }

    public void SetSpikeServeLaunchAngle(
        float angleDeg
    )
    {
        spikeServeLaunchAngle =
            angleDeg;
    }

    public void SetTossHeight(
        float height
    )
    {
        tossHeight =
            Mathf.Max(
                0.0f,
                height
            );
    }

    public void SetServeHitHeight(
        float height
    )
    {
        serveHitHeight =
            height;
    }

    public void SetTossForwardDistance(
        float distance
    )
    {
        tossForwardDistance =
            Mathf.Max(
                0.0f,
                distance
            );
    }

    public void SetReceiveApexHeight(
        float height
    )
    {
        receiveApexHeight =
            height;
    }

    public void SetReceiveBackspinRpm(
        float rpm
    )
    {
        receiveBackspinRpm =
            rpm;
    }

    public void SetReceiveContactFixedFrames(
        int frames
    )
    {
        receiveContactFixedFrames =
            Mathf.Max(
                0,
                frames
            );
    }


    // ============================================================
    // Experiment Fade Control
    // ============================================================

    public void SetExperimentFadeEnabled(
        bool enabled
    )
    {
        enableExperimentFade =
            enabled;

        if (!enableExperimentFade)
        {
            ResetExperimentFade();
        }
    }

    public void SetServeFadeDelay(
        float seconds
    )
    {
        serveFadeDelay =
            Mathf.Max(
                0.0f,
                seconds
            );
    }

    public void SetReceiveFadeDelay(
        float seconds
    )
    {
        receiveFadeDelay =
            Mathf.Max(
                0.0f,
                seconds
            );
    }

    public void SetTossFadeDelay(
        float seconds
    )
    {
        tossFadeDelay =
            Mathf.Max(
                0.0f,
                seconds
            );
    }

    public void SetSpikeFadeDelay(
        float seconds
    )
    {
        spikeFadeDelay =
            Mathf.Max(
                0.0f,
                seconds
            );
    }

    public void SetBlockFadeDelay(
        float seconds
    )
    {
        blockFadeDelay =
            Mathf.Max(
                0.0f,
                seconds
            );
    }

    public void SetFadeDuration(
        float seconds
    )
    {
        fadeDuration =
            Mathf.Max(
                0.0f,
                seconds
            );
    }

    private void NotifyPhaseStarted(
        PlayPhase phase
    )
    {
        if (!enableExperimentFade)
        {
            return;
        }

        if (phase != endPhase)
        {
            return;
        }

        if (imageController == null)
        {
            Debug.LogWarning(
                "[RallyController] Experiment Fade用の" +
                "ImageControllerが設定されていません。"
            );

            return;
        }

        CancelExperimentFadeTimer();

        float delay =
            GetFadeDelay(
                phase
            );

        experimentFadeTimerCoroutine =
            StartCoroutine(
                FadeAfterDelay(
                    phase,
                    delay
                )
            );

        Debug.Log(
            "[RallyController] Experiment Fade Scheduled\n" +
            $"End Phase = {phase}\n" +
            $"Delay = {delay:F3} s\n" +
            $"Fade Duration = {fadeDuration:F3} s"
        );
    }

    private IEnumerator FadeAfterDelay(
        PlayPhase phase,
        float delay
    )
    {
        if (delay > 0.0f)
        {
            yield return
                new WaitForSecondsRealtime(
                    delay
                );
        }

        experimentFadeTimerCoroutine =
            null;

        if (!enableExperimentFade)
        {
            yield break;
        }

        if (imageController == null)
        {
            yield break;
        }

        if (phase != endPhase)
        {
            yield break;
        }

        imageController.FadeOut(
            fadeDuration
        );

        Debug.Log(
            "[RallyController] Experiment Fade Started\n" +
            $"Phase = {phase}\n" +
            $"Fade Duration = {fadeDuration:F3} s"
        );
    }

    private float GetFadeDelay(
        PlayPhase phase
    )
    {
        switch (phase)
        {
            case PlayPhase.Serve:
                return
                    Mathf.Max(
                        0.0f,
                        serveFadeDelay
                    );

            case PlayPhase.Receive:
                return
                    Mathf.Max(
                        0.0f,
                        receiveFadeDelay
                    );

            case PlayPhase.Toss:
                return
                    Mathf.Max(
                        0.0f,
                        tossFadeDelay
                    );

            case PlayPhase.Spike:
                return
                    Mathf.Max(
                        0.0f,
                        spikeFadeDelay
                    );

            case PlayPhase.Block:
                return
                    Mathf.Max(
                        0.0f,
                        blockFadeDelay
                    );

            default:
                return 0.0f;
        }
    }

    private void CancelExperimentFadeTimer()
    {
        if (
            experimentFadeTimerCoroutine ==
            null
        )
        {
            return;
        }

        StopCoroutine(
            experimentFadeTimerCoroutine
        );

        experimentFadeTimerCoroutine =
            null;
    }

    public void ResetExperimentFade()
    {
        CancelExperimentFadeTimer();

        if (imageController == null)
        {
            return;
        }

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
    // Serve Target Offset
    // ============================================================

    private void CacheServeTargetBasePositions()
    {
        if (serveTargetLeft != null)
        {
            serveTargetLeftBaseLocalPosition =
                serveTargetLeft.localPosition;
        }

        if (serveTargetCenter != null)
        {
            serveTargetCenterBaseLocalPosition =
                serveTargetCenter.localPosition;
        }

        if (serveTargetRight != null)
        {
            serveTargetRightBaseLocalPosition =
                serveTargetRight.localPosition;
        }

        serveTargetBasePositionsCached =
            true;
    }

    private void ApplyAllServeTargetOffsets()
    {
        ApplyServeTargetOffset(
            ServeTargetPosition.Left
        );

        ApplyServeTargetOffset(
            ServeTargetPosition.Center
        );

        ApplyServeTargetOffset(
            ServeTargetPosition.Right
        );
    }

    private void ApplyServeTargetOffset(
        ServeTargetPosition targetPosition
    )
    {
        if (!serveTargetBasePositionsCached)
        {
            CacheServeTargetBasePositions();
        }

        switch (targetPosition)
        {
            case ServeTargetPosition.Left:

                if (serveTargetLeft != null)
                {
                    serveTargetLeft.localPosition =
                        serveTargetLeftBaseLocalPosition +
                        serveTargetLeftOffset;
                }

                break;

            case ServeTargetPosition.Center:

                if (serveTargetCenter != null)
                {
                    serveTargetCenter.localPosition =
                        serveTargetCenterBaseLocalPosition +
                        serveTargetCenterOffset;
                }

                break;

            case ServeTargetPosition.Right:

                if (serveTargetRight != null)
                {
                    serveTargetRight.localPosition =
                        serveTargetRightBaseLocalPosition +
                        serveTargetRightOffset;
                }

                break;
        }
    }


    // ============================================================
    // Receiver Selection
    // ============================================================

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
        switch (selectedTossSide)
        {
            case TossSide.Left:
                return leftTossTarget;

            case TossSide.Right:
                return rightTossTarget;

            default:
                return leftTossTarget;
        }
    }

    private Vector3 GetSelectedTossTargetPosition()
    {
        Transform tossTarget =
            GetSelectedTossTarget();

        if (tossTarget == null)
        {
            return Vector3.zero;
        }

        Vector3 zDirection =
            Vector3.forward;

        if (playRoot != null)
        {
            zDirection =
                playRoot.TransformDirection(
                    Vector3.forward
                );
        }

        return
            tossTarget.position +
            zDirection *
            tossTargetZOffset;
    }


    // ============================================================
    // Spike Target Selection
    // ============================================================

    private Transform GetSelectedSpikeTarget()
    {
        if (
            selectedTossSide ==
            TossSide.Left
        )
        {
            return
                selectedSpikeCourse ==
                SpikeCourse.Straight
                    ? spikeTargetRight
                    : spikeTargetLeft;
        }

        return
            selectedSpikeCourse ==
            SpikeCourse.Straight
                ? spikeTargetLeft
                : spikeTargetRight;
    }


    // ============================================================
    // Block Point Selection
    // ============================================================

    private Transform GetSelectedBlockContactPoint()
    {
        return
            selectedTossSide ==
            TossSide.Left
                ? blockContactPointLeft
                : blockContactPointRight;
    }

    private Transform GetSelectedBlockLandingPoint()
    {
        return
            selectedTossSide ==
            TossSide.Left
                ? blockLandingPointLeft
                : blockLandingPointRight;
    }


    // ============================================================
    // Event Cleanup
    // ============================================================

    private void UnbindCurrentBall()
    {
        if (currentBallPhysics == null)
        {
            return;
        }

        currentBallPhysics.OnTossApex -=
            HandleTossApex;

        currentBallPhysics.OnServeHitPoint -=
            HandleServeHitPoint;

        currentBallPhysics.OnServeReachedTarget -=
            HandleServeReachedTarget;

        currentBallPhysics.OnReceiveReachedTarget -=
            HandleReceiveReachedTarget;

        currentBallPhysics.OnSetReachedTarget -=
            HandleSetReachedTarget;

        currentBallPhysics.OnSpikeReachedTarget -=
            HandleSpikeReachedTarget;

        currentBallPhysics =
            null;
    }

    private void OnDestroy()
    {
        CancelExperimentFadeTimer();

        UnbindCurrentBall();
    }
}