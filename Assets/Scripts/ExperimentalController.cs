using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif


/// <summary>
/// 実験全体を管理するController。
///
/// 実験進行:
///
/// READY
/// ↓ Start Button
///
/// IN / OUT
/// ↓ 全Trial終了
///
/// REST
/// ↓ 実験者がStart Button
///
/// NEAR / FAR
/// ↓ 全Trial終了
///
/// FINISH
/// ↓ 実験者がFinish Button
///
/// COMPLETED
///
///
/// Trial Source:
///
/// InspectorへFolderを直接ドラッグする。
///
/// In / Out Trial Directories
/// Near / Far Trial Directories
///
/// 各Folder内の *.json を自動的に取得する。
///
///
/// Trial生成:
///
/// Rally JSON
/// × Static / Dynamic
/// × Repeat
///
/// を生成し、Blockごとに独立してRandomizeする。
///
///
/// Response:
///
/// IN / OUT
/// Left  = In
/// Right = Out
///
/// NEAR / FAR
/// Left  = Near
/// Right = Far
///
/// Keyboard:
/// Left  = ← / F
/// Right = → / J
///
/// Gamepad:
/// Left  = DPad Left / L1 / West Button
/// Right = DPad Right / R1 / East Button
///
///
/// 正答はこのControllerでは持たない。
/// 被験者回答だけCSVへ保存し、
/// 実験終了後に正答を後付けする。
///
///
/// UI Message:
///
/// UIControllerと同様に、ScrollView内のTMP_Textへ
///
/// outputText.text = ...
///
/// で表示する。
/// </summary>
public class ExperimentalController : MonoBehaviour
{
    // ============================================================
    // Experiment Stage
    // ============================================================

    public enum ExperimentStage
    {
        Ready = 0,

        InOut = 1,

        Rest = 2,

        NearFar = 3,

        Finish = 4,

        Completed = 5,

        Aborted = 6
    }


    // ============================================================
    // Trial State
    // ============================================================

    private enum TrialState
    {
        None = 0,

        Preparing = 1,

        Playing = 2,

        AwaitingResponse = 3,

        InterTrial = 4
    }


    // ============================================================
    // Response Side
    // ============================================================

    private enum ResponseSide
    {
        Left = 0,

        Right = 1
    }


    // ============================================================
    // Runtime Trial
    // ============================================================

    private class ExperimentTrial
    {
        public ExperimentStage stage;

        public string directoryName;

        public string directoryPath;

        public string rallyFileName;

        public string rallyFilePath;

        public bool dynamicShift;

        public int repetition;
    }


    // ============================================================
    // Controller
    // ============================================================

    [Header("Controller")]

    [SerializeField]
    private RallyController rallyController;

    [SerializeField]
    private DHITController dhitController;

    [Tooltip(
        "Fade完了判定に使用する。\n" +
        "FadeAmountが1になったら回答受付を開始する。"
    )]
    [SerializeField]
    private ImageController imageController;


    // ============================================================
    // Panel
    // ============================================================

    [Header("Panel")]

    [SerializeField]
    private GameObject mainPanel;

    [SerializeField]
    private GameObject experimentPanel;


    // ============================================================
    // Buttons
    // ============================================================

    [Header("Buttons")]

    [Tooltip(
        "Main PanelからExperiment Panelを開くButton"
    )]
    [SerializeField]
    private Button experimentButton;


    [Tooltip(
        "READY時:\n" +
        "In / Out試行開始\n\n" +
        "REST時:\n" +
        "Near / Far試行開始"
    )]
    [SerializeField]
    private Button startButton;


    [Tooltip(
        "FINISH表示後に実験者が押すButton"
    )]
    [SerializeField]
    private Button finishButton;


    [Tooltip(
        "実験途中で中断するButton。\n" +
        "不要なら未設定でもよい。"
    )]
    [SerializeField]
    private Button abortButton;


    [Tooltip(
        "実験開始前にMain Panelへ戻るButton。\n" +
        "不要なら未設定でもよい。"
    )]
    [SerializeField]
    private Button backButton;


    // ============================================================
    // UI
    // ============================================================

    [Header("UI")]

    [Tooltip(
        "UIControllerのOutput Textと同じ。\n" +
        "ScrollView / Viewport / Content内のTMP_Textを設定する。"
    )]
    [SerializeField]
    private TMP_Text outputText;


    [Tooltip(
        "被験者ID入力欄。\n" +
        "未設定ならParticipantとして保存する。"
    )]
    [SerializeField]
    private TMP_InputField participantIdInputField;


    // ============================================================
    // In / Out Trial Directories
    // ============================================================

    [Header("In / Out Trial Directories")]

    [Tooltip(
        "Project WindowからFolderを直接ドラッグする。\n" +
        "Folder内のJSONをIn / Out Trialとして使用する。"
    )]
    [SerializeField]
    private List<UnityEngine.Object> inOutTrialDirectories =
        new List<UnityEngine.Object>();


    // ============================================================
    // Near / Far Trial Directories
    // ============================================================

    [Header("Near / Far Trial Directories")]

    [Tooltip(
        "Project WindowからFolderを直接ドラッグする。\n" +
        "Folder内のJSONをNear / Far Trialとして使用する。"
    )]
    [SerializeField]
    private List<UnityEngine.Object> nearFarTrialDirectories =
        new List<UnityEngine.Object>();


    // ============================================================
    // Directory Search
    // ============================================================

    [Header("Directory Search")]

    [Tooltip(
        "ONなら指定Folder以下のサブFolderも検索する。\n" +
        "通常はOFF推奨。"
    )]
    [SerializeField]
    private bool includeSubdirectories =
        false;


    [Tooltip(
        "Rally JSONではないため読み込まないJSONファイル名。"
    )]
    [SerializeField]
    private List<string> excludedJsonFileNames =
        new List<string>()
        {
            "manifest.json"
        };


    // ============================================================
    // Trial Generation
    // ============================================================

    [Header("Trial Generation")]

    [Tooltip(
        "各JSONを何回繰り返すか。"
    )]
    [Min(1)]
    [SerializeField]
    private int repeatCount =
        1;


    [Tooltip(
        "Static条件をTrialとして生成する。"
    )]
    [SerializeField]
    private bool includeStaticCondition =
        true;


    [Tooltip(
        "Dynamic条件をTrialとして生成する。"
    )]
    [SerializeField]
    private bool includeDynamicCondition =
        true;


    [Tooltip(
        "JSON内部のStatic / Dynamic状態より、" +
        "ExperimentalControllerのTrial条件を優先する。"
    )]
    [SerializeField]
    private bool overrideShiftModeAfterJsonLoad =
        true;


    [Tooltip(
        "実験中はExperiment Fadeを強制的にONにする。"
    )]
    [SerializeField]
    private bool forceExperimentFadeEnabled =
        true;


