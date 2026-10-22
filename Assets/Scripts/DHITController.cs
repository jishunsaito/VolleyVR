using System;
using UnityEngine;

/// <summary>
/// Rally JSON / old UI compatibility settings.
/// Current Pole-based DHIT uses maxShiftVelocityPxPerSec and
/// usePoleMidpointAsStaticShift. Legacy fields are kept only so
/// older JSON can still deserialize without compile/runtime issues.
/// </summary>
[Serializable]
public class DhitSettings
{
    public int version = 3;
    public string mode = "static";
    public float maxShiftVelocityPxPerSec = 20.0f;
    public bool usePoleMidpointAsStaticShift = true;

    // Legacy JSON compatibility fields (not used by current algorithm)
    [HideInInspector] public float gain = 0.30f;
    [HideInInspector] public float maxPositiveAdditionalDisparityPx = 20.0f;
    [HideInInspector] public float maxNegativeAdditionalDisparityPx = 20.0f;
    [HideInInspector] public float maxAdditionalDisparityVelocityPxPerSec = 10.0f;
    [HideInInspector] public float velocityDeadZonePxPerSec = 0.5f;
    [HideInInspector] public float filterTimeConstantSec = 0.05f;
}


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
    private float maxShiftVelocityPxPerSec = 20.0f;


    // =========================================================
    // Static HIT
    // =========================================================

    [Header("Static HIT")]

    [Tooltip(
        "ONの場合、Static HITをNear/Far PoleのZDP用shiftの中点に固定します。\n\n" +
        "StaticShift = (NearPoleShift + FarPoleShift) / 2\n\n" +
        "これによりNear/Far Poleのシフト後視差が対称になります。"
    )]
    [SerializeField]
    private bool usePoleMidpointAsStaticShift = true;


    [Tooltip(
        "Static HITとして使用するshiftPixels。\n" +
        "Use Pole Midpoint As Static ShiftがONの場合は自動計算されます。"
    )]
    [SerializeField]
    private float baseShiftPixels = 0.0f;


    [Tooltip(
        "Pole MidpointモードがOFFの場合のみ使用します。\n" +
        "BeginTrial()時点のImageController.shiftPixelsを" +
        "Static HITとして取得します。"
    )]
    [SerializeField]
    private bool captureBaseShiftAtTrialStart = true;


    // Pole中点のStatic Shiftを取得済みか
    private bool poleMidpointStaticShiftInitialized = false;


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

    private bool depthInitialized = false;

    private float previousBallDepthMeters = 0.0f;

    private float currentShiftPixels = 0.0f;

    private float targetShiftPixels = 0.0f;


    // =========================================================
    // Public Debug Values
    // =========================================================

    public bool DHITEnabled =>
        dhitEnabled;

    // Existing RallyController / UIController compatibility
    public bool IsDynamic =>
        dhitEnabled;

    public float CurrentFinalShiftPixels =>
        currentShiftPixels;

    public float CurrentDynamicShiftPixels =>
        currentShiftPixels - baseShiftPixels;

    public float CurrentAdditionalDisparityPx =>
        -2.0f * CurrentDynamicShiftPixels;

    public float CurrentDhitVelocityPxPerSec
    {
        get;
        private set;
    }


    public bool UsePoleMidpointAsStaticShift =>
        usePoleMidpointAsStaticShift;


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

    /// <summary>
    /// Near PoleをZDPにするためのshiftPixels
    /// </summary>
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


    /// <summary>
    /// Far PoleをZDPにするためのshiftPixels
    /// </summary>
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


    /// <summary>
    /// Near/Far PoleのZDP用shiftの中点。
    ///
    /// Static HITの基準として使用する。
    /// </summary>
    public float PoleMidpointShiftPixels
    {
        get
        {
            if (
                ballDisparityCalculator == null ||
                !ballDisparityCalculator.HasNearPole ||
                !ballDisparityCalculator.HasFarPole
            )
            {
                return baseShiftPixels;
            }

            return
                (
                    NearPoleTargetShiftPixels +
                    FarPoleTargetShiftPixels
                )
                *
                0.5f;
        }
    }


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        /*
         * Pole中点がまだ取得できない可能性があるため、
         * Awakeでは一旦ImageControllerの現在値を保持する。
         *
         * Poleデータが取得可能になった時点で、
         * Pole中点モードなら自動的に置き換える。
         */

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
        // Pole midpoint Static Shift initialization
        // =====================================================

        /*
         * AwakeやBeginTrial時点でPole情報がまだ取得できていない場合、
         * Calculatorの計算が完了した最初のLateUpdateで取得する。
         *
         * 一度取得したら、その試行中は固定。
         */

        if (
            usePoleMidpointAsStaticShift &&
            !poleMidpointStaticShiftInitialized
        )
        {
            if (TrySetPoleMidpointAsBaseShift())
            {
                /*
                 * まだDynamicの方向が決まっていない場合は、
                 * Current ShiftもStatic位置へ揃える。
                 */

                if (
                    !dhitEnabled ||
                    currentTargetPole == TargetPole.None
                )
                {
                    currentShiftPixels =
                        baseShiftPixels;

                    targetShiftPixels =
                        baseShiftPixels;

                    ApplyCurrentShift();
                }
            }
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


            CurrentDhitVelocityPxPerSec =
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


        float shiftBefore =
            currentShiftPixels;


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


        CurrentDhitVelocityPxPerSec =
            (currentShiftPixels - shiftBefore) / dt;


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
    /// Pole Midpoint Static ShiftがONの場合：
    ///
    /// Near/Far PoleのZDP用shiftの中点を
    /// Static HITとして自動的に設定する。
    ///
    /// Dynamic条件でも、
    /// このStatic位置からDHITを開始する。
    /// </summary>
    /// <summary>
    /// Keep the currently selected Static/Dynamic mode and start a trial.
    /// Used by RallyController.
    /// </summary>
    public void BeginTrial()
    {
        BeginTrial(dhitEnabled);
    }


    public void BeginTrial(
        bool dynamicCondition
    )
    {
        dhitEnabled =
            dynamicCondition;


        // =====================================================
        // Static Shift determination
        // =====================================================

        if (usePoleMidpointAsStaticShift)
        {
            /*
             * 新しい試行なので、
             * Pole中点を改めて計算する。
             *
             * 試行中はこの値を固定する。
             */

            poleMidpointStaticShiftInitialized =
                false;


            TrySetPoleMidpointAsBaseShift();
        }
        else
        {
            /*
             * 従来方式。
             *
             * 現在のImageController.shiftPixelsを
             * Static HITとして取得する。
             */

            if (
                captureBaseShiftAtTrialStart &&
                imageController != null
            )
            {
                baseShiftPixels =
                    imageController.shiftPixels;
            }
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
    // Pole Midpoint Static Shift
    // =========================================================

    /// <summary>
    /// Near/Far PoleをそれぞれZDPにするためのshiftの中点を
    /// Static HITとして設定する。
    ///
    ///
    /// Near Pole:
    ///
    /// Snear = Dnear / 2
    ///
    ///
    /// Far Pole:
    ///
    /// Sfar = Dfar / 2
    ///
    ///
    /// Static:
    ///
    /// Sstatic = (Snear + Sfar) / 2
    ///
    ///
    /// このとき、Static HIT適用後のNear/Far Pole視差は
    /// 大きさが等しく符号が反対になる。
    /// </summary>
    private bool TrySetPoleMidpointAsBaseShift()
    {
        if (
            ballDisparityCalculator == null ||
            !ballDisparityCalculator.HasNearPole ||
            !ballDisparityCalculator.HasFarPole
        )
        {
            return false;
        }


        float nearShift =
            NearPoleTargetShiftPixels;


        float farShift =
            FarPoleTargetShiftPixels;


        baseShiftPixels =
            (
                nearShift +
                farShift
            )
            *
            0.5f;


        poleMidpointStaticShiftInitialized =
            true;


        return true;
    }


    /// <summary>
    /// Inspectorや外部スクリプトから、
    /// Pole中点Static Shiftを再計算したい場合に使用。
    /// </summary>
    public bool RecalculatePoleMidpointStaticShift()
    {
        poleMidpointStaticShiftInitialized =
            false;


        bool success =
            TrySetPoleMidpointAsBaseShift();


        if (!success)
        {
            return false;
        }


        /*
         * Static条件ならその場で反映する。
         *
         * Dynamic中はCurrent Shiftを急に飛ばさない。
         */

        if (!dhitEnabled)
        {
            currentShiftPixels =
                baseShiftPixels;


            targetShiftPixels =
                baseShiftPixels;


            ApplyCurrentShift();
        }


        return true;
    }


    /// <summary>
    /// Pole中点方式のON/OFFを外部から変更する。
    /// </summary>
    public void SetUsePoleMidpointAsStaticShift(
        bool enabled
    )
    {
        usePoleMidpointAsStaticShift =
            enabled;


        if (enabled)
        {
            poleMidpointStaticShiftInitialized =
                false;


            if (TrySetPoleMidpointAsBaseShift())
            {
                if (!dhitEnabled)
                {
                    currentShiftPixels =
                        baseShiftPixels;


                    targetShiftPixels =
                        baseShiftPixels;


                    ApplyCurrentShift();
                }
            }
        }
        else
        {
            poleMidpointStaticShiftInitialized =
                false;
        }
    }


    // =========================================================
    // Enable / Disable
    // =========================================================

    public void SetDynamicEnabled(
        bool enabled
    )
    {
        dhitEnabled = enabled;

        if (usePoleMidpointAsStaticShift)
        {
            poleMidpointStaticShiftInitialized = false;
            TrySetPoleMidpointAsBaseShift();
        }

        ResetInternalState();
        currentShiftPixels = baseShiftPixels;
        targetShiftPixels = baseShiftPixels;
        ApplyCurrentShift();
    }


    // Alias kept for older code.
    public void SetDHITEnabled(
        bool enabled
    )
    {
        SetDynamicEnabled(enabled);
    }



    // =========================================================
    // Base Shift
    // =========================================================

    /// <summary>
    /// Manual Static Shift用。
    ///
    /// Pole MidpointモードがONの場合、
    /// 次回BeginTrial時にPole中点で上書きされる。
    /// </summary>
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


    /// <summary>
    /// Manual Static Shift用。
    ///
    /// Pole MidpointモードがONの場合は使用しない。
    /// </summary>
    public void CaptureCurrentShiftAsBase()
    {
        if (
            imageController == null ||
            usePoleMidpointAsStaticShift
        )
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
    // Rally JSON Compatibility
    // =========================================================

    public DhitSettings CreateSettingsSnapshot()
    {
        return new DhitSettings
        {
            version = 3,
            mode = dhitEnabled ? "dynamic" : "static",
            maxShiftVelocityPxPerSec = maxShiftVelocityPxPerSec,
            usePoleMidpointAsStaticShift = usePoleMidpointAsStaticShift
        };
    }


    public void ApplySavedState(
        float savedBaseShiftPixels,
        bool dynamicEnabled,
        DhitSettings savedSettings
    )
    {
        if (savedSettings != null)
        {
            // Old JSON can have version 0/1 and no current fields.
            // Only overwrite the velocity limit when it is a meaningful saved value
            // or when this is a current-format setting.
            if (savedSettings.version >= 2 || savedSettings.maxShiftVelocityPxPerSec > 0.0f)
            {
                maxShiftVelocityPxPerSec =
                    Mathf.Max(0.0f, savedSettings.maxShiftVelocityPxPerSec);
            }

            if (savedSettings.version >= 3)
            {
                usePoleMidpointAsStaticShift =
                    savedSettings.usePoleMidpointAsStaticShift;
            }
        }

        dhitEnabled = dynamicEnabled;

        if (usePoleMidpointAsStaticShift)
        {
            poleMidpointStaticShiftInitialized = false;

            // If Pole values are already available, use them now.
            // Otherwise keep the saved value as a temporary fallback;
            // LateUpdate will replace it once the calculator becomes valid.
            baseShiftPixels = savedBaseShiftPixels;
            TrySetPoleMidpointAsBaseShift();
        }
        else
        {
            baseShiftPixels = savedBaseShiftPixels;
            poleMidpointStaticShiftInitialized = false;
        }

        ResetInternalState();
        currentShiftPixels = baseShiftPixels;
        targetShiftPixels = baseShiftPixels;
        ApplyCurrentShift();
    }


    /// <summary>
    /// Legacy overload. Applies saved settings, then starts a trial.
    /// </summary>
    public void BeginTrial(
        DhitSettings newSettings
    )
    {
        if (newSettings != null)
        {
            if (newSettings.version >= 2 || newSettings.maxShiftVelocityPxPerSec > 0.0f)
            {
                maxShiftVelocityPxPerSec =
                    Mathf.Max(0.0f, newSettings.maxShiftVelocityPxPerSec);
            }

            if (newSettings.version >= 3)
            {
                usePoleMidpointAsStaticShift =
                    newSettings.usePoleMidpointAsStaticShift;
            }

            if (!string.IsNullOrEmpty(newSettings.mode))
            {
                dhitEnabled =
                    string.Equals(
                        newSettings.mode,
                        "dynamic",
                        StringComparison.OrdinalIgnoreCase
                    );
            }
        }

        BeginTrial();
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
        // Near / Far Pole depth difference
        // =====================================================

        float depthDifference =
            Mathf.Abs(
                farDepth -
                nearDepth
            );


        // =====================================================
        // Near / Far Pole shift difference
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
        // Actual shiftは
        // 奥行き速度に応じた速度でそこへ向かう。
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


        imageController.SetShiftPixels(
            currentShiftPixels
        );
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


        CurrentDhitVelocityPxPerSec =
            0.0f;
    }
}