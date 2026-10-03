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
    // �Q��
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
    // �ۑ��ݒ�
    // =========================================================

    [Header("Save Settings")]

    [SerializeField]
    private string presetFolderName =
        "StereoParameterPresets";

    [SerializeField]
    private string defaultFileName =
        "stereo_parameters";


    // =========================================================
    // �����f�[�^
    // =========================================================

    private ParameterPreset initialPreset;


    /// <summary>
    /// �p�����[�^�ۑ���p�f�B���N�g��
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
    // Unity�C�x���g
    // =========================================================

    private void Awake()
    {
        if (imageController == null)
        {
            Debug.LogError(
                "ParameterPresetManager��" +
                "ImageController���ݒ肳��Ă��܂���B",
                this
            );

            enabled = false;
            return;
        }

        // �ۑ��p�t�H���_���쐬
        Directory.CreateDirectory(
            PresetDirectory
        );

        // Play�J�n���̒l��Reset�p�Ƃ��ċL�^
        initialPreset =
            CaptureCurrentParameters();

        BindButtons();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }


    // =========================================================
    // Button�ڑ�
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
    /// Play�J�n���̃p�����[�^�֖߂�
    /// </summary>
    public void ResetParameters()
    {
        if (initialPreset == null)
        {
            Debug.LogWarning(
                "�����p�����[�^���L�^����Ă��܂���B",
                this
            );

            return;
        }

        ApplyPreset(initialPreset);

        Debug.Log(
            "�p�����[�^��Play�J�n���̒l�֖߂��܂����B",
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

        // �L�����Z��
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
                "�p�����[�^��ۑ����܂����B\n" +
                path,
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "�p�����[�^�̕ۑ��Ɏ��s���܂����B\n" +
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

        // �L�����Z���܂��̓t�@�C���Ȃ�
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path))
        {
            Debug.LogError(
                "�I�������t�@�C�������݂��܂���B\n" +
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
                    "�I������JSON�t�@�C������ł��B",
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
                    "JSON���p�����[�^�֕ϊ��ł��܂���ł����B",
                    this
                );

                return;
            }

            if (preset.formatVersion <= 0)
            {
                Debug.LogError(
                    "�Ή����Ă��Ȃ��p�����[�^�t�@�C���ł��B",
                    this
                );

                return;
            }

            ApplyPreset(preset);

            Debug.Log(
                "�p�����[�^��ǂݍ��݂܂����B\n" +
                path,
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "�p�����[�^�̓Ǎ��Ɏ��s���܂����B\n" +
                exception.Message,
                this
            );
        }
    }


    // =========================================================
    // ���݂̃p�����[�^���擾
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
    // �p�����[�^��ImageController�֓K�p
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

        // Slider��InputField���X�V
        if (uiController != null)
        {
            uiController.RefreshFromController();
        }
    }


    // =========================================================
    // �ۑ��t�@�C���I��
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

        // Build�łł͓����t���t�@�C�����Ŏ����ۑ�
        return Path.Combine(
            PresetDirectory,
            fileName + ".json"
        );

#endif
    }


    // =========================================================
    // �Ǎ��t�@�C���I��
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
         * Build�łł͐�p�t�H���_����
         * �ŐVJSON�t�@�C����ǂݍ��ށB
         */
        return FindLatestPresetPath();

#endif
    }


    // =========================================================
    // �ŐV�̕ۑ��t�@�C�����擾
    // Build�ł�Load�Ŏg�p
    // =========================================================

    private string FindLatestPresetPath()
    {
        if (!Directory.Exists(PresetDirectory))
        {
            Debug.LogWarning(
                "�ۑ��t�H���_�����݂��܂���B\n" +
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
                "�ۑ����ꂽ�p�����[�^������܂���B\n" +
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
    // �ۑ��`��
    // =========================================================

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