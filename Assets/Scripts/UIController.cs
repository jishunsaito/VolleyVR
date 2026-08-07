using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    // =========================================================
    // Controller
    // =========================================================

    [Header("Controller")]

    [SerializeField]
    private ImageController imageController;


    // =========================================================
    // Ball Disparity
    // =========================================================

    [Header("Ball Disparity")]

    [SerializeField]
    private BallDisparityCalculator disparityCalculator;


    // =========================================================
    // Output
    // =========================================================

    [Header("Output")]

    [SerializeField]
    private TMP_Text outputText;


    // =========================================================
    // Shift
    // =========================================================

    [Header("Shift UI")]

    [SerializeField]
    private Slider shiftSlider;

    [SerializeField]
    private TMP_InputField shiftInputField;


    // =========================================================
    // Focal Length
    // =========================================================

    [Header("Focal Length UI")]

    [SerializeField]
    private Slider focalLengthSlider;

    [SerializeField]
    private TMP_InputField focalLengthInputField;


    // =========================================================
    // Baseline
    // =========================================================

    [Header("Baseline UI")]

    [SerializeField]
    private Slider baselineSlider;

    [SerializeField]
    private TMP_InputField baselineInputField;


    // =========================================================
    // Position X
    // =========================================================

    [Header("Position X UI")]

    [SerializeField]
    private Slider positionXSlider;

    [SerializeField]
    private TMP_InputField positionXInputField;


    // =========================================================
    // Position Y
    // =========================================================

    [Header("Position Y UI")]

    [SerializeField]
    private Slider positionYSlider;

    [SerializeField]
    private TMP_InputField positionYInputField;


    // =========================================================
    // Position Z
    // =========================================================

    [Header("Position Z UI")]

    [SerializeField]
    private Slider positionZSlider;

    [SerializeField]
    private TMP_InputField positionZInputField;


    // =========================================================
    // Pitch
    // =========================================================

    [Header("Pitch UI")]

    [SerializeField]
    private Slider pitchSlider;

    [SerializeField]
    private TMP_InputField pitchInputField;


    // =========================================================
    // Slider Range
    //
    // SerializeFieldを付けていないので
    // Inspectorには表示されない。
    // =========================================================

    private int shiftMin = -500;
    private int shiftMax = 500;

    private float focalLengthMin = 1.0f;
    private float focalLengthMax = 300.0f;

    private float baselineMin = 0.0f;
    private float baselineMax = 5000.0f;

    private float positionXMin = -100.0f;
    private float positionXMax = 100.0f;

    private float positionYMin = -200.0f;
    private float positionYMax = 200.0f;

    private float positionZMin = -300.0f;
    private float positionZMax = 100.0f;

    private float pitchMin = -60.0f;
    private float pitchMax = 60.0f;


    // =========================================================
    // Internal
    // =========================================================

    private bool isSynchronizing;


    // =========================================================
    // Unity
    // =========================================================

    private void Start()
    {
        if (imageController == null)
        {
            Debug.LogError(
                "UIControllerにImageControllerが設定されていません。",
                this
            );

            enabled = false;
            return;
        }


        BindShift();

        BindFocalLength();

        BindBaseline();

        BindPositionX();

        BindPositionY();

        BindPositionZ();

        BindPitch();


        UpdateOutputText();
    }


    private void Update()
    {
        if (imageController == null)
        {
            return;
        }


        UpdateOutputText();
    }


    // =========================================================
    // Shift
    // =========================================================

    private void BindShift()
    {
        BindParameter(
            shiftSlider,
            shiftInputField,

            () =>
                imageController.shiftPixels,

            value =>
                imageController.shiftPixels =
                    Mathf.RoundToInt(value),

            shiftMin,
            shiftMax,

            "F0",

            true
        );
    }


    // =========================================================
    // Focal Length
    // =========================================================

    private void BindFocalLength()
    {
        BindParameter(
            focalLengthSlider,
            focalLengthInputField,

            () =>
                imageController.focalLength,

            value =>
                imageController.focalLength =
                    value,

            focalLengthMin,
            focalLengthMax,

            "F1"
        );
    }


    // =========================================================
    // Baseline
    // =========================================================

    private void BindBaseline()
    {
        BindParameter(
            baselineSlider,
            baselineInputField,

            () =>
                imageController.baseline,

            value =>
                imageController.baseline =
                    value,

            baselineMin,
            baselineMax,

            "F1"
        );
    }


    // =========================================================
    // Position X
    // =========================================================

    private void BindPositionX()
    {
        BindParameter(
            positionXSlider,
            positionXInputField,

            () =>
                imageController
                    .stereoCameraPosition.x,

            value =>
            {
                Vector3 position =
                    imageController
                        .stereoCameraPosition;

                position.x = value;

                imageController
                    .stereoCameraPosition =
                        position;
            },

            positionXMin,
            positionXMax,

            "F3"
        );
    }


    // =========================================================
    // Position Y
    // =========================================================

    private void BindPositionY()
    {
        BindParameter(
            positionYSlider,
            positionYInputField,

            () =>
                imageController
                    .stereoCameraPosition.y,

            value =>
            {
                Vector3 position =
                    imageController
                        .stereoCameraPosition;

                position.y = value;

                imageController
                    .stereoCameraPosition =
                        position;
            },

            positionYMin,
            positionYMax,

            "F3"
        );
    }


    // =========================================================
    // Position Z
    // =========================================================

    private void BindPositionZ()
    {
        BindParameter(
            positionZSlider,
            positionZInputField,

            () =>
                imageController
                    .stereoCameraPosition.z,

            value =>
            {
                Vector3 position =
                    imageController
                        .stereoCameraPosition;

                position.z = value;

                imageController
                    .stereoCameraPosition =
                        position;
            },

            positionZMin,
            positionZMax,

            "F3"
        );
    }


    // =========================================================
    // Pitch
    // =========================================================

    private void BindPitch()
    {
        BindParameter(
            pitchSlider,
            pitchInputField,

            () =>
                imageController
                    .stereoCameraRotationX,

            value =>
                imageController
                    .stereoCameraRotationX =
                        value,

            pitchMin,
            pitchMax,

            "F1"
        );
    }


    // =========================================================
    // Parameter Binding
    // =========================================================

    private void BindParameter(
        Slider slider,
        TMP_InputField inputField,
        Func<float> getter,
        Action<float> setter,
        float defaultMin,
        float defaultMax,
        string format,
        bool wholeNumbers = false
    )
    {
        if (slider == null ||
            inputField == null)
        {
            Debug.LogWarning(
                "UIControllerに未設定のSliderまたはInputFieldがあります。",
                this
            );

            return;
        }


        // =====================================================
        // Slider / InputField設定
        // =====================================================

        slider.wholeNumbers =
            wholeNumbers;


        inputField.contentType =
            wholeNumbers

                ? TMP_InputField.ContentType
                    .IntegerNumber

                : TMP_InputField.ContentType
                    .DecimalNumber;


        // =====================================================
        // ImageControllerから初期値取得
        // =====================================================

        float initialValue =
            getter();


        if (wholeNumbers)
        {
            initialValue =
                Mathf.Round(
                    initialValue
                );
        }


        // =====================================================
        // Slider Range設定
        // =====================================================

        SetInitialSliderRange(
            slider,
            defaultMin,
            defaultMax,
            initialValue,
            wholeNumbers
        );


        // =====================================================
        // UIへ初期値反映
        // =====================================================

        SynchronizeControls(
            slider,
            inputField,
            initialValue,
            format
        );


        // =====================================================
        // Slider操作
        // =====================================================

        slider.onValueChanged.AddListener(
            value =>
            {
                if (isSynchronizing)
                {
                    return;
                }


                float newValue =
                    value;


                if (wholeNumbers)
                {
                    newValue =
                        Mathf.Round(
                            newValue
                        );
                }


                setter(
                    newValue
                );


                SynchronizeControls(
                    slider,
                    inputField,
                    newValue,
                    format
                );


                UpdateOutputText();
            }
        );


        // =====================================================
        // InputField操作
        // =====================================================

        inputField.onEndEdit.AddListener(
            text =>
            {
                if (isSynchronizing)
                {
                    return;
                }


                if (!TryParseFloat(
                    text,
                    out float inputValue
                ))
                {
                    float currentValue =
                        getter();


                    if (wholeNumbers)
                    {
                        currentValue =
                            Mathf.Round(
                                currentValue
                            );
                    }


                    SynchronizeControls(
                        slider,
                        inputField,
                        currentValue,
                        format
                    );


                    return;
                }


                if (wholeNumbers)
                {
                    inputValue =
                        Mathf.Round(
                            inputValue
                        );
                }


                // =============================================
                // Input値が範囲外なら
                // Slider範囲を拡張
                // =============================================

                ExpandSliderRange(
                    slider,
                    inputValue,
                    wholeNumbers
                );


                setter(
                    inputValue
                );


                SynchronizeControls(
                    slider,
                    inputField,
                    inputValue,
                    format
                );


                UpdateOutputText();
            }
        );
    }


    // =========================================================
    // ParameterManager用
    //
    // Reset / Load後に呼ぶ
    // =========================================================

    public void RefreshFromController()
    {
        if (imageController == null)
        {
            return;
        }


        RefreshParameter(
            shiftSlider,
            shiftInputField,
            imageController.shiftPixels,
            "F0",
            true
        );


        RefreshParameter(
            focalLengthSlider,
            focalLengthInputField,
            imageController.focalLength,
            "F1"
        );


        RefreshParameter(
            baselineSlider,
            baselineInputField,
            imageController.baseline,
            "F1"
        );


        RefreshParameter(
            positionXSlider,
            positionXInputField,
            imageController
                .stereoCameraPosition.x,
            "F3"
        );


        RefreshParameter(
            positionYSlider,
            positionYInputField,
            imageController
                .stereoCameraPosition.y,
            "F3"
        );


        RefreshParameter(
            positionZSlider,
            positionZInputField,
            imageController
                .stereoCameraPosition.z,
            "F3"
        );


        RefreshParameter(
            pitchSlider,
            pitchInputField,
            imageController
                .stereoCameraRotationX,
            "F1"
        );


        UpdateOutputText();
    }


    // =========================================================
    // Refresh Parameter
    // =========================================================

    private void RefreshParameter(
        Slider slider,
        TMP_InputField inputField,
        float value,
        string format,
        bool wholeNumbers = false
    )
    {
        if (slider == null ||
            inputField == null)
        {
            return;
        }


        if (wholeNumbers)
        {
            value =
                Mathf.Round(
                    value
                );
        }


        ExpandSliderRange(
            slider,
            value,
            wholeNumbers
        );


        SynchronizeControls(
            slider,
            inputField,
            value,
            format
        );
    }


    // =========================================================
    // Initial Slider Range
    // =========================================================

    private static void SetInitialSliderRange(
        Slider slider,
        float defaultMin,
        float defaultMax,
        float initialValue,
        bool wholeNumbers
    )
    {
        float minValue =
            Mathf.Min(
                defaultMin,
                defaultMax
            );


        float maxValue =
            Mathf.Max(
                defaultMin,
                defaultMax
            );


        if (initialValue < minValue)
        {
            minValue =
                initialValue;
        }


        if (initialValue > maxValue)
        {
            maxValue =
                initialValue;
        }


        if (wholeNumbers)
        {
            minValue =
                Mathf.Floor(
                    minValue
                );

            maxValue =
                Mathf.Ceil(
                    maxValue
                );
        }


        if (Mathf.Approximately(
            minValue,
            maxValue
        ))
        {
            minValue -=
                1.0f;

            maxValue +=
                1.0f;
        }


        slider.minValue =
            minValue;

        slider.maxValue =
            maxValue;
    }


    // =========================================================
    // Expand Slider Range
    // =========================================================

    private static void ExpandSliderRange(
        Slider slider,
        float value,
        bool wholeNumbers
    )
    {
        float minValue =
            slider.minValue;

        float maxValue =
            slider.maxValue;


        float currentRange =
            Mathf.Max(
                maxValue -
                minValue,
                1.0f
            );


        float padding =
            Mathf.Max(
                currentRange *
                0.1f,
                1.0f
            );


        if (value < minValue)
        {
            minValue =
                value -
                padding;
        }


        if (value > maxValue)
        {
            maxValue =
                value +
                padding;
        }


        if (wholeNumbers)
        {
            minValue =
                Mathf.Floor(
                    minValue
                );

            maxValue =
                Mathf.Ceil(
                    maxValue
                );
        }


        slider.minValue =
            minValue;

        slider.maxValue =
            maxValue;
    }


    // =========================================================
    // Slider / InputField Sync
    // =========================================================

    private void SynchronizeControls(
        Slider slider,
        TMP_InputField inputField,
        float value,
        string format
    )
    {
        isSynchronizing =
            true;


        try
        {
            slider.SetValueWithoutNotify(
                value
            );


            inputField.SetTextWithoutNotify(
                value.ToString(
                    format,
                    CultureInfo.InvariantCulture
                )
            );
        }
        finally
        {
            isSynchronizing =
                false;
        }
    }


    // =========================================================
    // String -> Float
    // =========================================================

    private static bool TryParseFloat(
        string text,
        out float value
    )
    {
        if (string.IsNullOrWhiteSpace(
            text
        ))
        {
            value =
                0.0f;

            return false;
        }


        if (float.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out value
        ))
        {
            return true;
        }


        string normalizedText =
            text
                .Trim()
                .Replace(
                    ',',
                    '.'
                );


        return float.TryParse(
            normalizedText,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value
        );
    }


    // =========================================================
    // Output
    // =========================================================

    private void UpdateOutputText()
    {
        if (outputText == null ||
            imageController == null)
        {
            return;
        }


        Vector3 cameraPosition =
            imageController
                .stereoCameraPosition;


        // =====================================================
        // Camera / Image Parameters
        // =====================================================

        string text =

            "<b>Image Parameters</b>\n" +

            $"Shift Pixels : " +
            $"{imageController.shiftPixels} px\n\n" +


            "<b>Stereo Camera Parameters</b>\n" +

            $"Baseline : " +
            $"{imageController.baseline:F1} mm\n" +

            $"Focal Length : " +
            $"{imageController.focalLength:F1} mm\n\n" +


            "<b>Stereo Camera Transform</b>\n" +

            $"Position X : " +
            $"{cameraPosition.x:F3}\n" +

            $"Position Y : " +
            $"{cameraPosition.y:F3}\n" +

            $"Position Z : " +
            $"{cameraPosition.z:F3}\n" +

            $"Pitch : " +
            $"{imageController.stereoCameraRotationX:F1}" +
            "\u00B0\n\n";


        // =====================================================
        // Ball Disparity
        // =====================================================

        text +=
            "<b>Ball Disparity</b>\n";


        if (disparityCalculator == null)
        {
            text +=
                "Calculator : Not Assigned";
        }
        else if (!disparityCalculator.HasBall)
        {
            text +=
                "Ball : Not Active";
        }
        else
        {
            Vector3 ballPosition =
                disparityCalculator
                    .BallWorldPosition;


            text +=

                $"Ball X : " +
                $"{ballPosition.x:F3}\n" +

                $"Ball Y : " +
                $"{ballPosition.y:F3}\n" +

                $"Ball Z : " +
                $"{ballPosition.z:F3}\n\n" +


                $"Depth Z : " +
                $"{disparityCalculator.DepthMeters:F3} m\n" +


                $"Sensor Disparity : " +
                $"{disparityCalculator.DisparityMm:F3} mm\n" +


                $"Image Disparity : " +
                $"{disparityCalculator.DisparityPixels:F2} px\n" +


                $"After Shift : " +
                $"{disparityCalculator.ShiftedDisparityPixels:F2} px";
        }


        outputText.text =
            text;
    }
}