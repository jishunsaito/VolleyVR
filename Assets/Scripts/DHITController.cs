using System;
using UnityEngine;

/// <summary>
/// Rally JSON互換用のDHIT設定。
///
/// version 2以降ではPole基準DHIT用の
/// maxShiftVelocityPxPerSecを保存する。
/// version 3以降では追従速度倍率とStartup Boost設定も保存する。
///
/// 旧DHITで使用していたパラメータも
/// 既存コード / 既存JSONとの互換性のため残しているが、
/// Pole基準DHITの制御には使用しない。
/// </summary>
[Serializable]
public class DhitSettings
{
    public int version = 3;

    [Tooltip("static or dynamic")]
    public string mode = "static";

    [Tooltip("Pole基準DHITのZDP shift最大変化速度 [px/s]。0 = 制限なし")]
    public float maxShiftVelocityPxPerSec = 20.0f;

    [Tooltip("通常時のPole追従速度倍率。1 = 従来速度")]
    public float trackingSpeedMultiplier = 1.25f;

    [Tooltip("Trial開始後、最初のTarget Poleを取得した直後だけ使う追従速度倍率")]
    public float startupBoostMultiplier = 2.0f;

    [Tooltip("Startup Boostを継続する時間 [s]")]
    public float startupBoostDurationSec = 0.25f;

    // =========================================================
    // Legacy fields
    // =========================================================
    // 以前の「視差速度 × gain」方式とのJSON / API互換用。
    // Pole基準DHITでは使用しない。

    [HideInInspector]
    public float gain = 0.30f;

    [HideInInspector]
    public float maxPositiveAdditionalDisparityPx = 20.0f;

    [HideInInspector]
    public float maxNegativeAdditionalDisparityPx = 20.0f;

    [HideInInspector]
    public float maxAdditionalDisparityVelocityPxPerSec = 10.0f;

    [HideInInspector]
    public float velocityDeadZonePxPerSec = 0.5f;

    [HideInInspector]
    public float filterTimeConstantSec = 0.05f;
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
    private bool dhitEnabled =
        false;


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


    [Header("Tracking Speed")]

