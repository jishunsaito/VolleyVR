using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Serve設定専用UI。
///
/// 役割:
/// - Serve Start / Serve Target の選択
/// - Toss Height / Hit Height / Forward / Speed / Angle の変更
/// - Serve用Fade Delayの変更
/// - Left / Center / Right のServe TargetへXYZ Offsetを付与
/// - Serve設定のJSON Save / Load
/// - Main Panel <-> Serve Panel の切り替え
///
/// 実際のプレー設定値はRallyControllerが保持し、
/// このクラスはUIからRallyControllerを操作するだけにする。
/// </summary>
public class ServeUIController : MonoBehaviour
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

    [Tooltip("Serve設定Panel")]
    [SerializeField]
    private GameObject servePanel;

    [Tooltip("Main画面にあるServe Button。未設定でも可。")]
    [SerializeField]
    private Button openServeButton;

    [Tooltip("Serve画面のBack Button")]
    [SerializeField]
    private Button backButton;

    [Tooltip("開始時にMain Panelを表示しServe Panelを隠す")]
    [SerializeField]
    private bool showMainPanelOnStart =
        true;


    // ============================================================
    // Serve Selection
    // ============================================================

    [Header("Serve Selection")]

    [Tooltip("Serve Start: Left / Center / Right")]
    [SerializeField]
    private TMP_Dropdown serveStartDropdown;

    [Tooltip("Serve Target: Left / Center / Right")]
    [SerializeField]
    private TMP_Dropdown serveTargetDropdown;


    // ============================================================
    // Serve Parameters
    // ============================================================

    [Header("Serve Parameters")]

    [SerializeField]
    private TMP_InputField tossHeightInputField;

    [SerializeField]
    private TMP_InputField hitHeightInputField;

    [SerializeField]
    private TMP_InputField forwardInputField;

    [SerializeField]
    private TMP_InputField speedInputField;

    [SerializeField]
    private TMP_InputField angleInputField;

    [Tooltip("Serve Phase開始からFade Out開始までの時間 [s]")]
    [SerializeField]
    private TMP_InputField fadeInputField;


    // ============================================================
    // Serve Target Offset
    // ============================================================

    [Header("Serve Target Offset")]

    [Tooltip("Offsetを適用するServe Target: Left / Center / Right")]
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
        "serve_settings.json";

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
    private class ServeSettingsData
    {
        public int version = 1;

        public int serveStartIndex;
        public int serveTargetIndex;

        public float tossHeight;
        public float hitHeight;
        public float forwardDistance;
        public float speedKmh;
        public float launchAngleDeg;
        public float fadeDelaySeconds;

        public int offsetTargetIndex;

        public Vector3 leftTargetOffset;
        public Vector3 centerTargetOffset;
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
                "[ServeUIController] RallyControllerが設定されていません。",
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
                    2
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

    public void OpenServePanel()
    {
        if (mainPanel != null)
        {
            mainPanel.SetActive(
                false
            );
        }

        if (servePanel != null)
        {
            servePanel.SetActive(
                true
            );
        }

        RefreshFromRallyController();
    }

    public void BackToMainPanel()
    {
        ApplyAllInputFields();

        if (servePanel != null)
        {
            servePanel.SetActive(
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
        List<string> positions =
            new List<string>
            {
                "Left",
                "Center",
                "Right"
            };

        ConfigureDropdown(
            serveStartDropdown,
            positions
        );

        ConfigureDropdown(
            serveTargetDropdown,
            positions
        );

        ConfigureDropdown(
            offsetTargetDropdown,
            positions
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
            tossHeightInputField
        );

        ConfigureDecimalInput(
            hitHeightInputField
        );

        ConfigureDecimalInput(
            forwardInputField
        );

        ConfigureDecimalInput(
            speedInputField
        );

        ConfigureDecimalInput(
            angleInputField
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
        if (openServeButton != null)
        {
            openServeButton.onClick.AddListener(
                OpenServePanel
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

        if (serveStartDropdown != null)
        {
            serveStartDropdown.onValueChanged.AddListener(
                OnServeStartChanged
            );
        }

        if (serveTargetDropdown != null)
        {
            serveTargetDropdown.onValueChanged.AddListener(
                OnServeTargetChanged
            );
        }

        if (offsetTargetDropdown != null)
        {
            offsetTargetDropdown.onValueChanged.AddListener(
                OnOffsetTargetChanged
            );
        }

        BindInputField(
            tossHeightInputField,
            OnTossHeightEndEdit
        );

        BindInputField(
            hitHeightInputField,
            OnHitHeightEndEdit
        );

        BindInputField(
            forwardInputField,
            OnForwardEndEdit
        );

        BindInputField(
            speedInputField,
            OnSpeedEndEdit
        );

        BindInputField(
            angleInputField,
            OnAngleEndEdit
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
        if (openServeButton != null)
        {
            openServeButton.onClick.RemoveListener(
                OpenServePanel
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

        if (serveStartDropdown != null)
        {
            serveStartDropdown.onValueChanged.RemoveListener(
                OnServeStartChanged
            );
        }

        if (serveTargetDropdown != null)
        {
            serveTargetDropdown.onValueChanged.RemoveListener(
                OnServeTargetChanged
            );
        }

        if (offsetTargetDropdown != null)
        {
            offsetTargetDropdown.onValueChanged.RemoveListener(
                OnOffsetTargetChanged
            );
        }

        UnbindInputField(
            tossHeightInputField,
            OnTossHeightEndEdit
        );

        UnbindInputField(
            hitHeightInputField,
            OnHitHeightEndEdit
        );

        UnbindInputField(
            forwardInputField,
            OnForwardEndEdit
        );

        UnbindInputField(
            speedInputField,
            OnSpeedEndEdit
        );

        UnbindInputField(
            angleInputField,
            OnAngleEndEdit
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

    private void OnServeStartChanged(
        int index
    )
    {
        if (isSynchronizing)
        {
            return;
        }

        rallyController.SetServeStart(
            index
        );
    }

    private void OnServeTargetChanged(
        int index
    )
    {
        if (isSynchronizing)
        {
            return;
        }

        rallyController.SetServeTarget(
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
                2
            );

        RefreshOffsetFields();
    }


    // ============================================================
    // Serve Parameter Events
    // ============================================================

    private void OnTossHeightEndEdit(
        string text
    )
    {
        if (TryParseFloat(
            text,
            out float value
        ))
        {
            rallyController.SetTossHeight(
                value
            );
        }

        SetInputFieldValue(
            tossHeightInputField,
            rallyController.ServeTossHeight
        );
    }

    private void OnHitHeightEndEdit(
        string text
    )
    {
        if (TryParseFloat(
            text,
            out float value
        ))
        {
            rallyController.SetServeHitHeight(
                value
            );
        }

        SetInputFieldValue(
            hitHeightInputField,
            rallyController.ServeHitHeight
        );
    }

    private void OnForwardEndEdit(
        string text
    )
    {
        if (TryParseFloat(
            text,
            out float value
        ))
        {
            rallyController.SetTossForwardDistance(
                value
            );
        }

        SetInputFieldValue(
            forwardInputField,
            rallyController.ServeTossForwardDistance
        );
    }

    private void OnSpeedEndEdit(
        string text
    )
    {
        if (TryParseFloat(
            text,
            out float value
        ))
        {
            rallyController.SetServeSpeed(
                value
            );
        }

        SetInputFieldValue(
            speedInputField,
            rallyController.ServeSpeedKmh
        );
    }

    private void OnAngleEndEdit(
        string text
    )
    {
        if (TryParseFloat(
            text,
            out float value
        ))
        {
            rallyController.SetSpikeServeLaunchAngle(
                value
            );
        }

        SetInputFieldValue(
            angleInputField,
            rallyController.SpikeServeLaunchAngle
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
            rallyController.SetServeFadeDelay(
                value
            );
        }

        SetInputFieldValue(
            fadeInputField,
            rallyController.ServeFadeDelay
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

        rallyController.SetServeTargetOffset(
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

        if (serveStartDropdown != null)
        {
            rallyController.SetServeStart(
                serveStartDropdown.value
            );
        }

        if (serveTargetDropdown != null)
        {
            rallyController.SetServeTarget(
                serveTargetDropdown.value
            );
        }

        ApplyFloatIfValid(
            tossHeightInputField,
            rallyController.SetTossHeight
        );

        ApplyFloatIfValid(
            hitHeightInputField,
            rallyController.SetServeHitHeight
        );

        ApplyFloatIfValid(
            forwardInputField,
            rallyController.SetTossForwardDistance
        );

        ApplyFloatIfValid(
            speedInputField,
            rallyController.SetServeSpeed
        );

        ApplyFloatIfValid(
            angleInputField,
            rallyController.SetSpikeServeLaunchAngle
        );

        ApplyFloatIfValid(
            fadeInputField,
            rallyController.SetServeFadeDelay
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
            if (serveStartDropdown != null)
            {
                serveStartDropdown.SetValueWithoutNotify(
                    (int)rallyController.CurrentServeStart
                );
            }

            if (serveTargetDropdown != null)
            {
                serveTargetDropdown.SetValueWithoutNotify(
                    (int)rallyController.CurrentServeTarget
                );
            }

            if (offsetTargetDropdown != null)
            {
                offsetTargetDropdown.SetValueWithoutNotify(
                    currentOffsetTargetIndex
                );
            }

            SetInputFieldValue(
                tossHeightInputField,
                rallyController.ServeTossHeight
            );

            SetInputFieldValue(
                hitHeightInputField,
                rallyController.ServeHitHeight
            );

            SetInputFieldValue(
                forwardInputField,
                rallyController.ServeTossForwardDistance
            );

            SetInputFieldValue(
                speedInputField,
                rallyController.ServeSpeedKmh
            );

            SetInputFieldValue(
                angleInputField,
                rallyController.SpikeServeLaunchAngle
            );

            SetInputFieldValue(
                fadeInputField,
                rallyController.ServeFadeDelay
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
            rallyController.GetServeTargetOffset(
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

        ServeSettingsData data =
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
                "[ServeUIController] Serve Settings Saved\n" +
                $"Path = {path}\n" +
                json
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[ServeUIController] JSON保存に失敗しました。\n" +
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
                "[ServeUIController] JSONファイルが存在しません。\n" +
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

            ServeSettingsData data =
                JsonUtility.FromJson<ServeSettingsData>(
                    json
                );

            if (data == null)
            {
                Debug.LogError(
                    "[ServeUIController] JSONを読み込めませんでした。"
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
                "[ServeUIController] Serve Settings Loaded\n" +
                $"Path = {path}"
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[ServeUIController] JSON読込に失敗しました。\n" +
                $"Path = {path}\n" +
                exception
            );
        }
    }

    private ServeSettingsData CreateCurrentData()
    {
        return new ServeSettingsData
        {
            version = 1,

            serveStartIndex =
                (int)rallyController.CurrentServeStart,

            serveTargetIndex =
                (int)rallyController.CurrentServeTarget,

            tossHeight =
                rallyController.ServeTossHeight,

            hitHeight =
                rallyController.ServeHitHeight,

            forwardDistance =
                rallyController.ServeTossForwardDistance,

            speedKmh =
                rallyController.ServeSpeedKmh,

            launchAngleDeg =
                rallyController.SpikeServeLaunchAngle,

            fadeDelaySeconds =
                rallyController.ServeFadeDelay,

            offsetTargetIndex =
                currentOffsetTargetIndex,

            leftTargetOffset =
                rallyController.GetServeTargetOffset(
                    0
                ),

            centerTargetOffset =
                rallyController.GetServeTargetOffset(
                    1
                ),

            rightTargetOffset =
                rallyController.GetServeTargetOffset(
                    2
                )
        };
    }

    private void ApplyData(
        ServeSettingsData data
    )
    {
        rallyController.SetServeStart(
            Mathf.Clamp(
                data.serveStartIndex,
                0,
                2
            )
        );

        rallyController.SetServeTarget(
            Mathf.Clamp(
                data.serveTargetIndex,
                0,
                2
            )
        );

        rallyController.SetTossHeight(
            data.tossHeight
        );

        rallyController.SetServeHitHeight(
            data.hitHeight
        );

        rallyController.SetTossForwardDistance(
            data.forwardDistance
        );

        rallyController.SetServeSpeed(
            data.speedKmh
        );

        rallyController.SetSpikeServeLaunchAngle(
            data.launchAngleDeg
        );

        rallyController.SetServeFadeDelay(
            data.fadeDelaySeconds
        );

        rallyController.SetServeTargetOffset(
            0,
            data.leftTargetOffset
        );

        rallyController.SetServeTargetOffset(
            1,
            data.centerTargetOffset
        );

        rallyController.SetServeTargetOffset(
            2,
            data.rightTargetOffset
        );

        currentOffsetTargetIndex =
            Mathf.Clamp(
                data.offsetTargetIndex,
                0,
                2
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
            "Save Serve Settings",
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
            "Load Serve Settings",
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
                ? "serve_settings.json"
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
