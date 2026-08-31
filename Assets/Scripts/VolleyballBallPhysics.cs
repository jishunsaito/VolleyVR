using System;
using UnityEngine;

/// <summary>
/// バレーボール1個の運動を担当する。
///
/// 実装:
/// 1. サーブ前トス
/// 2. スパイクサーブ
/// 3. サーブカット
/// 4. Setter Toss
/// 5. Spike
///
/// Setter TossのTarget
/// = Spike Contact Point
///
/// BlockなしのSpike打球後はTargetへの毎Frame強制移動を行わず、
/// Rigidbody Physicsに任せる。
///
/// Blockありの場合は、高速なSpikeがBlock Contact Pointを通り越してから戻る見え方を防ぐため、
/// Spike Contact Point -> Block Contact PointだけをKinematic + 解析弾道で正確に再生する。
/// Contact Point到達時に一度完全停止し、Receive Contactと同様に指定FixedUpdate数だけ保持する。
/// その後、指定Block Exit Speed / Block SpinでBlock Landing Pointを通る弾道へ切り替える。
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

        SetContact,
        Setting,
        AtSetTarget,

        Spiking,
        BlockContact,
        BlockLanding,
        AfterSpikeTarget
    }


    // ============================================================
    // Ball Physics
    // ============================================================

    [Header("Ball Physics")]

    [Tooltip("バレーボールの質量 [kg]")]
    [SerializeField]
    private float ballMass =
        0.27f;

    [Tooltip("軌道計算を正確にするため現段階では0推奨")]
    [SerializeField]
    private float linearDamping =
        0.0f;

    [Tooltip("回転減衰")]
    [SerializeField]
    private float angularDamping =
        0.02f;


    // ============================================================
    // Spike Spin
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

    [SerializeField]
    private float calculatedTossApexY =
        0.0f;

    [SerializeField]
    private float currentServeHitHeight =
        0.0f;

    [SerializeField]
    private float calculatedTimeToHit =
        0.0f;


    // ============================================================
    // Spike Serve Runtime
    // ============================================================

    [Header("Spike Serve Runtime")]

    [SerializeField]
    private float calculatedFlightTime =
        0.0f;

    [SerializeField]
    private float serveElapsedTime =
        0.0f;

    [SerializeField]
    private float additionalVerticalAcceleration =
        0.0f;

    [SerializeField]
    private Vector3 serveTargetPositionAtLaunch;


    // ============================================================
    // Receive Runtime
    // ============================================================

    [Header("Receive Runtime")]

    [SerializeField]
    private Vector3 receiveStartPosition;

    [SerializeField]
    private Vector3 receiveTargetPositionAtLaunch;

    [SerializeField]
    private float currentReceiveApexHeight =
        0.0f;

    [SerializeField]
    private float calculatedReceiveFlightTime =
        0.0f;

    [SerializeField]
    private float calculatedReceiveTimeToApex =
        0.0f;

    [SerializeField]
    private float receiveElapsedTime =
        0.0f;

    [SerializeField]
    private int receiveContactFramesRemaining =
        0;

    [SerializeField]
    private float currentReceiveBackspinRpm =
        0.0f;


    // ============================================================
    // Setter Toss Runtime
    // ============================================================

    [Header("Setter Toss Runtime")]

    [SerializeField]
    private Vector3 setStartPosition;

    [SerializeField]
    private Vector3 setTargetPositionAtLaunch;

    [SerializeField]
    private float currentSetApexHeight =
        0.0f;

    [SerializeField]
    private float calculatedSetTimeToApex =
        0.0f;

    [SerializeField]
    private float calculatedSetFlightTime =
        0.0f;

    [SerializeField]
    private float setElapsedTime =
        0.0f;

    [SerializeField]
    private int setContactFramesRemaining =
        0;

    [SerializeField]
    private float currentSetSpinRpm =
        0.0f;


    // ============================================================
    // Spike Runtime
    // ============================================================

    [Header("Spike Runtime")]

    [Tooltip(
        "SpikeTargetを通過したと判定する許容距離 [m]。" +
        "Targetへの強制移動には使わない。"
    )]
    [SerializeField]
    private float spikeTargetDetectionRadius =
        0.20f;

    [SerializeField]
    private Vector3 spikeStartPosition;

    [SerializeField]
    private Vector3 spikeTargetPositionAtLaunch;

    [SerializeField]
    private Vector3 spikeInitialVelocity;

    [SerializeField]
    private float calculatedSpikeFlightTime =
        0.0f;

    [SerializeField]
    private float spikeElapsedTime =
        0.0f;

    [SerializeField]
    private bool spikeTargetTriggered =
        false;


    // ============================================================
    // Block Runtime
    // ============================================================

    [Header("Block Runtime")]

    [SerializeField]
    private bool blockEnabledForCurrentSpike =
        false;

    [SerializeField]
    private Vector3 blockContactPointPositionAtLaunch;

    [SerializeField]
    private Vector3 blockLandingPointPositionAtLaunch;

    [SerializeField]
    private float currentBlockExitSpeedKmh =
        45.0f;

    [SerializeField]
    private float currentBlockSpinRpm =
        600.0f;

    [SerializeField]
    private int currentBlockContactFixedFrames =
        1;

    [SerializeField]
    private int blockContactFramesRemaining =
        0;

    [SerializeField]
    private bool blockContactTriggered =
        false;

    [SerializeField]
    private bool blockLandingTriggered =
        false;

    [SerializeField]
    private float calculatedBlockLandingFlightTime =
        0.0f;

    [SerializeField]
    private float blockLandingElapsedTime =
        0.0f;

    [SerializeField]
    private Vector3 blockLandingLaunchVelocity;

    [SerializeField]
    private Vector3 blockLandingArrivalVelocity;

    [SerializeField]
    private bool blockContactPending =
        false;

    [SerializeField]
    private Vector3 blockApproachSpinAxis =
        Vector3.right;

    [SerializeField]
    private float blockApproachSpinDegreesPerSecond =
        0.0f;

    private Transform currentBlockContactPoint;

    private Transform currentBlockLandingPoint;


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

    private Transform currentSpikeTarget;


    // ============================================================
    // Events
    // ============================================================

    public event Action OnTossApex;

    public event Action OnServeHitPoint;

    public event Action OnServeReachedTarget;

    public event Action OnReceiveReachedTarget;

    public event Action OnSetReachedTarget;

    public event Action OnSpikeReachedTarget;


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

        if (
            currentState ==
            BallMotionState.SetContact
        )
        {
            UpdateSetContact();

            previousPosition =
                rb.position;

            return;
        }

        if (
            currentState ==
            BallMotionState.BlockContact
        )
        {
            UpdateBlockContact();

            previousPosition =
                rb.position;

            return;
        }

        /*
         * Block ON時だけ、
         * Spike Contact -> Block ContactはKinematicで正確に進める。
         */
        if (
            currentState ==
            BallMotionState.Spiking &&
            blockEnabledForCurrentSpike
        )
        {
            UpdateBlockApproach();

            previousPosition =
                rb.position;

            return;
        }

        if (rb.isKinematic)
        {
            previousPosition =
                rb.position;

            return;
        }

        if (
            currentState ==
            BallMotionState.Tossing
        )
        {
            DetectTossState();
        }

        if (
            currentState ==
            BallMotionState.SpikeServing
        )
        {
            UpdateSpikeServe();
        }

        if (
            currentState ==
            BallMotionState.Receiving
        )
        {
            UpdateReceive();
        }

        if (
            currentState ==
            BallMotionState.Setting
        )
        {
            UpdateSet();
        }

        if (
            currentState ==
            BallMotionState.Spiking
        )
        {
            UpdateSpike();
        }

        if (
            currentState ==
            BallMotionState.BlockLanding
        )
        {
            UpdateBlockLanding();
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

        rb.maxLinearVelocity =
            100.0f;

        rb.maxAngularVelocity =
            250.0f;

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
    // Place
    // ============================================================

    public void PlaceAt(
        Vector3 position
    )
    {
        StopPhysics();

        rb.position =
            position;

        transform.position =
            position;

        previousPosition =
            position;

        currentState =
            BallMotionState.Waiting;
    }


    // ============================================================
    // Serve Toss
    // ============================================================

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

        float initialUpVelocity =
            Mathf.Sqrt(
                2.0f *
                gravity *
                tossHeight
            );

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

        Debug.Log(
            "[Ball] Serve Toss\n" +
            $"Start = {startPosition}\n" +
            $"Apex Y = {calculatedTossApexY:F3}\n" +
            $"Hit Y = {serveHitHeight:F3}\n" +
            $"Velocity = {tossVelocity}"
        );
    }


    // ============================================================
    // Serve Toss Detection
    // ============================================================

    private void DetectTossState()
    {
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
                $"Position = {exactHitPosition}"
            );

            OnServeHitPoint?.Invoke();
        }
    }


    // ============================================================
    // Spike Serve
    // ============================================================

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

        ActivatePhysics();

        currentServeTarget =
            targetTransform;

        Vector3 targetPosition =
            targetTransform.position;

        serveTargetPositionAtLaunch =
            targetPosition;

        Vector3 start =
            rb.position;

        float speed =
            speedKmh /
            3.6f;

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

        calculatedFlightTime =
            horizontalDistance /
            horizontalSpeed;

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

        Vector3 launchVelocity =
            horizontalDirection *
            horizontalSpeed
            +
            Vector3.up *
            verticalSpeed;

        rb.linearVelocity =
            launchVelocity;

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

        Debug.Log(
            "[Ball] Spike Serve\n" +
            $"Receiver = {targetTransform.name}\n" +
            $"Hit Position = {start}\n" +
            $"Receiver Position = {targetPosition}\n" +
            $"Speed = {speedKmh:F1} km/h"
        );
    }

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

    private void CompleteServeAtTarget()
    {
        if (currentServeTarget == null)
        {
            Debug.LogError(
                "[Ball] Serve Target が失われました。"
            );

            return;
        }

        additionalVerticalAcceleration =
            0.0f;

        currentState =
            BallMotionState.AtServeTarget;

        Debug.Log(
            "[Ball] Serve Reached Receiver\n" +
            $"Receiver = {currentServeTarget.name}\n" +
            $"Ball Position = {rb.position}\n" +
            $"Expected Receiver Position = " +
            $"{serveTargetPositionAtLaunch}"
        );

        OnServeReachedTarget?.Invoke();
    }


    // ============================================================
    // Receive
    // ============================================================

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
                "[Ball] Receive Apex Height が低すぎます。"
            );

            return;
        }

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

        StopPhysics();

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
            $"Setter Position = {target}"
        );
    }

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

        float gravity =
            Mathf.Abs(
                Physics.gravity.y
            );

        if (gravity <= 0.0001f)
        {
            return;
        }

        float riseHeight =
            currentReceiveApexHeight -
            start.y;

        if (riseHeight <= 0.0f)
        {
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

        float fallHeight =
            currentReceiveApexHeight -
            target.y;

        if (fallHeight < 0.0f)
        {
            return;
        }

        float timeFromApexToSetter =
            Mathf.Sqrt(
                2.0f *
                fallHeight /
                gravity
            );

        calculatedReceiveFlightTime =
            calculatedReceiveTimeToApex +
            timeFromApexToSetter;

        if (
            calculatedReceiveFlightTime <=
            0.0f
        )
        {
            return;
        }

        Vector3 horizontalVector =
            target -
            start;

        horizontalVector.y =
            0.0f;

        Vector3 horizontalVelocity =
            horizontalVector /
            calculatedReceiveFlightTime;

        Vector3 receiveVelocity =
            horizontalVelocity +
            Vector3.up *
            initialVerticalSpeed;

        ActivatePhysics();

        rb.linearVelocity =
            receiveVelocity;

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
    }

    private void UpdateReceive()
    {
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

    private void CompleteReceiveAtTarget()
    {
        rb.position =
            receiveTargetPositionAtLaunch;

        StopPhysics();

        currentState =
            BallMotionState.AtSetterTarget;

        Debug.Log(
            "[Ball] Receive Reached Setter\n" +
            $"Ball Position = {rb.position}"
        );

        OnReceiveReachedTarget?.Invoke();
    }


    // ============================================================
    // Setter Toss
    // ============================================================

    public void SetToTarget(
        Vector3 targetPosition,
        float setApexHeight,
        float spinRpm,
        int contactFixedFrames = 1
    )
    {
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

        if (
            setApexHeight <= start.y ||
            setApexHeight < targetPosition.y
        )
        {
            Debug.LogError(
                "[Ball] Set Apex Height が不正です。\n" +
                $"Setter Y = {start.y:F3} m\n" +
                $"Toss Target Y = {targetPosition.y:F3} m\n" +
                $"Apex Y = {setApexHeight:F3} m"
            );

            return;
        }

        setStartPosition =
            start;

        setTargetPositionAtLaunch =
            targetPosition;

        currentSetApexHeight =
            setApexHeight;

        currentSetSpinRpm =
            spinRpm;

        setContactFramesRemaining =
            Mathf.Max(
                0,
                contactFixedFrames
            );

        StopPhysics();

        if (
            setContactFramesRemaining <=
            0
        )
        {
            LaunchSet();

            return;
        }

        currentState =
            BallMotionState.SetContact;

        Debug.Log(
            "[Ball] Setter Contact\n" +
            $"Setter Position = {start}\n" +
            $"Toss Target Position = {targetPosition}\n" +
            $"Apex Height = {setApexHeight:F3} m"
        );
    }

    private void UpdateSetContact()
    {
        setContactFramesRemaining--;

        if (
            setContactFramesRemaining <=
            0
        )
        {
            LaunchSet();
        }
    }

    private void LaunchSet()
    {
        Vector3 start =
            rb.position;

        Vector3 target =
            setTargetPositionAtLaunch;

        setStartPosition =
            start;

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

        float riseHeight =
            currentSetApexHeight -
            start.y;

        if (riseHeight <= 0.0f)
        {
            Debug.LogError(
                "[Ball] Set Apex Height がSetter以下です。"
            );

            return;
        }

        float initialVerticalSpeed =
            Mathf.Sqrt(
                2.0f *
                gravity *
                riseHeight
            );

        calculatedSetTimeToApex =
            initialVerticalSpeed /
            gravity;

        float fallHeight =
            currentSetApexHeight -
            target.y;

        if (fallHeight < 0.0f)
        {
            Debug.LogError(
                "[Ball] Set Apex Height がToss Targetより低いです。"
            );

            return;
        }

        float timeFromApexToTarget =
            0.0f;

        if (fallHeight > 0.0f)
        {
            timeFromApexToTarget =
                Mathf.Sqrt(
                    2.0f *
                    fallHeight /
                    gravity
                );
        }

        calculatedSetFlightTime =
            calculatedSetTimeToApex +
            timeFromApexToTarget;

        if (
            calculatedSetFlightTime <=
            0.0f
        )
        {
            Debug.LogError(
                "[Ball] Setter Toss Flight Timeを計算できません。"
            );

            return;
        }

        Vector3 horizontalVector =
            target -
            start;

        horizontalVector.y =
            0.0f;

        Vector3 horizontalVelocity =
            horizontalVector /
            calculatedSetFlightTime;

        Vector3 setVelocity =
            horizontalVelocity +
            Vector3.up *
            initialVerticalSpeed;

        ActivatePhysics();

        rb.linearVelocity =
            setVelocity;

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
                currentSetSpinRpm *
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

        setElapsedTime =
            0.0f;

        currentState =
            BallMotionState.Setting;

        Vector3 calculatedApexPosition =
            start +
            horizontalVelocity *
            calculatedSetTimeToApex;

        calculatedApexPosition.y =
            currentSetApexHeight;

        Debug.Log(
            "[Ball] Setter Toss Launch\n" +
            $"Setter = {start}\n" +
            $"Toss Target = {target}\n" +
            $"Apex = {calculatedApexPosition}\n" +
            $"Velocity = {setVelocity}\n" +
            $"Flight Time = {calculatedSetFlightTime:F3} s"
        );
    }

    private void UpdateSet()
    {
        setElapsedTime +=
            Time.fixedDeltaTime;

        if (
            setElapsedTime >=
            calculatedSetFlightTime
        )
        {
            CompleteSetAtTarget();
        }
    }

    private void CompleteSetAtTarget()
    {
        rb.position =
            setTargetPositionAtLaunch;

        StopPhysics();

        currentState =
            BallMotionState.AtSetTarget;

        Debug.Log(
            "[Ball] Set Reached Spike Contact Point\n" +
            $"Position = {rb.position}"
        );

        OnSetReachedTarget?.Invoke();
    }


    // ============================================================
    // Normal Spike
    // ============================================================

    public void Spike(
        Transform targetTransform,
        float speedKmh
    )
    {
        if (targetTransform == null)
        {
            Debug.LogError(
                "[Ball] Spike Target がnullです。"
            );

            return;
        }

        if (
            currentState != BallMotionState.AtSetTarget &&
            currentState != BallMotionState.Waiting
        )
        {
            Debug.LogWarning(
                "[Ball] 現在のStateではSpikeできません。\n" +
                $"State = {currentState}"
            );

            return;
        }

        if (speedKmh <= 0.0f)
        {
            Debug.LogError(
                "[Ball] Spike Speed は0より大きくしてください。"
            );

            return;
        }

        Vector3 start =
            rb.position;

        Vector3 target =
            targetTransform.position;

        float speed =
            speedKmh /
            3.6f;

        if (
            !CalculateBallisticVelocity(
                start,
                target,
                speed,
                out Vector3 launchVelocity,
                out float flightTime
            )
        )
        {
            Debug.LogError(
                "[Ball] 指定SpeedではSpike Targetへ到達できません。\n" +
                $"Start = {start}\n" +
                $"Target = {target}\n" +
                $"Speed = {speedKmh:F1} km/h"
            );

            return;
        }

        currentSpikeTarget =
            targetTransform;

        spikeStartPosition =
            start;

        spikeTargetPositionAtLaunch =
            target;

        spikeInitialVelocity =
            launchVelocity;

        calculatedSpikeFlightTime =
            flightTime;

        spikeElapsedTime =
            0.0f;

        spikeTargetTriggered =
            false;

        ResetBlockRuntime();

        ActivatePhysics();

        rb.linearVelocity =
            launchVelocity;

        ApplySpinForVelocity(
            launchVelocity,
            spikeSpinRpm
        );

        currentState =
            BallMotionState.Spiking;

        float launchAngle =
            CalculateLaunchAngleDeg(
                launchVelocity
            );

        Debug.Log(
            "[Ball] Spike\n" +
            $"Contact Position = {start}\n" +
            $"Target = {targetTransform.name}\n" +
            $"Target Position = {target}\n" +
            $"Speed = {speedKmh:F1} km/h\n" +
            $"Launch Angle = {launchAngle:F2} deg\n" +
            "Block Enabled = False"
        );
    }


    // ============================================================
    // Block Spike
    // ============================================================

    public void SpikeWithBlock(
        Transform blockContactPoint,
        Transform blockLandingPoint,
        float spikeSpeedKmh,
        float blockExitSpeedKmh,
        float blockSpinRpm,
        int blockContactFixedFrames = 1
    )
    {
        if (blockContactPoint == null)
        {
            Debug.LogError(
                "[Ball] Block Contact Point がnullです。"
            );

            return;
        }

        if (blockLandingPoint == null)
        {
            Debug.LogError(
                "[Ball] Block Landing Point がnullです。"
            );

            return;
        }

        if (
            currentState != BallMotionState.AtSetTarget &&
            currentState != BallMotionState.Waiting
        )
        {
            Debug.LogWarning(
                "[Ball] 現在のStateではBlock Spikeを開始できません。\n" +
                $"State = {currentState}"
            );

            return;
        }

        if (spikeSpeedKmh <= 0.0f)
        {
            Debug.LogError(
                "[Ball] Spike Speed は0より大きくしてください。"
            );

            return;
        }

        if (blockExitSpeedKmh <= 0.0f)
        {
            Debug.LogError(
                "[Ball] Block Exit Speed は0より大きくしてください。"
            );

            return;
        }

        Vector3 start =
            rb.position;

        Vector3 contactPosition =
            blockContactPoint.position;

        Vector3 landingPosition =
            blockLandingPoint.position;

        float spikeSpeed =
            spikeSpeedKmh /
            3.6f;

        float blockExitSpeed =
            blockExitSpeedKmh /
            3.6f;


        // Spike Contact -> Block Contact
        if (
            !CalculateBallisticVelocity(
                start,
                contactPosition,
                spikeSpeed,
                out Vector3 contactLaunchVelocity,
                out float contactFlightTime
            )
        )
        {
            Debug.LogError(
                "[Ball] 指定Spike SpeedではBlock Contact Pointへ到達できません。\n" +
                $"Spike Contact Position = {start}\n" +
                $"Block Contact Position = {contactPosition}\n" +
                $"Spike Speed = {spikeSpeedKmh:F1} km/h"
            );

            return;
        }


        // Block Contact -> Block Landing の実現可能性を先に確認
        if (
            !CalculateBallisticVelocity(
                contactPosition,
                landingPosition,
                blockExitSpeed,
                out Vector3 landingLaunchVelocity,
                out float landingFlightTime
            )
        )
        {
            Debug.LogError(
                "[Ball] 指定Block Exit SpeedではBlock Landing Pointへ到達できません。\n" +
                $"Block Contact Position = {contactPosition}\n" +
                $"Block Landing Position = {landingPosition}\n" +
                $"Block Exit Speed = {blockExitSpeedKmh:F1} km/h"
            );

            return;
        }


        currentSpikeTarget =
            null;

        spikeStartPosition =
            start;

        spikeTargetPositionAtLaunch =
            contactPosition;

        spikeInitialVelocity =
            contactLaunchVelocity;

        calculatedSpikeFlightTime =
            contactFlightTime;

        spikeElapsedTime =
            0.0f;

        spikeTargetTriggered =
            false;


        blockEnabledForCurrentSpike =
            true;

        currentBlockContactPoint =
            blockContactPoint;

        currentBlockLandingPoint =
            blockLandingPoint;

        blockContactPointPositionAtLaunch =
            contactPosition;

        blockLandingPointPositionAtLaunch =
            landingPosition;

        currentBlockExitSpeedKmh =
            blockExitSpeedKmh;

        currentBlockSpinRpm =
            blockSpinRpm;

        currentBlockContactFixedFrames =
            Mathf.Max(
                0,
                blockContactFixedFrames
            );

        blockContactFramesRemaining =
            0;

        blockContactTriggered =
            false;

        blockLandingTriggered =
            false;

        blockLandingLaunchVelocity =
            landingLaunchVelocity;

        calculatedBlockLandingFlightTime =
            landingFlightTime;

        blockLandingElapsedTime =
            0.0f;

        blockLandingArrivalVelocity =
            CalculateVelocityAtTime(
                landingLaunchVelocity,
                landingFlightTime
            );


        /*
         * 重要:
         *
         * Contact前はDynamic Rigidbodyにしない。
         *
         * 100 km/hなら1 FixedUpdate = 0.02 sの間に
         * 約0.56 m進むため、Point通過を後から検出すると
         * 手を突き抜けてから戻る見た目になる。
         *
         * そこでContactまでだけは
         * Kinematic + MovePositionで解析弾道を再生する。
         */
        StartBlockApproach(
            contactLaunchVelocity
        );


        float launchAngle =
            CalculateLaunchAngleDeg(
                contactLaunchVelocity
            );


        Debug.Log(
            "[Ball] Spike With Block\n" +
            $"Spike Contact Position = {start}\n" +
            $"Block Contact Point = {blockContactPoint.name}\n" +
            $"Block Contact Position = {contactPosition}\n" +
            $"Spike Speed = {spikeSpeedKmh:F1} km/h\n" +
            $"Launch Angle = {launchAngle:F2} deg\n" +
            $"Time To Block Contact = {contactFlightTime:F3} s\n" +
            $"Block Contact Frames = {currentBlockContactFixedFrames}\n" +
            $"Block Landing Point = {blockLandingPoint.name}\n" +
            $"Block Landing Position = {landingPosition}\n" +
            $"Block Exit Speed = {blockExitSpeedKmh:F1} km/h\n" +
            $"Block Spin = {blockSpinRpm:F1} rpm"
        );
    }


    // ============================================================
    // Normal Spike Update
    // ============================================================

    private void UpdateSpike()
    {
        spikeElapsedTime +=
            Time.fixedDeltaTime;

        if (spikeTargetTriggered)
        {
            return;
        }

        float distance =
            DistancePointToSegment(
                spikeTargetPositionAtLaunch,
                previousPosition,
                rb.position
            );

        if (
            distance <=
            spikeTargetDetectionRadius
        )
        {
            spikeTargetTriggered =
                true;

            currentState =
                BallMotionState.AfterSpikeTarget;

            Debug.Log(
                "[Ball] Spike Target Passed\n" +
                $"Target = " +
                $"{(currentSpikeTarget != null ? currentSpikeTarget.name : "null")}\n" +
                $"Expected Position = {spikeTargetPositionAtLaunch}\n" +
                $"Ball Position = {rb.position}\n" +
                $"Distance = {distance:F3} m"
            );

            OnSpikeReachedTarget?.Invoke();
        }
    }


    // ============================================================
    // Block Approach
    // ============================================================

    private void StartBlockApproach(
        Vector3 launchVelocity
    )
    {
        StopPhysics();

        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        spikeElapsedTime =
            0.0f;

        blockContactPending =
            false;

        CalculateScriptedSpin(
            launchVelocity,
            spikeSpinRpm,
            out blockApproachSpinAxis,
            out blockApproachSpinDegreesPerSecond
        );

        previousPosition =
            rb.position;

        currentState =
            BallMotionState.Spiking;
    }


    private void UpdateBlockApproach()
    {
        if (!blockEnabledForCurrentSpike)
        {
            return;
        }

        /*
         * 前回のFixedUpdateでContact PointへMovePosition済み。
         * この時点ではすでにPoint上なので、そのまま停止状態へ。
         */
        if (blockContactPending)
        {
            BeginBlockContact();

            return;
        }

        float nextTime =
            Mathf.Min(
                spikeElapsedTime +
                Time.fixedDeltaTime,
                calculatedSpikeFlightTime
            );

        Vector3 nextPosition =
            EvaluateBallisticPosition(
                spikeStartPosition,
                spikeInitialVelocity,
                nextTime
            );

        bool reachesContactThisStep =
            nextTime >=
            calculatedSpikeFlightTime -
            0.000001f;

        if (reachesContactThisStep)
        {
            nextPosition =
                blockContactPointPositionAtLaunch;
        }

        /*
         * Kinematic Rigidbodyなので、この位置より先へ
         * 慣性で飛び出すことはない。
         */
        rb.MovePosition(
            nextPosition
        );

        AdvanceScriptedSpin(
            Time.fixedDeltaTime
        );

        spikeElapsedTime =
            nextTime;

        if (reachesContactThisStep)
        {
            blockContactPending =
                true;
        }
    }


    // ============================================================
    // Block Contact
    // ============================================================

    private void BeginBlockContact()
    {
        if (!blockEnabledForCurrentSpike)
        {
            return;
        }

        if (blockContactTriggered)
        {
            return;
        }

        blockContactTriggered =
            true;

        blockContactPending =
            false;

        Vector3 exactIncomingVelocity =
            CalculateVelocityAtTime(
                spikeInitialVelocity,
                calculatedSpikeFlightTime
            );

        float incomingSpeedKmh =
            exactIncomingVelocity.magnitude *
            3.6f;

        /*
         * 念のためContact Pointへ完全一致。
         * ここへ来る時点でMovePositionによって
         * すでにこの位置に到達している。
         */
        rb.position =
            blockContactPointPositionAtLaunch;

        transform.position =
            blockContactPointPositionAtLaunch;

        /*
         * ReceiveのContactと同様に完全停止。
         */
        StopPhysics();

        blockContactFramesRemaining =
            currentBlockContactFixedFrames;

        previousPosition =
            blockContactPointPositionAtLaunch;

        currentState =
            BallMotionState.BlockContact;

        string blockContactPointName =
            currentBlockContactPoint != null
                ? currentBlockContactPoint.name
                : "null";

        Debug.Log(
            "[Ball] Block Contact\n" +
            $"Block Contact Point = {blockContactPointName}\n" +
            $"Contact Position = {blockContactPointPositionAtLaunch}\n" +
            $"Incoming Speed = {incomingSpeedKmh:F1} km/h\n" +
            $"Stop Frames = {currentBlockContactFixedFrames}"
        );

        if (
            blockContactFramesRemaining <=
            0
        )
        {
            LaunchBlockLanding();
        }
    }


    private void UpdateBlockContact()
    {
        blockContactFramesRemaining--;

        if (
            blockContactFramesRemaining <=
            0
        )
        {
            LaunchBlockLanding();
        }
    }


    // ============================================================
    // Block Landing Launch
    // ============================================================

    private void LaunchBlockLanding()
    {
        float exitSpeed =
            currentBlockExitSpeedKmh /
            3.6f;

        if (
            !CalculateBallisticVelocity(
                blockContactPointPositionAtLaunch,
                blockLandingPointPositionAtLaunch,
                exitSpeed,
                out Vector3 landingVelocity,
                out float flightTime
            )
        )
        {
            Debug.LogError(
                "[Ball] Block後のLanding軌道を計算できません。\n" +
                $"Block Contact Position = {blockContactPointPositionAtLaunch}\n" +
                $"Block Landing Position = {blockLandingPointPositionAtLaunch}\n" +
                $"Block Exit Speed = {currentBlockExitSpeedKmh:F1} km/h"
            );

            return;
        }

        blockLandingLaunchVelocity =
            landingVelocity;

        calculatedBlockLandingFlightTime =
            flightTime;

        blockLandingElapsedTime =
            0.0f;

        blockLandingArrivalVelocity =
            CalculateVelocityAtTime(
                landingVelocity,
                flightTime
            );

        /*
         * ここからDynamic Rigidbodyへ戻す。
         */
        ActivatePhysics();

        rb.linearVelocity =
            blockLandingLaunchVelocity;

        ApplySpinForVelocity(
            blockLandingLaunchVelocity,
            currentBlockSpinRpm
        );

        previousPosition =
            blockContactPointPositionAtLaunch;

        currentState =
            BallMotionState.BlockLanding;

        string landingPointName =
            currentBlockLandingPoint != null
                ? currentBlockLandingPoint.name
                : "null";

        Debug.Log(
            "[Ball] Block Rebound Launch\n" +
            $"From = {blockContactPointPositionAtLaunch}\n" +
            $"Landing Point = {landingPointName}\n" +
            $"Landing Position = {blockLandingPointPositionAtLaunch}\n" +
            $"Exit Speed = {currentBlockExitSpeedKmh:F1} km/h\n" +
            $"Spin = {currentBlockSpinRpm:F1} rpm\n" +
            $"Velocity = {blockLandingLaunchVelocity}\n" +
            $"Flight Time = {calculatedBlockLandingFlightTime:F3} s"
        );
    }


    // ============================================================
    // Block Landing Update
    // ============================================================

    private void UpdateBlockLanding()
    {
        if (blockLandingTriggered)
        {
            return;
        }

        blockLandingElapsedTime +=
            Time.fixedDeltaTime;

        if (
            blockLandingElapsedTime <
            calculatedBlockLandingFlightTime
        )
        {
            return;
        }

        blockLandingTriggered =
            true;

        spikeTargetTriggered =
            true;

        /*
         * Landing PointではSnapしない。
         *
         * Contact時にLanding Pointを通るよう初速度を1回だけ
         * 計算済みなので、ここでは位置も速度も変更しない。
         */
        float landingError =
            Vector3.Distance(
                rb.position,
                blockLandingPointPositionAtLaunch
            );

        currentState =
            BallMotionState.AfterSpikeTarget;

        string landingPointName =
            currentBlockLandingPoint != null
                ? currentBlockLandingPoint.name
                : "null";

        Debug.Log(
            "[Ball] Block Landing Time Reached\n" +
            $"Landing Point = {landingPointName}\n" +
            $"Expected Position = {blockLandingPointPositionAtLaunch}\n" +
            $"Actual Ball Position = {rb.position}\n" +
            $"Landing Error = {landingError:F3} m\n" +
            $"Speed = {rb.linearVelocity.magnitude * 3.6f:F1} km/h\n" +
            $"Spin = {currentBlockSpinRpm:F1} rpm"
        );

        /*
         * この先もGravity + Collision。
         */
        OnSpikeReachedTarget?.Invoke();
    }


    // ============================================================
    // Block Runtime Reset
    // ============================================================

    private void ResetBlockRuntime()
    {
        blockEnabledForCurrentSpike =
            false;

        currentBlockContactPoint =
            null;

        currentBlockLandingPoint =
            null;

        blockContactPointPositionAtLaunch =
            Vector3.zero;

        blockLandingPointPositionAtLaunch =
            Vector3.zero;

        currentBlockExitSpeedKmh =
            0.0f;

        currentBlockSpinRpm =
            0.0f;

        currentBlockContactFixedFrames =
            0;

        blockContactFramesRemaining =
            0;

        blockContactTriggered =
            false;

        blockLandingTriggered =
            false;

        calculatedBlockLandingFlightTime =
            0.0f;

        blockLandingElapsedTime =
            0.0f;

        blockLandingLaunchVelocity =
            Vector3.zero;

        blockLandingArrivalVelocity =
            Vector3.zero;

        blockContactPending =
            false;

        blockApproachSpinAxis =
            Vector3.right;

        blockApproachSpinDegreesPerSecond =
            0.0f;
    }


    // ============================================================
    // Kinematic Block Spin
    // ============================================================

    private static void CalculateScriptedSpin(
        Vector3 velocity,
        float spinRpm,
        out Vector3 spinAxis,
        out float degreesPerSecond
    )
    {
        Vector3 horizontalDirection =
            velocity;

        horizontalDirection.y =
            0.0f;

        if (
            horizontalDirection.sqrMagnitude <=
            0.0001f ||
            Mathf.Abs(spinRpm) <=
            0.0001f
        )
        {
            spinAxis =
                Vector3.right;

            degreesPerSecond =
                0.0f;

            return;
        }

        horizontalDirection.Normalize();

        Vector3 lateralAxis =
            Vector3.Cross(
                Vector3.up,
                horizontalDirection
            ).normalized;

        float directionSign =
            spinRpm >= 0.0f
                ? -1.0f
                : 1.0f;

        spinAxis =
            lateralAxis *
            directionSign;

        /*
         * rpm -> degree/sec
         *
         * rpm * 360 / 60
         * = rpm * 6
         */
        degreesPerSecond =
            Mathf.Abs(spinRpm) *
            6.0f;
    }

    private void AdvanceScriptedSpin(
        float deltaTime
    )
    {
        if (
            blockApproachSpinDegreesPerSecond <=
            0.0001f
        )
        {
            return;
        }

        Quaternion deltaRotation =
            Quaternion.AngleAxis(
                blockApproachSpinDegreesPerSecond *
                deltaTime,
                blockApproachSpinAxis
            );

        /*
         * KinematicなのでangularVelocityは使わずMoveRotation。
         */
        rb.MoveRotation(
            deltaRotation *
            rb.rotation
        );
    }


    // ============================================================
    // Spin
    // ============================================================

    private void ApplySpinForVelocity(
        Vector3 velocity,
        float spinRpm
    )
    {
        Vector3 horizontalDirection =
            velocity;

        horizontalDirection.y =
            0.0f;

        if (
            horizontalDirection.sqrMagnitude <=
            0.0001f ||
            Mathf.Abs(spinRpm) <=
            0.0001f
        )
        {
            rb.angularVelocity =
                Vector3.zero;

            return;
        }

        horizontalDirection.Normalize();

        Vector3 lateralAxis =
            Vector3.Cross(
                Vector3.up,
                horizontalDirection
            ).normalized;

        float angularSpeed =
            Mathf.Abs(spinRpm) *
            2.0f *
            Mathf.PI /
            60.0f;

        float directionSign =
            spinRpm >= 0.0f
                ? -1.0f
                : 1.0f;

        rb.angularVelocity =
            lateralAxis *
            angularSpeed *
            directionSign;
    }


    // ============================================================
    // Utility
    // ============================================================

    private static float CalculateLaunchAngleDeg(
        Vector3 velocity
    )
    {
        float horizontalSpeed =
            new Vector2(
                velocity.x,
                velocity.z
            ).magnitude;

        return
            Mathf.Atan2(
                velocity.y,
                horizontalSpeed
            ) *
            Mathf.Rad2Deg;
    }

    private static Vector3 CalculateVelocityAtTime(
        Vector3 initialVelocity,
        float time
    )
    {
        return
            initialVelocity +
            Physics.gravity *
            time;
    }

    private static Vector3 EvaluateBallisticPosition(
        Vector3 start,
        Vector3 initialVelocity,
        float time
    )
    {
        return
            start +
            initialVelocity *
            time +
            0.5f *
            Physics.gravity *
            time *
            time;
    }


    // ============================================================
    // Ballistic Calculation
    // ============================================================

    private static bool CalculateBallisticVelocity(
        Vector3 start,
        Vector3 target,
        float speed,
        out Vector3 velocity,
        out float flightTime
    )
    {
        velocity =
            Vector3.zero;

        flightTime =
            0.0f;

        float gravity =
            Mathf.Abs(
                Physics.gravity.y
            );

        if (
            gravity <= 0.0001f ||
            speed <= 0.0001f
        )
        {
            return false;
        }

        Vector3 difference =
            target -
            start;

        float verticalDifference =
            difference.y;

        difference.y =
            0.0f;

        float horizontalDistance =
            difference.magnitude;

        if (
            horizontalDistance <=
            0.0001f
        )
        {
            return false;
        }

        Vector3 horizontalDirection =
            difference.normalized;

        float speedSquared =
            speed *
            speed;

        float discriminant =
            speedSquared *
            speedSquared
            -
            gravity *
            (
                gravity *
                horizontalDistance *
                horizontalDistance
                +
                2.0f *
                verticalDifference *
                speedSquared
            );

        if (discriminant < 0.0f)
        {
            return false;
        }

        float sqrtDiscriminant =
            Mathf.Sqrt(
                discriminant
            );

        /*
         * 低弾道解
         */
        float tanTheta =
            (
                speedSquared -
                sqrtDiscriminant
            )
            /
            (
                gravity *
                horizontalDistance
            );

        float cosTheta =
            1.0f /
            Mathf.Sqrt(
                1.0f +
                tanTheta *
                tanTheta
            );

        float sinTheta =
            tanTheta *
            cosTheta;

        float horizontalSpeed =
            speed *
            cosTheta;

        float verticalSpeed =
            speed *
            sinTheta;

        if (
            horizontalSpeed <=
            0.0001f
        )
        {
            return false;
        }

        velocity =
            horizontalDirection *
            horizontalSpeed
            +
            Vector3.up *
            verticalSpeed;

        flightTime =
            horizontalDistance /
            horizontalSpeed;

        return
            flightTime >
            0.0f;
    }


    // ============================================================
    // Geometry
    // ============================================================

    private static float DistancePointToSegment(
        Vector3 point,
        Vector3 segmentStart,
        Vector3 segmentEnd
    )
    {
        Vector3 segment =
            segmentEnd -
            segmentStart;

        float lengthSquared =
            segment.sqrMagnitude;

        if (
            lengthSquared <=
            0.000001f
        )
        {
            return Vector3.Distance(
                point,
                segmentStart
            );
        }

        float t =
            Vector3.Dot(
                point -
                segmentStart,
                segment
            )
            /
            lengthSquared;

        t =
            Mathf.Clamp01(
                t
            );

        Vector3 closestPoint =
            segmentStart +
            segment *
            t;

        return Vector3.Distance(
            point,
            closestPoint
        );
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

        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        rb.WakeUp();
    }


    // ============================================================
    // Physics STOP
    // ============================================================

    private void StopPhysics()
    {
        /*
         * Dynamic時だけ速度を書き換える。
         * Kinematic RigidbodyにangularVelocityを書き込むと
         * Unity警告が出るため。
         */
        if (!rb.isKinematic)
        {
            rb.linearVelocity =
                Vector3.zero;

            rb.angularVelocity =
                Vector3.zero;
        }

        rb.useGravity =
            false;

        if (!rb.isKinematic)
        {
            rb.isKinematic =
                true;
        }

        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;
    }
}