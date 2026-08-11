using System;
using UnityEngine;

/// <summary>
/// バレーボール1個の物理運動を担当する。
///
/// 現在実装:
///
/// 1. サーブ前トス
///    ・指定高さまで上昇
///    ・斜め前へ移動
///    ・最高点を通過
///    ・下降中に指定したServe Hit Heightへ到達
///
/// 2. スパイクサーブ
///    ・指定速度[km/h]
///    ・指定Launch Angle
///    ・レシーバーのTransform
///
///    を使ってレシーバー位置へ到達する弾道を計算する。
///
/// 3. Target到達
///    ・Target Transformの位置へ正確に合わせる
///    ・現段階ではその位置で停止
///
///    後でここをReceive処理へ置き換える。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VolleyballBallPhysics : MonoBehaviour
{
    public enum BallMotionState
    {
        Waiting,
        Tossing,
        SpikeServing,
        AtServeTarget
    }


    // ============================================================
    // Ball Physics
    // ============================================================

    [Header("Ball Physics")]

    [Tooltip("バレーボールの質量 [kg]")]
    [SerializeField]
    private float ballMass = 0.27f;


    [Tooltip("軌道計算を正確にするため現段階では0推奨")]
    [SerializeField]
    private float linearDamping = 0.0f;


    [Tooltip("回転減衰")]
    [SerializeField]
    private float angularDamping = 0.02f;


    // ============================================================
    // Spike Serve Spin
    // ============================================================

    [Header("Spike Serve Spin")]

    [Tooltip("トップスピン [rpm]")]
    [SerializeField]
    private float spikeSpinRpm = 1200.0f;


    // ============================================================
    // Runtime
    // ============================================================

    [Header("Runtime")]

    [SerializeField]
    private BallMotionState currentState =
        BallMotionState.Waiting;


    [Tooltip("今回のトスの最高到達Y座標")]
    [SerializeField]
    private float calculatedTossApexY = 0.0f;


    [Tooltip("サーブを打つY座標")]
    [SerializeField]
    private float currentServeHitHeight = 0.0f;


    [Tooltip("トス開始からサーブを打つまでの時間 [s]")]
    [SerializeField]
    private float calculatedTimeToHit = 0.0f;


    [Tooltip("サーブ打球からTarget到達までの時間 [s]")]
    [SerializeField]
    private float calculatedFlightTime = 0.0f;


    [Tooltip("サーブ開始後の経過時間 [s]")]
    [SerializeField]
    private float serveElapsedTime = 0.0f;


    [Tooltip(
        "Targetへ到達させるための追加鉛直加速度。" +
        "負なら下向き"
    )]
    [SerializeField]
    private float additionalVerticalAcceleration = 0.0f;


    [Tooltip("サーブ開始時に取得したTarget位置")]
    [SerializeField]
    private Vector3 serveTargetPositionAtLaunch;


    private Rigidbody rb;

    private bool apexReached = false;

    private bool serveHitTriggered = false;

    private Vector3 previousPosition;

    private Transform currentServeTarget;


    // ============================================================
    // Events
    // ============================================================

    /// <summary>
    /// トス最高点に到達。
    /// </summary>
    public event Action OnTossApex;


    /// <summary>
    /// 下降中にServe Hit Heightへ到達。
    /// RallyControllerがここでサーブを打つ。
    /// </summary>
    public event Action OnServeHitPoint;


    /// <summary>
    /// サーブがレシーバーのTarget位置へ到達。
    ///
    /// 後でここからReceive処理を開始できる。
    /// </summary>
    public event Action OnServeReachedTarget;


    // ============================================================
    // Properties
    // ============================================================

    public Rigidbody Rigidbody => rb;

    public BallMotionState CurrentState => currentState;


    // ============================================================
    // Unity
    // ============================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        ConfigureRigidbody();

        previousPosition = rb.position;
    }


    private void FixedUpdate()
    {
        if (rb == null)
            return;


        if (rb.isKinematic)
        {
            previousPosition = rb.position;
            return;
        }


        // ========================================================
        // Toss
        // ========================================================

        if (currentState == BallMotionState.Tossing)
        {
            DetectTossState();
        }


        // ========================================================
        // Spike Serve
        //
        // DetectTossState内でSpikeServeが呼ばれた場合も
        // このFixedUpdateからサーブ物理を開始する。
        // ========================================================

        if (currentState == BallMotionState.SpikeServing)
        {
            UpdateSpikeServe();
        }


        previousPosition = rb.position;
    }


    // ============================================================
    // Rigidbody Setup
    // ============================================================

    private void ConfigureRigidbody()
    {
        rb.mass = ballMass;

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


        // スポーン直後は固定
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
    // Toss
    // ============================================================

    /// <summary>
    /// サーブ前の斜め前トス。
    ///
    /// tossHeight
    ///   現在位置から最高点までの上昇量 [m]
    ///
    /// serveHitHeight
    ///   サーブを打つワールドY座標 [m]
    ///
    /// tossForwardDistance
    ///   Serve Hit Heightへ下降してくるまでに
    ///   コート方向へ進む距離 [m]
    ///
    /// forwardDirection
    ///   コート方向
    /// </summary>
    public void TossUp(
        float tossHeight,
        float serveHitHeight,
        float tossForwardDistance,
        Vector3 forwardDirection
    )
    {
        if (currentState != BallMotionState.Waiting)
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


        // ========================================================
        // Start
        // ========================================================

        Vector3 startPosition =
            rb.position;


        float startY =
            startPosition.y;


        // ========================================================
        // Apex Height
        // ========================================================

        calculatedTossApexY =
            startY +
            tossHeight;


        // ========================================================
        // Hit Height Check
        // ========================================================

        if (serveHitHeight >= calculatedTossApexY)
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


        ActivatePhysics();


        // ========================================================
        // Gravity
        // ========================================================

        float gravity =
            Mathf.Abs(
                Physics.gravity.y
            );


        // ========================================================
        // Vertical Toss Velocity
        //
        // v^2 = 2gh
        // ========================================================

        float initialUpVelocity =
            Mathf.Sqrt(
                2.0f *
                gravity *
                tossHeight
            );


        // ========================================================
        // 下降中にHit Heightへ到達する時間
        //
        // y = y0 + v0*t - 1/2*g*t^2
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


        // 下降時なので + の解
        calculatedTimeToHit =
            (
                initialUpVelocity +
                Mathf.Sqrt(discriminant)
            )
            /
            gravity;


        // ========================================================
        // Forward Direction
        // ========================================================

        forwardDirection.y =
            0.0f;


        if (forwardDirection.sqrMagnitude <= 0.0001f)
        {
            Debug.LogError(
                "[Ball] Toss Forward Direction が0です。"
            );

            return;
        }


        forwardDirection.Normalize();


        // ========================================================
        // Forward Speed
        //
        // サーブを打つ瞬間までに
        // tossForwardDistanceだけ移動
        // ========================================================

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


        // ========================================================
        // Debug
        // ========================================================

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
    // Toss Detection
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
                    "[Ball] Toss Apex\n" +
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
            return;


        // 下降中のみ
        if (rb.linearVelocity.y >= 0.0f)
            return;


        float previousY =
            previousPosition.y;


        float currentY =
            rb.position.y;


        // Hit Heightを横切った瞬間
        if (
            previousY > currentServeHitHeight &&
            currentY <= currentServeHitHeight
        )
        {
            serveHitTriggered =
                true;


            // ====================================================
            // FixedUpdate間を補間して、
            // Hit Heightちょうどの位置へ合わせる。
            // ====================================================

            float denominator =
                previousY -
                currentY;


            float t =
                1.0f;


            if (Mathf.Abs(denominator) > 0.00001f)
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
                Mathf.Clamp01(t);


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
    /// レシーバーTransformへ向けてサーブする。
    ///
    /// targetTransform.positionをそのままTargetとして使用する。
    ///
    /// Hidden Offset等は一切加えない。
    ///
    /// speedKmh
    ///   130 -> 130 km/h
    ///
    /// launchAngleDeg
    ///    0 -> 水平
    ///   +2 -> 2度上向き
    ///   -2 -> 2度下向き
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


        ActivatePhysics();


        // ========================================================
        // Target
        //
        // レシーバーのTransform位置をそのまま使う。
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
            Mathf.Cos(angleRad);


        float verticalSpeed =
            speed *
            Mathf.Sin(angleRad);


        if (horizontalSpeed <= 0.01f)
        {
            Debug.LogError(
                "[Ball] Launch Angle が不正です。"
            );

            return;
        }


        // ========================================================
        // Receiverまでの飛行時間
        //
        // XZ方向は等速。
        //
        // t = distance / horizontalSpeed
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
        // ReceiverのY座標へ到達するために必要な
        // 鉛直方向加速度を計算。
        //
        // Δy = vy*t + 1/2*a*t^2
        //
        // a = 2(Δy - vy*t) / t^2
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


        // ========================================================
        // Unity Gravityとの差分
        //
        // Gravityはすでにかかるので、
        // 足りない分だけ追加する。
        // ========================================================

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


        // ========================================================
        // Serve Timer
        // ========================================================

        serveElapsedTime =
            0.0f;


        currentState =
            BallMotionState.SpikeServing;


        // ========================================================
        // Debug
        // ========================================================

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
            Vector3.up * additionalVerticalAcceleration,
            ForceMode.Acceleration
        );

        serveElapsedTime += Time.fixedDeltaTime;
    }


    // ============================================================
    // Target Arrival
    // ============================================================

    /// <summary>
    /// サーブがReceiverへ到達した瞬間。
    ///
    /// 現段階ではReceiver Transformの位置へ
    /// ボールを正確に合わせて停止させる。
    ///
    /// 後でここをReceive処理へ変更する。
    /// </summary>
    private void CompleteServeAtTarget()
    {
        if (currentServeTarget == null)
        {
            Debug.LogError(
                "[Ball] Serve Target が失われました。"
            );

            return;
        }


        // ========================================================
        // 現在のReceiver Transform位置
        //
        // Transform.positionをそのまま使う。
        // ========================================================

        Vector3 exactTargetPosition =
            currentServeTarget.position;


        rb.position =
            exactTargetPosition;


        rb.linearVelocity =
            Vector3.zero;


        rb.angularVelocity =
            Vector3.zero;


        additionalVerticalAcceleration =
            0.0f;


        rb.useGravity =
            false;


        rb.isKinematic =
            true;


        currentState =
            BallMotionState.AtServeTarget;


        Debug.Log(
            "[Ball] Serve Reached Receiver\n" +
            $"Receiver = {currentServeTarget.name}\n" +
            $"Ball Position = {rb.position}\n" +
            $"Receiver Position = {currentServeTarget.position}"
        );


        // ========================================================
        // 後でここからReceiveを開始できる
        // ========================================================

        OnServeReachedTarget?.Invoke();
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


        // 高速ボールのすり抜け対策
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;


        rb.WakeUp();
    }
}