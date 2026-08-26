using UnityEngine;

public class RallyController : MonoBehaviour
{
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
        Cross = 1,
        Outside = 2
    }


    public enum BlockMode
    {
        None = 0,
        Block = 1
    }


    [Header("Ball Spawner")]

    [SerializeField]
    private BallSpawner ballSpawner;


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

    [Tooltip(
        "Left Toss / Left SpikeでOutsideを選択したときのSpike Target。" +
        "ブロックアウト用のコースとして、任意のEmptyを設定する。"
    )]
    [SerializeField]
    private Transform spikeTargetOutsideLeft;

    [Tooltip(
        "Right Toss / Right SpikeでOutsideを選択したときのSpike Target。" +
        "ブロックアウト用のコースとして、任意のEmptyを設定する。"
    )]
    [SerializeField]
    private Transform spikeTargetOutsideRight;

    [SerializeField]
    private SpikeCourse selectedSpikeCourse =
        SpikeCourse.Cross;

    [Tooltip("スパイク打球直後の速度 [km/h]")]
    [SerializeField]
    private float spikeSpeedKmh =
        100.0f;


    [Header("Block")]

    [Tooltip(
        "ブロックを発生させるかどうか。" +
        "NoneならブロックColliderは常にOFF、BlockならSpike開始時のみON。"
    )]
    [SerializeField]
    private BlockMode selectedBlockMode =
        BlockMode.None;

    [Tooltip(
        "Left Toss / Left Spike用のブロックCollider Root。" +
        "見た目は不要。BoxCollider等を持つEmptyをまとめて設定する。"
    )]
    [SerializeField]
    private GameObject blockLeftSpikeRoot;

    [Tooltip(
        "Right Toss / Right Spike用のブロックCollider Root。" +
        "見た目は不要。BoxCollider等を持つEmptyをまとめて設定する。"
    )]
    [SerializeField]
    private GameObject blockRightSpikeRoot;


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


    [Header("Startup")]

    [SerializeField]
    private bool spawnOnStart =
        true;


    [Header("Keyboard Debug")]

    [SerializeField]
    private bool enableKeyboardDebug =
        true;


    private VolleyballBallPhysics currentBallPhysics;

    private Quaternion basePlayRootRotation =
        Quaternion.identity;

    private bool playRootRotationCached =
        false;


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
        selectedBlockMode;

    public float TossTargetZOffset =>
        tossTargetZOffset;


    private void Awake()
    {
        CachePlayRootRotation();
    }


    private void Start()
    {
        ApplyCourtSide();

        DisableAllBlocks();

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


        if (Input.GetKeyDown(KeyCode.V))
        {
            StartSelectedSimulation();
        }


        if (Input.GetKeyDown(KeyCode.Z))
        {
            ResetSelectedSimulation();
        }
    }


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


    public void StartSelectedSimulation()
    {
        ValidateSimulationRange();

        DisableAllBlocks();


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
        }
    }


    public void ResetSelectedSimulation()
    {
        ValidateSimulationRange();

        DisableAllBlocks();


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
        }
    }


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


    public void StartSpikeServeSequence()
    {
        DisableAllBlocks();


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


        currentBallPhysics.TossUp(
            tossHeight,
            serveHitHeight,
            tossForwardDistance,
            tossDirection
        );
    }


    private void StartReceiveSequence()
    {
        DisableAllBlocks();


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


    private void StartSetterTossSequence()
    {
        DisableAllBlocks();


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


    private void StartSpikeSequence()
    {
        if (currentBallPhysics == null)
        {
            Debug.LogWarning(
                "[RallyController] Ballがありません。"
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


        ApplyBlockForSpikePhase();


        Debug.Log(
            "[RallyController] Spike Start\n" +
            $"Toss Side = {selectedTossSide}\n" +
            $"Course = {selectedSpikeCourse}\n" +
            $"Block = {selectedBlockMode}\n" +
            $"Contact Position = " +
            $"{currentBallPhysics.transform.position}\n" +
            $"Target = {spikeTarget.name}\n" +
            $"Target Position = {spikeTarget.position}\n" +
            $"Speed = {spikeSpeedKmh:F1} km/h"
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


        DisableAllBlocks();
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
                2
            );


        selectedSpikeCourse =
            (SpikeCourse)index;
    }


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


        selectedBlockMode =
            (BlockMode)index;


        DisableAllBlocks();
    }


    public void SetBlockEnabled(
        bool enabled
    )
    {
        selectedBlockMode =
            enabled
                ? BlockMode.Block
                : BlockMode.None;


        DisableAllBlocks();
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


    private void DisableAllBlocks()
    {
        if (blockLeftSpikeRoot != null)
        {
            blockLeftSpikeRoot.SetActive(
                false
            );
        }


        if (blockRightSpikeRoot != null)
        {
            blockRightSpikeRoot.SetActive(
                false
            );
        }
    }


    private void ApplyBlockForSpikePhase()
    {
        DisableAllBlocks();


        if (
            selectedBlockMode !=
            BlockMode.Block
        )
        {
            return;
        }


        GameObject selectedBlockRoot =
            selectedTossSide ==
            TossSide.Left
                ? blockLeftSpikeRoot
                : blockRightSpikeRoot;


        if (selectedBlockRoot == null)
        {
            Debug.LogWarning(
                "[RallyController] Blockが選択されていますが、" +
                $"{selectedTossSide} Spike用のBlock Rootが設定されていません。"
            );

            return;
        }


        selectedBlockRoot.SetActive(
            true
        );


        Debug.Log(
            "[RallyController] Block Collider ON\n" +
            $"Toss Side = {selectedTossSide}\n" +
            $"Block Root = {selectedBlockRoot.name}"
        );
    }


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


    private Transform GetSelectedSpikeTarget()
    {
        if (
            selectedSpikeCourse ==
            SpikeCourse.Outside
        )
        {
            return
                selectedTossSide ==
                TossSide.Left
                    ? spikeTargetOutsideLeft
                    : spikeTargetOutsideRight;
        }


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
        DisableAllBlocks();

        UnbindCurrentBall();
    }
}