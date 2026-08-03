using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    // =========================================================
    // Controller / Output
    // =========================================================

    [Header("Controller")]
    [SerializeField]
    private ImageController imageController;

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
    // Sliderの初期範囲
    // Inspectorから変更可能
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
    // 内部処理用
    // =========================================================

    private bool isSynchronizing;


    // =========================================================
    // Unityイベント
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
            () => imageController.shiftPixels,
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
            () => imageController.focalLength,
            value => imageController.focalLength = value,
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
            () => imageController.baseline,
            value => imageController.baseline = value,
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
            () => imageController.stereoCameraPosition.x,
            value =>
            {
                Vector3 position =
                    imageController.stereoCameraPosition;

                position.x = value;

                imageController.stereoCameraPosition =
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
            () => imageController.stereoCameraPosition.y,
            value =>
            {
                Vector3 position =
                    imageController.stereoCameraPosition;

                position.y = value;

                imageController.stereoCameraPosition =
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
            () => imageController.stereoCameraPosition.z,
            value =>
            {
                Vector3 position =
                    imageController.stereoCameraPosition;

                position.z = value;

                imageController.stereoCameraPosition =
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
            () => imageController.stereoCameraRotationX,
            value =>
                imageController.stereoCameraRotationX = value,
            pitchMin,
            pitchMax,
            "F1"
        );
    }


    // =========================================================
    // SliderとInputFieldを接続する共通処理
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
        if (slider == null || inputField == null)
        {
            Debug.LogWarning(
                "UIControllerに未設定のSliderまたはInputFieldがあります。",
                this
            );

            return;
        }

        slider.wholeNumbers = wholeNumbers;

        inputField.contentType =
            wholeNumbers
                ? TMP_InputField.ContentType.IntegerNumber
                : TMP_InputField.ContentType.DecimalNumber;

        float initialValue = getter();

        if (wholeNumbers)
        {
            initialValue = Mathf.Round(initialValue);
        }

        // ImageControllerの初期値が範囲外の場合は、
        // Controller側を変更せずSlider範囲を拡張する。
        SetInitialSliderRange(
            slider,
            defaultMin,
            defaultMax,
            initialValue,
            wholeNumbers
        );

        SynchronizeControls(
            slider,
            inputField,
            initialValue,
            format
        );

        slider.onValueChanged.AddListener(value =>
        {
            if (isSynchronizing)
            {
                return;
            }

            float newValue = value;

            if (wholeNumbers)
            {
                newValue = Mathf.Round(newValue);
            }

            setter(newValue);

            SynchronizeControls(
                slider,
                inputField,
                newValue,
                format
            );

            UpdateOutputText();
        });

        inputField.onEndEdit.AddListener(text =>
        {
            if (isSynchronizing)
            {
                return;
            }

            if (!TryParseFloat(text, out float inputValue))
            {
                float currentValue = getter();

                if (wholeNumbers)
                {
                    currentValue = Mathf.Round(currentValue);
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
                inputValue = Mathf.Round(inputValue);
            }

            // InputFieldから現在の範囲外の値を入力した場合は、
            // Slider範囲を自動的に拡張する。
            ExpandSliderRange(
                slider,
                inputValue,
                wholeNumbers
            );

            setter(inputValue);

            SynchronizeControls(
                slider,
                inputField,
                inputValue,
                format
            );

            UpdateOutputText();
        });
    }


    // =========================================================
    // Reset・Load後にImageControllerの値をUIへ再反映
    // ParameterPresetManagerから呼び出す
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
            imageController.stereoCameraPosition.x,
            "F3"
        );

        RefreshParameter(
            positionYSlider,
            positionYInputField,
            imageController.stereoCameraPosition.y,
            "F3"
        );

        RefreshParameter(
            positionZSlider,
            positionZInputField,
            imageController.stereoCameraPosition.z,
            "F3"
        );

        RefreshParameter(
            pitchSlider,
            pitchInputField,
            imageController.stereoCameraRotationX,
            "F1"
        );

        UpdateOutputText();
    }


    private void RefreshParameter(
        Slider slider,
        TMP_InputField inputField,
        float value,
        string format,
        bool wholeNumbers = false
    )
    {
        if (slider == null || inputField == null)
        {
            return;
        }

        if (wholeNumbers)
        {
            value = Mathf.Round(value);
        }

        // 読み込んだ値が現在の範囲外なら範囲を拡張する。
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
    // Sliderの初期範囲設定
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
            Mathf.Min(defaultMin, defaultMax);

        float maxValue =
            Mathf.Max(defaultMin, defaultMax);

        if (initialValue < minValue)
        {
            minValue = initialValue;
        }

        if (initialValue > maxValue)
        {
            maxValue = initialValue;
        }

        if (wholeNumbers)
        {
            minValue = Mathf.Floor(minValue);
            maxValue = Mathf.Ceil(maxValue);
        }

        if (Mathf.Approximately(minValue, maxValue))
        {
            minValue -= 1.0f;
            maxValue += 1.0f;
        }

        slider.minValue = minValue;
        slider.maxValue = maxValue;
    }


    // =========================================================
    // InputFieldやLoad値に応じてSlider範囲を拡張
    // =========================================================

    private static void ExpandSliderRange(
        Slider slider,
        float value,
        bool wholeNumbers
    )
    {
        float minValue = slider.minValue;
        float maxValue = slider.maxValue;

        float currentRange =
            Mathf.Max(maxValue - minValue, 1.0f);

        float padding =
            Mathf.Max(currentRange * 0.1f, 1.0f);

        if (value < minValue)
        {
            minValue = value - padding;
        }

        if (value > maxValue)
        {
            maxValue = value + padding;
        }

        if (wholeNumbers)
        {
            minValue = Mathf.Floor(minValue);
            maxValue = Mathf.Ceil(maxValue);
        }

        slider.minValue = minValue;
        slider.maxValue = maxValue;
    }


    // =========================================================
    // SliderとInputFieldを同期
    // =========================================================

    private void SynchronizeControls(
        Slider slider,
        TMP_InputField inputField,
        float value,
        string format
    )
    {
        isSynchronizing = true;

        try
        {
            slider.SetValueWithoutNotify(value);

            inputField.SetTextWithoutNotify(
                value.ToString(
                    format,
                    CultureInfo.InvariantCulture
                )
            );
        }
        finally
        {
            isSynchronizing = false;
        }
    }


    // =========================================================
    // InputFieldの文字列をfloatへ変換
    // =========================================================

    private static bool TryParseFloat(
        string text,
        out float value
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0.0f;
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
            text.Trim().Replace(',', '.');

        return float.TryParse(
            normalizedText,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value
        );
    }


    // =========================================================
    // Output表示
    // =========================================================

    private void UpdateOutputText()
    {
        if (outputText == null || imageController == null)
        {
            return;
        }

        Vector3 position =
            imageController.stereoCameraPosition;

        outputText.text =
            "<b>Image Parameters</b>\n" +
            $"Shift Pixels : {imageController.shiftPixels} px\n\n" +

            "<b>Stereo Camera Parameters</b>\n" +
            $"Baseline : {imageController.baseline:F1} mm\n" +
            $"Focal Length : {imageController.focalLength:F1} mm\n\n" +

            "<b>Stereo Camera Transform</b>\n" +
            $"Position X : {position.x:F3}\n" +
            $"Position Y : {position.y:F3}\n" +
            $"Position Z : {position.z:F3}\n" +
            $"Pitch : {imageController.stereoCameraRotationX:F1}\u00B0";
    }
}