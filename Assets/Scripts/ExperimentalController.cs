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
/// ============================================================
/// Experiment Flow
/// ============================================================
///
/// READY
///   ↓ Start
///
/// IN / OUT
///   ↓ 全Trial終了
///
/// REST
///   ↓ 実験者がStart
///
/// NEAR / FAR
///   ↓ 全Trial終了
///
/// FINISH
///   ↓ 実験者がFinish
///
/// COMPLETED
///
///
/// ============================================================
/// Trial Directory
/// ============================================================
///
/// InspectorにはRoot Folderだけを入れる。
///
/// 例:
///
/// Spike
/// ├─ Front
/// │   ├─ trial01.json
/// │   └─ trial02.json
/// │
/// └─ Behind
///     ├─ trial03.json
///     └─ trial04.json
///
/// Inspector:
///
/// In / Out Trial Directories
///   Element 0 = Spike
///
/// とするだけで、Spike以下のJSONをすべて取得する。
///
/// CSVのsource_directoryには
///
/// Spike/Front
/// Spike/Behind
///
/// のようにJSONの実際の親Folderまで記録する。
///
///
/// ============================================================
/// Trial Generation
/// ============================================================
///
/// Rally JSON
/// × Static / Dynamic
/// × Repeat
///
/// を生成する。
///
/// Blockごとに独立してRandomizeする。
///
///
/// ============================================================
/// Response
/// ============================================================
///
/// In / Out:
///
/// Left  = In
/// Right = Out
///
/// Near / Far:
///
/// Left  = Near
/// Right = Far
///
/// Keyboard:
///
/// Left  = ← / F
/// Right = → / J
///
/// Gamepad:
///
/// Left  = DPad Left / L1 / West
/// Right = DPad Right / R1 / East
///
///
/// ============================================================
/// Correct Answer
/// ============================================================
///
/// このControllerでは正答を持たない。
///
/// 実験時は
///
/// - Rally
/// - Directory
/// - Static / Dynamic
/// - Response
/// - RT
///
/// のみ保存する。
///
/// 後からCSVへ正答を追加する。
///
///
/// ============================================================
/// UI
/// ============================================================
///
/// UIControllerと同様に
///
/// ScrollView
/// └─ Viewport
///     └─ Content
///         └─ TMP_Text
///
/// のTMP_TextへoutputText.textで表示する。
///
///
/// ============================================================
/// Important
/// ============================================================
///
/// InspectorへFolderを直接Drag & Dropする方式なので、
/// 現在はUnity Editor上で実験することを前提とする。
///
/// Build版へ移行するときはStreamingAssets等へ変更する。
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
    // Response
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

        /// <summary>
        /// Inspectorへ入れたRoot Folder。
        /// 例: Spike
        /// </summary>
        public string rootDirectoryName;

        /// <summary>
        /// JSONが実際に存在するFolder。
        ///
        /// 例:
        /// Spike/Front
        /// Spike/Behind
        /// </summary>
        public string sourceDirectory;

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


    [Tooltip(
        "Static / Dynamic HITを管理するDHITController。\n" +
        "Rally JSON Load後にTrial条件を上書きする。"
    )]
    [SerializeField]
    private DHITController dhitController;


    [Tooltip(
        "Fade完了判定に使用するImageController。"
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
        "READYではIn / Out開始。\n" +
        "RESTではNear / Far開始。"
    )]
    [SerializeField]
    private Button startButton;


    [Tooltip(
        "全試行終了後に実験者が押すButton"
    )]
    [SerializeField]
    private Button finishButton;


    [Tooltip(
        "途中中断Button。\n" +
        "不要ならnullでもよい。"
    )]
    [SerializeField]
    private Button abortButton;


    [Tooltip(
        "実験開始前にMain Panelへ戻るButton。\n" +
        "不要ならnullでもよい。"
    )]
    [SerializeField]
    private Button backButton;


    // ============================================================
    // UI
    // ============================================================

    [Header("UI")]

    [Tooltip(
        "ScrollViewのContent内にあるTMP_Text"
    )]
    [SerializeField]
    private TMP_Text outputText;


    [Tooltip(
        "被験者ID入力欄"
    )]
    [SerializeField]
    private TMP_InputField participantIdInputField;


    // ============================================================
    // In / Out Directories
    // ============================================================

    [Header("In / Out Trial Directories")]

    [Tooltip(
        "Root Folderを入れる。\n\n" +
        "例:\n" +
        "Spike\n\n" +
        "Spike/FrontやSpike/Behind以下も自動検索する。"
    )]
    [SerializeField]
    private List<UnityEngine.Object> inOutTrialDirectories =
        new List<UnityEngine.Object>();


    // ============================================================
    // Near / Far Directories
    // ============================================================

    [Header("Near / Far Trial Directories")]

    [Tooltip(
        "Root Folderを入れる。\n\n" +
        "そのFolder以下を再帰的に検索する。"
    )]
    [SerializeField]
    private List<UnityEngine.Object> nearFarTrialDirectories =
        new List<UnityEngine.Object>();


    // ============================================================
    // JSON Search
    // ============================================================

    [Header("JSON Search")]

    [Tooltip(
        "Rally JSONではないため除外するJSONファイル名"
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

    [Min(1)]
    [Tooltip(
        "各Rally × Static/Dynamicを何回繰り返すか"
    )]
    [SerializeField]
    private int repeatCount =
        1;


    [SerializeField]
    private bool includeStaticCondition =
        true;


    [SerializeField]
    private bool includeDynamicCondition =
        true;


    [Tooltip(
        "Rally JSONに保存されているStatic/Dynamicより、" +
        "Trial側の条件を優先する。"
    )]
    [SerializeField]
    private bool overrideShiftModeAfterJsonLoad =
        true;


    [Tooltip(
        "実験中はExperiment FadeをONにする。"
    )]
    [SerializeField]
    private bool forceExperimentFadeEnabled =
        true;


    // ============================================================
    // Randomization
    // ============================================================

    [Header("Randomization")]

    [Tooltip(
        "0ならSession開始時に自動生成。\n" +
        "固定値なら同じTrial順を再現できる。"
    )]
    [SerializeField]
    private int randomSeed =
        0;


    [Tooltip(
        "同じStatic/Dynamic条件が連続してよい最大数。\n" +
        "0以下なら制限なし。"
    )]
    [SerializeField]
    private int maxConsecutiveSameShiftMode =
        3;


    [Tooltip(
        "同じRally JSONの連続を避ける。"
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

    [Min(0.0f)]
    [Tooltip(
        "JSON Load後から刺激開始までの待ち時間 [s]"
    )]
    [SerializeField]
    private float trialPreparationDelaySeconds =
        0.05f;


    [Min(0.0f)]
    [Tooltip(
        "回答から次Trialまでの待ち時間 [s]"
    )]
    [SerializeField]
    private float interTrialDelaySeconds =
        0.35f;


    [Min(1.0f)]
    [Tooltip(
        "Fade完了待機の最大時間 [s]"
    )]
    [SerializeField]
    private float fadeWaitTimeoutSeconds =
        30.0f;


    // ============================================================
    // Response Labels
    // ============================================================

    [Header("Response Labels")]

    [SerializeField]
    private string inOutLeftResponseLabel =
        "In";


    [SerializeField]
    private string inOutRightResponseLabel =
        "Out";


    [SerializeField]
    private string nearFarLeftResponseLabel =
        "Near";


    [SerializeField]
    private string nearFarRightResponseLabel =
        "Far";


    // ============================================================
    // Input
    // ============================================================

    [Header("Response Input")]

    [Tooltip(
        "Left = ← / F\n" +
        "Right = → / J"
    )]
    [SerializeField]
    private bool enableKeyboardResponse =
        true;


    [Tooltip(
        "Left = DPad Left / L1 / West\n" +
        "Right = DPad Right / R1 / East"
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
    // Public
    // ============================================================

    public ExperimentStage CurrentStage =>
        currentStage;


    public bool IsRunning =>
        currentStage == ExperimentStage.InOut ||
        currentStage == ExperimentStage.NearFar;


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
            currentStage == ExperimentStage.Completed ||
            currentStage == ExperimentStage.Aborted
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
    // Session Start
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
            $"InOut Trials = {inOutTrials.Count}\n" +
            $"NearFar Trials = {nearFarTrials.Count}\n" +
            $"Total Trials = {totalExperimentTrialCount}\n" +
            $"Seed = {currentSessionSeed}\n" +
            $"CSV = {resultCsvPath}"
        );


        StartInOutBlock();
    }


    // ============================================================
    // In / Out
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
    // Rest
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
            "[ExperimentalController] In/Out Completed -> REST"
        );
    }


    // ============================================================
    // Near / Far
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
    // Finish
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
    // Build Trial Pool
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
                $"{GetStageName(stage)}用のDirectoryがありません。";

            return false;
        }


        /*
         * 同じJSONが重複登録されることを防ぐ。
         *
         * 例:
         *
         * Element 0 = Spike
         * Element 1 = Spike/Front
         *
         * としてしまってもFrontのJSONを2回登録しない。
         */
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
                out string rootDirectoryPath,
                out string rootDirectoryName,
                out string directoryError
            ))
            {
                error =
                    directoryError;

                return false;
            }


            /*
             * 必ずAllDirectories。
             *
             * Spikeだけ入れて
             *
             * Spike/Front
             * Spike/Behind
             *
             * まで全部取得する。
             */
            string[] jsonPaths =
                Directory.GetFiles(
                    rootDirectoryPath,
                    "*.json",
                    SearchOption.AllDirectories
                );


            Array.Sort(
                jsonPaths,
                StringComparer.OrdinalIgnoreCase
            );


            Debug.Log(
                "[ExperimentalController] Directory Scan\n" +
                $"Stage = {GetStageName(stage)}\n" +
                $"Root = {rootDirectoryPath}\n" +
                $"JSON Count = {jsonPaths.Length}"
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


                if (!registeredJsonPaths.Add(
                    fullPath
                ))
                {
                    continue;
                }


                /*
                 * JSONの親Folderを取得する。
                 *
                 * Spike/Front/trial01.json
                 *
                 * なら
                 *
                 * Spike/Front
                 *
                 * を作る。
                 */
                string sourceDirectory =
                    GetRelativeSourceDirectory(
                        rootDirectoryPath,
                        rootDirectoryName,
                        fullPath
                    );


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
                                rootDirectoryName,
                                sourceDirectory,
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
                                rootDirectoryName,
                                sourceDirectory,
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
                $"{GetStageName(stage)}用Directory以下に" +
                "使用可能なRally JSONがありません。";

            return false;
        }


        return true;
    }


    private static ExperimentTrial CreateTrial(
        ExperimentStage stage,
        string rootDirectoryName,
        string sourceDirectory,
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

                rootDirectoryName =
                    rootDirectoryName,

                sourceDirectory =
                    sourceDirectory,

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
    // Source Directory
    // ============================================================

    /// <summary>
    /// Root Folderから見たJSONの親Folderを取得する。
    ///
    /// 例:
    ///
    /// Root:
    /// C:/Project/Assets/Experiment/Spike
    ///
    /// JSON:
    /// C:/Project/Assets/Experiment/Spike/Front/test.json
    ///
    /// Result:
    /// Spike/Front
    ///
    ///
    /// JSONがRoot直下なら:
    ///
    /// Spike
    ///
    /// になる。
    /// </summary>
    private static string GetRelativeSourceDirectory(
        string rootDirectoryPath,
        string rootDirectoryName,
        string jsonFilePath
    )
    {
        string rootFullPath =
            Path.GetFullPath(
                rootDirectoryPath
            )
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );


        string jsonParentDirectory =
            Path.GetDirectoryName(
                jsonFilePath
            );


        if (string.IsNullOrWhiteSpace(
            jsonParentDirectory
        ))
        {
            return
                rootDirectoryName;
        }


        string parentFullPath =
            Path.GetFullPath(
                jsonParentDirectory
            )
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );


        /*
         * JSONがRoot直下。
         */
        if (string.Equals(
            rootFullPath,
            parentFullPath,
            StringComparison.OrdinalIgnoreCase
        ))
        {
            return
                rootDirectoryName;
        }


        string rootPrefix =
            rootFullPath +
            Path.DirectorySeparatorChar;


        /*
         * JSONがRoot以下にある。
         */
        if (parentFullPath.StartsWith(
            rootPrefix,
            StringComparison.OrdinalIgnoreCase
        ))
        {
            string relativePart =
                parentFullPath.Substring(
                    rootPrefix.Length
                );


            relativePart =
                relativePart
                .Replace(
                    Path.DirectorySeparatorChar,
                    '/'
                )
                .Replace(
                    Path.AltDirectorySeparatorChar,
                    '/'
                );


            return
                rootDirectoryName +
                "/" +
                relativePart;
        }


        /*
         * 通常ここには来ないがSafety。
         */
        return
            Path.GetFileName(
                parentFullPath
            );
    }


    // ============================================================
    // Inspector Directory -> Path
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
         * Application.dataPath
         *
         * C:/Project/Assets
         *
         * ProjectRoot
         *
         * C:/Project
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
            "Folder Drag & Drop方式はUnity Editor用です。\n" +
            "Build版では別のFolder読込方式が必要です。";

        return false;

