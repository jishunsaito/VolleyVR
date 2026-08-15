using UnityEngine;

/// <summary>
/// ラリー全体の進行を管理する。
///
/// Phase:
///
/// Serve
/// ↓
/// Receive
/// ↓
/// Toss
/// ↓
/// Spike
///
/// Start Phase / End Phaseを指定することで、
/// 一部のプレーだけシミュレートできる。
///
/// 現在実装:
///
/// Serve
/// Receive
/// Toss
///
/// 今後:
///
/// Spike
///
/// Setter Toss:
///
/// Setter Position
/// ↓
/// Setter Toss Target
///
/// TargetとApex Heightから
/// VolleyballBallPhysicsが初速度を逆算する。
///
/// Target到達後もBallは停止しない。
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
        Spike = 3
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
    // Receive / Serve Cut
    // ============================================================

    [Header("Serve Receive / Cut")]

    [Tooltip(
        "サーブカット後にボールを返すSetter位置。" +
        "PlayRootの子に置く。"
    )]
    [SerializeField]
    private Transform setterPosition;


    [Tooltip(
        "サーブカット軌道の最高到達World Y [m]。" +
        "この高さで鉛直速度が0になる。"
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
        "0なら即座にカット。" +
        "1なら1物理フレーム停止。"
    )]
    [Min(0)]
    [SerializeField]
    private int receiveContactFixedFrames =
        1;


    // ============================================================
    // Setter Toss
    // ============================================================

    [Header("Setter Toss")]

    [Tooltip(
        "Setter Tossが通過するTarget位置。" +
        "将来的にはLeft / RightやToss Lengthで" +
        "このTargetを選択する。"
    )]
    [SerializeField]
    private Transform setterTossTarget;


    [Tooltip(
        "Setter Tossの最高到達World Y [m]。" +
        "Target位置とこの高さから初速度を自動計算する。"
    )]
    [SerializeField]
    private float setterTossApexHeight =
        4.0f;


    // ============================================================
    // Serve Toss
    // ============================================================

    [Header("Serve Toss")]

    [Tooltip(
        "ボール現在位置からサーブ前トス最高点までの上昇量 [m]"
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

    [Tooltip(
        "サーブ打球直後の速度 [km/h]"
    )]
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


    // ============================================================
    // Unity
    // ============================================================

    private void Awake()
    {
        CachePlayRootRotation();
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


        // ========================================================
        // 1 = Serveのみ
        // ========================================================

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectSimulationRange(
                PlayPhase.Serve,
                PlayPhase.Serve
            );
        }


        // ========================================================
        // 2 = Receiveのみ
        // ========================================================

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectSimulationRange(
                PlayPhase.Receive,
                PlayPhase.Receive
            );
        }


        // ========================================================
        // 3 = Serve -> Receive
        // ========================================================

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectSimulationRange(
                PlayPhase.Serve,
                PlayPhase.Receive
            );
        }


        // ========================================================
        // 4 = Tossのみ
        // ========================================================

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SelectSimulationRange(
                PlayPhase.Toss,
                PlayPhase.Toss
            );
        }


        // ========================================================
        // 5 = Toss -> Spike
        //
        // Spikeはまだ未実装
        // ========================================================

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            SelectSimulationRange(
                PlayPhase.Toss,
                PlayPhase.Spike
            );
        }


        // ========================================================
        // 6 = Serve -> Spike
        //
        // Serve
        // Receive
        // Toss
        // Spike
        // ========================================================

        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            SelectSimulationRange(
                PlayPhase.Serve,
                PlayPhase.Spike
            );
        }


        // ========================================================
        // V = Simulation Start
        // ========================================================

        if (Input.GetKeyDown(KeyCode.V))
        {
            StartSelectedSimulation();
        }


        // ========================================================
        // Z = Reset
        // ========================================================

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


    // ============================================================
    // UI
    // ============================================================

    public void SetStartPhase(
        int index
    )
    {
        index =
            Mathf.Clamp(
                index,
                0,
                3
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
                3
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


        switch (startPhase)
        {
            // ----------------------------------------------------
            // Serve
            // ----------------------------------------------------

            case PlayPhase.Serve:

                SpawnBallAtSelectedStart();


                StartSpikeServeSequence();

                break;


            // ----------------------------------------------------
            // Receive
            // ----------------------------------------------------

            case PlayPhase.Receive:

                SpawnBallAtSelectedReceiver();


                StartReceiveSequence();

                break;


            // ----------------------------------------------------
            // Toss
            // ----------------------------------------------------

            case PlayPhase.Toss:

                SpawnBallAtSetter();


                StartSetterTossSequence();

                break;


            // ----------------------------------------------------
            // Spike
            // ----------------------------------------------------

            case PlayPhase.Spike:

                Debug.LogWarning(
                    "[RallyController] " +
                    "Spike開始はまだ未実装です。"
                );

                break;
        }
    }


    // ============================================================
    // Simulation Reset
    // ============================================================

    public void ResetSelectedSimulation()
    {
        ValidateSimulationRange();


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

                Debug.LogWarning(
                    "[RallyController] " +
                    "Spike開始位置はまだ未実装です。"
                );

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


        // 古いBallのEvent解除
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


    // ============================================================
    // Event Binding
    // ============================================================

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


        currentBallPhysics.OnSetterTossReachedTarget +=
            HandleSetterTossReachedTarget;
    }


    // ============================================================
    // Serve Spawn
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


    // ============================================================
    // Receive Spawn
    // ============================================================

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


    // ============================================================
    // Setter Spawn
    // ============================================================

    public void SpawnBallAtSetter()
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


        // ========================================================
        // Toss Direction
        // ========================================================

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


        currentBallPhysics.ReceiveToSetter(
            setterPosition,
            receiveApexHeight,
            receiveBackspinRpm,
            receiveContactFixedFrames
        );
    }


    // ============================================================
    // Setter Toss Sequence
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


        Transform target =
            GetSelectedSetterTossTarget();


        if (target == null)
        {
            Debug.LogError(
                "[RallyController] " +
                "Setter Toss Target が設定されていません。"
            );

            return;
        }


        Debug.Log(
            "[RallyController] Setter Toss Start\n" +
            $"Setter Position = {currentBallPhysics.transform.position}\n" +
            $"Toss Target = {target.name}\n" +
            $"Target Position = {target.position}\n" +
            $"Apex Height = {setterTossApexHeight:F3} m"
        );


        currentBallPhysics.SetterToss(
            target,
            setterTossApexHeight
        );
    }


    // ============================================================
    // Serve Toss Apex
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


    // ============================================================
    // Serve Hit
    // ============================================================

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


    // ============================================================
    // Serve Completed
    // ============================================================

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


        // ========================================================
        // Serveで終了
        // ========================================================

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


        // ========================================================
        // Serve -> Receive
        // ========================================================

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


        // ========================================================
        // Receiveで終了
        // ========================================================

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


        // ========================================================
        // Receive -> Toss
        // ========================================================

        StartSetterTossSequence();
    }


    // ============================================================
    // Setter Toss Target Passed
    // ============================================================

    private void HandleSetterTossReachedTarget()
    {
        if (currentBallPhysics == null)
        {
            return;
        }


        Transform target =
            GetSelectedSetterTossTarget();


        Debug.Log(
            "[RallyController] Setter Toss Target Passed\n" +
            $"Target = " +
            $"{(target != null ? target.name : "null")}\n" +
            $"Ball Position = {currentBallPhysics.transform.position}\n" +
            $"Ball Velocity = {currentBallPhysics.Rigidbody.linearVelocity}"
        );


        // ========================================================
        // Tossで終了
        //
        // Ballは止めない。
        //
        // Gravityでそのまま落下を続ける。
        // ========================================================

        if (
            !ShouldContinueAfter(
                PlayPhase.Toss
            )
        )
        {
            Debug.Log(
                "[RallyController] " +
                "Simulation Finished at Toss.\n" +
                "Ball Physics continues."
            );

            return;
        }


        // ========================================================
        // Toss -> Spike
        // ========================================================

        StartSpikeSequence();
    }


    // ============================================================
    // Future : Spike
    // ============================================================

    private void StartSpikeSequence()
    {
        Debug.LogWarning(
            "[RallyController] " +
            "Spikeはまだ未実装です。\n" +
            "Toss Target通過後もBallはGravityで運動を続けます。"
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


    public void SetSetterTossApexHeight(
        float height
    )
    {
        setterTossApexHeight =
            height;
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
    // Setter Toss Target Selection
    // ============================================================

    /// <summary>
    /// 現在は単一のSetter Toss Targetを返す。
    ///
    /// 将来的にはここで、
    ///
    /// Toss Side:
    ///     Left / Right
    ///
    /// Toss Length:
    ///     Short / Normal / Long
    ///
    /// Target Offset:
    ///     X / Y / Z微調整
    ///
    /// などを判断してTargetを決定する。
    ///
    /// VolleyballBallPhysics側は変更不要。
    /// </summary>
    private Transform GetSelectedSetterTossTarget()
    {
        return setterTossTarget;
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


        currentBallPhysics.OnSetterTossReachedTarget -=
            HandleSetterTossReachedTarget;


        currentBallPhysics =
            null;
    }


    private void OnDestroy()
    {
        UnbindCurrentBall();
    }
}