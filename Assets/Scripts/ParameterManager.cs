using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ParameterManager : MonoBehaviour
{
    [Header("Controllers")]
    [SerializeField] private ImageController imageController;
    [SerializeField] private UIController uiController;

    [Header("Buttons")]
    [SerializeField] private Button resetButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;

    [Header("Save Settings")]
    [SerializeField] private string presetFolderName =
        "StereoParameterPresets";
    [SerializeField] private string defaultFileName =
        "stereo_parameters";

    private ParameterPreset initialPreset;

    private string PresetDirectory =>
        Path.Combine(Application.persistentDataPath, presetFolderName);

    private void Awake()
    {
        if (imageController == null)
        {
            Debug.LogError(
                "[ParameterManager] ImageController is not assigned.", this);
            enabled = false;
            return;
        }

        Directory.CreateDirectory(PresetDirectory);
        initialPreset = CaptureCurrentParameters();
        BindButtons();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    private void BindButtons()
    {
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetParameters);
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveParameters);
        if (loadButton != null)
            loadButton.onClick.AddListener(LoadParameters);
    }

    private void UnbindButtons()
    {
        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetParameters);
        if (saveButton != null)
            saveButton.onClick.RemoveListener(SaveParameters);
        if (loadButton != null)
            loadButton.onClick.RemoveListener(LoadParameters);
    }

    public void ResetParameters()
    {
        if (initialPreset == null)
        {
            Debug.LogWarning(
                "[ParameterManager] Initial parameters not found.", this);
            return;
        }

        ApplyPreset(initialPreset);
        Debug.Log("[ParameterManager] Parameters reset.", this);
    }

    public void SaveParameters()
    {
        string path = SelectSaveFilePath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            ParameterPreset preset = CaptureCurrentParameters();

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(preset, true);
            File.WriteAllText(path, json, new UTF8Encoding(false));

            Debug.Log("[ParameterManager] Saved: " + path, this);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[ParameterManager] Save failed: " + ex, this);
        }
    }

    public void LoadParameters()
    {
        string path = SelectLoadFilePath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (!File.Exists(path))
            {
                Debug.LogError(
                    "[ParameterManager] File not found: " + path, this);
                return;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError(
                    "[ParameterManager] JSON file is empty.", this);
                return;
            }

            ParameterPreset preset =
                JsonUtility.FromJson<ParameterPreset>(json);

            if (preset == null || preset.formatVersion <= 0)
            {
                Debug.LogError(
                    "[ParameterManager] Invalid preset JSON.", this);
                return;
            }

            ApplyPreset(preset);
            Debug.Log("[ParameterManager] Loaded: " + path, this);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[ParameterManager] Load failed: " + ex, this);
        }
    }

    private ParameterPreset CaptureCurrentParameters()
    {
        return new ParameterPreset
        {
            formatVersion = 1,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            shiftPixels = imageController.shiftPixels,
            focalLength = imageController.focalLength,
            baseline = imageController.baseline,
            stereoCameraPosition =
                imageController.stereoCameraPosition,
            stereoCameraRotationX =
                imageController.stereoCameraRotationX
        };
    }

    private void ApplyPreset(ParameterPreset preset)
    {
        imageController.shiftPixels = preset.shiftPixels;
        imageController.focalLength = preset.focalLength;
        imageController.baseline = preset.baseline;
        imageController.stereoCameraPosition =
            preset.stereoCameraPosition;
        imageController.stereoCameraRotationX =
            preset.stereoCameraRotationX;

        if (uiController != null)
            uiController.RefreshFromController();
    }

    private string SelectSaveFilePath()
    {
        string fileName = defaultFileName + "_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss");

        Debug.Log("[ParameterManager] Save dialog requested.", this);

        try
        {
            string path = SFB.StandaloneFileBrowser.SaveFilePanel(
                "Save Stereo Parameters",
                PresetDirectory,
                fileName,
                "json"
            );

            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return path.EndsWith(
                ".json", StringComparison.OrdinalIgnoreCase)
                ? path
                : path + ".json";
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[ParameterManager] Save dialog failed: " + ex, this);
            return string.Empty;
        }
    }

    private string SelectLoadFilePath()
    {
        Debug.Log("[ParameterManager] Load dialog requested.", this);

        try
        {
            string[] paths = SFB.StandaloneFileBrowser.OpenFilePanel(
                "Load Stereo Parameters",
                PresetDirectory,
                "json",
                false
            );

            if (paths == null || paths.Length == 0)
                return string.Empty;

            return paths[0];
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[ParameterManager] Load dialog failed: " + ex, this);
            return string.Empty;
        }
    }

    [Serializable]
    private class ParameterPreset
    {
        public int formatVersion;
        public string savedAt;
        public float shiftPixels;
        public float focalLength;
        public float baseline;
        public Vector3 stereoCameraPosition;
        public float stereoCameraRotationX;
    }
}