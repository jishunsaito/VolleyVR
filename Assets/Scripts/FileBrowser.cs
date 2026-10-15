using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Experimentフォルダ内のJSONを一覧表示して読み込むFile Browser。
///
/// 想定ディレクトリ:
///
/// Editor:
///
/// Assets/
/// └─ Experiment/
///    ├─ 撮影条件/
///    │  ├─ camera01.json
///    │  └─ camera02.json
///    │
///    └─ ラリー/
///       ├─ rally01.json
///       └─ rally02.json
///
///
/// Build:
///
/// BuildFolder/
/// ├─ YourApplication.exe
/// ├─ YourApplication_Data/
/// └─ Experiment/
///    ├─ 撮影条件/
///    └─ ラリー/
///
///
/// 使用方法:
///
/// OpenCameraConditionBrowser()
///     -> 撮影条件JSON一覧
///
/// OpenRallyBrowser()
///     -> Rally JSON一覧
///
/// JSON Buttonを押す
///     -> 対象をLoad
///     -> Browserを閉じる
///
///
/// 注意:
///
/// BuildではAssets/Experimentは自動的にはexe横へコピーされない。
/// Build後、Experimentフォルダをexeと同じ階層へコピーする。
///
/// 後でPostBuild処理による自動コピーにも変更可能。
/// </summary>
public class FileBrowser : MonoBehaviour
{
    // ============================================================
    // Browser Mode
    // ============================================================

    private enum BrowserMode
    {
        None = 0,

        CameraCondition = 1,

        Rally = 2
    }


    private BrowserMode currentMode =
        BrowserMode.None;


    // ============================================================
    // Controllers
    // ============================================================

    [Header("Controllers")]

    [Tooltip(
        "撮影条件JSONを反映するImageController"
    )]
    [SerializeField]
    private ImageController imageController;


    [Tooltip(
        "Rally JSONを読み込むRallyController"
    )]
    [SerializeField]
    private RallyController rallyController;


    [Tooltip(
        "JSON Load後、Slider / InputField表示を同期するUIController"
    )]
    [SerializeField]
    private UIController uiController;


    // ============================================================
    // Browser UI
    // ============================================================

    [Header("Browser UI")]

    [Tooltip(
        "File Browser全体のPanel"
    )]
    [SerializeField]
    private GameObject browserPanel;


    [Tooltip(
        "例: 撮影条件を選択 / ラリーを選択"
    )]
    [SerializeField]
    private TMP_Text titleText;


    [Tooltip(
        "Fileが無い場合やErrorを表示するText。\n" +
        "未設定でも動作します。"
    )]
    [SerializeField]
    private TMP_Text statusText;


    [Tooltip(
        "ScrollView本体。\n" +
        "Load後にScroll位置を一番上へ戻すために使用します。"
    )]
    [SerializeField]
    private ScrollRect scrollRect;


    [Tooltip(
        "ScrollView / Viewport / Content のContent"
    )]
    [SerializeField]
    private RectTransform contentRoot;


    [Tooltip(
        "JSON 1個につき生成するButton Prefab。\n" +
        "子にTMP_Textを持たせてください。"
    )]
    [SerializeField]
    private Button fileButtonPrefab;


    [Tooltip(
        "Browserを閉じるCancel Button"
    )]
    [SerializeField]
    private Button cancelButton;


    [Tooltip(
        "現在表示中のDirectoryを再読み込みするButton。\n" +
        "不要なら未設定でOK。"
    )]
    [SerializeField]
    private Button refreshButton;


    // ============================================================
    // Optional Open Buttons
    // ============================================================

    [Header("Optional Open Buttons")]

    [Tooltip(
        "撮影条件Browserを開くButton。\n" +
        "UIController等ですでに同じButtonをBindしている場合は" +
        "ここには設定しないでください。"
    )]
    [SerializeField]
    private Button cameraConditionOpenButton;


    [Tooltip(
        "Rally Browserを開くButton。\n" +
        "UIControllerですでに旧Rally Load処理をBindしている場合は" +
        "二重実行になるので注意してください。"
    )]
    [SerializeField]
    private Button rallyOpenButton;


