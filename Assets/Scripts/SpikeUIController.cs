using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spike設定専用UI。
///
/// 役割:
/// - Spike Course (Straight / Cross) の選択
/// - Spike Speed の変更
/// - Spike用Fade Delayの変更
/// - Left / Right のSpike TargetへXYZ Offsetを付与
/// - Spike設定のJSON Save / Load
/// - Main Panel <-> Spike Panel の切り替え
///
/// 実際のプレー設定値はRallyControllerが保持し、
/// このクラスはUIからRallyControllerを操作するだけにする。
/// </summary>
public class SpikeUIController : MonoBehaviour
{
    // ============================================================
    // Controller
    // ============================================================

    [Header("Controller")]

    [SerializeField]
    private RallyController rallyController;


    // ============================================================
    // Panel Navigation
    // ============================================================

    [Header("Panel Navigation")]

    [Tooltip("プレー選択などを置いたメインPanel")]
    [SerializeField]
    private GameObject mainPanel;

    [Tooltip("Spike設定Panel")]
    [SerializeField]
    private GameObject spikePanel;

    [Tooltip("Main画面にあるSpike Button。未設定でも可。")]
    [SerializeField]
    private Button openSpikeButton;

    [Tooltip("Spike画面のBack Button")]
    [SerializeField]
    private Button backButton;

    [Tooltip("開始時にMain Panelを表示しSpike Panelを隠す")]
    [SerializeField]
    private bool showMainPanelOnStart =
        false;


    // ============================================================
    // Spike Selection
    // ============================================================

    [Header("Spike Selection")]

    [Tooltip("Spike Course: Straight / Cross")]
    [SerializeField]
    private TMP_Dropdown spikeCourseDropdown;


    // ============================================================
    // Spike Parameters
    // ============================================================

    [Header("Spike Parameters")]

    [Tooltip("スパイク打球直後の速度 [km/h]")]
    [SerializeField]
    private TMP_InputField speedInputField;

    [Tooltip("Spike Phase開始からFade Out開始までの時間 [s]")]
    [SerializeField]
    private TMP_InputField fadeInputField;


    // ============================================================
    // Spike Target Offset
    // ============================================================

    [Header("Spike Target Offset")]

    [Tooltip("Offsetを適用するSpike Target: Left / Right")]
    [SerializeField]
    private TMP_Dropdown offsetTargetDropdown;

    [SerializeField]
    private TMP_InputField offsetXInputField;

    [SerializeField]
    private TMP_InputField offsetYInputField;

    [SerializeField]
    private TMP_InputField offsetZInputField;


    // ============================================================
    // JSON
    // ============================================================

    [Header("JSON")]

    [SerializeField]
    private Button saveButton;

    [SerializeField]
    private Button loadButton;

    [Tooltip("Build時に使用する既定JSONファイル名。EditorではSave/Loadダイアログを表示する。")]
    [SerializeField]
    private string defaultJsonFileName =
        "spike_settings.json";

    private string lastDirectory =
        string.Empty;


    // ============================================================
    // Runtime
    // ============================================================

    private int currentOffsetTargetIndex =
        0;

    private bool isSynchronizing =
        false;


    // ============================================================
    // JSON Data
    // ============================================================

    [Serializable]
    private class SpikeSettingsData
    {
        public int version = 1;

        public int spikeCourseIndex;
        public float speedKmh;
        public float fadeDelaySeconds;

        public int offsetTargetIndex;

        public Vector3 leftTargetOffset;
        public Vector3 rightTargetOffset;
    }


    // ============================================================
    // Unity
    // ============================================================

    private void Start()
    {
        if (rallyController == null)
        {
            Debug.LogError(
                "[SpikeUIController] RallyControllerが設定されていません。",
                this
            );

            enabled =
                false;

            return;
        }

        ConfigureDropdowns();
        ConfigureInputFields();
        BindUI();

        currentOffsetTargetIndex =
            offsetTargetDropdown != null
                ? Mathf.Clamp(
                    offsetTargetDropdown.value,
                    0,
                    1
                )
                : 0;

        RefreshFromRallyController();

        if (showMainPanelOnStart)
        {
            BackToMainPanel();
        }
    }

    private void OnDestroy()
    {
        UnbindUI();
    }


    // ============================================================
    // Panel Navigation
    // ============================================================

    public void OpenSpikePanel()
    {
        if (mainPanel != null)
        {
            mainPanel.SetActive(
                false
            );
        }

        if (spikePanel != null)
        {
            spikePanel.SetActive(
                true
            );
        }

        RefreshFromRallyController();
    }

