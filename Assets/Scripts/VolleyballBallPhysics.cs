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
/// Spike打球後はTargetへの強制移動を行わず、
/// Rigidbody Physicsに任せる。
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

    /// <summary>
    /// Setter TossがToss Targetへ到達。
    /// この位置がSpike Contact Point。
    /// </summary>
    public event Action OnSetReachedTarget;

    /// <summary>
    /// SpikeがSpikeTarget付近を通過。
    /// Ballは停止しない。
    /// </summary>
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

        // Receive接触
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

        // Setter接触
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

    /// <summary>
    /// Ballを指定World Positionへ置いてWaitingで保持する。
    /// </summary>
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

        // TargetへSnapしない。
        // StopPhysicsしない。
        // 到達イベントだけ発生させる。
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

    /// <summary>
    /// Setterから指定World Positionへトスする。
    ///
    /// targetPositionは
    /// LeftTossTarget / RightTossTarget
    /// + Z Offset
    /// の最終的なSpike Contact Point。
    /// </summary>
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

        // Setの回転
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
        // ここがSpike Contact Pointなので
        // 最後のFixedUpdate誤差だけ補正する。
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
    // Spike
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

        ActivatePhysics();

        rb.linearVelocity =
            launchVelocity;

        Vector3 horizontalDirection =
            launchVelocity;

        horizontalDirection.y =
            0.0f;

        if (
            horizontalDirection.sqrMagnitude >
            0.0001f
        )
        {
            horizontalDirection.Normalize();

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
        }
        else
        {
            rb.angularVelocity =
                Vector3.zero;
        }

        currentState =
            BallMotionState.Spiking;

        float horizontalSpeed =
            new Vector2(
                launchVelocity.x,
                launchVelocity.z
            ).magnitude;

        float launchAngle =
            Mathf.Atan2(
                launchVelocity.y,
                horizontalSpeed
            ) *
            Mathf.Rad2Deg;

        Debug.Log(
            "[Ball] Spike\n" +
            $"Contact Position = {start}\n" +
            $"Target = {targetTransform.name}\n" +
            $"Target Position = {target}\n" +
            $"Speed = {speedKmh:F1} km/h\n" +
            $"Launch Angle = {launchAngle:F2} deg"
        );
    }

    private void UpdateSpike()
    {
        // Spike中はGravity + Collisionのみ。
        // Targetへ強制誘導しない。

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

            // Ballは停止しない。
            OnSpikeReachedTarget?.Invoke();
        }
    }


    // ============================================================
    // Spike Ballistic Calculation
    // ============================================================

    /// <summary>
    /// 指定SpeedでTargetを通る投射初速度を計算する。
    /// 2解ある場合は低弾道を使用。
    /// </summary>
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

        // Low trajectory
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