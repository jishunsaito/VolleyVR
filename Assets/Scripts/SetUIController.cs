using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Setter Toss設定専用UI。
///
/// 役割:
/// - Toss Side (Left / Right) の選択
/// - Set Height (Setter Toss最高到達World Y) の変更
/// - Toss Target Z Offset の変更
/// - Set設定のJSON Save / Load
/// - Main Panel <-> Set Panel の切り替え
///
/// 実際のプレー設定値はRallyControllerが保持し、
/// このクラスはUIからRallyControllerを操作するだけにする。
/// </summary>
public class SetUIController : MonoBehaviour
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

    [Tooltip("Set設定Panel")]
    [SerializeField]
    private GameObject setPanel;

    [Tooltip("Main画面にあるSet Button。未設定でも可。")]
    [SerializeField]
    private Button openSetButton;

    [Tooltip("Set画面のBack Button")]
    [SerializeField]
    private Button backButton;

    [Tooltip("開始時にMain Panelを表示しSet Panelを隠す")]
    [SerializeField]
    private bool showMainPanelOnStart =
        false;


    // ============================================================
    // Set Selection
    // ============================================================

    [Header("Set Selection")]

    [Tooltip("Toss Side: Left / Right")]
    [SerializeField]
    private TMP_Dropdown tossSideDropdown;


    // ============================================================
    // Set Parameters
    // ============================================================

    [Header("Set Parameters")]

    [Tooltip("Setter Tossの最高到達World Y [m]")]
    [SerializeField]
    private TMP_InputField setHeightInputField;

    [Tooltip("選択したToss Targetに加えるZ方向Offset [m]")]
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
        "set_settings.json";

    private string lastDirectory =
        string.Empty;


    // ============================================================
    // Runtime
    // ============================================================

    private bool isSynchronizing =
        false;


    // ============================================================
    // JSON Data
    // ============================================================

    [Serializable]
    private class SetSettingsData
    {
        public int version = 1;

        public int tossSideIndex;
        public float setHeight;
        public float offsetZ;
    }


    // ============================================================
    // Unity
    // ============================================================

    private void Start()
    {
        if (rallyController == null)
        {
            Debug.LogError(
                "[SetUIController] RallyControllerが設定されていません。",
                this
            );

            enabled =
                false;

            return;
        }

        ConfigureDropdown();
        ConfigureInputFields();
        BindUI();

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

    public void OpenSetPanel()
    {
        if (mainPanel != null)
        {
            mainPanel.SetActive(
                false
            );
        }

        if (setPanel != null)
        {
            setPanel.SetActive(
                true
            );
        }

        RefreshFromRallyController();
    }

    public void BackToMainPanel()
    {
        if (setPanel != null)
        {
            setPanel.SetActive(
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
    // Configure
    // ============================================================

    private void ConfigureDropdown()
    {
        if (tossSideDropdown == null)
        {
            Debug.LogWarning(
                "[SetUIController] Toss Side Dropdownが設定されていません。",
                this
            );

            return;
        }

        tossSideDropdown.ClearOptions();

        tossSideDropdown.AddOptions(
            new List<string>
            {
                "Left",
                "Right"
            }
        );
    }

    private void ConfigureInputFields()
    {
        ConfigureDecimalInputField(
            setHeightInputField
        );

        ConfigureDecimalInputField(
            offsetZInputField
        );
    }

    private static void ConfigureDecimalInputField(
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
    // Bind / Unbind
    // ============================================================

    private void BindUI()
    {
        if (openSetButton != null)
        {
            openSetButton.onClick.AddListener(
                OpenSetPanel
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

        if (tossSideDropdown != null)
        {
            tossSideDropdown.onValueChanged.AddListener(
                OnTossSideChanged
            );
        }

        if (setHeightInputField != null)
        {
            setHeightInputField.onEndEdit.AddListener(
                OnSetHeightEndEdit
            );
        }

        if (offsetZInputField != null)
        {
            offsetZInputField.onEndEdit.AddListener(
                OnOffsetZEndEdit
            );
        }
    }

    private void UnbindUI()
    {
        if (openSetButton != null)
        {
            openSetButton.onClick.RemoveListener(
                OpenSetPanel
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

        if (tossSideDropdown != null)
        {
            tossSideDropdown.onValueChanged.RemoveListener(
                OnTossSideChanged
            );
        }

        if (setHeightInputField != null)
        {
            setHeightInputField.onEndEdit.RemoveListener(
                OnSetHeightEndEdit
            );
        }

        if (offsetZInputField != null)
        {
            offsetZInputField.onEndEdit.RemoveListener(
                OnOffsetZEndEdit
            );
        }
    }


    // ============================================================
    // UI -> RallyController
    // ============================================================

    private void OnTossSideChanged(
        int index
    )
    {
        if (
            isSynchronizing ||
            rallyController == null
        )
        {
            return;
        }

        rallyController.SetTossSide(
            Mathf.Clamp(
                index,
                0,
                1
            )
        );
    }

    private void OnSetHeightEndEdit(
        string text
    )
    {
        if (
            isSynchronizing ||
            rallyController == null
        )
        {
            return;
        }

        if (!TryParseFloat(
            text,
            out float value
        ))
        {
            RefreshFromRallyController();
            return;
        }

        rallyController.SetSetApexHeight(
            value
        );

        RefreshFromRallyController();
    }

    private void OnOffsetZEndEdit(
        string text
    )
    {
        if (
            isSynchronizing ||
            rallyController == null
        )
        {
            return;
        }

        if (!TryParseFloat(
            text,
            out float value
        ))
        {
            RefreshFromRallyController();
            return;
        }

        rallyController.SetTossTargetZOffset(
            value
        );

        RefreshFromRallyController();
    }

    private void ApplyAllInputFields()
    {
        if (rallyController == null)
        {
            return;
        }

        if (tossSideDropdown != null)
        {
            rallyController.SetTossSide(
                Mathf.Clamp(
                    tossSideDropdown.value,
                    0,
                    1
                )
            );
        }

        ApplyInputField(
            setHeightInputField,
            rallyController.SetSetApexHeight
        );

        ApplyInputField(
            offsetZInputField,
            rallyController.SetTossTargetZOffset
        );
    }

    private static void ApplyInputField(
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
            if (tossSideDropdown != null)
            {
                tossSideDropdown.SetValueWithoutNotify(
                    (int)rallyController.CurrentTossSide
                );
            }

            SetInputFieldValue(
                setHeightInputField,
                rallyController.SetterTossApexHeight
            );

            SetInputFieldValue(
                offsetZInputField,
                rallyController.TossTargetZOffset
            );
        }
        finally
        {
            isSynchronizing =
                false;
        }
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

        SetSettingsData data =
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
                "[SetUIController] Set Settings Saved\n" +
                $"Path = {path}\n" +
                json
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[SetUIController] JSON保存に失敗しました。\n" +
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
                "[SetUIController] JSONファイルが存在しません。\n" +
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

            SetSettingsData data =
                JsonUtility.FromJson<SetSettingsData>(
                    json
                );

            if (data == null)
            {
                Debug.LogError(
                    "[SetUIController] JSONを読み込めませんでした。"
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
                "[SetUIController] Set Settings Loaded\n" +
                $"Path = {path}"
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[SetUIController] JSON読込に失敗しました。\n" +
                $"Path = {path}\n" +
                exception
            );
        }
    }

    private SetSettingsData CreateCurrentData()
    {
        return new SetSettingsData
        {
            version = 1,

            tossSideIndex =
                (int)rallyController.CurrentTossSide,

            setHeight =
                rallyController.SetterTossApexHeight,

            offsetZ =
                rallyController.TossTargetZOffset
        };
    }

    private void ApplyData(
        SetSettingsData data
    )
    {
        rallyController.SetTossSide(
            Mathf.Clamp(
                data.tossSideIndex,
                0,
                1
            )
        );

        rallyController.SetSetApexHeight(
            data.setHeight
        );

        rallyController.SetTossTargetZOffset(
            data.offsetZ
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

        return UnityEditor.EditorUtility.SaveFilePanel(
            "Save Set Settings",
            initialDirectory,
            defaultJsonFileName,
            "json"
        );
#else
        return Path.Combine(
            Application.persistentDataPath,
            defaultJsonFileName
        );
#endif
    }

    private string RequestLoadPath()
    {
#if UNITY_EDITOR
        string initialDirectory =
            GetInitialDirectory();

        return UnityEditor.EditorUtility.OpenFilePanel(
            "Load Set Settings",
            initialDirectory,
            "json"
        );
#else
        return Path.Combine(
            Application.persistentDataPath,
            defaultJsonFileName
        );
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

        return Application.dataPath;
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
