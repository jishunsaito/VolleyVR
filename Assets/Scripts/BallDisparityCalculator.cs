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
    // Pole Targets
    // =========================================================

    [Header("Pole Targets")]

    [Tooltip("手前側ポールの視差を計算する基準点。ポール本体または任意位置に置いたEmptyを指定")]
    [SerializeField]
    private Transform nearPoleTarget;

    [Tooltip("奥側ポールの視差を計算する基準点。ポール本体または任意位置に置いたEmptyを指定")]
    [SerializeField]
    private Transform farPoleTarget;


    // =========================================================
    // Net Center Target
    // =========================================================

    [Header("Net Center Target")]

    [Tooltip("ネット中心の視差を計算する基準点。ネット中央に置いたEmptyなどを指定")]
    [SerializeField]
    private Transform netCenterTarget;


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
    // Internal Result Type
    // =========================================================

    private struct TargetDisparityResult
    {
        public bool isValid;
        public Vector3 worldPosition;
        public float depthMeters;
        public float depthMm;
        public float disparityMm;
        public float disparityPixels;
        public float shiftedDisparityPixels;
        public float disparityAngleDeg;
        public float shiftedDisparityAngleDeg;
    }

    private TargetDisparityResult ballResult;
    private TargetDisparityResult nearPoleResult;
    private TargetDisparityResult farPoleResult;
    private TargetDisparityResult netCenterResult;


    // =========================================================
    // Ball Result
    // =========================================================

    public bool HasBall => ballResult.isValid;

    public string StatusMessage { get; private set; } = "Ball : Not Found";

    public Vector3 BallWorldPosition => ballResult.worldPosition;

    public float DepthMeters => ballResult.depthMeters;

    public float DepthMm => ballResult.depthMm;

    public float DisparityMm => ballResult.disparityMm;

    public float DisparityPixels => ballResult.disparityPixels;

    public float ShiftedDisparityPixels => ballResult.shiftedDisparityPixels;

    public float DisparityAngleDeg => ballResult.disparityAngleDeg;

    public float ShiftedDisparityAngleDeg => ballResult.shiftedDisparityAngleDeg;


    // =========================================================
    // Near Pole Result
    // =========================================================

    public bool HasNearPole => nearPoleResult.isValid;

    public string NearPoleStatusMessage { get; private set; } =
        "Near Pole : Not Assigned";

    public Vector3 NearPoleWorldPosition => nearPoleResult.worldPosition;

    public float NearPoleDepthMeters => nearPoleResult.depthMeters;

    public float NearPoleDepthMm => nearPoleResult.depthMm;

    public float NearPoleDisparityMm => nearPoleResult.disparityMm;

    public float NearPoleDisparityPixels => nearPoleResult.disparityPixels;

    public float NearPoleShiftedDisparityPixels =>
        nearPoleResult.shiftedDisparityPixels;

    public float NearPoleDisparityAngleDeg =>
        nearPoleResult.disparityAngleDeg;

    public float NearPoleShiftedDisparityAngleDeg =>
        nearPoleResult.shiftedDisparityAngleDeg;


    // =========================================================
    // Far Pole Result
    // =========================================================

    public bool HasFarPole => farPoleResult.isValid;

    public string FarPoleStatusMessage { get; private set; } =
        "Far Pole : Not Assigned";

    public Vector3 FarPoleWorldPosition => farPoleResult.worldPosition;

    public float FarPoleDepthMeters => farPoleResult.depthMeters;

    public float FarPoleDepthMm => farPoleResult.depthMm;

    public float FarPoleDisparityMm => farPoleResult.disparityMm;

    public float FarPoleDisparityPixels => farPoleResult.disparityPixels;

    public float FarPoleShiftedDisparityPixels =>
        farPoleResult.shiftedDisparityPixels;

    public float FarPoleDisparityAngleDeg =>
        farPoleResult.disparityAngleDeg;

    public float FarPoleShiftedDisparityAngleDeg =>
        farPoleResult.shiftedDisparityAngleDeg;


    // =========================================================
    // Net Center Result
    // =========================================================

    public bool HasNetCenter => netCenterResult.isValid;

    public string NetCenterStatusMessage { get; private set; } =
        "Net Center : Not Assigned";

    public Vector3 NetCenterWorldPosition => netCenterResult.worldPosition;

    public float NetCenterDepthMeters => netCenterResult.depthMeters;

    public float NetCenterDepthMm => netCenterResult.depthMm;

    public float NetCenterDisparityMm => netCenterResult.disparityMm;

    public float NetCenterDisparityPixels => netCenterResult.disparityPixels;

    public float NetCenterShiftedDisparityPixels =>
        netCenterResult.shiftedDisparityPixels;

    public float NetCenterDisparityAngleDeg =>
        netCenterResult.disparityAngleDeg;

    public float NetCenterShiftedDisparityAngleDeg =>
        netCenterResult.shiftedDisparityAngleDeg;


    // =========================================================
    // Unity
    // =========================================================

    private void Update()
    {
        FindBallIfNeeded();
        CalculateAllDisparities();
    }


    // =========================================================
    // Ball Search
    // =========================================================

    private void FindBallIfNeeded()
    {
        if (ballTransform != null &&
            ballTransform.gameObject.activeInHierarchy)
        {
            return;
        }

        ballTransform = null;

        try
        {
            GameObject ballObject =
                GameObject.FindGameObjectWithTag(ballTag);

            if (ballObject != null)
            {
                ballTransform = ballObject.transform;
            }
        }
        catch (UnityException)
        {
            StatusMessage =
                $"Tag '{ballTag}' : Not Registered";
        }
    }


    // =========================================================
    // Calculate All Targets
    // =========================================================

    private void CalculateAllDisparities()
    {
        // 共通参照が未設定なら全対象を無効化
        if (imageController == null)
        {
            ballResult = default;
            nearPoleResult = default;
            farPoleResult = default;
            netCenterResult = default;

            StatusMessage = "ImageController : Not Assigned";
            NearPoleStatusMessage = "ImageController : Not Assigned";
            FarPoleStatusMessage = "ImageController : Not Assigned";
            NetCenterStatusMessage = "ImageController : Not Assigned";
            return;
        }

        if (stereoCameraRoot == null)
        {
            ballResult = default;
            nearPoleResult = default;
            farPoleResult = default;
            netCenterResult = default;

            StatusMessage = "Stereo Camera Root : Not Assigned";
            NearPoleStatusMessage = "Stereo Camera Root : Not Assigned";
            FarPoleStatusMessage = "Stereo Camera Root : Not Assigned";
            NetCenterStatusMessage = "Stereo Camera Root : Not Assigned";
            return;
        }

        if (referenceCamera == null)
        {
            ballResult = default;
            nearPoleResult = default;
            farPoleResult = default;
            netCenterResult = default;

            StatusMessage = "Reference Camera : Not Assigned";
            NearPoleStatusMessage = "Reference Camera : Not Assigned";
            FarPoleStatusMessage = "Reference Camera : Not Assigned";
            NetCenterStatusMessage = "Reference Camera : Not Assigned";
            return;
        }


        // -----------------------------------------------------
        // Ball
        // -----------------------------------------------------

        ballResult =
            CalculateTargetDisparity(
                ballTransform,
                out string ballStatus
            );

        StatusMessage =
            ballTransform == null
                ? "Ball : Not Found"
                : ballStatus;


        // -----------------------------------------------------
        // Near Pole
        // -----------------------------------------------------

        nearPoleResult =
            CalculateTargetDisparity(
                nearPoleTarget,
                out string nearPoleStatus
            );

        NearPoleStatusMessage =
            nearPoleTarget == null
                ? "Near Pole : Not Assigned"
                : nearPoleStatus;


        // -----------------------------------------------------
        // Far Pole
        // -----------------------------------------------------

        farPoleResult =
            CalculateTargetDisparity(
                farPoleTarget,
                out string farPoleStatus
            );

        FarPoleStatusMessage =
            farPoleTarget == null
                ? "Far Pole : Not Assigned"
                : farPoleStatus;


        // -----------------------------------------------------
        // Net Center
        // -----------------------------------------------------

        netCenterResult =
            CalculateTargetDisparity(
                netCenterTarget,
                out string netCenterStatus
            );

        NetCenterStatusMessage =
            netCenterTarget == null
                ? "Net Center : Not Assigned"
                : netCenterStatus;
    }


    // =========================================================
    // Disparity Calculation for One Target
    // =========================================================

    private TargetDisparityResult CalculateTargetDisparity(
        Transform target,
        out string statusMessage
    )
    {
        TargetDisparityResult result = default;

        if (target == null)
        {
            statusMessage = "Target : Not Assigned";
            return result;
        }

        if (!target.gameObject.activeInHierarchy)
        {
            statusMessage = "Target : Inactive";
            return result;
        }


        // =====================================================
        // Target Position
        // =====================================================

        result.worldPosition =
            target.position;


        // =====================================================
        // Camera -> Target Vector
        // =====================================================

        Vector3 cameraToTarget =
            result.worldPosition -
            stereoCameraRoot.position;


        // =====================================================
        // 奥行き Z
        //
        // カメラから対象までの直線距離ではなく、
        // カメラ光軸方向への射影距離。
        //
        // Z = dot(CameraToTarget, CameraForward)
        // =====================================================

        float depthUnity =
            Vector3.Dot(
                cameraToTarget,
                referenceCamera.transform.forward
            );

        if (depthUnity <= 0.0f)
        {
            statusMessage = "Target : Behind Camera";
            return result;
        }


        // =====================================================
        // Unit Conversion
        // =====================================================

        result.depthMeters =
            depthUnity;

        result.depthMm =
            depthUnity *
            unityUnitToMm;


        // =====================================================
        // Camera Parameters
        // =====================================================

        float focalLengthMm =
            imageController.focalLength;

        float baselineMm =
            imageController.baseline;

        if (result.depthMm <= 0.0f ||
            focalLengthMm <= 0.0f ||
            baselineMm < 0.0f)
        {
            statusMessage = "Invalid Camera Parameters";
            return result;
        }


        // =====================================================
        // Sensor Disparity [mm]
        //
        // d = fB / Z
        // =====================================================

        result.disparityMm =
            focalLengthMm *
            baselineMm /
            result.depthMm;


        // =====================================================
        // Image Disparity [pixel]
        //
        // Projection Matrixから水平方向焦点距離[pixel]を取得。
        //
        // f_px = width/2 * |m00|
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

        result.disparityPixels =
            focalLengthPixels *
            baselineMm /
            result.depthMm;


        // =====================================================
        // Shift後の視差 [pixel]
        //
        // Left  = +shiftPixels
        // Right = -shiftPixels
        // 左右の相対変化量は 2 * shiftPixels
        // =====================================================

        result.shiftedDisparityPixels =
            result.disparityPixels -
            2.0f *
            imageController.shiftPixels;


        // =====================================================
        // Disparity Angle [deg]
        // =====================================================

        result.disparityAngleDeg =
            DispPxToAngleDeg(
                result.disparityPixels
            );

        result.shiftedDisparityAngleDeg =
            DispPxToAngleDeg(
                result.shiftedDisparityPixels
            );


        result.isValid = true;
        statusMessage = "OK";

        return result;
    }


    // =========================================================
    // Pixel Disparity -> Disparity Angle
    // =========================================================

    private float DispPxToAngleDeg(
        float disparityPixels
    )
    {
        // Pixel disparity -> Display上の物理距離 [mm]
        float disparityMm =
            disparityPixels *
            displayPixelPitchMm;


        // Display center = (0, 0, 0)
        // Viewer center  = (0, 0, -936)
        // Left Eye  = (-IPD/2, 0, -936)
        // Right Eye = (+IPD/2, 0, -936)

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


        // 左右画像上の対応点
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


        // 視差ありの場合の左右視線
        Vector3 leftRayWithDisparity =
            leftPoint -
            leftEye;

        Vector3 rightRayWithDisparity =
            rightPoint -
            rightEye;


        // 視差0の場合の左右視線
        Vector3 leftRayZeroDisparity =
            midPoint -
            leftEye;

        Vector3 rightRayZeroDisparity =
            midPoint -
            rightEye;


        // alpha : 視差ありの輻輳角
        // beta  : 画面中心を見るときの輻輳角
        // disparity angle = alpha - beta

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