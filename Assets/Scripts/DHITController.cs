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
    // DHIT ON / OFF
    // =========================================================

    [Header("DHIT Condition")]

    [Tooltip(
        "OFF = Static HITのみ\n" +
        "ON = Static HIT + Dynamic HIT"
    )]
    [SerializeField]
    private bool dhitEnabled = false;


    // =========================================================
    // DHIT Parameters
    // =========================================================

    [Header("DHIT Parameters")]

    [Tooltip(
        "ボールの視差速度に対して、" +
        "どの程度の追加視差速度を与えるか。\n" +
        "dA/dt = gain * dD/dt"
    )]
    [SerializeField]
    private float gain = 0.30f;


    [Tooltip(
        "DHITで追加できる正方向の最大視差 [px]"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float maxPositiveAdditionalDisparityPx =
        20.0f;


    [Tooltip(
        "DHITで追加できる負方向の最大視差の絶対値 [px]"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float maxNegativeAdditionalDisparityPx =
        20.0f;


    [Tooltip(
        "DHITによる追加視差の最大変化速度 [px/s]"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float maxAdditionalDisparityVelocityPxPerSec =
        10.0f;


    [Tooltip(
        "これ以下の視差速度は0として扱う [px/s]"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float velocityDeadZonePxPerSec =
        0.5f;


    [Tooltip(
        "視差速度にかけるLow-pass filterの時定数 [s]\n" +
        "0ならフィルタなし"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float filterTimeConstantSec =
        0.05f;


    // =========================================================
    // Base Static HIT
    // =========================================================

    [Header("Base Static HIT")]

    [Tooltip(
        "DHITを加える前のStatic HITのshiftPixels"
    )]
    [SerializeField]
    private float baseShiftPixels =
        0.0f;


    [Tooltip(
        "試行開始時のImageController.shiftPixelsを" +
        "Static HITとして自動取得する"
    )]
    [SerializeField]
    private bool captureBaseShiftAtTrialStart =
        true;


    // =========================================================
    // Internal State
    // =========================================================

    /// <summary>
    /// 前フレームの有効な視差を持っているか
    /// </summary>
    private bool disparityInitialized =
        false;


    /// <summary>
    /// 1フレーム前のボール視差 [px]
    /// </summary>
    private float previousDisparityPx =
        0.0f;


    /// <summary>
    /// Low-pass filter後の視差速度 [px/s]
    /// </summary>
    private float filteredDisparityVelocityPxPerSec =
        0.0f;


    /// <summary>
    /// DHITによって追加する左右画像間の相対視差 [px]
    ///
    /// ImageController.shiftPixelsそのものではない。
    /// </summary>
    private float additionalDisparityPx =
        0.0f;


    // =========================================================
    // Public Runtime Values
    //
    // ログ・デバッグ・Inspector確認用
    // =========================================================

    public bool DHITEnabled =>
        dhitEnabled;


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


    public float CurrentDhitVelocityPxPerSec
    {
        get;
        private set;
    }


    public float CurrentAdditionalDisparityPx =>
        additionalDisparityPx;


    /// <summary>
    /// DHITによってImageController.shiftPixelsへ
    /// 加算されるシフト量
    /// </summary>
    public float CurrentDynamicShiftPixels
    {
        get;
        private set;
    }


    public float CurrentFinalShiftPixels
    {
        get;
        private set;
    }


    public float BaseShiftPixels =>
        baseShiftPixels;


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

        ApplyFinalShift();
    }


    /// <summary>
    /// BallDisparityCalculatorはUpdate()で視差を計算する。
    ///
    /// DHITControllerはLateUpdate()で、
    /// そのフレームの計算結果を取得する。
    ///
    /// Update
    /// BallDisparityCalculator
    ///
    /// ↓
    ///
    /// LateUpdate
    /// DHITController
    /// </summary>
    private void LateUpdate()
    {
        if (imageController == null)
        {
            return;
        }


        // =====================================================
        // DHIT OFF
        //
        // Static HITのみ
        // =====================================================

        if (!dhitEnabled)
        {
            additionalDisparityPx =
                0.0f;

            CurrentRawDisparityVelocityPxPerSec =
                0.0f;

            CurrentFilteredDisparityVelocityPxPerSec =
                0.0f;

            CurrentDhitVelocityPxPerSec =
                0.0f;

            // 次にONになったときに
            // 過去フレームとの差分を使わない
            disparityInitialized =
                false;

            ApplyFinalShift();

            return;
        }


        // =====================================================
        // DHIT ON
        // =====================================================

        float dt =
            Time.deltaTime;


        if (dt <= 0.0f)
        {
            return;
        }


        StepDHIT(
            dt
        );


        ApplyFinalShift();
    }


    // =========================================================
    // Trial Control
    // =========================================================

    /// <summary>
    /// 各試行開始時に呼ぶ。
    ///
    /// dynamicCondition:
    ///
    /// false
    /// → Static
    ///
    /// true
    /// → Dynamic
    ///
    ///
    /// 推奨順序：
    ///
    /// 1.
    /// ImageController.shiftPixelsに
    /// その試行のStatic ZDPを設定
    ///
    /// 2.
    /// BeginTrial(true / false)
    ///
    ///
    /// captureBaseShiftAtTrialStart = trueなら、
    /// その時点のshiftPixelsを
    /// Static HITとして保持する。
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

        ApplyFinalShift();
    }


    /// <summary>
    /// 試行終了。
    ///
    /// Dynamic追加分を削除して、
    /// Static HITへ戻す。
    /// </summary>
    public void EndTrial()
    {
        ResetInternalState();

        ApplyFinalShift();
    }


    // =========================================================
    // DHIT ON / OFF
    // =========================================================

    /// <summary>
    /// UI Toggleなどから直接呼べる。
    ///
    /// false = Static
    /// true  = Dynamic
    /// </summary>
    public void SetDHITEnabled(
        bool enabled
    )
    {
        dhitEnabled =
            enabled;


        ResetInternalState();

        ApplyFinalShift();
    }


    // =========================================================
    // Base Static HIT
    // =========================================================

    /// <summary>
    /// Static HITの基準値を直接指定する。
    /// </summary>
    public void SetBaseShiftPixels(
        float shiftPixels
    )
    {
        baseShiftPixels =
            shiftPixels;


        ApplyFinalShift();
    }


    /// <summary>
    /// 現在のImageController.shiftPixelsを
    /// Static HITとして取得する。
    /// </summary>
    public void CaptureCurrentShiftAsBase()
    {
        if (imageController == null)
        {
            return;
        }


        baseShiftPixels =
            imageController.shiftPixels;


        ApplyFinalShift();
    }


    // =========================================================
    // Trajectory Discontinuity
    // =========================================================

    /// <summary>
    /// ボール位置自体が瞬間移動した場合だけ呼ぶ。
    ///
    /// 例えば、
    /// 軌道切替時にTransform.positionを
    /// 不連続に変更している場合。
    ///
    ///
    /// 通常のToss -> Spikeで
    /// ボール位置が連続しているなら呼ばない。
    ///
    ///
    /// accumulated DHITは維持する。
    ///
    /// 視差速度計算だけをリセットする。
    /// </summary>
    public void NotifyTrajectoryDiscontinuity()
    {
        disparityInitialized =
            false;


        previousDisparityPx =
            0.0f;


        filteredDisparityVelocityPxPerSec =
            0.0f;


        CurrentRawDisparityVelocityPxPerSec =
            0.0f;


        CurrentFilteredDisparityVelocityPxPerSec =
            0.0f;


        CurrentDhitVelocityPxPerSec =
            0.0f;
    }


    // =========================================================
    // DHIT Main Logic
    // =========================================================

    private void StepDHIT(
        float dt
    )
    {
        // =====================================================
        // Raw Ball Disparity取得
        // =====================================================

        if (
            !TryGetRawBallDisparityPx(
                out float disparityPx
            )
        )
        {
            // Ballが存在しない、
            // あるいは視差が無効。

            // 次にBallが復帰したとき、
            // 古い値との差分を取らないようにする。
            disparityInitialized =
                false;


            CurrentRawDisparityVelocityPxPerSec =
                0.0f;


            CurrentFilteredDisparityVelocityPxPerSec =
                0.0f;


            CurrentDhitVelocityPxPerSec =
                0.0f;


            return;
        }


        CurrentBallDisparityPx =
            disparityPx;


        // =====================================================
        // First Valid Frame
        // =====================================================

        if (!disparityInitialized)
        {
            previousDisparityPx =
                disparityPx;


            filteredDisparityVelocityPxPerSec =
                0.0f;


            disparityInitialized =
                true;


            CurrentRawDisparityVelocityPxPerSec =
                0.0f;


            CurrentFilteredDisparityVelocityPxPerSec =
                0.0f;


            CurrentDhitVelocityPxPerSec =
                0.0f;


            return;
        }


        // =====================================================
        // 1.
        // Raw Disparity Velocity
        //
        //
        //        d(t) - d(t-dt)
        // vd = -------------------
        //               dt
        //
        //
        // [px/s]
        // =====================================================

        float rawDisparityVelocity =
            (
                disparityPx -
                previousDisparityPx
            )
            /
            dt;


        previousDisparityPx =
            disparityPx;


        CurrentRawDisparityVelocityPxPerSec =
            rawDisparityVelocity;


        // =====================================================
        // 2.
        // Low-pass Filter
        // =====================================================

        float tau =
            Mathf.Max(
                0.0f,
                filterTimeConstantSec
            );


        if (tau <= 0.0f)
        {
            // Filterなし
            filteredDisparityVelocityPxPerSec =
                rawDisparityVelocity;
        }
        else
        {
            // フレームレート依存を抑えた
            // exponential smoothing
            //
            //
            // alpha =
            //
            // 1 - exp(-dt / tau)
            //

            float alpha =
                1.0f -
                Mathf.Exp(
                    -dt / tau
                );


            filteredDisparityVelocityPxPerSec =
                Mathf.Lerp(
                    filteredDisparityVelocityPxPerSec,
                    rawDisparityVelocity,
                    alpha
                );
        }


        CurrentFilteredDisparityVelocityPxPerSec =
            filteredDisparityVelocityPxPerSec;


        // =====================================================
        // 3.
        // Dead Zone
        // =====================================================

        float velocityForControl =
            filteredDisparityVelocityPxPerSec;


        float deadZone =
            Mathf.Max(
                0.0f,
                velocityDeadZonePxPerSec
            );


        if (
            Mathf.Abs(
                velocityForControl
            )
            <
            deadZone
        )
        {
            velocityForControl =
                0.0f;
        }


        // =====================================================
        // 4.
        // Ball Disparity Velocity
        //
        // ->
        //
        // DHIT Additional Disparity Velocity
        //
        //
        // dA/dt
        // =
        // gain * dD/dt
        //
        //
        // D:
        // BallDisparityCalculator.DisparityPixels
        //
        // A:
        // DHITによって追加する視差
        // =====================================================

        float commandedDhitVelocity =
            gain *
            velocityForControl;


        // =====================================================
        // 5.
        // DHIT Velocity Clamp
        // =====================================================

        float maxVelocity =
            Mathf.Max(
                0.0f,
                maxAdditionalDisparityVelocityPxPerSec
            );


        commandedDhitVelocity =
            Mathf.Clamp(
                commandedDhitVelocity,
                -maxVelocity,
                +maxVelocity
            );


        // =====================================================
        // 6.
        // Integrate
        //
        //
        // A(t + dt)
        // =
        // A(t)
        // +
        // dA/dt * dt
        //
        // =====================================================

        float beforeAdditionalDisparity =
            additionalDisparityPx;


        additionalDisparityPx +=
            commandedDhitVelocity *
            dt;


        // =====================================================
        // 7.
        // Additional Disparity Clamp
        // =====================================================

        float maxPositive =
            Mathf.Max(
                0.0f,
                maxPositiveAdditionalDisparityPx
            );


        float maxNegative =
            Mathf.Max(
                0.0f,
                maxNegativeAdditionalDisparityPx
            );


        additionalDisparityPx =
            Mathf.Clamp(
                additionalDisparityPx,
                -maxNegative,
                +maxPositive
            );


        // =====================================================
        // 実際に適用されたDHIT速度
        //
        // Position Clampで上限に達した場合、
        // ここは0になる。
        // =====================================================

        CurrentDhitVelocityPxPerSec =
            (
                additionalDisparityPx -
                beforeAdditionalDisparity
            )
            /
            dt;
    }


    // =========================================================
    // Input
    // =========================================================

    /// <summary>
    /// BallDisparityCalculatorから
    /// 生のボール視差を取得する。
    ///
    ///
    /// 重要：
    ///
    /// DisparityPixels
    ///
    /// を使用する。
    ///
    ///
    /// ShiftedDisparityPixelsは使わない。
    ///
    ///
    /// ShiftedDisparityPixelsには
    /// ImageController.shiftPixelsの影響が
    /// すでに含まれているため。
    ///
    ///
    /// それをDHIT入力にすると、
    ///
    /// DHIT
    /// ↓
    /// shiftPixels
    /// ↓
    /// ShiftedDisparity
    /// ↓
    /// DHIT
    ///
    /// という自己フィードバックになる。
    /// </summary>
    private bool TryGetRawBallDisparityPx(
        out float disparityPx
    )
    {
        disparityPx =
            0.0f;


        if (
            ballDisparityCalculator ==
            null
        )
        {
            return false;
        }


        if (
            !ballDisparityCalculator
                .HasBall
        )
        {
            return false;
        }


        disparityPx =
            ballDisparityCalculator
                .DisparityPixels;


        if (
            float.IsNaN(
                disparityPx
            )
            ||
            float.IsInfinity(
                disparityPx
            )
        )
        {
            return false;
        }


        return true;
    }


    // =========================================================
    // Output
    // =========================================================

    /// <summary>
    ///
    /// BallDisparityCalculatorでは
    ///
    /// shiftedDisparity
    ///
    /// =
    ///
    /// rawDisparity
    /// -
    /// 2 * shiftPixels
    ///
    ///
    /// となっている。
    ///
    ///
    /// DHITによって
    /// additionalDisparity = A
    ///
    /// を加えたい。
    ///
    ///
    /// rawDisparity + A
    ///
    /// =
    ///
    /// rawDisparity
    /// -
    /// 2 * deltaShift
    ///
    ///
    /// なので
    ///
    /// deltaShift
    ///
    /// =
    ///
    /// -A / 2
    ///
    ///
    /// 最終的に
    ///
    /// finalShift
    ///
    /// =
    ///
    /// baseShift
    /// -
    /// A / 2
    ///
    /// とする。
    ///
    /// </summary>
    private void ApplyFinalShift()
    {
        if (imageController == null)
        {
            return;
        }


        // =====================================================
        // Additional disparity
        //
        // ->
        //
        // ImageController.shiftPixels
        // =====================================================

        CurrentDynamicShiftPixels =
            -0.5f *
            additionalDisparityPx;


        // =====================================================
        // Static HIT
        //
        // +
        //
        // Dynamic HIT
        // =====================================================

        CurrentFinalShiftPixels =
            baseShiftPixels +
            CurrentDynamicShiftPixels;


        // =====================================================
        // Apply
        // =====================================================

        imageController.shiftPixels =
            CurrentFinalShiftPixels;
    }


    // =========================================================
    // Reset
    // =========================================================

    private void ResetInternalState()
    {
        disparityInitialized =
            false;


        previousDisparityPx =
            0.0f;


        filteredDisparityVelocityPxPerSec =
            0.0f;


        additionalDisparityPx =
            0.0f;


        CurrentBallDisparityPx =
            0.0f;


        CurrentRawDisparityVelocityPxPerSec =
            0.0f;


        CurrentFilteredDisparityVelocityPxPerSec =
            0.0f;


        CurrentDhitVelocityPxPerSec =
            0.0f;


        CurrentDynamicShiftPixels =
            0.0f;


        CurrentFinalShiftPixels =
            baseShiftPixels;
    }
}