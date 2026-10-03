using UnityEngine;

public class DHITController : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [SerializeField]
    private BallDisparityCalculator ballDisparityCalculator;

    [SerializeField]
    private ImageController imageController;


    // =========================================================
    // DHIT
    // =========================================================

    [Header("DHIT")]

    [Tooltip(
        "OFF = Static HIT\n" +
        "ON = Dynamic HIT"
    )]
    [SerializeField]
    private bool dhitEnabled = false;


    // =========================================================
    // Shift Velocity Limit
    // =========================================================

    [Header("Shift Velocity Limit")]

    [Tooltip(
        "ZDP shiftの最大変化速度 [px/s]\n" +
        "スパイクなどで急激に変化することを防ぎます。\n" +
        "0 = 制限なし"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float maxShiftVelocityPxPerSec =
        20.0f;


    // =========================================================
    // Static HIT
    // =========================================================

    [Header("Static HIT")]

    [Tooltip(
        "DHIT OFF時のStatic shiftPixels。\n" +
        "Dynamic開始時の初期位置にもなります。"
    )]
    [SerializeField]
    private float baseShiftPixels =
        0.0f;


    [Tooltip(
        "BeginTrial()時点のImageController.shiftPixelsを" +
        "Static HITとして取得します。"
    )]
    [SerializeField]
    private bool captureBaseShiftAtTrialStart =
        true;


    // =========================================================
    // Target Pole
    // =========================================================

    private enum TargetPole
    {
        None,
        Near,
        Far
    }


    private TargetPole currentTargetPole =
        TargetPole.None;


    // =========================================================
    // Internal State
    // =========================================================

    private bool depthInitialized =
        false;


    private float previousBallDepthMeters =
        0.0f;


    private float currentShiftPixels =
        0.0f;


    private float targetShiftPixels =
        0.0f;


    // =========================================================
    // Public Debug Values
    // =========================================================

    public bool DHITEnabled =>
        dhitEnabled;


    public float BaseShiftPixels =>
        baseShiftPixels;


    public float CurrentShiftPixels =>
        currentShiftPixels;


    public float TargetShiftPixels =>
        targetShiftPixels;


    public float CurrentBallDepthMeters
    {
        get;
        private set;
    }


    /// <summary>
    /// Camera-space depth velocity [m/s]
    ///
    /// negative:
    /// Cameraへ近づく
    ///
    /// positive:
    /// Cameraから遠ざかる
    /// </summary>
    public float CurrentDepthVelocityMps
    {
        get;
        private set;
    }


    /// <summary>
    /// Near/Far Pole間の
    /// depth -> shift変換係数 [px/m]
    /// </summary>
    public float CurrentShiftPerDepthPxPerMeter
    {
        get;
        private set;
    }


    /// <summary>
    /// 上限制限前の自然なZDP追従速度 [px/s]
    /// </summary>
    public float CurrentNaturalShiftVelocityPxPerSec
    {
        get;
        private set;
    }


    /// <summary>
    /// 実際に使用しているZDP追従速度 [px/s]
    /// </summary>
    public float CurrentAppliedShiftVelocityPxPerSec
    {
        get;
        private set;
    }


    public string CurrentTargetPole =>
        currentTargetPole.ToString();


    // =========================================================
    // Pole Target Shifts
    // =========================================================

    public float NearPoleTargetShiftPixels
    {
        get
        {
            if (
                ballDisparityCalculator == null ||
                !ballDisparityCalculator.HasNearPole
            )
            {
                return 0.0f;
            }


            /*
             * shiftedDisparity
             * =
             * rawDisparity
             * -
             * 2 * shiftPixels
             *
             * ZDPでは
             *
             * shiftedDisparity = 0
             *
             * よって
             *
             * shiftPixels
             * =
             * rawDisparity / 2
             */

            return
                ballDisparityCalculator
                    .NearPoleDisparityPixels *
                0.5f;
        }
    }


    public float FarPoleTargetShiftPixels
    {
        get
        {
            if (
                ballDisparityCalculator == null ||
                !ballDisparityCalculator.HasFarPole
            )
            {
                return 0.0f;
            }


            return
                ballDisparityCalculator
                    .FarPoleDisparityPixels *
                0.5f;
        }
    }


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (imageController != null)
        {
            baseShiftPixels =
                imageController.shiftPixels;
        }


        ResetInternalState();


        currentShiftPixels =
            baseShiftPixels;


        targetShiftPixels =
            baseShiftPixels;


        ApplyCurrentShift();
    }


    /// <summary>
    /// BallDisparityCalculatorはUpdate()で計算するので、
    /// DHITControllerはLateUpdate()で結果を使用する。
    /// </summary>
    private void LateUpdate()
    {
        if (
            imageController == null ||
            ballDisparityCalculator == null
        )
        {
            return;
        }


        // =====================================================
        // Static condition
        // =====================================================

        if (!dhitEnabled)
        {
            currentShiftPixels =
                baseShiftPixels;


            targetShiftPixels =
                baseShiftPixels;


            currentTargetPole =
                TargetPole.None;


            depthInitialized =
                false;


            CurrentDepthVelocityMps =
                0.0f;


            CurrentShiftPerDepthPxPerMeter =
                0.0f;


            CurrentNaturalShiftVelocityPxPerSec =
                0.0f;


            CurrentAppliedShiftVelocityPxPerSec =
                0.0f;


            ApplyCurrentShift();

            return;
        }


        // =====================================================
        // Dynamic condition
        // =====================================================

        float dt =
            Time.deltaTime;


        if (dt <= 0.0f)
        {
            return;
        }


        // 1.
        // BallのCamera-space depth velocityを求める
        UpdateBallDepthVelocity(
            dt
        );


        // 2.
        // Velocityの符号からNear/Far Poleを決める
        UpdateTargetPole();


        // 3.
        // Target PoleをZDPにするshiftを求める
        UpdateTargetShift();


        // 4.
        // Near/Far Pole geometryから
        // depth velocity -> shift velocityへ変換
        UpdateTrackingVelocity();


        // 5.
        // Targetへ追従
        TrackTarget(
            dt
        );


        // 6.
        // ImageControllerへ反映
        ApplyCurrentShift();
    }


    // =========================================================
    // Trial Control
    // =========================================================

    /// <summary>
    /// false = Static
    /// true  = Dynamic
    ///
    /// 推奨：
    ///
    /// 1.
    /// ImageController.shiftPixelsに
    /// Static ZDPを設定
    ///
    /// 2.
    /// BeginTrial()
    /// </summary>
    public void BeginTrial(
        bool dynamicCondition
    )
    {
        dhitEnabled =
            dynamicCondition;


        if (
            captureBaseShiftAtTrialStart &&
            imageController != null
        )
        {
            baseShiftPixels =
                imageController.shiftPixels;
        }


        ResetInternalState();


        currentShiftPixels =
            baseShiftPixels;


        targetShiftPixels =
            baseShiftPixels;


        ApplyCurrentShift();
    }


    public void EndTrial()
    {
        ResetInternalState();


        currentShiftPixels =
            baseShiftPixels;


        targetShiftPixels =
            baseShiftPixels;


        ApplyCurrentShift();
    }


    // =========================================================
    // Enable / Disable
    // =========================================================

    public void SetDHITEnabled(
        bool enabled
    )
    {
        dhitEnabled =
            enabled;


        ResetInternalState();


        currentShiftPixels =
            baseShiftPixels;


        targetShiftPixels =
            baseShiftPixels;


        ApplyCurrentShift();
    }


    // =========================================================
    // Base Shift
    // =========================================================

    public void SetBaseShiftPixels(
        float shiftPixels
    )
    {
        baseShiftPixels =
            shiftPixels;


        if (!dhitEnabled)
        {
            currentShiftPixels =
                baseShiftPixels;


            targetShiftPixels =
                baseShiftPixels;


            ApplyCurrentShift();
        }
    }


    public void CaptureCurrentShiftAsBase()
    {
        if (imageController == null)
        {
            return;
        }


        baseShiftPixels =
            imageController.shiftPixels;


        if (!dhitEnabled)
        {
            currentShiftPixels =
                baseShiftPixels;


            targetShiftPixels =
                baseShiftPixels;
        }
    }


    // =========================================================
    // 1. Ball Depth Velocity
    // =========================================================

    private void UpdateBallDepthVelocity(
        float dt
    )
    {
        if (
            !ballDisparityCalculator
                .HasBall
        )
        {
            depthInitialized =
                false;


            CurrentDepthVelocityMps =
                0.0f;


            return;
        }


        float currentDepth =
            ballDisparityCalculator
                .DepthMeters;


        CurrentBallDepthMeters =
            currentDepth;


        // =====================================================
        // First valid frame
        // =====================================================

        if (!depthInitialized)
        {
            previousBallDepthMeters =
                currentDepth;


            CurrentDepthVelocityMps =
                0.0f;


            depthInitialized =
                true;


            return;
        }


        // =====================================================
        // Camera-space depth velocity
        //
        //
        // Vz =
        //
        // Z(t) - Z(t-dt)
        // ----------------
        //       dt
        //
        //
        // Vz < 0
        //
        // Cameraへ近づく
        //
        //
        // Vz > 0
        //
        // Cameraから遠ざかる
        // =====================================================

        CurrentDepthVelocityMps =
            (
                currentDepth -
                previousBallDepthMeters
            )
            /
            dt;


        previousBallDepthMeters =
            currentDepth;
    }


    // =========================================================
    // 2. Direction -> Target Pole
    // =========================================================

    private void UpdateTargetPole()
    {
        if (!depthInitialized)
        {
            return;
        }


        /*
         * 調整用Dead Zoneではなく、
         * 浮動小数点誤差対策だけ。
         */
        const float NumericalEpsilon =
            0.00001f;


        float velocity =
            CurrentDepthVelocityMps;


        // =====================================================
        // Cameraへ近づく
        //
        // -> Far Poleを目標
        // =====================================================

        if (
            velocity <
            -NumericalEpsilon
        )
        {
            if (
                ballDisparityCalculator
                    .HasFarPole
            )
            {
                currentTargetPole =
                    TargetPole.Far;
            }


            return;
        }


        // =====================================================
        // Cameraから遠ざかる
        //
        // -> Near Poleを目標
        // =====================================================

        if (
            velocity >
            NumericalEpsilon
        )
        {
            if (
                ballDisparityCalculator
                    .HasNearPole
            )
            {
                currentTargetPole =
                    TargetPole.Near;
            }


            return;
        }


        // =====================================================
        // Velocity ≈ 0
        //
        // Targetは変更しない。
        //
        // トス頂点などでBaseへ戻したり、
        // Near/Farを切り替えたりしない。
        // =====================================================
    }


    // =========================================================
    // 3. Target Pole -> Target Shift
    // =========================================================

    private void UpdateTargetShift()
    {
        switch (currentTargetPole)
        {
            // =================================================
            // Near Pole
            // =================================================

            case TargetPole.Near:

                if (
                    ballDisparityCalculator
                        .HasNearPole
                )
                {
                    targetShiftPixels =
                        NearPoleTargetShiftPixels;
                }

                break;


            // =================================================
            // Far Pole
            // =================================================

            case TargetPole.Far:

                if (
                    ballDisparityCalculator
                        .HasFarPole
                )
                {
                    targetShiftPixels =
                        FarPoleTargetShiftPixels;
                }

                break;


            // =================================================
            // No direction yet
            //
            // 試行開始直後はStatic位置
            // =================================================

            case TargetPole.None:

            default:

                targetShiftPixels =
                    baseShiftPixels;

                break;
        }
    }


    // =========================================================
    // 4. Depth Velocity -> Shift Velocity
    // =========================================================

    private void UpdateTrackingVelocity()
    {
        // Pole情報が取れなければ追従しない
        if (
            !ballDisparityCalculator.HasNearPole ||
            !ballDisparityCalculator.HasFarPole
        )
        {
            CurrentShiftPerDepthPxPerMeter =
                0.0f;


            CurrentNaturalShiftVelocityPxPerSec =
                0.0f;


            CurrentAppliedShiftVelocityPxPerSec =
                0.0f;


            return;
        }


        // =====================================================
        // Near / Far Pole depth [m]
        // =====================================================

        float nearDepth =
            ballDisparityCalculator
                .NearPoleDepthMeters;


        float farDepth =
            ballDisparityCalculator
                .FarPoleDepthMeters;


        // =====================================================
        // Near / Far Pole ZDP shift [px]
        // =====================================================

        float nearShift =
            NearPoleTargetShiftPixels;


        float farShift =
            FarPoleTargetShiftPixels;


        // =====================================================
        // Pole間のDepth距離
        // =====================================================

        float depthDifference =
            Mathf.Abs(
                farDepth -
                nearDepth
            );


        // =====================================================
        // Pole間のShift距離
        // =====================================================

        float shiftDifference =
            Mathf.Abs(
                farShift -
                nearShift
            );


        // 同一depthなど異常条件
        if (
            depthDifference <=
            0.000001f
        )
        {
            CurrentShiftPerDepthPxPerMeter =
                0.0f;


            CurrentNaturalShiftVelocityPxPerSec =
                0.0f;


            CurrentAppliedShiftVelocityPxPerSec =
                0.0f;


            return;
        }


        // =====================================================
        // Geometry-derived conversion
        //
        //
        // K =
        //
        // Pole間Shift [px]
        // -----------------
        // Pole間Depth [m]
        //
        //
        // [px/m]
        //
        //
        // 調整用gainではなく、
        // 現在の撮影条件とPole geometryから
        // 自動的に決まる。
        // =====================================================

        float shiftPerDepth =
            shiftDifference /
            depthDifference;


        CurrentShiftPerDepthPxPerMeter =
            shiftPerDepth;


        // =====================================================
        // Natural shift velocity
        //
        //
        // Vshift =
        //
        // K * |Vz|
        //
        //
        // [px/m] * [m/s]
        //
        // =
        //
        // [px/s]
        // =====================================================

        float naturalShiftVelocity =
            shiftPerDepth *
            Mathf.Abs(
                CurrentDepthVelocityMps
            );


        CurrentNaturalShiftVelocityPxPerSec =
            naturalShiftVelocity;


        // =====================================================
        // Maximum velocity limit
        //
        // 0 = unlimited
        // =====================================================

        if (
            maxShiftVelocityPxPerSec >
            0.0f
        )
        {
            CurrentAppliedShiftVelocityPxPerSec =
                Mathf.Min(
                    naturalShiftVelocity,
                    maxShiftVelocityPxPerSec
                );
        }
        else
        {
            CurrentAppliedShiftVelocityPxPerSec =
                naturalShiftVelocity;
        }
    }


    // =========================================================
    // 5. Target Tracking
    // =========================================================

    private void TrackTarget(
        float dt
    )
    {
        // まだ進行方向が決まっていない
        if (
            currentTargetPole ==
            TargetPole.None
        )
        {
            return;
        }


        float shiftVelocity =
            CurrentAppliedShiftVelocityPxPerSec;


        if (
            shiftVelocity <=
            0.0f
        )
        {
            return;
        }


        // =====================================================
        // TargetはNear/Far Poleそのもの。
        //
        // ただしActual shiftは
        // 奥行き速度に応じた速度でそこへ向かう。
        //
        //
        // S(t + dt)
        //
        // =
        //
        // MoveTowards(
        //     S(t),
        //     Starget,
        //     Vshift * dt
        // )
        //
        // =====================================================

        currentShiftPixels =
            Mathf.MoveTowards(
                currentShiftPixels,
                targetShiftPixels,
                shiftVelocity * dt
            );
    }


    // =========================================================
    // Apply
    // =========================================================

    private void ApplyCurrentShift()
    {
        if (imageController == null)
        {
            return;
        }


        imageController.shiftPixels =
            currentShiftPixels;
    }


    // =========================================================
    // Trajectory Discontinuity
    // =========================================================

    /// <summary>
    /// ボール位置がTeleportした場合のみ呼ぶ。
    ///
    /// Toss -> Spikeで位置が連続しているなら
    /// 呼ばない。
    ///
    /// Current ShiftとTarget Poleは維持し、
    /// 速度計算だけリセットする。
    /// </summary>
    public void NotifyTrajectoryDiscontinuity()
    {
        depthInitialized =
            false;


        previousBallDepthMeters =
            0.0f;


        CurrentDepthVelocityMps =
            0.0f;


        CurrentNaturalShiftVelocityPxPerSec =
            0.0f;


        CurrentAppliedShiftVelocityPxPerSec =
            0.0f;
    }


    // =========================================================
    // Reset
    // =========================================================

    private void ResetInternalState()
    {
        depthInitialized =
            false;


        previousBallDepthMeters =
            0.0f;


        currentTargetPole =
            TargetPole.None;


        CurrentBallDepthMeters =
            0.0f;


        CurrentDepthVelocityMps =
            0.0f;


        CurrentShiftPerDepthPxPerMeter =
            0.0f;


        CurrentNaturalShiftVelocityPxPerSec =
            0.0f;


        CurrentAppliedShiftVelocityPxPerSec =
            0.0f;
    }
}