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
    // Result
    // =========================================================

    /// <summary>
    /// 現在、有効なBallを取得できているか
    /// </summary>
    public bool HasBall { get; private set; }


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
        /*
         * 以前取得したBallがまだ存在し、
         * Activeならそのまま使用する。
         */
        if (ballTransform != null &&
            ballTransform.gameObject.activeInHierarchy)
        {
            return;
        }


        /*
         * Destroyされた場合やInactiveの場合は
         * 一旦参照をクリア。
         */
        if (ballTransform == null ||
            !ballTransform.gameObject.activeInHierarchy)
        {
            ballTransform = null;
        }


        /*
         * 現在シーン内に存在するActiveなBallを探す。
         *
         * トス終了後にBallがDestroyされ、
         * Zキーで新しくInstantiateされた場合も、
         * 新しいBallをここで再取得できる。
         */
        GameObject ballObject = null;

        try
        {
            ballObject =
                GameObject.FindGameObjectWithTag(
                    ballTag
                );
        }
        catch (UnityException)
        {
            /*
             * "Ball" Tag自体がUnity側に登録されていない場合。
             */
            HasBall = false;
            return;
        }


        if (ballObject != null)
        {
            ballTransform =
                ballObject.transform;
        }
    }


    // =========================================================
    // Disparity Calculation
    // =========================================================

    private void CalculateDisparity()
    {
        // -----------------------------------------------------
        // 必要なオブジェクトが存在するか
        // -----------------------------------------------------

        if (imageController == null ||
            stereoCameraRoot == null ||
            referenceCamera == null ||
            ballTransform == null)
        {
            HasBall = false;
            return;
        }


        if (!ballTransform.gameObject.activeInHierarchy)
        {
            HasBall = false;
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
        // 重要：
        // Vector3.Distanceではない。
        //
        // Camera -> Ballのベクトルを、
        // カメラ光軸方向へ射影する。
        //
        // Z = dot(CameraToBall, CameraForward)
        // =====================================================

        float depthUnity =
            Vector3.Dot(
                cameraToBall,
                referenceCamera.transform.forward
            );


        /*
         * Ballがカメラより後方にある場合は
         * 視差計算をしない。
         */
        if (depthUnity <= 0.0f)
        {
            HasBall = false;
            return;
        }


        // =====================================================
        // Unit変換
        // =====================================================

        DepthMeters =
            depthUnity;

        DepthMm =
            depthUnity *
            unityUnitToMm;


        // =====================================================
        // f, B
        //
        // ImageControllerでは
        // focalLength = mm
        // baseline    = mm
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
            return;
        }


        // =====================================================
        // センサー上の視差
        //
        // d = fB / Z
        //
        // f : mm
        // B : mm
        // Z : mm
        //
        // → d : mm
        // =====================================================

        DisparityMm =
            focalLengthMm *
            baselineMm /
            DepthMm;


        // =====================================================
        // Pixel上の視差
        //
        // UnityのProjection Matrixから
        // 実際の水平方向焦点距離[pixel]を取得する。
        //
        // これによりSensor Aspect Ratioや
        // Gate Fitの影響もある程度反映できる。
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


        /*
         * Projection Matrix:
         *
         * x_ndc = m00 * X/Z
         *
         * NDC [-1, +1]をpixelへ変換するため
         *
         * f_px = width/2 * m00
         */
        float focalLengthPixels =
            0.5f *
            imageWidthPixels *
            Mathf.Abs(
                referenceCamera.projectionMatrix.m00
            );


        /*
         * d_px = f_px * B/Z
         *
         * BとZは同じ単位ならよい。
         */
        DisparityPixels =
            focalLengthPixels *
            baselineMm /
            DepthMm;


        // =====================================================
        // Shift後の視差
        //
        // 現在の構成
        //
        // Left  Material = +shiftPixels
        // Right Material = -shiftPixels
        //
        // Shader:
        // uv.x += _ShiftPixels * TexelSize
        //
        // +Shiftすると画像自体は左へ移動する。
        //
        // よって
        //
        // Left  -> 左へ shift
        // Right -> 右へ shift
        //
        // 左右の相対視差は
        //
        // d_after = d - 2 * shift
        // =====================================================

        ShiftedDisparityPixels =
            DisparityPixels -
            2.0f *
            imageController.shiftPixels;


        HasBall = true;
    }
}