    public void BackToMainPanel()
    {
        ApplyAllInputFields();

        if (spikePanel != null)
        {
            spikePanel.SetActive(
                false
            );
        }

        if (mainPanel != null)
        {
            mainPanel.SetActive(
                true
            );
        }
    }


    // ============================================================
    // Dropdown Setup
    // ============================================================

    private void ConfigureDropdowns()
    {
        ConfigureDropdown(
            spikeCourseDropdown,
            new List<string>
            {
                "Straight",
                "Cross"
            }
        );

        ConfigureDropdown(
            offsetTargetDropdown,
            new List<string>
            {
                "Left",
                "Right"
            }
        );
    }

    private static void ConfigureDropdown(
        TMP_Dropdown dropdown,
        List<string> options
    )
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(
            options
        );
    }


    // ============================================================
    // Input Setup
    // ============================================================

    private void ConfigureInputFields()
    {
        ConfigureDecimalInput(
            speedInputField
        );

        ConfigureDecimalInput(
            fadeInputField
        );

        ConfigureDecimalInput(
            offsetXInputField
        );

        ConfigureDecimalInput(
            offsetYInputField
        );

        ConfigureDecimalInput(
            offsetZInputField
        );
    }

    private static void ConfigureDecimalInput(
        TMP_InputField inputField
    )
    {
        if (inputField == null)
        {
            return;
        }

        inputField.contentType =
            TMP_InputField.ContentType.DecimalNumber;
    }


    // ============================================================
    // UI Binding
    // ============================================================

    private void BindUI()
    {
        if (openSpikeButton != null)
        {
            openSpikeButton.onClick.AddListener(
                OpenSpikePanel
            );
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(
                BackToMainPanel
            );
        }

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(
                SaveSettingsJson
            );
        }

        if (loadButton != null)
        {
            loadButton.onClick.AddListener(
                LoadSettingsJson
            );
        }

        if (spikeCourseDropdown != null)
        {
            spikeCourseDropdown.onValueChanged.AddListener(
                OnSpikeCourseChanged
            );
        }

        if (offsetTargetDropdown != null)
        {
            offsetTargetDropdown.onValueChanged.AddListener(
                OnOffsetTargetChanged
            );
        }

        BindInputField(
            speedInputField,
            OnSpeedEndEdit
        );

        BindInputField(
            fadeInputField,
            OnFadeEndEdit
        );

        BindInputField(
            offsetXInputField,
            OnOffsetEndEdit
        );

        BindInputField(
            offsetYInputField,
            OnOffsetEndEdit
        );

        BindInputField(
            offsetZInputField,
            OnOffsetEndEdit
        );
    }

    private void UnbindUI()
    {
        if (openSpikeButton != null)
        {
            openSpikeButton.onClick.RemoveListener(
                OpenSpikePanel
            );
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(
                BackToMainPanel
            );
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(
                SaveSettingsJson
            );
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(
                LoadSettingsJson
            );
        }

        if (spikeCourseDropdown != null)
        {
            spikeCourseDropdown.onValueChanged.RemoveListener(
                OnSpikeCourseChanged
            );
        }

        if (offsetTargetDropdown != null)
        {
            offsetTargetDropdown.onValueChanged.RemoveListener(
                OnOffsetTargetChanged
            );
        }

        UnbindInputField(
            speedInputField,
            OnSpeedEndEdit
        );

        UnbindInputField(
            fadeInputField,
            OnFadeEndEdit
        );

        UnbindInputField(
            offsetXInputField,
            OnOffsetEndEdit
        );

        UnbindInputField(
            offsetYInputField,
            OnOffsetEndEdit
        );

        UnbindInputField(
            offsetZInputField,
            OnOffsetEndEdit
        );
    }

    private static void BindInputField(
        TMP_InputField inputField,
        UnityEngine.Events.UnityAction<string> action
    )
    {
        if (inputField == null)
        {
            return;
        }

        inputField.onEndEdit.AddListener(
            action
        );
    }

    private static void UnbindInputField(
        TMP_InputField inputField,
        UnityEngine.Events.UnityAction<string> action
    )
    {
        if (inputField == null)
        {
            return;
        }

        inputField.onEndEdit.RemoveListener(
            action
        );
    }


    // ============================================================
    // Dropdown Events
    // ============================================================

    private void OnSpikeCourseChanged(
        int index
    )
    {
        if (isSynchronizing)
        {
            return;
        }

        rallyController.SetSpikeCourse(
            index
        );
    }

    private void OnOffsetTargetChanged(
        int index
    )
    {
        if (isSynchronizing)
        {
            return;
        }

        currentOffsetTargetIndex =
            Mathf.Clamp(
                index,
                0,
                1
            );

        RefreshOffsetFields();
    }


    // ============================================================
    // Spike Parameter Events
    // ============================================================

    private void OnSpeedEndEdit(
        string text
    )
    {
        if (TryParseFloat(
            text,
            out float value
        ))
        {
            rallyController.SetSpikeSpeed(
                value
            );
        }

        SetInputFieldValue(
            speedInputField,
            rallyController.SpikeSpeedKmh
        );
    }

    private void OnFadeEndEdit(
        string text
    )
    {
        if (TryParseFloat(
            text,
            out float value
        ))
        {
            rallyController.SetSpikeFadeDelay(
                value
            );
        }

        SetInputFieldValue(
            fadeInputField,
            rallyController.SpikeFadeDelay
        );
    }


    // ============================================================
    // Target Offset Events
    // ============================================================

    private void OnOffsetEndEdit(
        string unused
    )
    {
        ApplyOffsetFields();
        RefreshOffsetFields();
    }

    private void ApplyOffsetFields()
    {
        if (
            !TryParseFloat(
                offsetXInputField != null
                    ? offsetXInputField.text
                    : string.Empty,
                out float x
            ) ||
            !TryParseFloat(
                offsetYInputField != null
                    ? offsetYInputField.text
                    : string.Empty,
                out float y
            ) ||
            !TryParseFloat(
                offsetZInputField != null
                    ? offsetZInputField.text
                    : string.Empty,
                out float z
            )
        )
        {
            return;
        }

        rallyController.SetSpikeTargetOffset(
            currentOffsetTargetIndex,
            new Vector3(
                x,
                y,
                z
            )
        );
    }


    // ============================================================
    // Apply All UI Values
    // ============================================================

    private void ApplyAllInputFields()
    {
        if (rallyController == null)
        {
            return;
        }

        if (spikeCourseDropdown != null)
        {
            rallyController.SetSpikeCourse(
                spikeCourseDropdown.value
            );
        }

        ApplyFloatIfValid(
            speedInputField,
            rallyController.SetSpikeSpeed
        );

        ApplyFloatIfValid(
            fadeInputField,
            rallyController.SetSpikeFadeDelay
        );

        ApplyOffsetFields();
    }

    private static void ApplyFloatIfValid(
        TMP_InputField inputField,
        Action<float> setter
    )
    {
        if (
            inputField == null ||
            setter == null
        )
        {
            return;
        }

        if (TryParseFloat(
            inputField.text,
            out float value
        ))
        {
            setter(
                value
            );
        }
    }


    // ============================================================
    // UI Refresh
    // ============================================================

    public void RefreshFromRallyController()
    {
        if (rallyController == null)
        {
            return;
        }

        isSynchronizing =
            true;

        try
        {
            if (spikeCourseDropdown != null)
            {
                spikeCourseDropdown.SetValueWithoutNotify(
                    (int)rallyController.CurrentSpikeCourse
                );
            }

            if (offsetTargetDropdown != null)
            {
                offsetTargetDropdown.SetValueWithoutNotify(
                    currentOffsetTargetIndex
                );
            }

            SetInputFieldValue(
                speedInputField,
                rallyController.SpikeSpeedKmh
            );

            SetInputFieldValue(
                fadeInputField,
                rallyController.SpikeFadeDelay
            );
        }
        finally
        {
            isSynchronizing =
                false;
        }

        RefreshOffsetFields();
    }

    private void RefreshOffsetFields()
    {
        if (rallyController == null)
        {
            return;
        }

        Vector3 offset =
            rallyController.GetSpikeTargetOffset(
                currentOffsetTargetIndex
            );

        SetInputFieldValue(
            offsetXInputField,
            offset.x
        );

        SetInputFieldValue(
            offsetYInputField,
            offset.y
        );

        SetInputFieldValue(
            offsetZInputField,
            offset.z
        );
    }

    private static void SetInputFieldValue(
        TMP_InputField inputField,
        float value
    )
    {
        if (inputField == null)
        {
            return;
        }

        inputField.SetTextWithoutNotify(
            value.ToString(
                "0.###",
                CultureInfo.InvariantCulture
            )
        );
    }


    // ============================================================
    // JSON Save
    // ============================================================

    public void SaveSettingsJson()
    {
        if (rallyController == null)
        {
            return;
        }

        ApplyAllInputFields();

        string path =
            RequestSavePath();

        if (string.IsNullOrWhiteSpace(
            path
        ))
        {
            return;
        }

        SpikeSettingsData data =
            CreateCurrentData();

        string json =
            JsonUtility.ToJson(
                data,
                true
            );

        try
        {
            string directory =
                Path.GetDirectoryName(
                    path
                );

            if (!string.IsNullOrWhiteSpace(
                directory
            ))
            {
                Directory.CreateDirectory(
                    directory
                );

                lastDirectory =
                    directory;
            }

            File.WriteAllText(
                path,
                json
            );

            Debug.Log(
                "[SpikeUIController] Spike Settings Saved\n" +
                $"Path = {path}\n" +
                json
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[SpikeUIController] JSON保存に失敗しました。\n" +
                $"Path = {path}\n" +
                exception
            );
        }
    }


    // ============================================================
    // JSON Load
    // ============================================================

    public void LoadSettingsJson()
    {
        if (rallyController == null)
        {
            return;
        }

        string path =
            RequestLoadPath();

        if (string.IsNullOrWhiteSpace(
            path
        ))
        {
            return;
        }

        if (!File.Exists(
            path
        ))
        {
            Debug.LogError(
                "[SpikeUIController] JSONファイルが存在しません。\n" +
                $"Path = {path}"
            );

            return;
        }

        try
        {
            string json =
                File.ReadAllText(
                    path
                );

            SpikeSettingsData data =
                JsonUtility.FromJson<SpikeSettingsData>(
                    json
                );

            if (data == null)
            {
                Debug.LogError(
                    "[SpikeUIController] JSONを読み込めませんでした。"
                );

                return;
            }

            ApplyData(
                data
            );

            string directory =
                Path.GetDirectoryName(
                    path
                );

            if (!string.IsNullOrWhiteSpace(
                directory
            ))
            {
                lastDirectory =
                    directory;
            }

            Debug.Log(
                "[SpikeUIController] Spike Settings Loaded\n" +
                $"Path = {path}"
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[SpikeUIController] JSON読込に失敗しました。\n" +
                $"Path = {path}\n" +
                exception
            );
        }
    }

    private SpikeSettingsData CreateCurrentData()
    {
        return new SpikeSettingsData
        {
            version = 1,

            spikeCourseIndex =
                (int)rallyController.CurrentSpikeCourse,

            speedKmh =
                rallyController.SpikeSpeedKmh,

            fadeDelaySeconds =
                rallyController.SpikeFadeDelay,

            offsetTargetIndex =
                currentOffsetTargetIndex,

            leftTargetOffset =
                rallyController.GetSpikeTargetOffset(
                    0
                ),

            rightTargetOffset =
                rallyController.GetSpikeTargetOffset(
                    1
                )
        };
    }

    private void ApplyData(
        SpikeSettingsData data
    )
    {
        rallyController.SetSpikeCourse(
            Mathf.Clamp(
                data.spikeCourseIndex,
                0,
                1
            )
        );

        rallyController.SetSpikeSpeed(
            data.speedKmh
        );

        rallyController.SetSpikeFadeDelay(
            data.fadeDelaySeconds
        );

        rallyController.SetSpikeTargetOffset(
            0,
            data.leftTargetOffset
        );

        rallyController.SetSpikeTargetOffset(
            1,
            data.rightTargetOffset
        );

        currentOffsetTargetIndex =
            Mathf.Clamp(
                data.offsetTargetIndex,
                0,
                1
            );

        RefreshFromRallyController();
    }


    // ============================================================
    // File Path
    // ============================================================

    private string RequestSavePath()
    {
#if UNITY_EDITOR
        string initialDirectory =
            GetInitialDirectory();

        string fileNameWithoutExtension =
            Path.GetFileNameWithoutExtension(
                GetDefaultFileName()
            );

        return UnityEditor.EditorUtility.SaveFilePanel(
            "Save Spike Settings",
            initialDirectory,
            fileNameWithoutExtension,
            "json"
        );
#else
        return GetDefaultJsonPath();
#endif
    }

    private string RequestLoadPath()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel(
            "Load Spike Settings",
            GetInitialDirectory(),
            "json"
        );
#else
        return GetDefaultJsonPath();
#endif
    }

    private string GetInitialDirectory()
    {
        if (
            !string.IsNullOrWhiteSpace(
                lastDirectory
            ) &&
            Directory.Exists(
                lastDirectory
            )
        )
        {
            return lastDirectory;
        }

        return Application.persistentDataPath;
    }

    private string GetDefaultJsonPath()
    {
        return Path.Combine(
            Application.persistentDataPath,
            GetDefaultFileName()
        );
    }

    private string GetDefaultFileName()
    {
        string fileName =
            string.IsNullOrWhiteSpace(
                defaultJsonFileName
            )
                ? "spike_settings.json"
                : defaultJsonFileName.Trim();

        if (!fileName.EndsWith(
            ".json",
            StringComparison.OrdinalIgnoreCase
        ))
        {
            fileName +=
                ".json";
        }

        return fileName;
    }


    // ============================================================
    // Parse
    // ============================================================

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
}
