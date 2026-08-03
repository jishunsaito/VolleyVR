using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ParameterManager : MonoBehaviour
{
    // =========================================================
    // 参照
    // =========================================================

    [Header("Controllers")]

    [SerializeField]
    private ImageController imageController;

    [SerializeField]
    private UIController uiController;


    // =========================================================
    // Buttons
    // =========================================================

    [Header("Buttons")]

    [SerializeField]
    private Button resetButton;

    [SerializeField]
    private Button saveButton;

    [SerializeField]
    private Button loadButton;


    // =========================================================
    // 保存設定
    // =========================================================

    [Header("Save Settings")]

    [SerializeField]
    private string presetFolderName =
        "StereoParameterPresets";

    [SerializeField]
    private string defaultFileName =
        "stereo_parameters";


    // =========================================================
    // 内部データ
    // =========================================================

    private ParameterPreset initialPreset;


    /// <summary>
    /// パラメータ保存専用ディレクトリ
    /// </summary>
    private string PresetDirectory
    {
        get
        {
            return Path.Combine(
                Application.persistentDataPath,
                presetFolderName
            );
        }
    }


    // =========================================================
    // Unityイベント
    // =========================================================

    private void Awake()
    {
        if (imageController == null)
        {
            Debug.LogError(
                "ParameterPresetManagerに" +
                "ImageControllerが設定されていません。",
                this
            );

            enabled = false;
            return;
        }

        // 保存用フォルダを作成
        Directory.CreateDirectory(
            PresetDirectory
        );

        // Play開始時の値をReset用として記録
        initialPreset =
            CaptureCurrentParameters();

        BindButtons();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }


    // =========================================================
    // Button接続
    // =========================================================

    private void BindButtons()
    {
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(
                ResetParameters
            );
        }

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(
                SaveParameters
            );
        }

        if (loadButton != null)
        {
            loadButton.onClick.AddListener(
                LoadParameters
            );
        }
    }

    private void UnbindButtons()
    {
        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(
                ResetParameters
            );
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(
                SaveParameters
            );
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(
                LoadParameters
            );
        }
    }


    // =========================================================
    // Reset
    // =========================================================

    /// <summary>
    /// Play開始時のパラメータへ戻す
    /// </summary>
    public void ResetParameters()
    {
        if (initialPreset == null)
        {
            Debug.LogWarning(
                "初期パラメータが記録されていません。",
                this
            );

            return;
        }

        ApplyPreset(initialPreset);

        Debug.Log(
            "パラメータをPlay開始時の値へ戻しました。",
            this
        );
    }


    // =========================================================
    // Save
    // =========================================================

    public void SaveParameters()
    {
        ParameterPreset preset =
            CaptureCurrentParameters();

        string path =
            SelectSaveFilePath();

        // キャンセル
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string directory =
                Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json =
                JsonUtility.ToJson(
                    preset,
                    true
                );

            File.WriteAllText(
                path,
                json,
                new UTF8Encoding(false)
            );

            Debug.Log(
                "パラメータを保存しました。\n" +
                path,
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "パラメータの保存に失敗しました。\n" +
                exception.Message,
                this
            );
        }
    }


    // =========================================================
    // Load
    // =========================================================

    public void LoadParameters()
    {
        string path =
            SelectLoadFilePath();

        // キャンセルまたはファイルなし
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path))
        {
            Debug.LogError(
                "選択したファイルが存在しません。\n" +
                path,
                this
            );

            return;
        }

        try
        {
            string json =
                File.ReadAllText(
                    path,
                    Encoding.UTF8
                );

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError(
                    "選択したJSONファイルが空です。",
                    this
                );

                return;
            }

            ParameterPreset preset =
                JsonUtility.FromJson<ParameterPreset>(
                    json
                );

            if (preset == null)
            {
                Debug.LogError(
                    "JSONをパラメータへ変換できませんでした。",
                    this
                );

                return;
            }

            if (preset.formatVersion <= 0)
            {
                Debug.LogError(
                    "対応していないパラメータファイルです。",
                    this
                );

                return;
            }

            ApplyPreset(preset);

            Debug.Log(
                "パラメータを読み込みました。\n" +
                path,
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "パラメータの読込に失敗しました。\n" +
                exception.Message,
                this
            );
        }
    }


    // =========================================================
    // 現在のパラメータを取得
    // =========================================================

    private ParameterPreset CaptureCurrentParameters()
    {
        return new ParameterPreset
        {
            formatVersion = 1,

            savedAt =
                DateTime.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss"
                ),

            shiftPixels =
                imageController.shiftPixels,

            focalLength =
                imageController.focalLength,

            baseline =
                imageController.baseline,

            stereoCameraPosition =
                imageController.stereoCameraPosition,

            stereoCameraRotationX =
                imageController.stereoCameraRotationX
        };
    }


    // =========================================================
    // パラメータをImageControllerへ適用
    // =========================================================

    private void ApplyPreset(
        ParameterPreset preset
    )
    {
        imageController.shiftPixels =
            preset.shiftPixels;

        imageController.focalLength =
            preset.focalLength;

        imageController.baseline =
            preset.baseline;

        imageController.stereoCameraPosition =
            preset.stereoCameraPosition;

        imageController.stereoCameraRotationX =
            preset.stereoCameraRotationX;

        // SliderとInputFieldも更新
        if (uiController != null)
        {
            uiController.RefreshFromController();
        }
    }


    // =========================================================
    // 保存ファイル選択
    // =========================================================

    private string SelectSaveFilePath()
    {
        string time =
            DateTime.Now.ToString(
                "yyyyMMdd_HHmmss"
            );

        string fileName =
            $"{defaultFileName}_{time}";

#if UNITY_EDITOR

        string path =
            EditorUtility.SaveFilePanel(
                "Save Stereo Parameters",
                PresetDirectory,
                fileName,
                "json"
            );

        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (!path.EndsWith(
            ".json",
            StringComparison.OrdinalIgnoreCase
        ))
        {
            path += ".json";
        }

        return path;

#else

        // Build版では日時付きファイル名で自動保存
        return Path.Combine(
            PresetDirectory,
            fileName + ".json"
        );

#endif
    }


    // =========================================================
    // 読込ファイル選択
    // =========================================================

    private string SelectLoadFilePath()
    {
#if UNITY_EDITOR

        return EditorUtility.OpenFilePanel(
            "Load Stereo Parameters",
            PresetDirectory,
            "json"
        );

#else

        /*
         * Build版では専用フォルダ内の
         * 最新JSONファイルを読み込む。
         */
        return FindLatestPresetPath();

#endif
    }


    // =========================================================
    // 最新の保存ファイルを取得
    // Build版のLoadで使用
    // =========================================================

    private string FindLatestPresetPath()
    {
        if (!Directory.Exists(PresetDirectory))
        {
            Debug.LogWarning(
                "保存フォルダが存在しません。\n" +
                PresetDirectory,
                this
            );

            return string.Empty;
        }

        string[] files =
            Directory.GetFiles(
                PresetDirectory,
                "*.json"
            );

        if (files.Length == 0)
        {
            Debug.LogWarning(
                "保存されたパラメータがありません。\n" +
                PresetDirectory,
                this
            );

            return string.Empty;
        }

        Array.Sort(
            files,
            (left, right) =>
                File.GetLastWriteTimeUtc(right)
                    .CompareTo(
                        File.GetLastWriteTimeUtc(left)
                    )
        );

        return files[0];
    }


    // =========================================================
    // 保存形式
    // =========================================================

    [Serializable]
    private class ParameterPreset
    {
        public int formatVersion;

        public string savedAt;

        public int shiftPixels;

        public float focalLength;

        public float baseline;

        public Vector3 stereoCameraPosition;

        public float stereoCameraRotationX;
    }
}