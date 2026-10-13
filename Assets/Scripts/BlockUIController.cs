using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Block設定専用UI。
///
/// 役割:
/// - Block Spinの変更
/// - Block Exit Speedの変更
/// - Block用Fade Delayの変更
/// - Left / Right のBlock Landing PointのLocal Position XYZを直接変更
/// - Block設定のJSON Save / Load
/// - Main Panel <-> Block Panel の切り替え
///
/// 実際のプレー設定値はRallyControllerが保持し、
/// このクラスはUIからRallyControllerを操作するだけにする。
/// </summary>
public class BlockUIController : MonoBehaviour
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

    [Tooltip("Block設定Panel")]
    [SerializeField]
    private GameObject blockPanel;

    [Tooltip("Main画面にあるBlock Button。未設定でも可。")]
    [SerializeField]
    private Button openBlockButton;

    [Tooltip("Block画面のBack Button")]
    [SerializeField]
    private Button backButton;

    [Tooltip("開始時にMain Panelを表示しBlock Panelを隠す")]
    [SerializeField]
    private bool showMainPanelOnStart =
        false;


    // ============================================================
    // Block Parameters
    // ============================================================

    [Header("Block Parameters")]

    [Tooltip("ブロック接触後のボール回転 [rpm]")]
    [SerializeField]
    private TMP_InputField blockSpinInputField;

    [Tooltip("ブロック接触後のボール速度 [km/h]")]
    [SerializeField]
    private TMP_InputField speedInputField;

    [Tooltip("Block Phase開始からFade Out開始までの時間 [s]")]
    [SerializeField]
    private TMP_InputField fadeInputField;


    // ============================================================
    // Block Landing Point Local Position
    // ============================================================

    [Header("Block Landing Point Position")]

    [Tooltip("座標を編集するBlock Landing Point: Left / Right")]
    [FormerlySerializedAs("offsetTargetDropdown")]
    [SerializeField]
    private TMP_Dropdown positionTargetDropdown;

    [Tooltip("Block Landing Point Local X [m]")]
    [FormerlySerializedAs("offsetXInputField")]
    [SerializeField]
    private TMP_InputField positionXInputField;

    [Tooltip("Block Landing Point Local Y [m]")]
    [FormerlySerializedAs("offsetYInputField")]
    [SerializeField]
    private TMP_InputField positionYInputField;

    [Tooltip("Block Landing Point Local Z [m]")]
    [FormerlySerializedAs("offsetZInputField")]
    [SerializeField]
    private TMP_InputField positionZInputField;


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
        "block_settings.json";

    private string lastDirectory =
        string.Empty;


    // ============================================================
    // Runtime
    // ============================================================

    private int currentPositionTargetIndex =
        0;

    private bool isSynchronizing =
        false;


    // ============================================================
    // JSON Data
    // ============================================================

    [Serializable]
    private class BlockSettingsData
    {
        public int version = 2;

        public float blockSpinRpm;
        public float speedKmh;
        public float fadeDelaySeconds;

        // 旧JSONとのフィールド名互換のため名前はそのまま。
        public int offsetTargetIndex;

        // version 2以降:
        // Block Landing PointのLocal Positionそのもの。
        public Vector3 leftLandingPointLocalPosition;
        public Vector3 rightLandingPointLocalPosition;

        // version 1互換用:
        // 旧方式のBase + Offset。
        public Vector3 leftLandingPointOffset;
        public Vector3 rightLandingPointOffset;
    }


    // ============================================================
    // Unity
    // ============================================================

    private void Start()
    {
        if (rallyController == null)
        {
            Debug.LogError(
                "[BlockUIController] RallyControllerが設定されていません。",
                this
            );

            enabled =
                false;

            return;
        }

        ConfigureDropdowns();
        ConfigureInputFields();
        BindUI();

        currentPositionTargetIndex =
            positionTargetDropdown != null
                ? Mathf.Clamp(
                    positionTargetDropdown.value,
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

    public void OpenBlockPanel()
    {
        if (mainPanel != null)
        {
            mainPanel.SetActive(
                false
            );
        }

        if (blockPanel != null)
        {
            blockPanel.SetActive(
                true
            );
        }

        RefreshFromRallyController();
    }

    public void BackToMainPanel()
    {
        ApplyAllInputFields();

        if (blockPanel != null)
        {
            blockPanel.SetActive(
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
            positionTargetDropdown,
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
            blockSpinInputField
        );

        ConfigureDecimalInput(
            speedInputField
        );

        ConfigureDecimalInput(
            fadeInputField
        );

        ConfigureDecimalInput(
            positionXInputField
        );

        ConfigureDecimalInput(
            positionYInputField
        );

        ConfigureDecimalInput(
            positionZInputField
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
        if (openBlockButton != null)
        {
            openBlockButton.onClick.AddListener(
                OpenBlockPanel
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

        if (positionTargetDropdown != null)
        {
            positionTargetDropdown.onValueChanged.AddListener(
                OnPositionTargetChanged
            );
        }

        BindInputField(
            blockSpinInputField,
            OnBlockSpinEndEdit
        );

        BindInputField(
            speedInputField,
            OnSpeedEndEdit
        );

        BindInputField(
            fadeInputField,
            OnFadeEndEdit
        );

        BindInputField(
            positionXInputField,
            OnPositionEndEdit
        );

        BindInputField(
            positionYInputField,
            OnPositionEndEdit
        );

        BindInputField(
            positionZInputField,
            OnPositionEndEdit
        );
    }

    private void UnbindUI()
    {
        if (openBlockButton != null)
        {
            openBlockButton.onClick.RemoveListener(
                OpenBlockPanel
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

        if (positionTargetDropdown != null)
        {
            positionTargetDropdown.onValueChanged.RemoveListener(
                OnPositionTargetChanged
            );
        }

        UnbindInputField(
            blockSpinInputField,
            OnBlockSpinEndEdit
        );

        UnbindInputField(
            speedInputField,
            OnSpeedEndEdit
        );

        UnbindInputField(
            fadeInputField,
            OnFadeEndEdit
        );

        UnbindInputField(
            positionXInputField,
            OnPositionEndEdit
        );

        UnbindInputField(
            positionYInputField,
            OnPositionEndEdit
        );

        UnbindInputField(
            positionZInputField,
            OnPositionEndEdit
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

    private void OnPositionTargetChanged(
        int index
    )
    {
        if (isSynchronizing)
        {
            return;
        }

        currentPositionTargetIndex =
            Mathf.Clamp(
                index,
                0,
                1
            );

        RefreshPositionFields();
    }


    // ============================================================
    // Block Parameter Events
    // ============================================================

    private void OnBlockSpinEndEdit(
        string text
    )
    {
        if (TryParseFloat(
            text,
            out float value
        ))
        {
            rallyController.SetBlockSpinRpm(
                value
            );
        }

        SetInputFieldValue(
            blockSpinInputField,
            rallyController.BlockSpinRpm
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
            rallyController.SetBlockExitSpeed(
                value
            );
        }

        SetInputFieldValue(
            speedInputField,
            rallyController.BlockExitSpeedKmh
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
            rallyController.SetBlockFadeDelay(
                value
            );
        }

        SetInputFieldValue(
            fadeInputField,
            rallyController.BlockFadeDelay
        );
    }


    // ============================================================
    // Landing Point Position Events
    // ============================================================

    private void OnPositionEndEdit(
        string unused
    )
    {
        ApplyPositionFields();
        RefreshPositionFields();
    }

    private void ApplyPositionFields()
    {
        if (
            !TryParseFloat(
                positionXInputField != null
                    ? positionXInputField.text
                    : string.Empty,
                out float x
            ) ||
            !TryParseFloat(
                positionYInputField != null
                    ? positionYInputField.text
                    : string.Empty,
                out float y
            ) ||
            !TryParseFloat(
                positionZInputField != null
                    ? positionZInputField.text
                    : string.Empty,
                out float z
            )
        )
        {
            return;
        }

        rallyController.SetBlockLandingPointLocalPosition(
            currentPositionTargetIndex,
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

        ApplyFloatIfValid(
            blockSpinInputField,
            rallyController.SetBlockSpinRpm
        );

        ApplyFloatIfValid(
            speedInputField,
            rallyController.SetBlockExitSpeed
        );

        ApplyFloatIfValid(
            fadeInputField,
            rallyController.SetBlockFadeDelay
        );

        ApplyPositionFields();
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
            if (positionTargetDropdown != null)
            {
                positionTargetDropdown.SetValueWithoutNotify(
                    currentPositionTargetIndex
                );
            }

            SetInputFieldValue(
                blockSpinInputField,
                rallyController.BlockSpinRpm
            );

            SetInputFieldValue(
                speedInputField,
                rallyController.BlockExitSpeedKmh
            );

            SetInputFieldValue(
                fadeInputField,
                rallyController.BlockFadeDelay
            );
        }
        finally
        {
            isSynchronizing =
                false;
        }

        RefreshPositionFields();
    }

    private void RefreshPositionFields()
    {
        if (rallyController == null)
        {
            return;
        }

        Vector3 localPosition =
            rallyController.GetBlockLandingPointLocalPosition(
                currentPositionTargetIndex
            );

        SetInputFieldValue(
            positionXInputField,
            localPosition.x
        );

        SetInputFieldValue(
            positionYInputField,
            localPosition.y
        );

        SetInputFieldValue(
            positionZInputField,
            localPosition.z
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

        BlockSettingsData data =
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
                "[BlockUIController] Block Settings Saved\n" +
                $"Path = {path}\n" +
                json
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[BlockUIController] JSON保存に失敗しました。\n" +
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
                "[BlockUIController] JSONファイルが存在しません。\n" +
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

            BlockSettingsData data =
                JsonUtility.FromJson<BlockSettingsData>(
                    json
                );

            if (data == null)
            {
                Debug.LogError(
                    "[BlockUIController] JSONを読み込めませんでした。"
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
                "[BlockUIController] Block Settings Loaded\n" +
                $"Path = {path}"
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[BlockUIController] JSON読込に失敗しました。\n" +
                $"Path = {path}\n" +
                exception
            );
        }
    }

    private BlockSettingsData CreateCurrentData()
    {
        return new BlockSettingsData
        {
            version = 2,

            blockSpinRpm =
                rallyController.BlockSpinRpm,

            speedKmh =
                rallyController.BlockExitSpeedKmh,

            fadeDelaySeconds =
                rallyController.BlockFadeDelay,

            offsetTargetIndex =
                currentPositionTargetIndex,

            leftLandingPointLocalPosition =
                rallyController.GetBlockLandingPointLocalPosition(
                    0
                ),

            rightLandingPointLocalPosition =
                rallyController.GetBlockLandingPointLocalPosition(
                    1
                ),

            // version 1互換フィールド。
            // version 2ではLocal Positionを正とするためzeroで保存する。
            leftLandingPointOffset =
                Vector3.zero,

            rightLandingPointOffset =
                Vector3.zero
        };
    }

    private void ApplyData(
        BlockSettingsData data
    )
    {
        rallyController.SetBlockSpinRpm(
            data.blockSpinRpm
        );

        rallyController.SetBlockExitSpeed(
            data.speedKmh
        );

        rallyController.SetBlockFadeDelay(
            data.fadeDelaySeconds
        );

        if (data.version >= 2)
        {
            rallyController.SetBlockLandingPointLocalPosition(
                0,
                data.leftLandingPointLocalPosition
            );

            rallyController.SetBlockLandingPointLocalPosition(
                1,
                data.rightLandingPointLocalPosition
            );
        }
        else
        {
            /*
             * 旧version 1 JSONはXYZ Offsetだったため、
             * Legacy APIへ渡して元の挙動を再現する。
             */
            rallyController.SetBlockLandingPointOffset(
                0,
                data.leftLandingPointOffset
            );

            rallyController.SetBlockLandingPointOffset(
                1,
                data.rightLandingPointOffset
            );
        }

        currentPositionTargetIndex =
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
            "Save Block Settings",
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
            "Load Block Settings",
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
                ? "block_settings.json"
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
