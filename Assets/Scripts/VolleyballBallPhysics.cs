using System;
using UnityEngine;

/// <summary>
/// バレーボール1個の運動を担当する。
///
/// 現在実装:
///
/// 1. サーブ前トス
/// 2. スパイクサーブ
/// 3. サーブカット
/// 4. セッタートス
///
/// サーブカット:
///
/// Receiver
/// ↓
/// 指定したReceive Apex Heightまで上昇
/// ↓
/// 重力に従って自然落下
/// ↓
/// Setter
///
/// セッタートス:
///
/// Setter
/// ↓
/// 指定したSetter Toss Apex Heightまで上昇
/// ↓
/// 重力に従って自然落下
/// ↓
/// Setter Toss Targetを通過
/// ↓
/// そのまま飛び続ける
///
/// Receive / Setter Tossともに
/// XZ方向は等速、Y方向はUnity Gravityに従う。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VolleyballBallPhysics : MonoBehaviour
{
    // ============================================================
    // State
    // ============================================================

    public enum BallMotionState
    {
        Waiting,
        Tossing,
        SpikeServing,
        AtServeTarget,
        ReceiveContact,
        Receiving,
        AtSetterTarget,
        SetterTossing,
        AfterSetterTossTarget
    }


    // ============================================================
    // Ball Physics
    // ============================================================

    [Header("Ball Physics")]

    [Tooltip("バレーボールの質量 [kg]")]
    [SerializeField]
    private float ballMass =
        0.27f;


    [Tooltip(
        "軌道計算を正確にするため現段階では0推奨"
    )]
    [SerializeField]
    private float linearDamping =
        0.0f;


    [Tooltip("回転減衰")]
    [SerializeField]
    private float angularDamping =
        0.02f;


    // ============================================================
    // Spike Serve Spin
    // ============================================================

    [Header("Spike Serve Spin")]

    [Tooltip("トップスピン [rpm]")]
    [SerializeField]
    private float spikeSpinRpm =
        1200.0f;


    // ============================================================
    // Runtime
    // ============================================================

    [Header("Runtime")]

    [SerializeField]
    private BallMotionState currentState =
        BallMotionState.Waiting;


    // ============================================================
    // Serve Toss Runtime
    // ============================================================

    [Header("Serve Toss Runtime")]

    [Tooltip("今回のサーブ前トスの最高到達Y座標")]
    [SerializeField]
    private float calculatedTossApexY =
        0.0f;


    [Tooltip("サーブを打つY座標")]
    [SerializeField]
    private float currentServeHitHeight =
        0.0f;


    [Tooltip(
        "サーブ前トス開始からサーブを打つまでの時間 [s]"
    )]
    [SerializeField]
    private float calculatedTimeToHit =
        0.0f;


    // ============================================================
    // Spike Serve Runtime
    // ============================================================

    [Header("Spike Serve Runtime")]

    [Tooltip(
        "サーブ打球からReceiver到達までの時間 [s]"
    )]
    [SerializeField]
    private float calculatedFlightTime =
        0.0f;


    [Tooltip("サーブ開始後の経過時間 [s]")]
    [SerializeField]
    private float serveElapsedTime =
        0.0f;


    [Tooltip(
        "Receiverへ到達させるための追加鉛直加速度。" +
        "負なら下向き"
    )]
    [SerializeField]
    private float additionalVerticalAcceleration =
        0.0f;


    [Tooltip(
        "サーブ開始時に取得したReceiver位置"
    )]
    [SerializeField]
    private Vector3 serveTargetPositionAtLaunch;


    // ============================================================
    // Receive Runtime
    // ============================================================

    [Header("Receive Runtime")]

    [Tooltip("Receive開始位置")]
    [SerializeField]
    private Vector3 receiveStartPosition;


    [Tooltip(
        "Receive開始時に取得したSetter位置"
    )]
    [SerializeField]
    private Vector3 receiveTargetPositionAtLaunch;


    [Tooltip(
        "今回のReceive最高到達Y座標"
    )]
    [SerializeField]
    private float currentReceiveApexHeight =
        0.0f;


    [Tooltip(
        "ReceiverからSetterまでの飛行時間 [s]"
    )]
    [SerializeField]
    private float calculatedReceiveFlightTime =
        0.0f;


    [Tooltip(
        "Receiverから最高点までの時間 [s]"
    )]
    [SerializeField]
    private float calculatedReceiveTimeToApex =
        0.0f;


    [Tooltip(
        "Receive開始後の経過時間 [s]"
    )]
    [SerializeField]
    private float receiveElapsedTime =
        0.0f;


    [Tooltip(
        "Receiver位置で停止する残りFixedUpdate数"
    )]
    [SerializeField]
    private int receiveContactFramesRemaining =
        0;


    [Tooltip(
        "今回のReceive Backspin [rpm]"
    )]
    [SerializeField]
    private float currentReceiveBackspinRpm =
        0.0f;


    // ============================================================
    // Setter Toss Runtime
    // ============================================================

    [Header("Setter Toss Runtime")]

    [Tooltip("Setter Toss開始位置")]
    [SerializeField]
    private Vector3 setterTossStartPosition;


    [Tooltip(
        "Setter Toss開始時に取得したTarget位置"
    )]
    [SerializeField]
    private Vector3 setterTossTargetPositionAtLaunch;


    [Tooltip(
        "今回のSetter Toss最高到達Y座標"
    )]
    [SerializeField]
    private float currentSetterTossApexHeight =
        0.0f;


    [Tooltip(
        "SetterからToss Targetまでの飛行時間 [s]"
    )]
    [SerializeField]
    private float calculatedSetterTossFlightTime =
        0.0f;


    [Tooltip(
        "Setterから最高点までの時間 [s]"
    )]
    [SerializeField]
    private float calculatedSetterTossTimeToApex =
        0.0f;


    [Tooltip(
        "Setter Toss開始後の経過時間 [s]"
    )]
    [SerializeField]
    private float setterTossElapsedTime =
        0.0f;


    [Tooltip(
        "Setter Toss開始時のVelocity"
    )]
    [SerializeField]
    private Vector3 setterTossLaunchVelocity;


    // ============================================================
    // Internal
    // ============================================================

    private Rigidbody rb;


    private bool apexReached =
        false;


    private bool serveHitTriggered =
        false;


    private Vector3 previousPosition;


    private Transform currentServeTarget;


    private Transform currentReceiveTarget;


    private Transform currentSetterTossTarget;


    // ============================================================
    // Events
    // ============================================================

    /// <summary>
    /// サーブ前トスの最高点。
    /// </summary>
    public event Action OnTossApex;


    /// <summary>
    /// 下降中にServe Hit Heightへ到達。
    /// RallyControllerがここでサーブを打つ。
    /// </summary>
    public event Action OnServeHitPoint;


    /// <summary>
    /// サーブがReceiverへ到達。
    /// </summary>
    public event Action OnServeReachedTarget;


    /// <summary>
    /// サーブカットがSetterへ到達。
    /// </summary>
    public event Action OnReceiveReachedTarget;


    /// <summary>
    /// Setter TossがToss Targetを通過。
    ///
    /// このイベントではBallを停止させない。
    ///
    /// 将来的にはRallyControllerが
    /// ここでSpikeを開始する。
    /// </summary>
    public event Action OnSetterTossReachedTarget;


    // ============================================================
    // Properties
    // ============================================================

    public Rigidbody Rigidbody =>
        rb;


    public BallMotionState CurrentState =>
        currentState;


    // ============================================================
    // Unity
    // ============================================================

    private void Awake()
    {
        rb =
            GetComponent<Rigidbody>();


        ConfigureRigidbody();


        previousPosition =
            rb.position;
    }


    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }


        // ========================================================
        // Receive Contact
        //
        // Kinematicなので通常処理より先
        // ========================================================

        if (
            currentState ==
            BallMotionState.ReceiveContact
        )
        {
            UpdateReceiveContact();


            previousPosition =
                rb.position;


            return;
        }


        // ========================================================
        // Kinematic
        // ========================================================

        if (rb.isKinematic)
        {
            previousPosition =
                rb.position;


            return;
        }


        // ========================================================
        // Serve Toss
        // ========================================================

        if (
            currentState ==
            BallMotionState.Tossing
        )
        {
            DetectTossState();
        }


        // ========================================================
        // Spike Serve
        // ========================================================

        if (
            currentState ==
            BallMotionState.SpikeServing
        )
        {
            UpdateSpikeServe();
        }


        // ========================================================
        // Receive
        // ========================================================

        if (
            currentState ==
            BallMotionState.Receiving
        )
        {
            UpdateReceive();
        }


        // ========================================================
        // Setter Toss
        // ========================================================

        if (
            currentState ==
            BallMotionState.SetterTossing
        )
        {
            UpdateSetterToss();
        }


        previousPosition =
            rb.position;
    }


    // ============================================================
    // Rigidbody Setup
    // ============================================================

    private void ConfigureRigidbody()
    {
        rb.mass =
            ballMass;


        rb.linearDamping =
            linearDamping;


        rb.angularDamping =
            angularDamping;


        // 130 km/h以上にも対応
        rb.maxLinearVelocity =
            100.0f;


        // 1200 rpm等にも対応
        rb.maxAngularVelocity =
            250.0f;


        // Spawn直後は固定
        rb.useGravity =
            false;


        rb.isKinematic =
            true;


        rb.interpolation =
            RigidbodyInterpolation.Interpolate;


        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;


        currentState =
            BallMotionState.Waiting;
    }


    // ============================================================
    // Serve Toss
    // ============================================================

    /// <summary>
    /// サーブ前の斜め前トス。
    ///
    /// tossHeight:
    ///     現在位置から最高点までの上昇量 [m]
    ///
    /// serveHitHeight:
    ///     サーブを打つWorld Y [m]
    ///
    /// tossForwardDistance:
    ///     サーブ打点までにXZ方向へ進む距離 [m]
    /// </summary>
    public void TossUp(
        float tossHeight,
        float serveHitHeight,
        float tossForwardDistance,
        Vector3 forwardDirection
    )
    {
        if (
            currentState !=
            BallMotionState.Waiting
        )
        {
            Debug.LogWarning(
                "[Ball] Waiting状態ではないためトスできません。"
            );

            return;
        }


        if (tossHeight <= 0.0f)
        {
            Debug.LogWarning(
                "[Ball] Toss Height は0より大きくしてください。"
            );

            return;
        }


        if (tossForwardDistance < 0.0f)
        {
            Debug.LogWarning(
                "[Ball] Toss Forward Distance は0以上にしてください。"
            );

            return;
        }


        Vector3 startPosition =
            rb.position;


        float startY =
            startPosition.y;


        // ========================================================
        // Apex
        // ========================================================

        calculatedTossApexY =
            startY +
            tossHeight;


        if (
            serveHitHeight >=
            calculatedTossApexY
        )
        {
            Debug.LogError(
                "[Ball] Serve Hit Height がToss最高点以上です。\n" +
                $"Start Y = {startY:F2} m\n" +
                $"Apex Y = {calculatedTossApexY:F2} m\n" +
                $"Hit Y = {serveHitHeight:F2} m"
            );

            return;
        }


        currentServeHitHeight =
            serveHitHeight;


        // ========================================================
        // Gravity
        // ========================================================

        float gravity =
            Mathf.Abs(
                Physics.gravity.y
            );


        if (gravity <= 0.0001f)
        {
            Debug.LogError(
                "[Ball] Gravity Y が0です。"
            );

            return;
        }


        // ========================================================
        // Vertical Toss Velocity
        //
        // v² = 2gh
        // ========================================================

        float initialUpVelocity =
            Mathf.Sqrt(
                2.0f *
                gravity *
                tossHeight
            );


        // ========================================================
        // 下降中にHit Heightへ到達する時間
        // ========================================================

        float deltaY =
            serveHitHeight -
            startY;


        float discriminant =
            initialUpVelocity *
            initialUpVelocity
            -
            2.0f *
            gravity *
            deltaY;


        if (discriminant < 0.0f)
        {
            Debug.LogError(
                "[Ball] Serve Hit Heightへ到達できません。"
            );

            return;
        }


        calculatedTimeToHit =
            (
                initialUpVelocity +
                Mathf.Sqrt(
                    discriminant
                )
            )
            /
            gravity;


        // ========================================================
        // Forward Direction
        // ========================================================

        forwardDirection.y =
            0.0f;


        if (
            forwardDirection.sqrMagnitude <=
            0.0001f
        )
        {
            Debug.LogError(
                "[Ball] Toss Forward Direction が0です。"
            );

            return;
        }


        forwardDirection.Normalize();


        float forwardSpeed =
            tossForwardDistance /
            calculatedTimeToHit;


        // ========================================================
        // Toss Velocity
        // ========================================================

        Vector3 tossVelocity =
            Vector3.up *
            initialUpVelocity
            +
            forwardDirection *
            forwardSpeed;


        ActivatePhysics();


        rb.linearVelocity =
            tossVelocity;


        rb.angularVelocity =
            Vector3.zero;


        apexReached =
            false;


        serveHitTriggered =
            false;


        previousPosition =
            rb.position;


        currentState =
            BallMotionState.Tossing;


        float timeToApex =
            initialUpVelocity /
            gravity;


        Debug.Log(
            "[Ball] Serve Toss\n" +
            $"Start = {startPosition}\n" +
            $"Toss Height = {tossHeight:F2} m\n" +
            $"Apex Y = {calculatedTossApexY:F2} m\n" +
            $"Serve Hit Y = {serveHitHeight:F2} m\n" +
            $"Forward Distance = {tossForwardDistance:F2} m\n" +
            $"Time To Apex = {timeToApex:F3} s\n" +
            $"Time To Hit = {calculatedTimeToHit:F3} s\n" +
            $"Toss Velocity = {tossVelocity}"
        );
    }


    // ============================================================
    // Serve Toss Detection
    // ============================================================

    private void DetectTossState()
    {
        // ========================================================
        // Apex
        // ========================================================

        if (!apexReached)
        {
            if (rb.linearVelocity.y <= 0.0f)
            {
                apexReached =
                    true;


                Debug.Log(
                    "[Ball] Serve Toss Apex\n" +
                    $"Position = {rb.position}"
                );


                OnTossApex?.Invoke();
            }


            return;
        }


        // ========================================================
        // Serve Hit Point
        // ========================================================

        if (serveHitTriggered)
        {
            return;
        }


        if (rb.linearVelocity.y >= 0.0f)
        {
            return;
        }


        float previousY =
            previousPosition.y;


        float currentY =
            rb.position.y;


        if (
            previousY > currentServeHitHeight &&
            currentY <= currentServeHitHeight
        )
        {
            serveHitTriggered =
                true;


            float denominator =
                previousY -
                currentY;


            float t =
                1.0f;


            if (
                Mathf.Abs(
                    denominator
                ) >
                0.00001f
            )
            {
                t =
                    (
                        previousY -
                        currentServeHitHeight
                    )
                    /
                    denominator;
            }


            t =
                Mathf.Clamp01(
                    t
                );


            Vector3 exactHitPosition =
                Vector3.Lerp(
                    previousPosition,
                    rb.position,
                    t
                );


            exactHitPosition.y =
                currentServeHitHeight;


            rb.position =
                exactHitPosition;


            Debug.Log(
                "[Ball] Serve Hit Point\n" +
                $"Position = {exactHitPosition}\n" +
                $"Height = {currentServeHitHeight:F2} m"
            );


            OnServeHitPoint?.Invoke();
        }
    }


    // ============================================================
    // Spike Serve
    // ============================================================

    /// <summary>
    /// Receiver Transformへ向けてスパイクサーブする。
    /// </summary>
    public void SpikeServe(
        Transform targetTransform,
        float speedKmh,
        float launchAngleDeg
    )
    {
        if (targetTransform == null)
        {
            Debug.LogError(
                "[Ball] Serve Target Transform がnullです。"
            );

            return;
        }


        if (speedKmh <= 0.0f)
        {
            Debug.LogError(
                "[Ball] Serve Speed は0より大きくしてください。"
            );

            return;
        }


        // ========================================================
        // Target
        // ========================================================

        currentServeTarget =
            targetTransform;


        Vector3 targetPosition =
            targetTransform.position;


        serveTargetPositionAtLaunch =
            targetPosition;


        Vector3 start =
            rb.position;


        // ========================================================
        // km/h -> m/s
        // ========================================================

        float speed =
            speedKmh /
            3.6f;


        // ========================================================
        // TargetへのXZ方向
        // ========================================================

        Vector3 horizontalVector =
            targetPosition -
            start;


        horizontalVector.y =
            0.0f;


        float horizontalDistance =
            horizontalVector.magnitude;


        if (horizontalDistance < 0.01f)
        {
            Debug.LogError(
                "[Ball] Receiverとの水平距離が小さすぎます。"
            );

            return;
        }


        Vector3 horizontalDirection =
            horizontalVector.normalized;


        // ========================================================
        // Launch Angle
        // ========================================================

        float angleRad =
            launchAngleDeg *
            Mathf.Deg2Rad;


        float horizontalSpeed =
            speed *
            Mathf.Cos(
                angleRad
            );


        float verticalSpeed =
            speed *
            Mathf.Sin(
                angleRad
            );


        if (horizontalSpeed <= 0.01f)
        {
            Debug.LogError(
                "[Ball] Launch Angle が不正です。"
            );

            return;
        }


        // ========================================================
        // Receiverまでの時間
        // ========================================================

        calculatedFlightTime =
            horizontalDistance /
            horizontalSpeed;


        if (calculatedFlightTime <= 0.0f)
        {
            Debug.LogError(
                "[Ball] Flight Timeを計算できません。"
            );

            return;
        }


        // ========================================================
        // Vertical Acceleration
        // ========================================================

        float verticalDifference =
            targetPosition.y -
            start.y;


        float requiredVerticalAcceleration =
            2.0f *
            (
                verticalDifference -
                verticalSpeed *
                calculatedFlightTime
            )
            /
            (
                calculatedFlightTime *
                calculatedFlightTime
            );


        additionalVerticalAcceleration =
            requiredVerticalAcceleration -
            Physics.gravity.y;


        // ========================================================
        // Launch Velocity
        // ========================================================

        Vector3 launchVelocity =
            horizontalDirection *
            horizontalSpeed
            +
            Vector3.up *
            verticalSpeed;


        ActivatePhysics();


        rb.linearVelocity =
            launchVelocity;


        // ========================================================
        // Top Spin
        // ========================================================

        Vector3 lateralAxis =
            Vector3.Cross(
                Vector3.up,
                horizontalDirection
            ).normalized;


        float angularSpeed =
            spikeSpinRpm *
            2.0f *
            Mathf.PI /
            60.0f;


        rb.angularVelocity =
            -lateralAxis *
            angularSpeed;


        serveElapsedTime =
            0.0f;


        currentState =
            BallMotionState.SpikeServing;


        float actualSpeedKmh =
            rb.linearVelocity.magnitude *
            3.6f;


        Debug.Log(
            "[Ball] Spike Serve\n" +
            $"Receiver = {targetTransform.name}\n" +
            $"Hit Position = {start}\n" +
            $"Receiver Position = {targetPosition}\n" +
            $"Input Speed = {speedKmh:F1} km/h\n" +
            $"Actual Speed = {actualSpeedKmh:F1} km/h\n" +
            $"Launch Angle = {launchAngleDeg:F2} deg\n" +
            $"Flight Time = {calculatedFlightTime:F3} s\n" +
            $"Required Vertical Acceleration = " +
            $"{requiredVerticalAcceleration:F2} m/s²\n" +
            $"Additional Vertical Acceleration = " +
            $"{additionalVerticalAcceleration:F2} m/s²"
        );
    }


    // ============================================================
    // Spike Serve Update
    // ============================================================

    private void UpdateSpikeServe()
    {
        rb.AddForce(
            Vector3.up *
            additionalVerticalAcceleration,
            ForceMode.Acceleration
        );


        serveElapsedTime +=
            Time.fixedDeltaTime;


        if (
            serveElapsedTime >=
            calculatedFlightTime
        )
        {
            CompleteServeAtTarget();
        }
    }


    // ============================================================
    // Serve Target Arrival
    // ============================================================

    private void CompleteServeAtTarget()
    {
        if (currentServeTarget == null)
        {
            Debug.LogError(
                "[Ball] Serve Target が失われました。"
            );

            return;
        }


        Vector3 exactTargetPosition =
            currentServeTarget.position;


        rb.position =
            exactTargetPosition;


        additionalVerticalAcceleration =
            0.0f;


        StopPhysics();


        currentState =
            BallMotionState.AtServeTarget;


        Debug.Log(
            "[Ball] Serve Reached Receiver\n" +
            $"Receiver = {currentServeTarget.name}\n" +
            $"Ball Position = {rb.position}\n" +
            $"Receiver Position = {currentServeTarget.position}"
        );


        OnServeReachedTarget?.Invoke();
    }


    // ============================================================
    // Receive / Serve Cut
    // ============================================================

    /// <summary>
    /// Receiver位置からSetterへサーブカットする。
    ///
    /// receiveApexHeight:
    ///     Receive軌道の最高到達World Y [m]
    ///
    /// Y:
    ///     Unity Gravityに完全に従う。
    ///
    /// XZ:
    ///     Receiver -> Setterを一定速度で移動。
    ///
    /// Apex Heightから初速度・飛行時間を逆算する。
    /// </summary>
    public void ReceiveToSetter(
        Transform setterTarget,
        float receiveApexHeight,
        float backspinRpm,
        int contactFixedFrames = 1
    )
    {
        if (setterTarget == null)
        {
            Debug.LogError(
                "[Ball] Setter Target がnullです。"
            );

            return;
        }


        // Receive単体実行:
        //     Waiting
        //
        // Serveから継続:
        //     AtServeTarget

        if (
            currentState != BallMotionState.Waiting &&
            currentState != BallMotionState.AtServeTarget
        )
        {
            Debug.LogWarning(
                "[Ball] 現在のStateではReceiveを開始できません。\n" +
                $"State = {currentState}"
            );

            return;
        }


        Vector3 start =
            rb.position;


        Vector3 target =
            setterTarget.position;


        // ========================================================
        // Apex Validation
        // ========================================================

        float minimumApexHeight =
            Mathf.Max(
                start.y,
                target.y
            );


        if (
            receiveApexHeight <=
            minimumApexHeight
        )
        {
            Debug.LogError(
                "[Ball] Receive Apex Height が低すぎます。\n" +
                $"Receiver Y = {start.y:F3} m\n" +
                $"Setter Y = {target.y:F3} m\n" +
                $"Apex Y = {receiveApexHeight:F3} m\n" +
                "Apex HeightはReceiverとSetterの両方より高くしてください。"
            );

            return;
        }


        // ========================================================
        // Save
        // ========================================================

        currentReceiveTarget =
            setterTarget;


        receiveStartPosition =
            start;


        receiveTargetPositionAtLaunch =
            target;


        currentReceiveApexHeight =
            receiveApexHeight;


        currentReceiveBackspinRpm =
            backspinRpm;


        receiveContactFramesRemaining =
            Mathf.Max(
                0,
                contactFixedFrames
            );


        // ========================================================
        // Receiverで一度Physicsを止める
        // ========================================================

        rb.position =
            start;


        StopPhysics();


        // ========================================================
        // Contact Frame = 0
        // ========================================================

        if (
            receiveContactFramesRemaining <=
            0
        )
        {
            LaunchReceive();

            return;
        }


        currentState =
            BallMotionState.ReceiveContact;


        Debug.Log(
            "[Ball] Receive Contact\n" +
            $"Receiver Position = {start}\n" +
            $"Setter = {setterTarget.name}\n" +
            $"Setter Position = {target}\n" +
            $"Apex Height = {receiveApexHeight:F3} m\n" +
            $"Contact Fixed Frames = {receiveContactFramesRemaining}"
        );
    }


    // ============================================================
    // Receive Contact
    // ============================================================

    private void UpdateReceiveContact()
    {
        receiveContactFramesRemaining--;


        if (
            receiveContactFramesRemaining <=
            0
        )
        {
            LaunchReceive();
        }
    }


    // ============================================================
    // Receive Launch
    // ============================================================

    private void LaunchReceive()
    {
        if (currentReceiveTarget == null)
        {
            Debug.LogError(
                "[Ball] Setter Target が失われました。"
            );

            return;
        }


        Vector3 start =
            rb.position;


        Vector3 target =
            currentReceiveTarget.position;


        receiveStartPosition =
            start;


        receiveTargetPositionAtLaunch =
            target;


        // ========================================================
        // Gravity
        // ========================================================

        float gravity =
            Mathf.Abs(
                Physics.gravity.y
            );


        if (gravity <= 0.0001f)
        {
            Debug.LogError(
                "[Ball] Gravity Y が0です。"
            );

            return;
        }


        // ========================================================
        // Receiver -> Apex
        // ========================================================

        float riseHeight =
            currentReceiveApexHeight -
            start.y;


        if (riseHeight <= 0.0f)
        {
            Debug.LogError(
                "[Ball] Receive Apex Height がReceiver以下です。"
            );

            return;
        }


        float initialVerticalSpeed =
            Mathf.Sqrt(
                2.0f *
                gravity *
                riseHeight
            );


        calculatedReceiveTimeToApex =
            initialVerticalSpeed /
            gravity;


        // ========================================================
        // Apex -> Setter
        // ========================================================

        float fallHeight =
            currentReceiveApexHeight -
            target.y;


        if (fallHeight < 0.0f)
        {
            Debug.LogError(
                "[Ball] Receive Apex Height がSetterより低いです。"
            );

            return;
        }


        float timeFromApexToSetter =
            Mathf.Sqrt(
                2.0f *
                fallHeight /
                gravity
            );


        // ========================================================
        // Total Flight Time
        // ========================================================

        calculatedReceiveFlightTime =
            calculatedReceiveTimeToApex +
            timeFromApexToSetter;


        if (
            calculatedReceiveFlightTime <=
            0.0f
        )
        {
            Debug.LogError(
                "[Ball] Receive Flight Timeを計算できません。"
            );

            return;
        }


        // ========================================================
        // XZ
        // ========================================================

        Vector3 horizontalVector =
            target -
            start;


        horizontalVector.y =
            0.0f;


        Vector3 horizontalVelocity =
            horizontalVector /
            calculatedReceiveFlightTime;


        // ========================================================
        // Initial Velocity
        // ========================================================

        Vector3 receiveVelocity =
            horizontalVelocity +
            Vector3.up *
            initialVerticalSpeed;


        // ========================================================
        // Physics ON
        // ========================================================

        ActivatePhysics();


        // この後はGravityのみ
        rb.linearVelocity =
            receiveVelocity;


        // ========================================================
        // Backspin
        // ========================================================

        if (
            horizontalVector.sqrMagnitude >
            0.0001f
        )
        {
            Vector3 horizontalDirection =
                horizontalVector.normalized;


            Vector3 lateralAxis =
                Vector3.Cross(
                    Vector3.up,
                    horizontalDirection
                ).normalized;


            float angularSpeed =
                currentReceiveBackspinRpm *
                2.0f *
                Mathf.PI /
                60.0f;


            rb.angularVelocity =
                lateralAxis *
                angularSpeed;
        }
        else
        {
            rb.angularVelocity =
                Vector3.zero;
        }


        receiveElapsedTime =
            0.0f;


        currentState =
            BallMotionState.Receiving;


        // ========================================================
        // Calculated Apex
        // ========================================================

        Vector3 calculatedApexPosition =
            start +
            horizontalVelocity *
            calculatedReceiveTimeToApex;


        calculatedApexPosition.y =
            currentReceiveApexHeight;


        Debug.Log(
            "[Ball] Receive Launch\n" +
            $"Receiver = {start}\n" +
            $"Setter = {target}\n" +
            $"Apex Height = {currentReceiveApexHeight:F3} m\n" +
            $"Calculated Apex Position = {calculatedApexPosition}\n" +
            $"Initial Vertical Speed = {initialVerticalSpeed:F3} m/s\n" +
            $"Horizontal Velocity = {horizontalVelocity}\n" +
            $"Time To Apex = {calculatedReceiveTimeToApex:F3} s\n" +
            $"Total Flight Time = {calculatedReceiveFlightTime:F3} s\n" +
            $"Backspin = {currentReceiveBackspinRpm:F1} rpm"
        );
    }


    // ============================================================
    // Receive Update
    // ============================================================

    private void UpdateReceive()
    {
        // Receive中は追加Forceなし。
        // Gravityのみ。

        receiveElapsedTime +=
            Time.fixedDeltaTime;


        if (
            receiveElapsedTime >=
            calculatedReceiveFlightTime
        )
        {
            CompleteReceiveAtTarget();
        }
    }


    // ============================================================
    // Receive Arrival
    // ============================================================

    private void CompleteReceiveAtTarget()
    {
        // Setterへ正確に合わせる。
        //
        // Receiveの後はSetter Tossが開始されるため、
        // ここでは一度停止する。

        rb.position =
            receiveTargetPositionAtLaunch;


        StopPhysics();


        currentState =
            BallMotionState.AtSetterTarget;


        Debug.Log(
            "[Ball] Receive Reached Setter\n" +
            $"Setter = " +
            $"{(currentReceiveTarget != null ? currentReceiveTarget.name : "null")}\n" +
            $"Ball Position = {rb.position}\n" +
            $"Flight Time = {calculatedReceiveFlightTime:F3} s"
        );


        OnReceiveReachedTarget?.Invoke();
    }


    // ============================================================
    // Setter Toss
    // ============================================================

    /// <summary>
    /// SetterからToss Targetへトスする。
    ///
    /// setterTossApexHeight:
    ///     トスの最高到達World Y [m]
    ///
    /// Setter位置・Target位置・Apex Height・Gravityから
    /// 必要な初速度を逆算する。
    ///
    /// Launch後は追加Forceなし。
    /// Unity Gravityだけで移動する。
    ///
    /// Targetに到達しても停止しない。
    /// Target位置を通過した瞬間にEventを発火し、
    /// そのまま自然落下を継続する。
    /// </summary>
    public void SetterToss(
        Transform targetTransform,
        float setterTossApexHeight
    )
    {
        if (targetTransform == null)
        {
            Debug.LogError(
                "[Ball] Setter Toss Target がnullです。"
            );

            return;
        }


        // Toss単体:
        //     Waiting
        //
        // Receiveから継続:
        //     AtSetterTarget

        if (
            currentState != BallMotionState.Waiting &&
            currentState != BallMotionState.AtSetterTarget
        )
        {
            Debug.LogWarning(
                "[Ball] 現在のStateではSetter Tossを開始できません。\n" +
                $"State = {currentState}"
            );

            return;
        }


        Vector3 start =
            rb.position;


        Vector3 target =
            targetTransform.position;


        // ========================================================
        // Apex Validation
        // ========================================================

        float minimumApexHeight =
            Mathf.Max(
                start.y,
                target.y
            );


        if (
            setterTossApexHeight <=
            minimumApexHeight
        )
        {
            Debug.LogError(
                "[Ball] Setter Toss Apex Height が低すぎます。\n" +
                $"Setter Y = {start.y:F3} m\n" +
                $"Toss Target Y = {target.y:F3} m\n" +
                $"Apex Y = {setterTossApexHeight:F3} m\n" +
                "Apex HeightはSetterとToss Targetの両方より高くしてください。"
            );

            return;
        }


        // ========================================================
        // Save
        // ========================================================

        currentSetterTossTarget =
            targetTransform;


        setterTossStartPosition =
            start;


        setterTossTargetPositionAtLaunch =
            target;


        currentSetterTossApexHeight =
            setterTossApexHeight;


        // ========================================================
        // Gravity
        // ========================================================

        float gravity =
            Mathf.Abs(
                Physics.gravity.y
            );


        if (gravity <= 0.0001f)
        {
            Debug.LogError(
                "[Ball] Gravity Y が0です。"
            );

            return;
        }


        // ========================================================
        // Setter -> Apex
        //
        // v² = 2gh
        // ========================================================

        float riseHeight =
            currentSetterTossApexHeight -
            start.y;


        if (riseHeight <= 0.0f)
        {
            Debug.LogError(
                "[Ball] Setter Toss Apex Height がSetter以下です。"
            );

            return;
        }


        float initialVerticalSpeed =
            Mathf.Sqrt(
                2.0f *
                gravity *
                riseHeight
            );


        // ========================================================
        // Setter -> Apex Time
        // ========================================================

        calculatedSetterTossTimeToApex =
            initialVerticalSpeed /
            gravity;


        // ========================================================
        // Apex -> Toss Target
        // ========================================================

        float fallHeight =
            currentSetterTossApexHeight -
            target.y;


        if (fallHeight < 0.0f)
        {
            Debug.LogError(
                "[Ball] Setter Toss Apex Height がTargetより低いです。"
            );

            return;
        }


        float timeFromApexToTarget =
            Mathf.Sqrt(
                2.0f *
                fallHeight /
                gravity
            );


        // ========================================================
        // Total Flight Time
        // ========================================================

        calculatedSetterTossFlightTime =
            calculatedSetterTossTimeToApex +
            timeFromApexToTarget;


        if (
            calculatedSetterTossFlightTime <=
            0.0f
        )
        {
            Debug.LogError(
                "[Ball] Setter Toss Flight Timeを計算できません。"
            );

            return;
        }


        // ========================================================
        // XZ
        //
        // Total Flight TimeでTargetへ到達するための
        // 水平速度を逆算する。
        // ========================================================

        Vector3 horizontalVector =
            target -
            start;


        horizontalVector.y =
            0.0f;


        Vector3 horizontalVelocity =
            horizontalVector /
            calculatedSetterTossFlightTime;


        // ========================================================
        // Initial Velocity
        // ========================================================

        setterTossLaunchVelocity =
            horizontalVelocity +
            Vector3.up *
            initialVerticalSpeed;


        // ========================================================
        // Physics ON
        // ========================================================

        ActivatePhysics();


        rb.linearVelocity =
            setterTossLaunchVelocity;


        // 現時点ではSetter TossにSpinは与えない
        rb.angularVelocity =
            Vector3.zero;


        setterTossElapsedTime =
            0.0f;


        currentState =
            BallMotionState.SetterTossing;


        // ========================================================
        // Calculated Apex Position
        // ========================================================

        Vector3 calculatedApexPosition =
            start +
            horizontalVelocity *
            calculatedSetterTossTimeToApex;


        calculatedApexPosition.y =
            currentSetterTossApexHeight;


        // ========================================================
        // Debug
        // ========================================================

        Debug.Log(
            "[Ball] Setter Toss Launch\n" +
            $"Setter = {start}\n" +
            $"Target = {target}\n" +
            $"Apex Height = {currentSetterTossApexHeight:F3} m\n" +
            $"Calculated Apex Position = {calculatedApexPosition}\n" +
            $"Launch Velocity = {setterTossLaunchVelocity}\n" +
            $"Launch Speed = {setterTossLaunchVelocity.magnitude:F3} m/s\n" +
            $"Time To Apex = {calculatedSetterTossTimeToApex:F3} s\n" +
            $"Total Flight Time = {calculatedSetterTossFlightTime:F3} s"
        );
    }


    // ============================================================
    // Setter Toss Update
    // ============================================================

    private void UpdateSetterToss()
    {
        // 追加Forceなし。
        //
        // RigidbodyにはGravityのみが作用する。

        setterTossElapsedTime +=
            Time.fixedDeltaTime;


        if (
            setterTossElapsedTime >=
            calculatedSetterTossFlightTime
        )
        {
            PassSetterTossTarget();
        }
    }


    // ============================================================
    // Setter Toss Target Pass
    // ============================================================

    /// <summary>
    /// Setter TossがTarget位置へ到達した瞬間。
    ///
    /// FixedUpdateによる微小な時間誤差だけ補正する。
    ///
    /// ★重要
    ///
    /// ここではBallを停止しない。
    ///
    /// Position:
    ///     Targetへ正確に補正
    ///
    /// Velocity:
    ///     Target到達時点の理論Velocityへ補正
    ///
    /// Gravity:
    ///     ONのまま
    ///
    /// isKinematic:
    ///     falseのまま
    ///
    /// そのためイベント後も自然に飛び続ける。
    /// </summary>
    private void PassSetterTossTarget()
    {
        // ========================================================
        // Exact Target Position
        // ========================================================

        rb.position =
            setterTossTargetPositionAtLaunch;


        // ========================================================
        // Target通過時の理論Velocity
        //
        // XZ速度はLaunch時から変化しない。
        //
        // Y:
        //
        // vy = vy0 + g*t
        //
        // Physics.gravity.y は負。
        // ========================================================

        Vector3 exactTargetVelocity =
            setterTossLaunchVelocity;


        exactTargetVelocity.y =
            setterTossLaunchVelocity.y
            +
            Physics.gravity.y *
            calculatedSetterTossFlightTime;


        rb.linearVelocity =
            exactTargetVelocity;


        // ========================================================
        // State
        //
        // Physicsは止めない。
        // ========================================================

        currentState =
            BallMotionState.AfterSetterTossTarget;


        Debug.Log(
            "[Ball] Setter Toss Passed Target\n" +
            $"Target = " +
            $"{(currentSetterTossTarget != null ? currentSetterTossTarget.name : "null")}\n" +
            $"Position = {rb.position}\n" +
            $"Velocity = {rb.linearVelocity}\n" +
            "Ballは停止せずGravityで運動を継続します。"
        );


        // ========================================================
        // Event
        //
        // 将来的にはここからSpikeへ接続する。
        // ========================================================

        OnSetterTossReachedTarget?.Invoke();
    }


    // ============================================================
    // Physics ON
    // ============================================================

    private void ActivatePhysics()
    {
        if (rb.isKinematic)
        {
            rb.isKinematic =
                false;
        }


        rb.useGravity =
            true;


        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;


        rb.WakeUp();
    }


    // ============================================================
    // Physics STOP
    // ============================================================

    private void StopPhysics()
    {
        rb.linearVelocity =
            Vector3.zero;


        rb.angularVelocity =
            Vector3.zero;


        rb.useGravity =
            false;


        rb.isKinematic =
            true;


        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;
    }
}