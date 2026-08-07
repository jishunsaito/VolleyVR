using UnityEngine;

public class BallDisparityCalculator : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [SerializeField]
    private ImageController imageController;

    [Tooltip("左右ステレオカメラの親。左右カメラの中点にあるTransform")]
    [SerializeField]
    private Transform stereoCameraRoot;

    [Tooltip("光軸方向とProjection Matrix取得用。Left CameraでOK")]
    [SerializeField]
    private Camera referenceCamera;


    // =========================================================
    // Ball
    // =========================================================

    [Header("Ball")]

    [Tooltip("ボールPrefabに設定したTag")]
    [SerializeField]
    private string ballTag = "Ball";

    private Transform ballTransform;


    // =========================================================
    // Unit
    // =========================================================

    [Header("Unit")]

    [Tooltip("1 Unity Unit = 1 m の場合は1000")]
    [SerializeField]
    private float unityUnitToMm = 1000.0f;


    // =========================================================
    // Display / Viewer Geometry
    // =========================================================

    [Header("Display / Viewer Geometry")]

    [Tooltip("最終表示ディスプレイの1 pixelあたりの物理幅 [mm/px]")]
    [SerializeField]
    private float displayPixelPitchMm = 0.1845236f;

    // 今回は固定条件
    private const float ViewingDistanceMm = 936.0f;
    private const float IpdMm = 63.0f;


    // =========================================================
    // Result
    // =========================================================

    /// <summary>
    /// 現在、有効なBallを取得できているか
    /// </summary>
    public bool HasBall { get; private set; }

    /// <summary>
    /// 現在の状態
    /// </summary>
    public string StatusMessage { get; private set; } = "Ball : Not Found";

    /// <summary>
    /// BallのWorld Position
    /// </summary>
    public Vector3 BallWorldPosition { get; private set; }

    /// <summary>
    /// カメラ光軸方向の奥行き [m]
    /// </summary>
    public float DepthMeters { get; private set; }

    /// <summary>
    /// カメラ光軸方向の奥行き [mm]
    /// </summary>
    public float DepthMm { get; private set; }

    /// <summary>
    /// センサー面上の幾何学的視差 [mm]
    /// d = fB/Z
    /// </summary>
    public float DisparityMm { get; private set; }

    /// <summary>
    /// 画像上の幾何学的視差 [pixel]
    /// </summary>
    public float DisparityPixels { get; private set; }

    /// <summary>
    /// Horizontal Shift適用後の視差 [pixel]
    /// </summary>
    public float ShiftedDisparityPixels { get; private set; }

    /// <summary>
    /// Shift前の視差角 [deg]
    /// 画面中央注視時を0 degとした相対視差角
    /// </summary>
    public float DisparityAngleDeg { get; private set; }

    /// <summary>
    /// Shift後の視差角 [deg]
    /// 画面中央注視時を0 degとした相対視差角
    /// </summary>
    public float ShiftedDisparityAngleDeg { get; private set; }


    // =========================================================
    // Unity
    // =========================================================

    private void Update()
    {
        FindBallIfNeeded();
        CalculateDisparity();
    }


    // =========================================================
    // Ball Search
    // =========================================================

    private void FindBallIfNeeded()
    {
        // 以前取得したBallがまだ存在し、Activeならそのまま使用
        if (ballTransform != null &&
            ballTransform.gameObject.activeInHierarchy)
        {
            return;
        }

        // Destroy / InactiveになったBall参照を破棄
        ballTransform = null;

        GameObject ballObject = null;

        try
        {
            ballObject =
                GameObject.FindGameObjectWithTag(ballTag);
        }
        catch (UnityException)
        {
            HasBall = false;
            StatusMessage = $"Tag '{ballTag}' : Not Registered";
            return;
        }

        if (ballObject != null)
        {
            ballTransform = ballObject.transform;
        }
    }


    // =========================================================
    // Disparity Calculation
    // =========================================================

    private void CalculateDisparity()
    {
        // -----------------------------------------------------
        // Reference check
        // -----------------------------------------------------

        if (imageController == null)
        {
            HasBall = false;
            StatusMessage = "ImageController : Not Assigned";
            return;
        }

        if (stereoCameraRoot == null)
        {
            HasBall = false;
            StatusMessage = "Stereo Camera Root : Not Assigned";
            return;
        }

        if (referenceCamera == null)
        {
            HasBall = false;
            StatusMessage = "Reference Camera : Not Assigned";
            return;
        }

        if (ballTransform == null)
        {
            HasBall = false;
            StatusMessage = "Ball : Not Found";
            return;
        }

        if (!ballTransform.gameObject.activeInHierarchy)
        {
            HasBall = false;
            StatusMessage = "Ball : Inactive";
            return;
        }


        // =====================================================
        // Ball Position
        // =====================================================

        BallWorldPosition =
            ballTransform.position;


        // =====================================================
        // Camera -> Ball Vector
        // =====================================================

        Vector3 cameraToBall =
            BallWorldPosition -
            stereoCameraRoot.position;


        // =====================================================
        // 奥行き Z
        //
        // カメラからBallまでの直線距離ではなく、
        // カメラ光軸方向への射影距離。
        //
        // Z = dot(CameraToBall, CameraForward)
        // =====================================================

        float depthUnity =
            Vector3.Dot(
                cameraToBall,
                referenceCamera.transform.forward
            );

        if (depthUnity <= 0.0f)
        {
            HasBall = false;
            StatusMessage = "Ball : Behind Camera";
            return;
        }


        // =====================================================
        // Unit Conversion
        // =====================================================

        DepthMeters =
            depthUnity;

        DepthMm =
            depthUnity *
            unityUnitToMm;


        // =====================================================
        // Camera Parameters
        // =====================================================

        float focalLengthMm =
            imageController.focalLength;

        float baselineMm =
            imageController.baseline;

        if (DepthMm <= 0.0f ||
            focalLengthMm <= 0.0f ||
            baselineMm < 0.0f)
        {
            HasBall = false;
            StatusMessage = "Invalid Camera Parameters";
            return;
        }


        // =====================================================
        // Sensor Disparity [mm]
        //
        // d = fB / Z
        // =====================================================

        DisparityMm =
            focalLengthMm *
            baselineMm /
            DepthMm;


        // =====================================================
        // Image Disparity [pixel]
        //
        // Projection Matrixから水平方向焦点距離[pixel]を取得。
        //
        // x_ndc = m00 * X/Z
        // f_px = width/2 * m00
        // d_px = f_px * B/Z
        // =====================================================

        int imageWidthPixels;

        if (referenceCamera.targetTexture != null)
        {
            imageWidthPixels =
                referenceCamera.targetTexture.width;
        }
        else
        {
            imageWidthPixels =
                referenceCamera.pixelWidth;
        }

        float focalLengthPixels =
            0.5f *
            imageWidthPixels *
            Mathf.Abs(
                referenceCamera.projectionMatrix.m00
            );

        DisparityPixels =
            focalLengthPixels *
            baselineMm /
            DepthMm;


        // =====================================================
        // Shift後の視差 [pixel]
        //
        // Left  = +shiftPixels
        // Right = -shiftPixels
        //
        // 左右の相対変化量は 2 * shiftPixels
        // =====================================================

        ShiftedDisparityPixels =
            DisparityPixels -
            2.0f *
            imageController.shiftPixels;


        // =====================================================
        // Disparity Angle [deg]
        //
        // 視距離 936 mm
        // IPD 63 mm
        // 観察者は画面中心に固定
        // =====================================================

        DisparityAngleDeg =
            DispPxToAngleDeg(
                DisparityPixels
            );

        ShiftedDisparityAngleDeg =
            DispPxToAngleDeg(
                ShiftedDisparityPixels
            );


        HasBall = true;
        StatusMessage = "OK";
    }


    // =========================================================
    // Pixel Disparity -> Disparity Angle
    // =========================================================

    private float DispPxToAngleDeg(
        float disparityPixels
    )
    {
        // -----------------------------------------------------
        // Pixel disparity -> Display上の物理距離 [mm]
        // -----------------------------------------------------

        float disparityMm =
            disparityPixels *
            displayPixelPitchMm;


        // -----------------------------------------------------
        // Display座標系
        //
        // Display center = (0, 0, 0)
        // Viewer center  = (0, 0, -936)
        //
        // Left Eye  = (-IPD/2, 0, -936)
        // Right Eye = (+IPD/2, 0, -936)
        // -----------------------------------------------------

        Vector3 leftEye =
            new Vector3(
                -IpdMm * 0.5f,
                0.0f,
                -ViewingDistanceMm
            );

        Vector3 rightEye =
            new Vector3(
                IpdMm * 0.5f,
                0.0f,
                -ViewingDistanceMm
            );


        // -----------------------------------------------------
        // 左右画像上の対応点
        //
        // 画面中心を基準に左右へ disparity/2 ずつ配置
        // -----------------------------------------------------

        Vector3 leftPoint =
            new Vector3(
                disparityMm * 0.5f,
                0.0f,
                0.0f
            );

        Vector3 rightPoint =
            new Vector3(
                -disparityMm * 0.5f,
                0.0f,
                0.0f
            );

        Vector3 midPoint =
            Vector3.zero;


        // -----------------------------------------------------
        // 視差ありの場合の左右視線
        // -----------------------------------------------------

        Vector3 leftRayWithDisparity =
            leftPoint -
            leftEye;

        Vector3 rightRayWithDisparity =
            rightPoint -
            rightEye;


        // -----------------------------------------------------
        // 視差0の場合の左右視線
        // -----------------------------------------------------

        Vector3 leftRayZeroDisparity =
            midPoint -
            leftEye;

        Vector3 rightRayZeroDisparity =
            midPoint -
            rightEye;


        // -----------------------------------------------------
        // 元の式と同じ考え方
        //
        // alpha : 視差ありの輻輳角
        // beta  : 画面中心を見るときの輻輳角
        //
        // disparity angle = alpha - beta
        // -----------------------------------------------------

        float alpha =
            Vector3.Angle(
                leftRayWithDisparity,
                rightRayWithDisparity
            );

        float beta =
            Vector3.Angle(
                leftRayZeroDisparity,
                rightRayZeroDisparity
            );

        return
            alpha - beta;
    }
}