    // ============================================================
    // Directory
    // ============================================================

    [Header("Experiment Directory")]

    [Tooltip(
        "Experiment Root Folder名"
    )]
    [SerializeField]
    private string experimentFolderName =
        "Experiment";


    [Tooltip(
        "Experiment内の撮影条件Folder名"
    )]
    [SerializeField]
    private string cameraConditionFolderName =
        "撮影条件";


    [Tooltip(
        "Experiment内のRally Folder名"
    )]
    [SerializeField]
    private string rallyFolderName =
        "ラリー";


    [Tooltip(
        "ONの場合、各Folder以下のサブフォルダにあるJSONも表示します。"
    )]
    [SerializeField]
    private bool includeSubdirectories =
        true;


    [Tooltip(
        "InspectorでRoot Pathを直接指定したい場合に使用。\n" +
        "空ならEditor / Build環境から自動判定します。"
    )]
    [SerializeField]
    private string customExperimentRootPath =
        string.Empty;


    // ============================================================
    // Display
    // ============================================================

    [Header("File Display")]

    [Tooltip(
        "Buttonに.json拡張子を表示する"
    )]
    [SerializeField]
    private bool showJsonExtension =
        true;


    [Tooltip(
        "サブフォルダ検索時、Folder名もButtonへ表示する。\n" +
        "同名JSONがある場合に区別できます。"
    )]
    [SerializeField]
    private bool showRelativePath =
        true;


    [Tooltip(
        "Load成功後にBrowserを自動的に閉じる"
    )]
    [SerializeField]
    private bool closeBrowserAfterLoad =
        true;


    // ============================================================
    // Rally
    // ============================================================

    [Header("Rally Load")]

    [Tooltip(
        "Rally JSON Load直後に自動再生する。\n" +
        "通常はOFF推奨。"
    )]
    [SerializeField]
    private bool playRallyImmediatelyAfterLoad =
        false;


    // ============================================================
    // Runtime
    // ============================================================

    private readonly List<Button> generatedButtons =
        new List<Button>();


    private string currentDirectory =
        string.Empty;


    // ============================================================
    // Camera Condition JSON
    // ============================================================

    /// <summary>
    /// 既存ParameterManagerが保存している撮影条件JSON形式。
    ///
    /// 現在のParameterManagerと互換にするため、
    /// 既存保存項目だけを持つ。
    /// </summary>
    [Serializable]
    private class CameraConditionData
    {
        public int formatVersion;

        public string savedAt;

        public float shiftPixels;

        public float focalLength;

        public float baseline;

        public Vector3 stereoCameraPosition;

        public float stereoCameraRotationX;
    }


    // ============================================================
    // Public Properties
    // ============================================================

    public bool IsOpen =>
        browserPanel != null &&
        browserPanel.activeSelf;


    public string CurrentDirectory =>
        currentDirectory;


    public string ExperimentRootDirectory =>
        GetExperimentRootDirectory();


    public string CameraConditionDirectory =>
        Path.Combine(
            GetExperimentRootDirectory(),
            cameraConditionFolderName
        );


    public string RallyDirectory =>
        Path.Combine(
            GetExperimentRootDirectory(),
            rallyFolderName
        );


    // ============================================================
    // Unity
    // ============================================================

    private void Awake()
    {
        BindButtons();


        if (browserPanel != null)
        {
            browserPanel.SetActive(
                false
            );
        }
    }


    private void OnDestroy()
    {
        UnbindButtons();


        ClearFileButtons();
    }


    private void Update()
    {
        /*
         * EscapeでFile Browserを閉じる。
         */
        if (
            IsOpen &&
            Input.GetKeyDown(
                KeyCode.Escape
            )
        )
        {
            CloseBrowser();
        }
    }


    // ============================================================
    // Button Binding
    // ============================================================