    // ============================================================
    // Randomization
    // ============================================================

    [Header("Randomization")]

    [Tooltip(
        "0ならSession開始時に自動生成する。\n" +
        "固定値なら同じTrial順を再現できる。"
    )]
    [SerializeField]
    private int randomSeed =
        0;


    [Tooltip(
        "同じStatic / Dynamic条件が連続してよい最大数。\n" +
        "0以下なら制限なし。"
    )]
    [SerializeField]
    private int maxConsecutiveSameShiftMode =
        3;


    [Tooltip(
        "同じRally JSONが連続することを避ける。"
    )]
    [SerializeField]
    private bool avoidSameRallyConsecutive =
        true;


    [Min(1)]
    [SerializeField]
    private int maxShuffleAttempts =
        500;


    // ============================================================
    // Timing
    // ============================================================

    [Header("Timing")]

    [Tooltip(
        "JSON Load後からRally開始までの待ち時間 [s]"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float trialPreparationDelaySeconds =
        0.05f;


    [Tooltip(
        "回答後から次Trial開始までの時間 [s]"
    )]
    [Min(0.0f)]
    [SerializeField]
    private float interTrialDelaySeconds =
        0.35f;


    [Tooltip(
        "FadeAmount=1を待つ最大時間 [s]。\n" +
        "Fade設定ミスで無限待機しないためのSafety Timeout。"
    )]
    [Min(1.0f)]
    [SerializeField]
    private float fadeWaitTimeoutSeconds =
        30.0f;


    // ============================================================
    // Response Labels
    // ============================================================

    [Header("Response Labels")]

    [Tooltip(
        "In / Out試行で左入力したときの回答名"
    )]
    [SerializeField]
    private string inOutLeftResponseLabel =
        "In";


    [Tooltip(
        "In / Out試行で右入力したときの回答名"
    )]
    [SerializeField]
    private string inOutRightResponseLabel =
        "Out";


    [Tooltip(
        "Near / Far試行で左入力したときの回答名"
    )]
    [SerializeField]
    private string nearFarLeftResponseLabel =
        "Near";


    [Tooltip(
        "Near / Far試行で右入力したときの回答名"
    )]
    [SerializeField]
    private string nearFarRightResponseLabel =
        "Far";


    // ============================================================
    // Input
    // ============================================================

    [Header("Response Input")]

    [Tooltip(
        "Keyboard回答を有効にする。\n" +
        "Left  = ← / F\n" +
        "Right = → / J"
    )]
    [SerializeField]
    private bool enableKeyboardResponse =
        true;


    [Tooltip(
        "Gamepad回答を有効にする。\n" +
        "Left  = DPad Left / L1 / West Button\n" +
        "Right = DPad Right / R1 / East Button"
    )]
    [SerializeField]
    private bool enableGamepadResponse =
        true;


    // ============================================================
    // CSV
    // ============================================================

    [Header("CSV")]

    [Tooltip(
        "Application.persistentDataPathからの相対Folder"
    )]
    [SerializeField]
    private string resultRelativeFolder =
        "ExperimentResults";


    // ============================================================
    // Finish
    // ============================================================

    [Header("Finish")]

    [Tooltip(
        "Finish Button後にExperiment Panelを閉じ、" +
        "Main Panelへ戻る。"
    )]
    [SerializeField]
    private bool returnToMainPanelOnFinish =
        true;


    // ============================================================
    // Runtime
    // ============================================================

    private ExperimentStage currentStage =
        ExperimentStage.Ready;


    private TrialState currentTrialState =
        TrialState.None;


    private readonly List<ExperimentTrial> inOutTrials =
        new List<ExperimentTrial>();


    private readonly List<ExperimentTrial> nearFarTrials =
        new List<ExperimentTrial>();


    private List<ExperimentTrial> currentTrialPool;


    private int currentBlockTrialIndex =
        -1;


    private int globalCompletedTrialCount =
        0;


    private int totalExperimentTrialCount =
        0;


    private int currentSessionSeed =
        0;


    private int inOutBlockSeed =
        0;


    private int nearFarBlockSeed =
        0;


    private double responseStartRealtime =
        0.0;


    private string resultCsvPath =
        string.Empty;


    private Coroutine trialCoroutine;


    // ============================================================
    // Properties
    // ============================================================

    public ExperimentStage CurrentStage =>
        currentStage;


    public bool IsRunning =>
        currentStage ==
        ExperimentStage.InOut ||

        currentStage ==
        ExperimentStage.NearFar;


    public string ResultCsvPath =>
        resultCsvPath;


    // ============================================================
    // Unity
    // ============================================================

    private void Start()
    {
        if (rallyController == null)
        {
            Debug.LogError(
                "[ExperimentalController] " +
                "RallyControllerが設定されていません。",
                this
            );

            enabled =
                false;

            return;
        }


        if (dhitController == null)
        {
            Debug.LogError(
                "[ExperimentalController] " +
                "DHITControllerが設定されていません。",
                this
            );

            enabled =
                false;

            return;
        }


        if (imageController == null)
        {
            Debug.LogError(
                "[ExperimentalController] " +
                "ImageControllerが設定されていません。",
                this
            );

            enabled =
                false;

            return;
        }


        BindButtons();


        SetStage(
            ExperimentStage.Ready
        );


        /*
         * ExperimentalController自体は
         * ExperimentPanelの外に置いておく。
         */
        if (experimentPanel != null)
        {
            experimentPanel.SetActive(
                false
            );
        }
    }


    private void Update()
    {
        if (
            currentTrialState !=
            TrialState.AwaitingResponse
        )
        {
            return;
        }


        ReadResponseInput();
    }


    private void OnDestroy()
    {
        UnbindButtons();

        StopTrialCoroutine();
    }


    // ============================================================
    // Button Bind
    // ============================================================

    private void BindButtons()
    {
        if (experimentButton != null)
        {
            experimentButton.onClick.AddListener(
                OpenExperimentPanel
            );
        }


        if (startButton != null)
        {
            startButton.onClick.AddListener(
                OnStartButtonClicked
            );
        }


        if (finishButton != null)
        {
            finishButton.onClick.AddListener(
                OnFinishButtonClicked
            );
        }


        if (abortButton != null)
        {
            abortButton.onClick.AddListener(
                AbortExperiment
            );
        }


        if (backButton != null)
        {
            backButton.onClick.AddListener(
                BackToMainPanel
            );
        }
    }


    private void UnbindButtons()
    {
        if (experimentButton != null)
        {
            experimentButton.onClick.RemoveListener(
                OpenExperimentPanel
            );
        }


        if (startButton != null)
        {
            startButton.onClick.RemoveListener(
                OnStartButtonClicked
            );
        }


        if (finishButton != null)
        {
            finishButton.onClick.RemoveListener(
                OnFinishButtonClicked
            );
        }


        if (abortButton != null)
        {
            abortButton.onClick.RemoveListener(
                AbortExperiment
            );
        }


        if (backButton != null)
        {
            backButton.onClick.RemoveListener(
                BackToMainPanel
            );
        }
    }


    // ============================================================
    // Panel
    // ============================================================

    public void OpenExperimentPanel()
    {
        if (IsRunning)
        {
            return;
        }


        if (mainPanel != null)
        {
            mainPanel.SetActive(
                false
            );
        }


        if (experimentPanel != null)
        {
            experimentPanel.SetActive(
                true
            );
        }


        if (
            currentStage ==
            ExperimentStage.Completed ||

            currentStage ==
            ExperimentStage.Aborted
        )
        {
            ResetExperimentSession();
        }
        else
        {
            RefreshButtonState();

            RefreshOutputText();
        }
    }


    public void BackToMainPanel()
    {
        /*
         * Session開始後は誤ってPanelを閉じない。
         */
        if (
            currentStage !=
            ExperimentStage.Ready
        )
        {
            return;
        }


        if (experimentPanel != null)
        {
            experimentPanel.SetActive(
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
    // Start Button
    // ============================================================

    private void OnStartButtonClicked()
    {
        switch (currentStage)
        {
            case ExperimentStage.Ready:

                StartExperimentSession();

                break;


            case ExperimentStage.Rest:

                StartNearFarBlock();

                break;
        }
    }


    // ============================================================
    // Experiment Session
    // ============================================================

    private void StartExperimentSession()
    {
        if (!ValidateExperimentSettings(
            out string validationError
        ))
        {
            ShowError(
                validationError
            );

            return;
        }


        inOutTrials.Clear();

        nearFarTrials.Clear();


        currentSessionSeed =
            randomSeed != 0
                ? randomSeed
                : CreateRuntimeSeed();


        /*
         * 同一Session Seedから
         * Blockごとに異なるSeedを作る。
         */
        inOutBlockSeed =
            unchecked(
                currentSessionSeed ^
                0x13572468
            );


        nearFarBlockSeed =
            unchecked(
                currentSessionSeed ^
                0x24681357
            );


        if (!BuildTrialPool(
            ExperimentStage.InOut,
            inOutTrialDirectories,
            inOutTrials,
            out string inOutError
        ))
        {
            ShowError(
                inOutError
            );

            return;
        }


        if (!BuildTrialPool(
            ExperimentStage.NearFar,
            nearFarTrialDirectories,
            nearFarTrials,
            out string nearFarError
        ))
        {
            ShowError(
                nearFarError
            );

            return;
        }


        RandomizeTrialPool(
            inOutTrials,
            inOutBlockSeed
        );


        RandomizeTrialPool(
            nearFarTrials,
            nearFarBlockSeed
        );


        totalExperimentTrialCount =
            inOutTrials.Count +
            nearFarTrials.Count;


        globalCompletedTrialCount =
            0;


        if (!CreateResultCsv())
        {
            ShowError(
                "CSVファイルを作成できませんでした。\n" +
                "Consoleを確認してください。"
            );

            return;
        }


        Debug.Log(
            "[ExperimentalController] Experiment Start\n" +
            $"Participant = {GetParticipantId()}\n" +
            $"InOut = {inOutTrials.Count}\n" +
            $"NearFar = {nearFarTrials.Count}\n" +
            $"Total = {totalExperimentTrialCount}\n" +
            $"Seed = {currentSessionSeed}\n" +
            $"CSV = {resultCsvPath}"
        );


        StartInOutBlock();
    }


    // ============================================================
    // IN / OUT
    // ============================================================

    private void StartInOutBlock()
    {
        currentStage =
            ExperimentStage.InOut;


        currentTrialPool =
            inOutTrials;


        currentBlockTrialIndex =
            -1;


        currentTrialState =
            TrialState.None;


        RefreshButtonState();

        RefreshOutputText();


        StartNextTrial();
    }


    // ============================================================
    // REST
    // ============================================================

    private void EnterRest()
    {
        StopTrialCoroutine();


        rallyController.ResetExperimentFade();


        currentStage =
            ExperimentStage.Rest;


        currentTrialState =
            TrialState.None;


        currentTrialPool =
            null;


        RefreshButtonState();

        RefreshOutputText();


        Debug.Log(
            "[ExperimentalController] REST"
        );
    }


    // ============================================================
    // NEAR / FAR
    // ============================================================

    private void StartNearFarBlock()
    {
        currentStage =
            ExperimentStage.NearFar;


        currentTrialPool =
            nearFarTrials;


        currentBlockTrialIndex =
            -1;


        currentTrialState =
            TrialState.None;


        RefreshButtonState();

        RefreshOutputText();


        StartNextTrial();
    }


    // ============================================================
    // FINISH
    // ============================================================

    private void EnterFinish()
    {
        StopTrialCoroutine();


        rallyController.ResetExperimentFade();


        currentStage =
            ExperimentStage.Finish;


        currentTrialState =
            TrialState.None;


        currentTrialPool =
            null;


        RefreshButtonState();

        RefreshOutputText();


        Debug.Log(
            "[ExperimentalController] FINISH\n" +
            $"CSV = {resultCsvPath}"
        );
    }


    private void OnFinishButtonClicked()
    {
        if (
            currentStage !=
            ExperimentStage.Finish
        )
        {
            return;
        }


        currentStage =
            ExperimentStage.Completed;


        currentTrialState =
            TrialState.None;


        RefreshButtonState();

        RefreshOutputText();


        Debug.Log(
            "[ExperimentalController] COMPLETED\n" +
            $"CSV = {resultCsvPath}"
        );


        if (returnToMainPanelOnFinish)
        {
            if (experimentPanel != null)
            {
                experimentPanel.SetActive(
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
    }


    // ============================================================
    // Trial Pool
    // ============================================================

    private bool BuildTrialPool(
        ExperimentStage stage,
        List<UnityEngine.Object> directoryObjects,
        List<ExperimentTrial> destination,
        out string error
    )
    {
        error =
            string.Empty;


        destination.Clear();


        if (
            directoryObjects == null ||
            directoryObjects.Count <= 0
        )
        {
            error =
                $"{GetStageName(stage)}のDirectoryがありません。";

            return false;
        }


        HashSet<string> registeredJsonPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );


        foreach (
            UnityEngine.Object directoryObject
            in directoryObjects
        )
        {
            if (!TryGetDirectoryPath(
                directoryObject,
                out string directoryPath,
                out string directoryName,
                out string directoryError
            ))
            {
                error =
                    directoryError;

                return false;
            }


            SearchOption searchOption =
                includeSubdirectories
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;


            string[] jsonPaths =
                Directory.GetFiles(
                    directoryPath,
                    "*.json",
                    searchOption
                );


            Array.Sort(
                jsonPaths,
                StringComparer.OrdinalIgnoreCase
            );


            foreach (
                string jsonPath
                in jsonPaths
            )
            {
                string fileName =
                    Path.GetFileName(
                        jsonPath
                    );


                if (IsExcludedJsonFile(
                    fileName
                ))
                {
                    continue;
                }


                string fullPath =
                    Path.GetFullPath(
                        jsonPath
                    );


                /*
                 * 同じFolderを2回入れた場合や、
                 * 親Folder + 子Folderを入れた場合の
                 * 二重登録を防ぐ。
                 */
                if (!registeredJsonPaths.Add(
                    fullPath
                ))
                {
                    continue;
                }


                for (
                    int repetition = 1;
                    repetition <= Mathf.Max(
                        1,
                        repeatCount
                    );
                    repetition++
                )
                {
                    if (includeStaticCondition)
                    {
                        destination.Add(
                            CreateTrial(
                                stage,
                                directoryName,
                                directoryPath,
                                fileName,
                                fullPath,
                                false,
                                repetition
                            )
                        );
                    }


                    if (includeDynamicCondition)
                    {
                        destination.Add(
                            CreateTrial(
                                stage,
                                directoryName,
                                directoryPath,
                                fileName,
                                fullPath,
                                true,
                                repetition
                            )
                        );
                    }
                }
            }
        }


        if (destination.Count <= 0)
        {
            error =
                $"{GetStageName(stage)}のDirectory内に" +
                "Rally JSONがありません。";

            return false;
        }


        return true;
    }


    private static ExperimentTrial CreateTrial(
        ExperimentStage stage,
        string directoryName,
        string directoryPath,
        string rallyFileName,
        string rallyFilePath,
        bool dynamicShift,
        int repetition
    )
    {
        return
            new ExperimentTrial
            {
                stage =
                    stage,

                directoryName =
                    directoryName,

                directoryPath =
                    directoryPath,

                rallyFileName =
                    rallyFileName,

                rallyFilePath =
                    rallyFilePath,

                dynamicShift =
                    dynamicShift,

                repetition =
                    repetition
            };
    }


    // ============================================================
    // Directory Object -> Path
    // ============================================================

    private bool TryGetDirectoryPath(
        UnityEngine.Object directoryObject,
        out string absoluteDirectoryPath,
        out string directoryName,
        out string error
    )
    {
        absoluteDirectoryPath =
            string.Empty;


        directoryName =
            string.Empty;


        error =
            string.Empty;


        if (directoryObject == null)
        {
            error =
                "Directory Listにnullがあります。";

            return false;
        }


#if UNITY_EDITOR

        string assetPath =
            AssetDatabase.GetAssetPath(
                directoryObject
            );


        if (string.IsNullOrWhiteSpace(
            assetPath
        ))
        {
            error =
                "Asset Pathを取得できません。\n" +
                directoryObject.name;

            return false;
        }


        if (!AssetDatabase.IsValidFolder(
            assetPath
        ))
        {
            error =
                "Directory欄にはFolderを入れてください。\n" +
                $"現在: {assetPath}";

            return false;
        }


        /*
         * Application.dataPath:
         *
         * .../Project/Assets
         *
         * なので1つ上がProject Root。
         */
        string projectRoot =
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    ".."
                )
            );


        absoluteDirectoryPath =
            Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath
                )
            );


        directoryName =
            Path.GetFileName(
                absoluteDirectoryPath
            );


        if (!Directory.Exists(
            absoluteDirectoryPath
        ))
        {
            error =
                "Directoryが存在しません。\n" +
                absoluteDirectoryPath;

            return false;
        }


        return true;

#else

        error =
            "Folder Drag & Drop方式はUnity Editor実行用です。\n" +
            "Build版ではStreamingAssets方式が必要です。";

        return false;

#endif
    }


    private bool IsExcludedJsonFile(
        string fileName
    )
    {
        if (string.IsNullOrWhiteSpace(
            fileName
        ))
        {
            return true;
        }


        if (excludedJsonFileNames == null)
        {
            return false;
        }


        foreach (
            string excludedFileName
            in excludedJsonFileNames
        )
        {
            if (string.IsNullOrWhiteSpace(
                excludedFileName
            ))
            {
                continue;
            }


            if (string.Equals(
                fileName,
                excludedFileName.Trim(),
                StringComparison.OrdinalIgnoreCase
            ))
            {
                return true;
            }
        }


        return false;
    }


    // ============================================================
    // Randomization
    // ============================================================

    private void RandomizeTrialPool(
        List<ExperimentTrial> trials,
        int seed
    )
    {
        if (
            trials == null ||
            trials.Count <= 1
        )
        {
            return;
        }


        List<ExperimentTrial> original =
            new List<ExperimentTrial>(
                trials
            );


        List<ExperimentTrial> candidate =
            null;


        System.Random random =
            new System.Random(
                seed
            );


        int attempts =
            Mathf.Max(
                1,
                maxShuffleAttempts
            );


        for (
            int attempt = 0;
            attempt < attempts;
            attempt++
        )
        {
            candidate =
                new List<ExperimentTrial>(
                    original
                );


            FisherYatesShuffle(
                candidate,
                random
            );


            if (IsRandomizedSequenceValid(
                candidate
            ))
            {
                trials.Clear();


                trials.AddRange(
                    candidate
                );


                return;
            }
        }


        /*
         * 条件数の関係で制約を満たせない場合でも
         * 実験自体は止めない。
         */
        trials.Clear();


        if (candidate != null)
        {
            trials.AddRange(
                candidate
            );
        }
        else
        {
            trials.AddRange(
                original
            );
        }


        Debug.LogWarning(
            "[ExperimentalController] " +
            "Randomize制約を完全には満たせませんでした。"
        );
    }


    private static void FisherYatesShuffle(
        List<ExperimentTrial> trials,
        System.Random random
    )
    {
        for (
            int i = trials.Count - 1;
            i > 0;
            i--
        )
        {
            int j =
                random.Next(
                    0,
                    i + 1
                );


            ExperimentTrial temp =
                trials[i];


            trials[i] =
                trials[j];


            trials[j] =
                temp;
        }
    }


    private bool IsRandomizedSequenceValid(
        List<ExperimentTrial> trials
    )
    {
        if (trials.Count <= 1)
        {
            return true;
        }


        int sameShiftCount =
            1;


        for (
            int i = 1;
            i < trials.Count;
            i++
        )
        {
            ExperimentTrial previous =
                trials[i - 1];


            ExperimentTrial current =
                trials[i];


            if (
                avoidSameRallyConsecutive &&

                string.Equals(
                    previous.rallyFilePath,
                    current.rallyFilePath,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return false;
            }


            if (
                previous.dynamicShift ==
                current.dynamicShift
            )
            {
                sameShiftCount++;
            }
            else
            {
                sameShiftCount =
                    1;
            }


            if (
                maxConsecutiveSameShiftMode > 0 &&

                sameShiftCount >
                maxConsecutiveSameShiftMode
            )
            {
                return false;
            }
        }


        return true;
    }


    // ============================================================
    // Trial Sequence
    // ============================================================

    private void StartNextTrial()
    {
        StopTrialCoroutine();


        if (currentTrialPool == null)
        {
            return;
        }


        currentBlockTrialIndex++;


        if (
            currentBlockTrialIndex >=
            currentTrialPool.Count
        )
        {
            CompleteCurrentBlock();

            return;
        }


        trialCoroutine =
            StartCoroutine(
                PrepareAndStartCurrentTrial()
            );
    }


    private IEnumerator PrepareAndStartCurrentTrial()
    {
        ExperimentTrial trial =
            GetCurrentTrial();


        if (trial == null)
        {
            AbortExperimentWithMessage(
                "現在のTrialを取得できません。"
            );

            yield break;
        }


        // ========================================================
        // Preparing
        // ========================================================

        currentTrialState =
            TrialState.Preparing;


        RefreshOutputText();


        /*
         * Rally JSONを先にLoadする。
         *
         * JSONにはStatic / Dynamic設定が保存されているので、
         * Shift Mode上書きより必ず先。
         */
        bool loaded =
            rallyController.LoadRallyJsonFromPath(
                trial.rallyFilePath,
                false
            );


        if (!loaded)
        {
            AbortExperimentWithMessage(
                "Rally JSONの読み込みに失敗しました。\n" +
                trial.rallyFilePath
            );

            yield break;
        }


        // ========================================================
        // Static / Dynamic Override
        // ========================================================

        if (overrideShiftModeAfterJsonLoad)
        {
            dhitController.SetDynamicEnabled(
                trial.dynamicShift
            );
        }


        // ========================================================
        // Experiment Fade
        // ========================================================

        if (forceExperimentFadeEnabled)
        {
            rallyController.SetExperimentFadeEnabled(
                true
            );
        }


        /*
         * Load時にもResetされるが、
         * Trial開始前に明示的にFade=0へ戻す。
         */
        rallyController.ResetExperimentFade();


        // ========================================================
        // Preparation Delay
        // ========================================================

        if (
            trialPreparationDelaySeconds >
            0.0f
        )
        {
            yield return
                new WaitForSecondsRealtime(
                    trialPreparationDelaySeconds
                );
        }
        else
        {
            yield return null;
        }


        if (!IsRunning)
        {
            yield break;
        }


        // ========================================================
        // Playing
        // ========================================================

        currentTrialState =
            TrialState.Playing;


        RefreshOutputText();


        Debug.Log(
            "[ExperimentalController] Trial Start\n" +
            $"Stage = {GetStageName(currentStage)}\n" +
            $"Trial = {currentBlockTrialIndex + 1}" +
            $" / {currentTrialPool.Count}\n" +
            $"Directory = {trial.directoryName}\n" +
            $"Rally = {trial.rallyFileName}\n" +
            $"Shift = {(trial.dynamicShift ? "Dynamic" : "Static")}\n" +
            $"Repeat = {trial.repetition}"
        );


        rallyController.StartSelectedSimulation();


        // ========================================================
        // Wait For Fade Complete
        // ========================================================

        /*
         * RallyがどのPhaseから始まり、
         * End Phaseまで何秒かかるかはTrialごとに違う。
         *
         * そのため「FadeDelay秒待つ」のではなく、
         * ImageController.FadeAmountそのものを監視する。
         *
         * FadeAmount >= 1
         * = 完全遮蔽
         *
         * になった瞬間から回答RTを開始する。
         */

        double fadeWaitStartTime =
            Time.realtimeSinceStartupAsDouble;


        while (
            imageController.FadeAmount <
            0.999f
        )
        {
            if (
                !IsRunning ||

                currentTrialState !=
                TrialState.Playing
            )
            {
                yield break;
            }


            double elapsed =
                Time.realtimeSinceStartupAsDouble -
                fadeWaitStartTime;


            if (
                fadeWaitTimeoutSeconds > 0.0f &&

                elapsed >
                fadeWaitTimeoutSeconds
            )
            {
                AbortExperimentWithMessage(
                    "Fadeが完了しませんでした。\n\n" +
                    "Rally JSONのEnd Phase / Fade設定を確認してください。"
                );

                yield break;
            }


            yield return null;
        }


        // ========================================================
        // Awaiting Response
        // ========================================================

        responseStartRealtime =
            Time.realtimeSinceStartupAsDouble;


        currentTrialState =
            TrialState.AwaitingResponse;


        RefreshOutputText();


        trialCoroutine =
            null;
    }


    private void CompleteCurrentBlock()
    {
        currentTrialState =
            TrialState.None;


        if (
            currentStage ==
            ExperimentStage.InOut
        )
        {
            EnterRest();

            return;
        }


        if (
            currentStage ==
            ExperimentStage.NearFar
        )
        {
            EnterFinish();
        }
    }


    // ============================================================
    // Keyboard / Controller Input
    // ============================================================

    private void ReadResponseInput()
    {
        bool leftPressed =
            false;


        bool rightPressed =
            false;


#if ENABLE_INPUT_SYSTEM

        // ========================================================
        // New Input System
        // ========================================================

        if (enableKeyboardResponse)
        {
            Keyboard keyboard =
                Keyboard.current;


            if (keyboard != null)
            {
                leftPressed =
                    keyboard.leftArrowKey.wasPressedThisFrame ||
                    keyboard.fKey.wasPressedThisFrame;


                rightPressed =
                    keyboard.rightArrowKey.wasPressedThisFrame ||
                    keyboard.jKey.wasPressedThisFrame;
            }
        }


        if (enableGamepadResponse)
        {
            Gamepad gamepad =
                Gamepad.current;


            if (gamepad != null)
            {
                leftPressed =
                    leftPressed ||

                    gamepad.dpad.left.wasPressedThisFrame ||

                    gamepad.leftShoulder.wasPressedThisFrame ||

                    gamepad.buttonWest.wasPressedThisFrame;


                rightPressed =
                    rightPressed ||

                    gamepad.dpad.right.wasPressedThisFrame ||

                    gamepad.rightShoulder.wasPressedThisFrame ||

                    gamepad.buttonEast.wasPressedThisFrame;
            }
        }

#else

        // ========================================================
        // Legacy Input System
        // ========================================================

        if (enableKeyboardResponse)
        {
            leftPressed =
                Input.GetKeyDown(
                    KeyCode.LeftArrow
                ) ||

                Input.GetKeyDown(
                    KeyCode.F
                );


            rightPressed =
                Input.GetKeyDown(
                    KeyCode.RightArrow
                ) ||

                Input.GetKeyDown(
                    KeyCode.J
                );
        }


        if (enableGamepadResponse)
        {
            leftPressed =
                leftPressed ||

                Input.GetKeyDown(
                    KeyCode.JoystickButton2
                ) ||

                Input.GetKeyDown(
                    KeyCode.JoystickButton4
                );


            rightPressed =
                rightPressed ||

                Input.GetKeyDown(
                    KeyCode.JoystickButton1
                ) ||

                Input.GetKeyDown(
                    KeyCode.JoystickButton5
                );
        }

#endif


        /*
         * 左右同時入力は採用しない。
         */
        if (
            leftPressed &&
            rightPressed
        )
        {
            return;
        }


        if (leftPressed)
        {
            SubmitResponse(
                ResponseSide.Left
            );

            return;
        }


        if (rightPressed)
        {
            SubmitResponse(
                ResponseSide.Right
            );
        }
    }


    // ============================================================
    // Response
    // ============================================================

    private void SubmitResponse(
        ResponseSide responseSide
    )
    {
        if (
            currentTrialState !=
            TrialState.AwaitingResponse
        )
        {
            return;
        }


        ExperimentTrial trial =
            GetCurrentTrial();


        if (trial == null)
        {
            return;
        }


        /*
         * 二重入力防止。
         */
        currentTrialState =
            TrialState.InterTrial;


        double responseEndTime =
            Time.realtimeSinceStartupAsDouble;


        double responseTimeSeconds =
            Math.Max(
                0.0,
                responseEndTime -
                responseStartRealtime
            );


        string responseLabel =
            GetResponseLabel(
                trial.stage,
                responseSide
            );


        globalCompletedTrialCount++;


        AppendTrialResultToCsv(
            trial,
            responseSide,
            responseLabel,
            responseTimeSeconds
        );


        Debug.Log(
            "[ExperimentalController] Response\n" +
            $"Stage = {GetStageName(trial.stage)}\n" +
            $"Rally = {trial.rallyFileName}\n" +
            $"Side = {responseSide}\n" +
            $"Response = {responseLabel}\n" +
            $"RT = {responseTimeSeconds * 1000.0:F3} ms"
        );


        RefreshOutputText();


        StopTrialCoroutine();


        trialCoroutine =
            StartCoroutine(
                MoveToNextTrialAfterDelay()
            );
    }


    private IEnumerator MoveToNextTrialAfterDelay()
    {
        if (
            interTrialDelaySeconds >
            0.0f
        )
        {
            yield return
                new WaitForSecondsRealtime(
                    interTrialDelaySeconds
                );
        }


        trialCoroutine =
            null;


        if (!IsRunning)
        {
            yield break;
        }


        StartNextTrial();
    }


    private string GetResponseLabel(
        ExperimentStage stage,
        ResponseSide side
    )
    {
        if (
            stage ==
            ExperimentStage.InOut
        )
        {
            return
                side ==
                ResponseSide.Left
                    ? inOutLeftResponseLabel
                    : inOutRightResponseLabel;
        }


        if (
            stage ==
            ExperimentStage.NearFar
        )
        {
            return
                side ==
                ResponseSide.Left
                    ? nearFarLeftResponseLabel
                    : nearFarRightResponseLabel;
        }


        return
            side.ToString();
    }


    // ============================================================
    // CSV
    // ============================================================

    private bool CreateResultCsv()
    {
        try
        {
            string resultDirectory =
                GetResultDirectory();


            Directory.CreateDirectory(
                resultDirectory
            );


            string participantId =
                MakeSafeFileName(
                    GetParticipantId()
                );


            string timestamp =
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss",
                    CultureInfo.InvariantCulture
                );


            string fileName =
                participantId +
                "_" +
                timestamp +
                "_seed" +
                currentSessionSeed +
                ".csv";


            resultCsvPath =
                Path.Combine(
                    resultDirectory,
                    fileName
                );


            string[] headerColumns =
            {
                "participant_id",

                "stage",

                "block_trial_index",

                "block_trial_total",

                "global_trial_index",

                "global_trial_total",

                "source_directory",

                "rally_file",

                "shift_mode",

                "response_side",

                "response",

                "response_time_ms",

                "repetition",

                "session_seed",

                "block_seed",

                "start_phase",

                "end_phase",

                "toss_side",

                "spike_course",

                "toss_target_z_offset_m",

                "spike_left_distance_r_m",

                "spike_right_distance_r_m",

                "base_shift_pixels",

                "timestamp"
            };


            File.WriteAllText(
                resultCsvPath,

                string.Join(
                    ",",
                    headerColumns
                ) +
                Environment.NewLine,

                new UTF8Encoding(
                    true
                )
            );


            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[ExperimentalController] CSV作成失敗\n" +
                exception
            );


            return false;
        }
    }


    private void AppendTrialResultToCsv(
        ExperimentTrial trial,
        ResponseSide responseSide,
        string responseLabel,
        double responseTimeSeconds
    )
    {
        if (string.IsNullOrWhiteSpace(
            resultCsvPath
        ))
        {
            return;
        }


        try
        {
            int blockSeed =
                trial.stage ==
                ExperimentStage.InOut
                    ? inOutBlockSeed
                    : nearFarBlockSeed;


            string[] columns =
            {
                GetParticipantId(),


                GetStageCsvName(
                    trial.stage
                ),


                (currentBlockTrialIndex + 1)
                    .ToString(
                        CultureInfo.InvariantCulture
                    ),


                currentTrialPool.Count
                    .ToString(
                        CultureInfo.InvariantCulture
                    ),


                globalCompletedTrialCount
                    .ToString(
                        CultureInfo.InvariantCulture
                    ),


                totalExperimentTrialCount
                    .ToString(
                        CultureInfo.InvariantCulture
                    ),


                trial.directoryName,


                trial.rallyFileName,


                trial.dynamicShift
                    ? "Dynamic"
                    : "Static",


                responseSide.ToString(),


                responseLabel,


                (responseTimeSeconds * 1000.0)
                    .ToString(
                        "F3",
                        CultureInfo.InvariantCulture
                    ),


                trial.repetition
                    .ToString(
                        CultureInfo.InvariantCulture
                    ),


                currentSessionSeed
                    .ToString(
                        CultureInfo.InvariantCulture
                    ),


                blockSeed
                    .ToString(
                        CultureInfo.InvariantCulture
                    ),


                rallyController.StartPhase.ToString(),


                rallyController.EndPhase.ToString(),


                rallyController.CurrentTossSide.ToString(),


                rallyController.CurrentSpikeCourse.ToString(),


                rallyController.TossTargetZOffset
                    .ToString(
                        "F6",
                        CultureInfo.InvariantCulture
                    ),


                rallyController
                    .GetSpikeTargetDistanceOffset(
                        0
                    )
                    .ToString(
                        "F6",
                        CultureInfo.InvariantCulture
                    ),


                rallyController
                    .GetSpikeTargetDistanceOffset(
                        1
                    )
                    .ToString(
                        "F6",
                        CultureInfo.InvariantCulture
                    ),


                rallyController.BaseShiftPixels
                    .ToString(
                        "F6",
                        CultureInfo.InvariantCulture
                    ),


                DateTime.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss.fff",
                    CultureInfo.InvariantCulture
                )
            };


            StringBuilder line =
                new StringBuilder();


            for (
                int i = 0;
                i < columns.Length;
                i++
            )
            {
                if (i > 0)
                {
                    line.Append(
                        ','
                    );
                }


                line.Append(
                    EscapeCsv(
                        columns[i]
                    )
                );
            }


            /*
             * Trialごとに即時保存。
             */
            File.AppendAllText(
                resultCsvPath,

                line.ToString() +
                Environment.NewLine,

                new UTF8Encoding(
                    true
                )
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[ExperimentalController] CSV保存失敗\n" +
                exception
            );
        }
    }


    private static string EscapeCsv(
        string value
    )
    {
        if (value == null)
        {
            return string.Empty;
        }


        bool needsQuote =
            value.Contains(",") ||

            value.Contains("\"") ||

            value.Contains("\n") ||

            value.Contains("\r");


        string escaped =
            value.Replace(
                "\"",
                "\"\""
            );


        if (needsQuote)
        {
            return
                "\"" +
                escaped +
                "\"";
        }


        return
            escaped;
    }


    // ============================================================
    // UI Output
    // ============================================================

    private void RefreshOutputText()
    {
        if (outputText == null)
        {
            return;
        }


        string text =
            string.Empty;


        // ========================================================
        // READY
        // ========================================================

        if (
            currentStage ==
            ExperimentStage.Ready
        )
        {
            text =
                "<b>EXPERIMENT</b>\n\n" +

                "<b>READY</b>\n\n" +

                "実験者がStart Buttonを押すと\n" +
                "IN / OUT試行を開始します。\n\n" +

                $"Participant : {GetParticipantId()}\n" +

                $"Repeat : {Mathf.Max(1, repeatCount)}";


            outputText.text =
                text;


            return;
        }


        // ========================================================
        // IN / OUT
        // ========================================================

        if (
            currentStage ==
            ExperimentStage.InOut
        )
        {
            text =
                "<b>IN / OUT</b>\n\n";


            AppendTrialProgress(
                ref text
            );


            if (
                currentTrialState ==
                TrialState.Preparing
            )
            {
                text +=
                    "\n次の試行を準備しています。";
            }


            if (
                currentTrialState ==
                TrialState.Playing
            )
            {
                text +=
                    "\n刺激提示中";
            }


            if (
                currentTrialState ==
                TrialState.AwaitingResponse
            )
            {
                text +=
                    "\n<b>回答してください</b>\n\n" +

                    $"←  {inOutLeftResponseLabel}" +

                    $"          {inOutRightResponseLabel}  →";
            }


            if (
                currentTrialState ==
                TrialState.InterTrial
            )
            {
                text +=
                    "\n回答を記録しました。";
            }


            outputText.text =
                text;


            return;
        }


        // ========================================================
        // REST
        // ========================================================

        if (
            currentStage ==
            ExperimentStage.Rest
        )
        {
            text =
                "<b>REST</b>\n\n" +

                "IN / OUT試行は終了です。\n\n" +

                "休憩してください。\n\n" +

                "準備ができたら\n" +

                "実験者がStart Buttonを押してください。";


            outputText.text =
                text;


            return;
        }


        // ========================================================
        // NEAR / FAR
        // ========================================================

        if (
            currentStage ==
            ExperimentStage.NearFar
        )
        {
            text =
                "<b>NEAR / FAR</b>\n\n";


            AppendTrialProgress(
                ref text
            );


            if (
                currentTrialState ==
                TrialState.Preparing
            )
            {
                text +=
                    "\n次の試行を準備しています。";
            }


            if (
                currentTrialState ==
                TrialState.Playing
            )
            {
                text +=
                    "\n刺激提示中";
            }


            if (
                currentTrialState ==
                TrialState.AwaitingResponse
            )
            {
                text +=
                    "\n<b>回答してください</b>\n\n" +

                    $"←  {nearFarLeftResponseLabel}" +

                    $"          {nearFarRightResponseLabel}  →";
            }


            if (
                currentTrialState ==
                TrialState.InterTrial
            )
            {
                text +=
                    "\n回答を記録しました。";
            }


            outputText.text =
                text;


            return;
        }


        // ========================================================
        // FINISH
        // ========================================================

        if (
            currentStage ==
            ExperimentStage.Finish
        )
        {
            text =
                "<b>FINISH</b>\n\n" +

                "すべての試行が終了しました。\n\n" +

                "実験者がFinish Buttonを押してください。";


            outputText.text =
                text;


            return;
        }


        // ========================================================
        // COMPLETED
        // ========================================================

        if (
            currentStage ==
            ExperimentStage.Completed
        )
        {
            text =
                "<b>COMPLETED</b>\n\n" +

                "実験を完了しました。\n\n" +

                "CSV:\n" +

                resultCsvPath;


            outputText.text =
                text;


            return;
        }


        // ========================================================
        // ABORTED
        // ========================================================

        if (
            currentStage ==
            ExperimentStage.Aborted
        )
        {
            text =
                "<b>ABORTED</b>\n\n" +

                "実験を中断しました。\n\n" +

                "回答済みTrialはCSVに保存されています。";


            outputText.text =
                text;
        }
    }


    private void AppendTrialProgress(
        ref string text
    )
    {
        if (
            currentTrialPool == null ||

            currentTrialPool.Count <= 0
        )
        {
            return;
        }


        int trialNumber =
            Mathf.Clamp(
                currentBlockTrialIndex + 1,
                1,
                currentTrialPool.Count
            );


        text +=
            $"Trial : {trialNumber}" +
            $" / {currentTrialPool.Count}\n";
    }


    private void ShowError(
        string message
    )
    {
        if (outputText != null)
        {
            outputText.text =
                "<b>ERROR</b>\n\n" +
                message;
        }


        Debug.LogError(
            "[ExperimentalController] " +
            message
        );
    }


    // ============================================================
    // Button State
    // ============================================================

    private void RefreshButtonState()
    {
        if (startButton != null)
        {
            bool visible =
                currentStage ==
                ExperimentStage.Ready ||

                currentStage ==
                ExperimentStage.Rest;


            startButton.gameObject.SetActive(
                visible
            );


            startButton.interactable =
                visible;
        }


        if (finishButton != null)
        {
            bool visible =
                currentStage ==
                ExperimentStage.Finish;


            finishButton.gameObject.SetActive(
                visible
            );


            finishButton.interactable =
                visible;
        }


        if (abortButton != null)
        {
            abortButton.interactable =
                currentStage ==
                ExperimentStage.InOut ||

                currentStage ==
                ExperimentStage.Rest ||

                currentStage ==
                ExperimentStage.NearFar ||

                currentStage ==
                ExperimentStage.Finish;
        }


        if (backButton != null)
        {
            backButton.interactable =
                currentStage ==
                ExperimentStage.Ready;
        }


        if (participantIdInputField != null)
        {
            participantIdInputField.interactable =
                currentStage ==
                ExperimentStage.Ready;
        }
    }


    // ============================================================
    // Abort
    // ============================================================

    public void AbortExperiment()
    {
        if (
            currentStage ==
            ExperimentStage.Ready ||

            currentStage ==
            ExperimentStage.Completed ||

            currentStage ==
            ExperimentStage.Aborted
        )
        {
            return;
        }


        AbortExperimentWithMessage(
            "実験者によって中断されました。"
        );
    }


    private void AbortExperimentWithMessage(
        string message
    )
    {
        StopTrialCoroutine();


        rallyController.ResetExperimentFade();


        rallyController.ResetSelectedSimulation();


        currentStage =
            ExperimentStage.Aborted;


        currentTrialState =
            TrialState.None;


        RefreshButtonState();


        RefreshOutputText();


        Debug.LogWarning(
            "[ExperimentalController] ABORTED\n" +
            message +
            "\nCSV = " +
            resultCsvPath
        );
    }


    // ============================================================
    // Reset Session
    // ============================================================

    private void ResetExperimentSession()
    {
        StopTrialCoroutine();


        rallyController.ResetExperimentFade();


        inOutTrials.Clear();


        nearFarTrials.Clear();


        currentTrialPool =
            null;


        currentBlockTrialIndex =
            -1;


        globalCompletedTrialCount =
            0;


        totalExperimentTrialCount =
            0;


        currentSessionSeed =
            0;


        inOutBlockSeed =
            0;


        nearFarBlockSeed =
            0;


        responseStartRealtime =
            0.0;


        resultCsvPath =
            string.Empty;


        SetStage(
            ExperimentStage.Ready
        );
    }


    private void StopTrialCoroutine()
    {
        if (trialCoroutine == null)
        {
            return;
        }


        StopCoroutine(
            trialCoroutine
        );


        trialCoroutine =
            null;
    }


    // ============================================================
    // Stage
    // ============================================================

    private void SetStage(
        ExperimentStage stage
    )
    {
        currentStage =
            stage;


        currentTrialState =
            TrialState.None;


        RefreshButtonState();


        RefreshOutputText();
    }


    // ============================================================
    // Validation
    // ============================================================

    private bool ValidateExperimentSettings(
        out string error
    )
    {
        error =
            string.Empty;


        if (rallyController == null)
        {
            error =
                "RallyControllerが設定されていません。";

            return false;
        }


        if (dhitController == null)
        {
            error =
                "DHITControllerが設定されていません。";

            return false;
        }


        if (imageController == null)
        {
            error =
                "ImageControllerが設定されていません。";

            return false;
        }


        if (
            !includeStaticCondition &&
            !includeDynamicCondition
        )
        {
            error =
                "Static / Dynamicのどちらかを有効にしてください。";

            return false;
        }


        if (
            inOutTrialDirectories == null ||

            inOutTrialDirectories.Count <= 0
        )
        {
            error =
                "In / Out Trial Directoriesが空です。";

            return false;
        }


        if (
            nearFarTrialDirectories == null ||

            nearFarTrialDirectories.Count <= 0
        )
        {
            error =
                "Near / Far Trial Directoriesが空です。";

            return false;
        }


        return true;
    }


    // ============================================================
    // Helpers
    // ============================================================

    private ExperimentTrial GetCurrentTrial()
    {
        if (
            currentTrialPool == null ||

            currentBlockTrialIndex < 0 ||

            currentBlockTrialIndex >=
            currentTrialPool.Count
        )
        {
            return null;
        }


        return
            currentTrialPool[
                currentBlockTrialIndex
            ];
    }


    private string GetParticipantId()
    {
        if (participantIdInputField == null)
        {
            return "Participant";
        }


        if (string.IsNullOrWhiteSpace(
            participantIdInputField.text
        ))
        {
            return "Participant";
        }


        return
            participantIdInputField.text.Trim();
    }


    private static int CreateRuntimeSeed()
    {
        return
            unchecked(
                (int)(
                    DateTime.UtcNow.Ticks ^
                    Environment.TickCount
                )
            );
    }


    private string GetResultDirectory()
    {
        string folder =
            string.IsNullOrWhiteSpace(
                resultRelativeFolder
            )
                ? "ExperimentResults"
                : resultRelativeFolder.Trim();


        folder =
            folder
            .Replace(
                '/',
                Path.DirectorySeparatorChar
            )
            .Replace(
                '\\',
                Path.DirectorySeparatorChar
            );


        return
            Path.GetFullPath(
                Path.Combine(
                    Application.persistentDataPath,
                    folder
                )
            );
    }


    private static string MakeSafeFileName(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(
            value
        ))
        {
            return "Participant";
        }


        char[] invalidChars =
            Path.GetInvalidFileNameChars();


        StringBuilder builder =
            new StringBuilder();


        foreach (
            char c
            in value.Trim()
        )
        {
            bool invalid =
                false;


            for (
                int i = 0;
                i < invalidChars.Length;
                i++
            )
            {
                if (
                    c ==
                    invalidChars[i]
                )
                {
                    invalid =
                        true;

                    break;
                }
            }


            builder.Append(
                invalid
                    ? '_'
                    : c
            );
        }


        string result =
            builder.ToString();


        return
            string.IsNullOrWhiteSpace(
                result
            )
                ? "Participant"
                : result;
    }


    private static string GetStageName(
        ExperimentStage stage
    )
    {
        switch (stage)
        {
            case ExperimentStage.InOut:

                return "IN / OUT";


            case ExperimentStage.Rest:

                return "REST";


            case ExperimentStage.NearFar:

                return "NEAR / FAR";


            case ExperimentStage.Finish:

                return "FINISH";


            case ExperimentStage.Completed:

                return "COMPLETED";


            case ExperimentStage.Aborted:

                return "ABORTED";


            default:

                return "READY";
        }
    }


    private static string GetStageCsvName(
        ExperimentStage stage
    )
    {
        switch (stage)
        {
            case ExperimentStage.InOut:

                return "InOut";


            case ExperimentStage.NearFar:

                return "NearFar";


            default:

                return stage.ToString();
        }
    }
}