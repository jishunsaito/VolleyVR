using UnityEngine;

/// <summary>
/// ラリー全体の進行を管理する。
///
/// 現在:
///
/// Spawn
/// ↓
/// V
/// ↓
/// 斜め前へToss
/// ↓
/// Apex
/// ↓
/// 下降
/// ↓
/// Serve Hit Height
/// ↓
/// Spike Serve
/// ↓
/// Receiver Transform
///
/// Z
/// ↓
/// Respawn
///
/// Court Change:
///
/// Left Court
/// ↓
/// PlayRoot Y = 基準角度
///
/// Right Court
/// ↓
/// PlayRoot Y = 基準角度 + 180°
///
/// PlayRootの子にある
/// Player / ServePoint / ReceivePoint等を
/// 一括で反対コートへ移動する。
/// </summary>
public class RallyController : MonoBehaviour
{
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
    //
    // ここにはカットする人のTransformをそのまま登録する。
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
        "下降中にこのワールドY座標へ到達した瞬間にサーブを打つ [m]"
    )]
    [SerializeField]
    private float serveHitHeight =
        3.0f;


    [Tooltip(
        "ServeStartからサーブ打点までにコート方向へ何m進むか"
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
    // Startup
    // ============================================================

    [Header("Startup")]

    [SerializeField]
    private bool spawnOnStart =
        true;


    // ============================================================
    // Debug
    // ============================================================

    [Header("Keyboard Debug")]

    [Tooltip("V = Spike Serve / Z = Respawn")]
    [SerializeField]
    private bool enableKeyboardDebug =
        true;


    // ============================================================
    // Runtime
    // ============================================================

    private VolleyballBallPhysics currentBallPhysics;


    /// <summary>
    /// PlayRootの左コート時の基準Rotation。
    ///
    /// Inspector上でPlayRootが最初に持っていた
    /// Rotationをそのまま基準として保存する。
    /// </summary>
    private Quaternion basePlayRootRotation =
        Quaternion.identity;


    private bool playRootRotationCached =
        false;


    // ============================================================
    // Properties
    // ============================================================

    public CourtSide CurrentCourtSide =>
        currentCourtSide;


    // ============================================================
    // Unity
    // ============================================================

    private void Awake()
    {
        CachePlayRootRotation();
    }


    private void Start()
    {
        // InspectorでRightを選択した状態から
        // 開始する場合にも対応。
        ApplyCourtSide();


        if (spawnOnStart)
        {
            SpawnBallAtSelectedStart();
        }
    }


    private void Update()
    {
        if (!enableKeyboardDebug)
            return;


        // ========================================================
        // V
        //
        // Toss
        // ↓
        // Hit Height
        // ↓
        // Serve
        // ========================================================

        if (Input.GetKeyDown(KeyCode.V))
        {
            StartSpikeServeSequence();
        }


        // ========================================================
        // Z
        //
        // Respawn
        // ========================================================

        if (Input.GetKeyDown(KeyCode.Z))
        {
            SpawnBallAtSelectedStart();
        }
    }


    // ============================================================
    // Court Side
    // ============================================================

    /// <summary>
    /// PlayRootの初期Rotationを保存する。
    ///
    /// Left Court:
    ///     初期Rotation
    ///
    /// Right Court:
    ///     初期Rotation + Y 180°
    ///
    /// として使用する。
    /// </summary>
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


    /// <summary>
    /// 現在選択されているCourtSideを
    /// PlayRootへ反映する。
    /// </summary>
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


        switch (currentCourtSide)
        {
            // ----------------------------------------------------
            // Left
            //
            // 元のプレー配置
            // ----------------------------------------------------

            case CourtSide.Left:

                playRoot.localRotation =
                    basePlayRootRotation;

                break;


            // ----------------------------------------------------
            // Right
            //
            // コート中央を中心に180°回転。
            //
            // 原点中心なら
            //
            // (x, y, z)
            // ↓
            // (-x, y, -z)
            //
            // と同じ。
            // ----------------------------------------------------

            case CourtSide.Right:

                playRoot.localRotation =
                    basePlayRootRotation *
                    Quaternion.Euler(
                        0.0f,
                        180.0f,
                        0.0f
                    );

                break;
        }
    }


    /// <summary>
    /// CourtChangeボタンから呼ぶ。
    ///
    /// Left <-> Right
    ///
    /// を切り替える。
    ///
    /// PlayRootを反転した後、
    /// 現在存在するBallを削除して
    /// 新しいServeStart位置へRespawnする。
    /// </summary>
    public void ToggleCourt()
    {
        if (playRoot == null)
        {
            Debug.LogError(
                "[RallyController] PlayRoot が設定されていません。"
            );

            return;
        }


        // ========================================================
        // Court Side切り替え
        // ========================================================

        if (currentCourtSide == CourtSide.Left)
        {
            currentCourtSide =
                CourtSide.Right;
        }
        else
        {
            currentCourtSide =
                CourtSide.Left;
        }


        // ========================================================
        // PlayRootへ反映
        // ========================================================

        ApplyCourtSide();


        Debug.Log(
            "[RallyController] Court Changed\n" +
            $"Court Side = {currentCourtSide}\n" +
            $"PlayRoot Position = {playRoot.position}\n" +
            $"PlayRoot Rotation = {playRoot.localEulerAngles}"
        );


        // ========================================================
        // 既存Ballは反転しないのでRespawn
        //
        // PlayRootを反転した後なので、
        // ServeStart.positionも既に反対側になっている。
        // ========================================================

        SpawnBallAtSelectedStart();
    }


    // ============================================================
    // Spawn
    // ============================================================

    public void SpawnBallAtSelectedStart()
    {
        if (ballSpawner == null)
        {
            Debug.LogError(
                "[RallyController] BallSpawner が設定されていません。"
            );

            return;
        }


        Transform spawnPoint =
            GetSelectedServeStart();


        if (spawnPoint == null)
        {
            Debug.LogError(
                "[RallyController] ServeStart が設定されていません。"
            );

            return;
        }


        // 古いBallとのイベント解除
        UnbindCurrentBall();


        // ========================================================
        // Respawn
        // ========================================================

        GameObject newBall =
            ballSpawner.RespawnBall(
                spawnPoint
            );


        if (newBall == null)
            return;


        // ========================================================
        // Physics
        // ========================================================

        currentBallPhysics =
            newBall.GetComponent<VolleyballBallPhysics>();


        if (currentBallPhysics == null)
        {
            Debug.LogError(
                "[RallyController] " +
                "Ball PrefabにVolleyballBallPhysicsがありません。"
            );

            return;
        }


        // ========================================================
        // Events
        // ========================================================

        currentBallPhysics.OnTossApex +=
            HandleTossApex;


        currentBallPhysics.OnServeHitPoint +=
            HandleServeHitPoint;


        currentBallPhysics.OnServeReachedTarget +=
            HandleServeReachedTarget;


        Debug.Log(
            "[RallyController] Spawn\n" +
            $"Court = {currentCourtSide}\n" +
            $"Start = {selectedServeStart}\n" +
            $"Position = {spawnPoint.position}"
        );
    }


    // ============================================================
    // Serve Sequence Start
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
        // Toss方向
        //
        // 現在のBall
        // →
        // 選択されたReceiver
        //
        // のXZ方向
        //
        // PlayRoot反転後でも
        // World Positionから計算するため
        // 既存処理をそのまま使用できる。
        // ========================================================

        Vector3 tossDirection =
            target.position -
            currentBallPhysics.transform.position;


        tossDirection.y =
            0.0f;


        if (tossDirection.sqrMagnitude <= 0.0001f)
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


        // ========================================================
        // Toss
        // ========================================================

        currentBallPhysics.TossUp(
            tossHeight,
            serveHitHeight,
            tossForwardDistance,
            tossDirection
        );
    }


    // ============================================================
    // Apex
    // ============================================================

    /// <summary>
    /// 最高点ではまだ打たない。
    /// </summary>
    private void HandleTossApex()
    {
        if (currentBallPhysics == null)
            return;


        Debug.Log(
            "[RallyController] Toss Apex\n" +
            $"Position = {currentBallPhysics.transform.position}"
        );
    }


    // ============================================================
    // Serve Hit Point
    // ============================================================

    /// <summary>
    /// 下降中にServe Hit Heightへ到達。
    ///
    /// この瞬間のReceiver Transformを取得し、
    /// そのTransformを直接BallPhysicsへ渡す。
    /// </summary>
    private void HandleServeHitPoint()
    {
        if (currentBallPhysics == null)
            return;


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


        // ========================================================
        // Receiver Transformそのものを渡す。
        //
        // PlayRootが反転していれば
        // target.positionも反転後のWorld座標になる。
        // ========================================================

        currentBallPhysics.SpikeServe(
            target,
            serveSpeedKmh,
            spikeServeLaunchAngle
        );
    }


    // ============================================================
    // Receiver Arrival
    // ============================================================

    /// <summary>
    /// BallがReceiver Transformへ到達した瞬間。
    ///
    /// 次の段階ではここから
    /// Receive / Cutを開始する。
    /// </summary>
    private void HandleServeReachedTarget()
    {
        if (currentBallPhysics == null)
            return;


        Transform target =
            GetSelectedServeTarget();


        Debug.Log(
            "[RallyController] Serve Reached Receiver\n" +
            $"Receiver = {(target != null ? target.name : "null")}\n" +
            $"Ball Position = {currentBallPhysics.transform.position}"
        );


        // ========================================================
        // 次の段階:
        //
        // currentBallPhysics.Receive(...);
        //
        // などをここから呼ぶ。
        // ========================================================
    }


    // ============================================================
    // External Control
    // ============================================================

    public void SetServeStart(int index)
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


    public void SetServeStartAndRespawn(int index)
    {
        SetServeStart(index);

        SpawnBallAtSelectedStart();
    }


    public void SetServeTarget(int index)
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


    public void SetServeSpeed(float speedKmh)
    {
        serveSpeedKmh =
            Mathf.Max(
                0.0f,
                speedKmh
            );
    }


    public void SetSpikeServeLaunchAngle(float angleDeg)
    {
        spikeServeLaunchAngle =
            angleDeg;
    }


    public void SetTossHeight(float height)
    {
        tossHeight =
            Mathf.Max(
                0.0f,
                height
            );
    }


    public void SetServeHitHeight(float height)
    {
        serveHitHeight =
            height;
    }


    public void SetTossForwardDistance(float distance)
    {
        tossForwardDistance =
            Mathf.Max(
                0.0f,
                distance
            );
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
    // Event Cleanup
    // ============================================================

    private void UnbindCurrentBall()
    {
        if (currentBallPhysics == null)
            return;


        currentBallPhysics.OnTossApex -=
            HandleTossApex;


        currentBallPhysics.OnServeHitPoint -=
            HandleServeHitPoint;


        currentBallPhysics.OnServeReachedTarget -=
            HandleServeReachedTarget;


        currentBallPhysics =
            null;
    }


    private void OnDestroy()
    {
        UnbindCurrentBall();
    }
}