#endif
    }


    // ============================================================
    // Excluded JSON
    // ============================================================

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
    // Randomize
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


                Debug.Log(
                    "[ExperimentalController] Randomization OK\n" +
                    $"Stage = {GetStageName(trials[0].stage)}\n" +
                    $"Seed = {seed}\n" +
                    $"Attempt = {attempt + 1}"
                );


                return;
            }
        }


        /*
         * 条件数の都合でConstraintを満たせない場合でも
         * 実験は止めない。
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
            "Randomization制約を完全には満たせませんでした。" +
            "最後のShuffle結果を使用します。"
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


            // ====================================================
            // Same Rally
            // ====================================================

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


            // ====================================================
            // Static / Dynamic
            // ====================================================

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
         * 1.
         * Rally JSON Load
         *
         * JSON内部にDHIT設定が保存されている可能性があるため、
         * Static / Dynamic Overrideより必ず先にLoadする。
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
        // Static / Dynamic
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
         * 前TrialのFadeを確実に解除。
         */
        rallyController.ResetExperimentFade();


        // ========================================================
        // Preparation Wait
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
        // Start Rally
        // ========================================================

        currentTrialState =
            TrialState.Playing;


        RefreshOutputText();


        Debug.Log(
            "[ExperimentalController] Trial Start\n" +
            $"Stage = {GetStageName(currentStage)}\n" +
            $"Trial = {currentBlockTrialIndex + 1}/{currentTrialPool.Count}\n" +
            $"Directory = {trial.sourceDirectory}\n" +
            $"Rally = {trial.rallyFileName}\n" +
            $"Shift = {(trial.dynamicShift ? "Dynamic" : "Static")}\n" +
            $"Repeat = {trial.repetition}"
        );


        rallyController.StartSelectedSimulation();


        // ========================================================
        // Wait For Fade Complete
        // ========================================================

        /*
         * FadeAmount:
         *
         * 0 = Normal
         * 1 = Completely Black
         *
         * FadeAmountが1になった時点から
         * Response Timeを開始する。
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
                    "Rally JSONのEnd PhaseやFade設定を確認してください。"
                );

                yield break;
            }


            yield return null;
        }


        // ========================================================
        // Response Start
        // ========================================================

        responseStartRealtime =
            Time.realtimeSinceStartupAsDouble;


        currentTrialState =
            TrialState.AwaitingResponse;


        RefreshOutputText();


        trialCoroutine =
            null;
    }


    // ============================================================
    // Block Complete
    // ============================================================

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
    // Input
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
        // Legacy Input
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
         * 同一Frameで左右両方押された場合は無視。
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
    // Submit Response
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


        double responseEndRealtime =
            Time.realtimeSinceStartupAsDouble;


        double responseTimeSeconds =
            Math.Max(
                0.0,
                responseEndRealtime -
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
            $"Directory = {trial.sourceDirectory}\n" +
            $"Rally = {trial.rallyFileName}\n" +
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


    // ============================================================
    // Response Label
    // ============================================================

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


            /*
             * Result_<ID>.csv
             *
             * 例:
             * Result_P01.csv
             */
            string fileName =
                "Result_" +
                participantId +
                ".csv";


            resultCsvPath =
                Path.Combine(
                    resultDirectory,
                    fileName
                );


            /*
             * 同じParticipant IDのCSVが既にある場合は
             * 誤上書きを防ぐ。
             */
            if (File.Exists(
                resultCsvPath
            ))
            {
                string timestamp =
                    DateTime.Now.ToString(
                        "yyyyMMdd_HHmmss",
                        CultureInfo.InvariantCulture
                    );


                fileName =
                    "Result_" +
                    participantId +
                    "_" +
                    timestamp +
                    ".csv";


                resultCsvPath =
                    Path.Combine(
                        resultDirectory,
                        fileName
                    );
            }


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
            Debug.LogError(
                "[ExperimentalController] CSV Pathがありません。"
            );

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


                /*
                 * 例:
                 *
                 * Spike/Front
                 * Spike/Behind
                 */
                trial.sourceDirectory,


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


            StringBuilder lineBuilder =
                new StringBuilder();


            for (
                int i = 0;
                i < columns.Length;
                i++
            )
            {
                if (i > 0)
                {
                    lineBuilder.Append(
                        ','
                    );
                }


                lineBuilder.Append(
                    EscapeCsv(
                        columns[i]
                    )
                );
            }


            /*
             * Trialごとに即時保存。
             *
             * Unityが途中で落ちても
             * それまでのTrialは残る。
             */
            File.AppendAllText(
                resultCsvPath,

                lineBuilder.ToString() +
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


        bool requiresQuotes =
            value.Contains(",") ||
            value.Contains("\"") ||
            value.Contains("\n") ||
            value.Contains("\r");


        string escaped =
            value.Replace(
                "\"",
                "\"\""
            );


        return
            requiresQuotes
                ? "\"" + escaped + "\""
                : escaped;
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
        // Ready
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
        // In / Out
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


            switch (currentTrialState)
            {
                case TrialState.Preparing:

                    text +=
                        "\n次の試行を準備しています。";

                    break;


                case TrialState.Playing:

                    text +=
                        "\n刺激提示中";

                    break;


                case TrialState.AwaitingResponse:

                    text +=
                        "\n<b>回答してください</b>\n\n" +

                        $"←  {inOutLeftResponseLabel}" +

                        $"          {inOutRightResponseLabel}  →";

                    break;


                case TrialState.InterTrial:

                    text +=
                        "\n回答を記録しました。";

                    break;
            }


            outputText.text =
                text;


            return;
        }


        // ========================================================
        // Rest
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
        // Near / Far
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


            switch (currentTrialState)
            {
                case TrialState.Preparing:

                    text +=
                        "\n次の試行を準備しています。";

                    break;


                case TrialState.Playing:

                    text +=
                        "\n刺激提示中";

                    break;


                case TrialState.AwaitingResponse:

                    text +=
                        "\n<b>回答してください</b>\n\n" +

                        $"←  {nearFarLeftResponseLabel}" +

                        $"          {nearFarRightResponseLabel}  →";

                    break;


                case TrialState.InterTrial:

                    text +=
                        "\n回答を記録しました。";

                    break;
            }


            outputText.text =
                text;


            return;
        }


        // ========================================================
        // Finish
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
        // Completed
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
        // Aborted
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
            bool showStart =
                currentStage == ExperimentStage.Ready ||
                currentStage == ExperimentStage.Rest;


            startButton.gameObject.SetActive(
                showStart
            );


            startButton.interactable =
                showStart;
        }


        if (finishButton != null)
        {
            bool showFinish =
                currentStage ==
                ExperimentStage.Finish;


            finishButton.gameObject.SetActive(
                showFinish
            );


            finishButton.interactable =
                showFinish;
        }


        if (abortButton != null)
        {
            abortButton.interactable =
                currentStage == ExperimentStage.InOut ||
                currentStage == ExperimentStage.Rest ||
                currentStage == ExperimentStage.NearFar ||
                currentStage == ExperimentStage.Finish;
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
            currentStage == ExperimentStage.Ready ||
            currentStage == ExperimentStage.Completed ||
            currentStage == ExperimentStage.Aborted
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
    // Reset
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
                "Static / Dynamicのどちらか1つ以上を" +
                "有効にしてください。";

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
            currentBlockTrialIndex >= currentTrialPool.Count
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