    private void BindButtons()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(
                CloseBrowser
            );
        }


        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(
                RefreshCurrentDirectory
            );
        }


        if (cameraConditionOpenButton != null)
        {
            cameraConditionOpenButton
                .onClick
                .AddListener(
                    OpenCameraConditionBrowser
                );
        }


        if (rallyOpenButton != null)
        {
            rallyOpenButton
                .onClick
                .AddListener(
                    OpenRallyBrowser
                );
        }
    }


    private void UnbindButtons()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(
                CloseBrowser
            );
        }


        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(
                RefreshCurrentDirectory
            );
        }


        if (cameraConditionOpenButton != null)
        {
            cameraConditionOpenButton
                .onClick
                .RemoveListener(
                    OpenCameraConditionBrowser
                );
        }


        if (rallyOpenButton != null)
        {
            rallyOpenButton
                .onClick
                .RemoveListener(
                    OpenRallyBrowser
                );
        }
    }


    // ============================================================
    // Open Browser
    // ============================================================

    /// <summary>
    /// 撮影条件JSON一覧を表示する。
    ///
    /// UI Buttonから直接呼び出してOK。
    /// </summary>
    public void OpenCameraConditionBrowser()
    {
        currentMode =
            BrowserMode.CameraCondition;


        currentDirectory =
            CameraConditionDirectory;


        if (titleText != null)
        {
            titleText.text =
                "撮影条件を選択";
        }


        OpenBrowserPanel();


        RefreshCurrentDirectory();
    }


    /// <summary>
    /// Rally JSON一覧を表示する。
    ///
    /// UI Buttonから直接呼び出してOK。
    /// </summary>
    public void OpenRallyBrowser()
    {
        currentMode =
            BrowserMode.Rally;


        currentDirectory =
            RallyDirectory;


        if (titleText != null)
        {
            titleText.text =
                "ラリーを選択";
        }


        OpenBrowserPanel();


        RefreshCurrentDirectory();
    }


    private void OpenBrowserPanel()
    {
        if (browserPanel == null)
        {
            Debug.LogError(
                "[FileBrowser] Browser Panelが設定されていません。",
                this
            );

            return;
        }


        browserPanel.SetActive(
            true
        );


        SetStatus(
            string.Empty
        );
    }


    // ============================================================
    // Close Browser
    // ============================================================

    public void CloseBrowser()
    {
        ClearFileButtons();


        currentMode =
            BrowserMode.None;


        currentDirectory =
            string.Empty;


        SetStatus(
            string.Empty
        );


        if (browserPanel != null)
        {
            browserPanel.SetActive(
                false
            );
        }
    }


    // ============================================================
    // Refresh
    // ============================================================

    public void RefreshCurrentDirectory()
    {
        if (
            currentMode ==
            BrowserMode.None
        )
        {
            return;
        }


        ClearFileButtons();


        if (string.IsNullOrWhiteSpace(
            currentDirectory
        ))
        {
            SetStatus(
                "Directoryが設定されていません。"
            );

            return;
        }


        if (!Directory.Exists(
            currentDirectory
        ))
        {
            SetStatus(
                "Directoryが存在しません。\n\n" +
                currentDirectory
            );


            Debug.LogWarning(
                "[FileBrowser] Directoryが存在しません。\n" +
                currentDirectory,
                this
            );


            return;
        }


        SearchOption searchOption =
            includeSubdirectories
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;


        string[] jsonFiles;


        try
        {
            jsonFiles =
                Directory.GetFiles(
                    currentDirectory,
                    "*.json",
                    searchOption
                );
        }
        catch (Exception exception)
        {
            SetStatus(
                "JSON一覧の取得に失敗しました。\n\n" +
                exception.Message
            );


            Debug.LogError(
                "[FileBrowser] JSON一覧取得失敗\n" +
                exception,
                this
            );


            return;
        }


        Array.Sort(
            jsonFiles,
            StringComparer.OrdinalIgnoreCase
        );


        if (jsonFiles.Length == 0)
        {
            SetStatus(
                "JSONファイルがありません。\n\n" +
                currentDirectory
            );


            return;
        }


        if (
            contentRoot == null ||
            fileButtonPrefab == null
        )
        {
            SetStatus(
                "Content RootまたはFile Button Prefabが設定されていません。"
            );


            Debug.LogError(
                "[FileBrowser] " +
                "Content Root / File Button Prefabが設定されていません。",
                this
            );


            return;
        }


        foreach (
            string filePath
            in jsonFiles
        )
        {
            CreateFileButton(
                filePath
            );
        }


        SetStatus(
            $"{jsonFiles.Length} files"
        );


        /*
         * Layout更新後にScrollを先頭へ戻す。
         */
        Canvas.ForceUpdateCanvases();


        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition =
                1.0f;
        }
    }


    // ============================================================
    // Create File Button
    // ============================================================

    private void CreateFileButton(
        string filePath
    )
    {
        Button button =
            Instantiate(
                fileButtonPrefab,
                contentRoot
            );


        button.gameObject.SetActive(
            true
        );


        TMP_Text label =
            button.GetComponentInChildren<TMP_Text>(
                true
            );


        if (label != null)
        {
            label.text =
                GetDisplayFileName(
                    filePath
                );
        }


        /*
         * foreachのPathを明示的にlocal変数へ保存。
         */
        string capturedPath =
            filePath;


        button.onClick.AddListener(
            () =>
            {
                OnFileButtonClicked(
                    capturedPath
                );
            }
        );


        generatedButtons.Add(
            button
        );
    }


    private string GetDisplayFileName(
        string filePath
    )
    {
        string displayName;


        if (
            includeSubdirectories &&
            showRelativePath
        )
        {
            displayName =
                GetRelativePath(
                    currentDirectory,
                    filePath
                );
        }
        else
        {
            displayName =
                Path.GetFileName(
                    filePath
                );
        }


        if (!showJsonExtension)
        {
            displayName =
                Path.ChangeExtension(
                    displayName,
                    null
                );
        }


        return displayName;
    }


    // ============================================================
    // File Selected
    // ============================================================

    private void OnFileButtonClicked(
        string filePath
    )
    {
        if (string.IsNullOrWhiteSpace(
            filePath
        ))
        {
            return;
        }


        if (!File.Exists(
            filePath
        ))
        {
            SetStatus(
                "ファイルが存在しません。\n\n" +
                filePath
            );


            return;
        }


        bool loaded =
            false;


        switch (currentMode)
        {
            case BrowserMode.CameraCondition:

                loaded =
                    LoadCameraCondition(
                        filePath
                    );

                break;


            case BrowserMode.Rally:

                loaded =
                    LoadRally(
                        filePath
                    );

                break;
        }


        if (!loaded)
        {
            return;
        }


        Debug.Log(
            "[FileBrowser] Loaded\n" +
            $"Mode = {currentMode}\n" +
            $"Path = {filePath}",
            this
        );


        if (closeBrowserAfterLoad)
        {
            CloseBrowser();
        }
        else
        {
            SetStatus(
                "Loaded\n" +
                Path.GetFileName(
                    filePath
                )
            );
        }
    }


    // ============================================================
    // Rally Load
    // ============================================================

    private bool LoadRally(
        string filePath
    )
    {
        if (rallyController == null)
        {
            SetStatus(
                "RallyControllerが設定されていません。"
            );


            Debug.LogError(
                "[FileBrowser] RallyControllerが設定されていません。",
                this
            );


            return false;
        }


        bool loaded =
            rallyController.LoadRallyJsonFromPath(
                filePath,
                playRallyImmediatelyAfterLoad
            );


        if (!loaded)
        {
            SetStatus(
                "Rally JSONの読み込みに失敗しました。\n\n" +
                Path.GetFileName(
                    filePath
                )
            );


            return false;
        }


        /*
         * Rally Load後、
         * Start / End Phase、Fade、DHITなどをUIへ反映。
         */
        if (uiController != null)
        {
            uiController.RefreshFromController();
        }


        return true;
    }


    // ============================================================
    // Camera Condition Load
    // ============================================================

    private bool LoadCameraCondition(
        string filePath
    )
    {
        if (imageController == null)
        {
            SetStatus(
                "ImageControllerが設定されていません。"
            );


            Debug.LogError(
                "[FileBrowser] ImageControllerが設定されていません。",
                this
            );


            return false;
        }


        try
        {
            string json =
                File.ReadAllText(
                    filePath,
                    Encoding.UTF8
                );


            if (string.IsNullOrWhiteSpace(
                json
            ))
            {
                SetStatus(
                    "JSONファイルが空です。"
                );


                return false;
            }


            CameraConditionData data =
                JsonUtility.FromJson<CameraConditionData>(
                    json
                );


            if (data == null)
            {
                SetStatus(
                    "撮影条件JSONを読み込めませんでした。"
                );


                return false;
            }


            /*
             * 現行ParameterManagerではformatVersion > 0を
             * 有効なPresetとして扱っている。
             */
            if (data.formatVersion <= 0)
            {
                SetStatus(
                    "対応していない撮影条件JSONです。\n\n" +
                    Path.GetFileName(
                        filePath
                    )
                );


                Debug.LogWarning(
                    "[FileBrowser] " +
                    "Camera Condition formatVersionが無効です。\n" +
                    filePath,
                    this
                );


                return false;
            }


            ApplyCameraCondition(
                data
            );


            Debug.Log(
                "[FileBrowser] Camera Condition Loaded\n" +
                $"Path = {filePath}\n" +
                $"Focal Length = {data.focalLength:F3}\n" +
                $"Baseline = {data.baseline:F3}\n" +
                $"Position = {data.stereoCameraPosition}\n" +
                $"Pitch = {data.stereoCameraRotationX:F3}",
                this
            );


            return true;
        }
        catch (Exception exception)
        {
            SetStatus(
                "撮影条件JSONの読み込みに失敗しました。\n\n" +
                exception.Message
            );


            Debug.LogError(
                "[FileBrowser] Camera Condition Load Error\n" +
                $"Path = {filePath}\n" +
                exception,
                this
            );


            return false;
        }
    }


    private void ApplyCameraCondition(
        CameraConditionData data
    )
    {
        /*
         * Shift
         *
         * 現在のImageControllerはfloat Shift対応なので
         * SetShiftPixels()経由で反映する。
         *
         * DHITControllerを使用している場合は、
         * Static / Dynamic動作によってNet Center基準へ
         * 再設定されることがあります。
         */
        imageController.SetShiftPixels(
            data.shiftPixels
        );


        /*
         * Focal Length / Baseline
         *
         * ImageController側のUpdateで
         * Cameraへ適用される。
         */
        imageController.focalLength =
            data.focalLength;


        imageController.baseline =
            data.baseline;


        /*
         * Camera Positionはpublic setterを使用し、
         * Transformへ即時反映する。
         */
        imageController.SetStereoCameraPosition(
            data.stereoCameraPosition
        );


        /*
         * Pitchもsetter経由で即時反映。
         */
        imageController.SetStereoCameraPitch(
            data.stereoCameraRotationX
        );


        /*
         * Slider / InputField / Output Textを同期。
         */
        if (uiController != null)
        {
            uiController.RefreshFromController();
        }
    }


    // ============================================================
    // Experiment Root
    // ============================================================

    /// <summary>
    /// Experiment Root Directoryを返す。
    ///
    /// Editor:
    ///
    ///     Assets/Experiment
    ///
    /// Build:
    ///
    ///     exeと同じ階層/Experiment
    ///
    /// customExperimentRootPathが設定されている場合は
    /// それを最優先する。
    /// </summary>
    private string GetExperimentRootDirectory()
    {
        // ========================================================
        // Custom Path
        // ========================================================

        if (!string.IsNullOrWhiteSpace(
            customExperimentRootPath
        ))
        {
            string customPath =
                customExperimentRootPath.Trim();


            /*
             * 相対Pathの場合は、
             * Build / Project Rootからの相対として扱う。
             */
            if (!Path.IsPathRooted(
                customPath
            ))
            {
                customPath =
                    Path.Combine(
                        GetApplicationRootDirectory(),
                        customPath
                    );
            }


            return Path.GetFullPath(
                customPath
            );
        }


        // ========================================================
        // Default
        // ========================================================

#if UNITY_EDITOR

        /*
         * Unity Editor:
         *
         * Application.dataPath
         * =
         * Project/Assets
         *
         * → Assets/Experiment
         */
        string editorPath =
            Path.Combine(
                Application.dataPath,
                experimentFolderName
            );


        return Path.GetFullPath(
            editorPath
        );

#else

        /*
         * Windows Build:
         *
         * Application.dataPath
         * =
         * BuildFolder/ApplicationName_Data
         *
         * その1つ上
         * =
         * BuildFolder
         *
         * → BuildFolder/Experiment
         */
        string buildPath =
            Path.Combine(
                GetApplicationRootDirectory(),
                experimentFolderName
            );


        /*
         * exe横のExperimentが存在すれば最優先。
         */
        if (Directory.Exists(
            buildPath
        ))
        {
            return Path.GetFullPath(
                buildPath
            );
        }


        /*
         * StreamingAssetsにもExperimentがある場合は
         * fallbackとして使用。
         *
         * Windows Standaloneを想定。
         */
        string streamingPath =
            Path.Combine(
                Application.streamingAssetsPath,
                experimentFolderName
            );


        if (Directory.Exists(
            streamingPath
        ))
        {
            return Path.GetFullPath(
                streamingPath
            );
        }


        /*
         * persistentDataPathにも存在すれば
         * 最後のfallbackとして使用。
         */
        string persistentPath =
            Path.Combine(
                Application.persistentDataPath,
                experimentFolderName
            );


        if (Directory.Exists(
            persistentPath
        ))
        {
            return Path.GetFullPath(
                persistentPath
            );
        }


        /*
         * まだFolderが存在しない場合でも、
         * Error表示で期待Pathが分かるよう
         * exe横Pathを返す。
         */
        return Path.GetFullPath(
            buildPath
        );

#endif
    }


    /// <summary>
    /// EditorではProject Root、
    /// BuildではexeがあるFolderを返す。
    /// </summary>
    private static string GetApplicationRootDirectory()
    {
#if UNITY_EDITOR

        return Path.GetFullPath(
            Path.Combine(
                Application.dataPath,
                ".."
            )
        );

#else

        return Path.GetFullPath(
            Path.Combine(
                Application.dataPath,
                ".."
            )
        );

#endif
    }


    // ============================================================
    // Relative Path
    // ============================================================

    private static string GetRelativePath(
        string baseDirectory,
        string fullPath
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                baseDirectory
            ) ||
            string.IsNullOrWhiteSpace(
                fullPath
            )
        )
        {
            return fullPath;
        }


        string baseFullPath =
            Path.GetFullPath(
                baseDirectory
            )
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            )
            +
            Path.DirectorySeparatorChar;


        string fileFullPath =
            Path.GetFullPath(
                fullPath
            );


        Uri baseUri =
            new Uri(
                baseFullPath
            );


        Uri fileUri =
            new Uri(
                fileFullPath
            );


        string relativePath =
            Uri.UnescapeDataString(
                baseUri
                    .MakeRelativeUri(
                        fileUri
                    )
                    .ToString()
            );


        return relativePath.Replace(
            '/',
            Path.DirectorySeparatorChar
        );
    }


    // ============================================================
    // Clear Buttons
    // ============================================================

    private void ClearFileButtons()
    {
        foreach (
            Button button
            in generatedButtons
        )
        {
            if (button == null)
            {
                continue;
            }


            /*
             * Listenerを明示的に解除してからDestroy。
             */
            button.onClick.RemoveAllListeners();


            Destroy(
                button.gameObject
            );
        }


        generatedButtons.Clear();
    }


    // ============================================================
    // Status
    // ============================================================

    private void SetStatus(
        string message
    )
    {
        if (statusText == null)
        {
            return;
        }


        statusText.text =
            message;
    }


    // ============================================================
    // Debug
    // ============================================================

    [ContextMenu("Print Experiment Paths")]
    private void PrintExperimentPaths()
    {
        Debug.Log(
            "[FileBrowser] Experiment Paths\n" +
            $"Root = {ExperimentRootDirectory}\n" +
            $"Camera = {CameraConditionDirectory}\n" +
            $"Rally = {RallyDirectory}",
            this
        );
    }
}