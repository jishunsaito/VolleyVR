using System;
using UnityEngine;

[Serializable]
public class DhitSettings
{
    [Tooltip("static or dynamic")]
    public string mode = "dynamic";

    [Tooltip("d(additional disparity)/dt = gain * d(ball disparity)/dt")]
    public float gain = 0.30f;

    [Tooltip("Maximum positive additional disparity [px]")]
    public float maxPositiveAdditionalDisparityPx = 20.0f;

    [Tooltip("Maximum magnitude of negative additional disparity [px]")]
    public float maxNegativeAdditionalDisparityPx = 20.0f;

    [Tooltip("Maximum velocity of additional disparity [px/s]")]
    public float maxAdditionalDisparityVelocityPxPerSec = 10.0f;

    [Tooltip("Ignore filtered disparity velocities smaller than this [px/s]")]
    public float velocityDeadZonePxPerSec = 0.5f;

    [Tooltip("Time constant of the first-order low-pass filter [s]. 0 = no filtering")]
    public float filterTimeConstantSec = 0.05f;
}

public class DHITController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BallDisparityCalculator ballDisparityCalculator;
    [SerializeField] private ImageController imageController;

    [Header("Default DHIT Settings")]
    [Tooltip("Used until BeginTrial() supplies trial-specific settings.")]
    [SerializeField] private DhitSettings settings = new DhitSettings();

    [Header("Base Static HIT")]
    [Tooltip(
        "Static / Base Shift [px]。\n" +
        "Static時はこの値がそのままImageControllerへ出力され、" +
        "Dynamic時はこの値にDynamic Shiftを加算する。\n" +
        "InspectorまたはUIControllerから変更可能。"
    )]
    [SerializeField]
    private float baseShiftPixels = 0.0f;

    [Tooltip(
        "ONの場合のみ、Awake時にImageController.shiftPixelsを" +
        "Base Shiftの初期値として取り込む。\n" +
        "通常はOFFにしてDHITControllerのBase Shiftを正とする。"
    )]
    [SerializeField]
    private bool initializeBaseShiftFromImageControllerOnAwake = false;

    private bool disparityInitialized = false;
    private float previousDisparityPx = 0.0f;
    private float filteredDisparityVelocityPxPerSec = 0.0f;
    private float additionalDisparityPx = 0.0f;

    public float CurrentBallDisparityPx { get; private set; }
    public float CurrentRawDisparityVelocityPxPerSec { get; private set; }
    public float CurrentFilteredDisparityVelocityPxPerSec { get; private set; }
    public float CurrentDhitVelocityPxPerSec { get; private set; }
    public float CurrentAdditionalDisparityPx => additionalDisparityPx;
    public float CurrentDynamicShiftPixels { get; private set; }
    public float CurrentFinalShiftPixels { get; private set; }
    public float BaseShiftPixels => baseShiftPixels;

    public bool IsDynamic =>
        settings != null &&
        string.Equals(settings.mode, "dynamic", StringComparison.OrdinalIgnoreCase);

    private void Awake()
    {
        EnsureSettings();

        if (
            initializeBaseShiftFromImageControllerOnAwake &&
            imageController != null
        )
        {
            baseShiftPixels =
                imageController.shiftPixels;
        }

        ResetInternalState();
        ApplyFinalShift();
    }

    private void LateUpdate()
    {
        if (settings == null || imageController == null)
            return;

        if (!IsDynamic)
        {
            additionalDisparityPx = 0.0f;

            CurrentRawDisparityVelocityPxPerSec = 0.0f;
            CurrentFilteredDisparityVelocityPxPerSec = 0.0f;
            CurrentDhitVelocityPxPerSec = 0.0f;

            disparityInitialized = false;

            ApplyFinalShift();
            return;
        }

        float dt = Time.deltaTime;

        if (dt <= 0.0f)
            return;

        StepDHIT(dt);
        ApplyFinalShift();
    }

    public void BeginTrial()
    {
        BeginTrial(
            null
        );
    }

    public void BeginTrial(DhitSettings newSettings)
    {
        if (newSettings != null)
        {
            settings =
                CopySettings(
                    newSettings
                );
        }

        EnsureSettings();

        /*
         * Base ShiftはUIまたはDHITController Inspectorの値を正とする。
         * ImageController.shiftPixelsはFinal Shiftなので、
         * Trial開始時にここからBaseへ逆コピーしない。
         */
        ResetInternalState();
        ApplyFinalShift();
    }

    public void EndTrial()
    {
        ResetInternalState();
        ApplyFinalShift();
    }

    public void SetBaseShiftPixels(float shiftPixels)
    {
        baseShiftPixels =
            shiftPixels;

        ApplyFinalShift();
    }

    public void CaptureCurrentShiftAsBase()
    {
        if (imageController == null)
            return;

        baseShiftPixels = imageController.shiftPixels;
        ApplyFinalShift();
    }

    public void SetDynamicEnabled(bool enabled)
    {
        EnsureSettings();

        settings.mode =
            enabled
                ? "dynamic"
                : "static";

        ResetInternalState();
        ApplyFinalShift();
    }

    /// <summary>
    /// 現在のDHIT設定をJSON保存用にコピーして返す。
    /// 参照をそのまま返さないため、保存側で値を変更してもRuntime設定に影響しない。
    /// </summary>
    public DhitSettings CreateSettingsSnapshot()
    {
        EnsureSettings();

        return CopySettings(
            settings
        );
    }

    /// <summary>
    /// Rally JSONなどからDHIT状態を完全復元する。
    /// </summary>
    public void ApplySavedState(
        float savedBaseShiftPixels,
        bool dynamicEnabled,
        DhitSettings savedSettings
    )
    {
        if (savedSettings != null)
        {
            settings =
                CopySettings(
                    savedSettings
                );
        }
        else
        {
            EnsureSettings();
        }

        settings.mode =
            dynamicEnabled
                ? "dynamic"
                : "static";

        baseShiftPixels =
            savedBaseShiftPixels;

        ResetInternalState();
        ApplyFinalShift();
    }

    public void NotifyTrajectoryDiscontinuity()
    {
        disparityInitialized = false;
        previousDisparityPx = 0.0f;
        filteredDisparityVelocityPxPerSec = 0.0f;

        CurrentRawDisparityVelocityPxPerSec = 0.0f;
        CurrentFilteredDisparityVelocityPxPerSec = 0.0f;
        CurrentDhitVelocityPxPerSec = 0.0f;
    }

    private void StepDHIT(float dt)
    {
        if (!TryGetRawBallDisparityPx(out float disparityPx))
        {
            disparityInitialized = false;

            CurrentRawDisparityVelocityPxPerSec = 0.0f;
            CurrentFilteredDisparityVelocityPxPerSec = 0.0f;
            CurrentDhitVelocityPxPerSec = 0.0f;

            return;
        }

        CurrentBallDisparityPx = disparityPx;

        if (!disparityInitialized)
        {
            previousDisparityPx = disparityPx;
            filteredDisparityVelocityPxPerSec = 0.0f;
            disparityInitialized = true;

            CurrentRawDisparityVelocityPxPerSec = 0.0f;
            CurrentFilteredDisparityVelocityPxPerSec = 0.0f;
            CurrentDhitVelocityPxPerSec = 0.0f;

            return;
        }

        float rawDisparityVelocity =
            (disparityPx - previousDisparityPx) / dt;

        previousDisparityPx = disparityPx;
        CurrentRawDisparityVelocityPxPerSec = rawDisparityVelocity;

        float tau = Mathf.Max(0.0f, settings.filterTimeConstantSec);

        if (tau <= 0.0f)
        {
            filteredDisparityVelocityPxPerSec = rawDisparityVelocity;
        }
        else
        {
            float alpha = 1.0f - Mathf.Exp(-dt / tau);

            filteredDisparityVelocityPxPerSec =
                Mathf.Lerp(
                    filteredDisparityVelocityPxPerSec,
                    rawDisparityVelocity,
                    alpha
                );
        }

        CurrentFilteredDisparityVelocityPxPerSec =
            filteredDisparityVelocityPxPerSec;

        float velocityForControl =
            filteredDisparityVelocityPxPerSec;

        float deadZone =
            Mathf.Max(0.0f, settings.velocityDeadZonePxPerSec);

        if (Mathf.Abs(velocityForControl) < deadZone)
            velocityForControl = 0.0f;

        float commandedDhitVelocity =
            settings.gain * velocityForControl;

        float maxVelocity =
            Mathf.Max(
                0.0f,
                settings.maxAdditionalDisparityVelocityPxPerSec
            );

        commandedDhitVelocity =
            Mathf.Clamp(
                commandedDhitVelocity,
                -maxVelocity,
                +maxVelocity
            );

        float before = additionalDisparityPx;

        additionalDisparityPx +=
            commandedDhitVelocity * dt;

        float maxPositive =
            Mathf.Max(
                0.0f,
                settings.maxPositiveAdditionalDisparityPx
            );

        float maxNegative =
            Mathf.Max(
                0.0f,
                settings.maxNegativeAdditionalDisparityPx
            );

        additionalDisparityPx =
            Mathf.Clamp(
                additionalDisparityPx,
                -maxNegative,
                +maxPositive
            );

        CurrentDhitVelocityPxPerSec =
            (additionalDisparityPx - before) / dt;
    }

    private bool TryGetRawBallDisparityPx(out float disparityPx)
    {
        disparityPx = 0.0f;

        if (ballDisparityCalculator == null)
            return false;

        if (!ballDisparityCalculator.HasBall)
            return false;

        // IMPORTANT:
        // Use the raw disparity, not ShiftedDisparityPixels.
        // Otherwise the controller's own output feeds back into its input.
        disparityPx = ballDisparityCalculator.DisparityPixels;

        if (float.IsNaN(disparityPx) ||
            float.IsInfinity(disparityPx))
        {
            return false;
        }

        return true;
    }

    private void ApplyFinalShift()
    {
        if (imageController == null)
            return;

        // BallDisparityCalculator uses:
        // shiftedDisparity = rawDisparity - 2 * shiftPixels
        //
        // Therefore, adding +A [px] disparity requires:
        // deltaShiftPixels = -A / 2
        CurrentDynamicShiftPixels =
            -0.5f * additionalDisparityPx;

        CurrentFinalShiftPixels =
            baseShiftPixels + CurrentDynamicShiftPixels;

        imageController.shiftPixels =
            CurrentFinalShiftPixels;
    }

    private void EnsureSettings()
    {
        if (settings == null)
        {
            settings =
                new DhitSettings();
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
            mode =
                source.mode,

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

    private void ResetInternalState()
    {
        disparityInitialized = false;
        previousDisparityPx = 0.0f;
        filteredDisparityVelocityPxPerSec = 0.0f;
        additionalDisparityPx = 0.0f;

        CurrentBallDisparityPx = 0.0f;
        CurrentRawDisparityVelocityPxPerSec = 0.0f;
        CurrentFilteredDisparityVelocityPxPerSec = 0.0f;
        CurrentDhitVelocityPxPerSec = 0.0f;
        CurrentDynamicShiftPixels = 0.0f;
        CurrentFinalShiftPixels = baseShiftPixels;
    }
}