    [Tooltip(
        "通常時のPole追従速度倍率。\n" +
        "1 = 従来速度、1.25 = 25%高速化。"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float trackingSpeedMultiplier =
        1.25f;


    [Tooltip(
        "Trialごとに最初のTarget Poleを取得した直後だけ、\n" +
        "追従速度へ追加で掛ける倍率。\n" +
        "前TrialのShiftを引き継がず、毎回同じBase Shiftから\n" +
        "素早くTarget Pole側へ収束させるために使います。"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float startupBoostMultiplier =
        2.0f;


    [Tooltip(
        "最初のTarget Pole取得後、Startup Boostを継続する時間 [s]。\n" +
        "0ならStartup Boostなし。"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float startupBoostDurationSec =
        0.25f;


    // =========================================================
    // Static HIT
    // =========================================================

    [Header("Static HIT")]

    [Tooltip(
        "DHIT OFF時のStatic shiftPixels。\n" +
        "Dynamic開始時の初期位置にもなります。\n" +
        "UIControllerのShiftから変更できます。"
    )]
    [SerializeField]
    private float baseShiftPixels =
        0.0f;


    [Tooltip(
        "ONの場合のみ、Awake時にImageControllerの現在Shiftを" +
        "Base Shiftの初期値として取り込みます。\n" +
        "通常はOFFにして、DHITController / UI側のBase Shiftを正とします。"
    )]
    [SerializeField]
    private bool initializeBaseShiftFromImageControllerOnAwake =
        false;


    // =========================================================
    // Rally JSON compatibility
    // =========================================================

    [Header("Saved DHIT Settings")]

    [Tooltip(
        "Rally JSONとの互換用。\n" +
        "modeとmaxShiftVelocityPxPerSecを保存します。"
    )]
    [SerializeField]
    private DhitSettings settings =
        new DhitSettings();


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


    // Trialごとに一度だけ発動するStartup Boost。
    private bool startupBoostTriggered =
        false;

    private float startupBoostRemainingSec =
        0.0f;


    // 旧DHITの公開Debug値との互換用。
    // Pole制御には使用しない。
    private bool disparityInitializedForDebug =
        false;

    private float previousBallDisparityPxForDebug =
        0.0f;


    // =========================================================
    // Public State / Compatibility
    // =========================================================

    public bool DHITEnabled =>
        dhitEnabled;


    // 現在のUIControllerが参照する名前。
    public bool IsDynamic =>
        dhitEnabled;


    public float BaseShiftPixels =>
        baseShiftPixels;


    public float CurrentShiftPixels =>
        currentShiftPixels;


    public float TargetShiftPixels =>
        targetShiftPixels;


    public float CurrentFinalShiftPixels =>
        currentShiftPixels;


    public float CurrentDynamicShiftPixels =>
        currentShiftPixels -
        baseShiftPixels;


    /// <summary>
    /// 旧「additional disparity」APIとの互換値。
    ///
    /// shiftedDisparity = rawDisparity - 2 * shiftPixels
    /// なので、BaseからのShift差を追加視差へ戻すと
    /// additionalDisparity = -2 * dynamicShift。
    ///
    /// Pole制御には使用しない。
    /// </summary>
    public float CurrentAdditionalDisparityPx =>
        -2.0f *
        CurrentDynamicShiftPixels;


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


    public bool StartupBoostActive =>
        startupBoostRemainingSec > 0.0f;


    public float StartupBoostRemainingSec =>
        startupBoostRemainingSec;


    public float CurrentTrackingSpeedMultiplier
    {
        get;
        private set;
    } = 1.0f;


    // =========================================================
    // Legacy Debug Properties
    // =========================================================
    // 現在の他スクリプトが参照していても壊れないように残す。
    // Pole基準の制御入力ではない。

    public float CurrentBallDisparityPx
    {
        get;
        private set;
    }


    public float CurrentRawDisparityVelocityPxPerSec
    {
        get;
        private set;
    }


    public float CurrentFilteredDisparityVelocityPxPerSec
    {
        get;
        private set;
    }


    /// <summary>
    /// 現在Shiftが実際に変化している速度の符号付き値 [px/s]。
    /// </summary>
    public float CurrentDhitVelocityPxPerSec
    {
        get;
        private set;
    }


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
        EnsureSettings();


        if (
            initializeBaseShiftFromImageControllerOnAwake &&
            imageController != null
        )
        {
            baseShiftPixels =
                imageController.ShiftPixels;
        }


        // Sceneに保存されているboolを正とし、
        // JSON互換settings側へ同期する。
        settings.mode =
            dhitEnabled
                ? "dynamic"
                : "static";


        settings.version =
            3;


        settings.maxShiftVelocityPxPerSec =
            maxShiftVelocityPxPerSec;


        settings.trackingSpeedMultiplier =
            trackingSpeedMultiplier;


        settings.startupBoostMultiplier =
            startupBoostMultiplier;


        settings.startupBoostDurationSec =
            startupBoostDurationSec;


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


        UpdateLegacyDisparityDebug();


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


        // 3.5.
        // Trialごとの最初のTarget Pole取得時だけ
        // Startup Boostを開始する。
        UpdateStartupBoost(
            dt
        );


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
            (
                currentShiftPixels -
                shiftBefore
            )
            /
            dt;


        // 6.
        // ImageControllerへ反映
        ApplyCurrentShift();
    }


    // =========================================================
    // Trial Control
    // =========================================================

    /// <summary>
    /// 現在のStatic / Dynamic状態を維持したままTrialを開始する。
    ///
    /// Base ShiftはUI / DHITController Inspectorの値を正とする。
    /// ImageController側はFinal Shiftなので、
    /// Trial開始時にそこからBaseへ逆コピーしない。
    /// </summary>
    public void BeginTrial()
    {
        ResetInternalState();


        currentShiftPixels =
            baseShiftPixels;


        targetShiftPixels =
            baseShiftPixels;


        ApplyCurrentShift();
    }


    /// <summary>
    /// 旧Pole版との互換用。
    /// false = Static
    /// true  = Dynamic
    /// </summary>
    public void BeginTrial(
        bool dynamicCondition
    )
    {
        SetDynamicEnabled(
            dynamicCondition
        );


        BeginTrial();
    }


    /// <summary>
    /// 旧「trial-specific DhitSettings」呼出しとの互換用。
    /// Pole基準方式ではmodeと最大Shift速度だけを使用する。
    /// </summary>
    public void BeginTrial(
        DhitSettings newSettings
    )
    {
        if (newSettings != null)
        {
            ApplySettings(
                newSettings
            );
        }


        BeginTrial();
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

    public void SetDynamicEnabled(
        bool enabled
    )
    {
        dhitEnabled =
            enabled;


        EnsureSettings();


        settings.mode =
            dhitEnabled
                ? "dynamic"
                : "static";


        ResetInternalState();


        currentShiftPixels =
            baseShiftPixels;


        targetShiftPixels =
            baseShiftPixels;


        ApplyCurrentShift();
    }


    // 旧Pole版APIの別名。
    public void SetDHITEnabled(
        bool enabled
    )
    {
        SetDynamicEnabled(
            enabled
        );
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
            imageController.ShiftPixels;


        if (!dhitEnabled)
        {
            currentShiftPixels =
                baseShiftPixels;


            targetShiftPixels =
                baseShiftPixels;


            ApplyCurrentShift();
        }
    }


    // =========================================================
    // JSON Save / Load Compatibility
    // =========================================================

    public DhitSettings CreateSettingsSnapshot()
    {
        EnsureSettings();


        settings.version =
            3;


        settings.mode =
            dhitEnabled
                ? "dynamic"
                : "static";


        settings.maxShiftVelocityPxPerSec =
            maxShiftVelocityPxPerSec;


        settings.trackingSpeedMultiplier =
            trackingSpeedMultiplier;


        settings.startupBoostMultiplier =
            startupBoostMultiplier;


        settings.startupBoostDurationSec =
            startupBoostDurationSec;


        return CopySettings(
            settings
        );
    }


    public void ApplySavedState(
        float savedBaseShiftPixels,
        bool dynamicEnabled,
        DhitSettings savedSettings
    )
    {
        baseShiftPixels =
            savedBaseShiftPixels;


        if (savedSettings != null)
        {
            ApplySettings(
                savedSettings
            );
        }


        // RallySettingsData側のboolを最終的なON/OFFの正とする。
        dhitEnabled =
            dynamicEnabled;


        EnsureSettings();


        settings.mode =
            dhitEnabled
                ? "dynamic"
                : "static";


        ResetInternalState();


        currentShiftPixels =
            baseShiftPixels;


        targetShiftPixels =
            baseShiftPixels;


        ApplyCurrentShift();
    }


    private void ApplySettings(
        DhitSettings source
    )
    {
        if (source == null)
        {
            return;
        }


        EnsureSettings();


        settings =
            CopySettings(
                source
            );


        /*
         * 新Pole版JSON(version 2以降)だけ、
         * maxShiftVelocityPxPerSecを復元する。
         *
         * 旧「視差速度 × gain」版JSONにはこの値が存在しないため、
         * そのJSONを読んだだけで0 (= unlimited)へ変わらないようにする。
         */
        if (source.version >= 2)
        {
            maxShiftVelocityPxPerSec =
                Mathf.Max(
                    0.0f,
                    source.maxShiftVelocityPxPerSec
                );
        }


        if (source.version >= 3)
        {
            trackingSpeedMultiplier =
                Mathf.Max(
                    0.0f,
                    source.trackingSpeedMultiplier
                );


            startupBoostMultiplier =
                Mathf.Max(
                    0.0f,
                    source.startupBoostMultiplier
                );


            startupBoostDurationSec =
                Mathf.Max(
                    0.0f,
                    source.startupBoostDurationSec
                );
        }


        if (!string.IsNullOrWhiteSpace(
            source.mode
        ))
        {
            dhitEnabled =
                string.Equals(
                    source.mode,
                    "dynamic",
                    StringComparison.OrdinalIgnoreCase
                );
        }
    }


    private static DhitSettings CopySettings(
        DhitSettings source
    )
    {
        if (source == null)
        {
            return new DhitSettings();
        }


        return new DhitSettings
        {
            version =
                source.version,

            mode =
                source.mode,

            maxShiftVelocityPxPerSec =
                source.maxShiftVelocityPxPerSec,

            trackingSpeedMultiplier =
                source.trackingSpeedMultiplier,

            startupBoostMultiplier =
                source.startupBoostMultiplier,

            startupBoostDurationSec =
                source.startupBoostDurationSec,

            // Legacy valuesもそのまま保持する。
            gain =
                source.gain,

            maxPositiveAdditionalDisparityPx =
                source.maxPositiveAdditionalDisparityPx,

            maxNegativeAdditionalDisparityPx =
                source.maxNegativeAdditionalDisparityPx,

            maxAdditionalDisparityVelocityPxPerSec =
                source.maxAdditionalDisparityVelocityPxPerSec,

            velocityDeadZonePxPerSec =
                source.velocityDeadZonePxPerSec,

            filterTimeConstantSec =
                source.filterTimeConstantSec
        };
    }


    private void EnsureSettings()
    {
        if (settings == null)
        {
            settings =
                new DhitSettings();
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
        // Vz =
        //
        // Z(t) - Z(t-dt)
        // ----------------
        //       dt
        //
        // Vz < 0
        // Cameraへ近づく
        //
        // Vz > 0
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
    // 3.5. Startup Catch-Up
    // =========================================================

    /// <summary>
    /// Trialごとに最初のNear/Far Poleが決まった瞬間から、
    /// 一定時間だけ追従速度を上げる。
    ///
    /// Shift値そのものを前Trialから引き継いだり、
    /// Poleへ瞬間移動させたりはしない。
    ///
    /// BeginTrial()で毎回Base Shiftへ戻るため、
    /// 同一条件なら毎回同じ初期状態から始まる。
    /// </summary>
    private void UpdateStartupBoost(
        float dt
    )
    {
        if (
            !startupBoostTriggered &&
            currentTargetPole != TargetPole.None
        )
        {
            startupBoostTriggered =
                true;


            startupBoostRemainingSec =
                Mathf.Max(
                    0.0f,
                    startupBoostDurationSec
                );
        }


        if (startupBoostRemainingSec <= 0.0f)
        {
            startupBoostRemainingSec =
                0.0f;

            return;
        }


        startupBoostRemainingSec =
            Mathf.Max(
                0.0f,
                startupBoostRemainingSec - dt
            );
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
        // K =
        //
        // Pole間Shift [px]
        // -----------------
        // Pole間Depth [m]
        //
        // [px/m]
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
        // Vshift =
        //
        // K * |Vz|
        //
        // [px/m] * [m/s]
        // =
        // [px/s]
        // =====================================================

        float baseNaturalShiftVelocity =
            shiftPerDepth *
            Mathf.Abs(
                CurrentDepthVelocityMps
            );


        float speedMultiplier =
            Mathf.Max(
                0.0f,
                trackingSpeedMultiplier
            );


        if (StartupBoostActive)
        {
            speedMultiplier *=
                Mathf.Max(
                    0.0f,
                    startupBoostMultiplier
                );
        }


        CurrentTrackingSpeedMultiplier =
            speedMultiplier;


        float naturalShiftVelocity =
            baseNaturalShiftVelocity *
            speedMultiplier;


        CurrentNaturalShiftVelocityPxPerSec =
            naturalShiftVelocity;


        // =====================================================
        // Maximum velocity limit
        //
        // trackingSpeedMultiplier / Startup Boostで
        // 全体の追従速度を高速化するときは、
        // 上限側も同じ倍率で拡張する。
        //
        // 0 = unlimited
        // =====================================================

        if (
            maxShiftVelocityPxPerSec >
            0.0f
        )
        {
            float effectiveMaxVelocity =
                maxShiftVelocityPxPerSec *
                speedMultiplier;


            CurrentAppliedShiftVelocityPxPerSec =
                Mathf.Min(
                    naturalShiftVelocity,
                    effectiveMaxVelocity
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
        // S(t + dt)
        //
        // =
        //
        // MoveTowards(
        //     S(t),
        //     Starget,
        //     Vshift * dt
        // )
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
    // Legacy Debug Update
    // =========================================================

    private void UpdateLegacyDisparityDebug()
    {
        if (
            ballDisparityCalculator == null ||
            !ballDisparityCalculator.HasBall
        )
        {
            disparityInitializedForDebug =
                false;


            CurrentBallDisparityPx =
                0.0f;


            CurrentRawDisparityVelocityPxPerSec =
                0.0f;


            CurrentFilteredDisparityVelocityPxPerSec =
                0.0f;


            return;
        }


        float currentDisparity =
            ballDisparityCalculator
                .DisparityPixels;


        CurrentBallDisparityPx =
            currentDisparity;


        float dt =
            Time.deltaTime;


        if (
            !disparityInitializedForDebug ||
            dt <= 0.0f
        )
        {
            previousBallDisparityPxForDebug =
                currentDisparity;


            disparityInitializedForDebug =
                true;


            CurrentRawDisparityVelocityPxPerSec =
                0.0f;


            CurrentFilteredDisparityVelocityPxPerSec =
                0.0f;


            return;
        }


        float rawVelocity =
            (
                currentDisparity -
                previousBallDisparityPxForDebug
            )
            /
            dt;


        previousBallDisparityPxForDebug =
            currentDisparity;


        CurrentRawDisparityVelocityPxPerSec =
            rawVelocity;


        // Pole版ではfilterを制御に使わないため、
        // 互換表示上はRaw値と同じ値を返す。
        CurrentFilteredDisparityVelocityPxPerSec =
            rawVelocity;
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


        disparityInitializedForDebug =
            false;


        previousBallDisparityPxForDebug =
            0.0f;


        CurrentDepthVelocityMps =
            0.0f;


        CurrentNaturalShiftVelocityPxPerSec =
            0.0f;


        CurrentAppliedShiftVelocityPxPerSec =
            0.0f;


        CurrentRawDisparityVelocityPxPerSec =
            0.0f;


        CurrentFilteredDisparityVelocityPxPerSec =
            0.0f;


        CurrentDhitVelocityPxPerSec =
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


        startupBoostTriggered =
            false;


        startupBoostRemainingSec =
            0.0f;


        CurrentTrackingSpeedMultiplier =
            1.0f;


        disparityInitializedForDebug =
            false;


        previousBallDisparityPxForDebug =
            0.0f;


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


        CurrentBallDisparityPx =
            0.0f;


        CurrentRawDisparityVelocityPxPerSec =
            0.0f;


        CurrentFilteredDisparityVelocityPxPerSec =
            0.0f;


        CurrentDhitVelocityPxPerSec =
            0.0f;
    }
}
