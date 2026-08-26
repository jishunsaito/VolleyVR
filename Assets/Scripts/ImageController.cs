using UnityEngine;
using UnityEngine.UI;

public class ImageController : MonoBehaviour
{
    // =========================================================
    // Materials
    // =========================================================

    [Header("Main Display Materials")]
    [SerializeField]
    private Material leftMaterial;

    [SerializeField]
    private Material rightMaterial;


    [Header("Preview Materials")]
    [SerializeField]
    private Material leftPreviewMaterial;

    [SerializeField]
    private Material rightPreviewMaterial;


    // =========================================================
    // Preview RawImages
    // =========================================================

    [Header("Preview RawImages")]
    [SerializeField]
    private RawImage leftPreviewRawImage;

    [SerializeField]
    private RawImage rightPreviewRawImage;


    // =========================================================
    // Image Shift
    // =========================================================

    [Header("Image Shift")]

    [Tooltip("左右画像を逆方向にシフトする量[pixel]")]
    public int shiftPixels = 0;


    // =========================================================
    // Guard Band
    // =========================================================

    [Header("Guard Band")]

    [Tooltip(
        "最終表示領域の左右に追加でレンダリングする幅[pixel]。\n" +
        "最大Shift以上にしてください。"
    )]
    [Min(0)]
    [SerializeField]
    private int guardBandPixels = 500;


    // =========================================================
    // Stereo Camera
    // =========================================================

    [Header("Stereo Camera Objects")]

    [Tooltip("左右Cameraを子に持つ親Transform")]
    [SerializeField]
    private Transform stereoCameraRoot;

    [SerializeField]
    private Camera leftCamera;

    [SerializeField]
    private Camera rightCamera;


    // =========================================================
    // Polar / Orbit Camera
    // =========================================================

    [Header("Polar / Orbit Camera")]

    [Tooltip(
        "極座標の中心。\n" +
        "ネット中心に配置したTransformを指定してください。"
    )]
    [SerializeField]
    private Transform orbitCenter;


    [Tooltip(
        "ネット中心からStereoCameraまでの距離 r [Unity Unit]"
    )]
    [Min(0.001f)]
    public float orbitRadius = 10.0f;


    [Tooltip(
        "ネット中心から見た仰角 Theta [deg]\n" +
        "0° = ネット中心と同じ高さ\n" +
        "正 = ネット中心より上"
    )]
    public float orbitThetaDeg = 0.0f;


    /*
     * Thetaを変更するときの円軌道の向き。
     *
     * ユーザーが直接操作する値ではない。
     *
     * 現在のCameraとNetCenterのXZ方向から
     * 自動的に求める。
     */
    private float orbitAzimuthDeg = 180.0f;

    private bool orbitInitialized;


    // =========================================================
    // Baseline
    // =========================================================

    [Header("Baseline [mm]")]

    [Min(0.0f)]
    public float baseline = 100.0f;


    // =========================================================
    // Focal Length
    // =========================================================

    [Header("Focal Length [mm]")]

    [Min(0.1f)]
    public float focalLength = 90.0f;


    // =========================================================
    // Stereo Camera Transform
    // =========================================================

    [Header("Stereo Camera Root Transform")]

    public Vector3 stereoCameraPosition =
        Vector3.zero;


    [Tooltip("StereoCamera親のPitch [deg]")]
    public float stereoCameraRotationX =
        0.0f;


    [Tooltip("StereoCamera親のYaw [deg]")]
    public float stereoCameraRotationY =
        0.0f;


    // =========================================================
    // Shader Property IDs
    // =========================================================

    private static readonly int ShiftPixelsProperty =
        Shader.PropertyToID("_ShiftPixels");

    private static readonly int GuardBandPixelsProperty =
        Shader.PropertyToID("_GuardBandPixels");

    private static readonly int PreviewModeProperty =
        Shader.PropertyToID("_PreviewMode");

    private static readonly int MainTextureProperty =
        Shader.PropertyToID("_MainTex");


    // =========================================================
    // Original Camera Settings
    // =========================================================

    private RenderTexture originalLeftTexture;
    private RenderTexture originalRightTexture;

    private Vector2 originalLeftSensorSize;
    private Vector2 originalRightSensorSize;


    // =========================================================
    // Runtime Guard Band RenderTextures
    // =========================================================

    private RenderTexture leftGuardTexture;
    private RenderTexture rightGuardTexture;


    // =========================================================
    // Unity
    // =========================================================

    private void Reset()
    {
        stereoCameraRoot =
            transform;

        stereoCameraPosition =
            transform.localPosition;

        stereoCameraRotationX =
            NormalizeAngle(
                transform.localEulerAngles.x
            );

        stereoCameraRotationY =
            NormalizeAngle(
                transform.localEulerAngles.y
            );
    }


    private void Awake()
    {
        CacheOriginalCameraSettings();

        /*
         * Inspector上のXYZ/Pitch/Yawを
         * まずTransformへ反映する。
         */
        ApplyStereoCameraTransform();

        /*
         * 現在のXYZから
         * r / Thetaを初期化する。
         *
         * AwakeなのでUIController.Startより先に実行される。
         */
        SynchronizePolarFromCartesian();
    }


    private void Start()
    {
        CreateGuardBandRenderTextures();

        ApplyTexturesToMaterials();

        // ApplyMaterialModes();

        ValidateMaterials();

        ApplyAllParameters();
    }


    private void Update()
    {
        // ApplyMaterialModes();

        ApplyImageShift();

        ApplyBaseline();

        ApplyFocalLength();

        ApplyStereoCameraTransform();
    }


    private void OnDestroy()
    {
        RestoreOriginalCameraSettings();

        ReleaseGuardBandTextures();
    }


    // =========================================================
    // Initial Cache
    // =========================================================

    private void CacheOriginalCameraSettings()
    {
        if (leftCamera != null)
        {
            originalLeftTexture =
                leftCamera.targetTexture;

            originalLeftSensorSize =
                leftCamera.sensorSize;
        }


        if (rightCamera != null)
        {
            originalRightTexture =
                rightCamera.targetTexture;

            originalRightSensorSize =
                rightCamera.sensorSize;
        }
    }


    // =========================================================
    // Guard Band RenderTexture
    // =========================================================

    private void CreateGuardBandRenderTextures()
    {
        if (leftCamera == null ||
            rightCamera == null)
        {
            Debug.LogError(
                "LeftCamera / RightCamera が設定されていません。",
                this
            );

            return;
        }


        if (originalLeftTexture == null ||
            originalRightTexture == null)
        {
            Debug.LogError(
                "LeftCamera / RightCamera に" +
                "元のRenderTextureを設定してください。",
                this
            );

            return;
        }


        leftGuardTexture =
            CreateGuardTexture(
                originalLeftTexture,
                "LeftEye_GuardBand"
            );


        rightGuardTexture =
            CreateGuardTexture(
                originalRightTexture,
                "RightEye_GuardBand"
            );


        leftCamera.targetTexture =
            leftGuardTexture;


        rightCamera.targetTexture =
            rightGuardTexture;


        ApplyGuardBandSensorSize();
    }


    private RenderTexture CreateGuardTexture(
        RenderTexture source,
        string textureName
    )
    {
        RenderTextureDescriptor descriptor =
            source.descriptor;


        int visibleWidth =
            source.width;


        descriptor.width =
            visibleWidth +
            guardBandPixels * 2;


        descriptor.height =
            source.height;


        RenderTexture texture =
            new RenderTexture(
                descriptor
            );


        texture.name =
            textureName;


        texture.filterMode =
            source.filterMode;


        texture.wrapMode =
            TextureWrapMode.Clamp;


        texture.Create();


        return texture;
    }


    // =========================================================
    // Sensor Size
    // =========================================================

    private void ApplyGuardBandSensorSize()
    {
        if (leftCamera != null &&
            originalLeftTexture != null)
        {
            float scale =
                GetOverscanScale(
                    originalLeftTexture.width
                );


            leftCamera.sensorSize =
                new Vector2(
                    originalLeftSensorSize.x *
                    scale,

                    originalLeftSensorSize.y
                );
        }


        if (rightCamera != null &&
            originalRightTexture != null)
        {
            float scale =
                GetOverscanScale(
                    originalRightTexture.width
                );


            rightCamera.sensorSize =
                new Vector2(
                    originalRightSensorSize.x *
                    scale,

                    originalRightSensorSize.y
                );
        }
    }


    private float GetOverscanScale(
        int visibleWidth
    )
    {
        if (visibleWidth <= 0)
        {
            return 1.0f;
        }


        float guardWidth =
            visibleWidth +
            guardBandPixels * 2.0f;


        return
            guardWidth /
            visibleWidth;
    }


    // =========================================================
    // Texture Assignment
    // =========================================================

    private void ApplyTexturesToMaterials()
    {
        if (leftMaterial != null &&
            leftGuardTexture != null)
        {
            leftMaterial.SetTexture(
                MainTextureProperty,
                leftGuardTexture
            );
        }


        if (rightMaterial != null &&
            rightGuardTexture != null)
        {
            rightMaterial.SetTexture(
                MainTextureProperty,
                rightGuardTexture
            );
        }


        if (leftPreviewMaterial != null &&
            leftGuardTexture != null)
        {
            leftPreviewMaterial.SetTexture(
                MainTextureProperty,
                leftGuardTexture
            );
        }


        if (rightPreviewMaterial != null &&
            rightGuardTexture != null)
        {
            rightPreviewMaterial.SetTexture(
                MainTextureProperty,
                rightGuardTexture
            );
        }


        if (leftPreviewRawImage != null &&
            leftGuardTexture != null)
        {
            leftPreviewRawImage.texture =
                leftGuardTexture;
        }


        if (rightPreviewRawImage != null &&
            rightGuardTexture != null)
        {
            rightPreviewRawImage.texture =
                rightGuardTexture;
        }
    }


    // =========================================================
    // Preview / Wheatstone Mode
    // =========================================================

    private void ApplyMaterialModes()
    {
        SetMaterialMode(
            leftMaterial,
            false
        );


        SetMaterialMode(
            rightMaterial,
            false
        );


        SetMaterialMode(
            leftPreviewMaterial,
            true
        );


        SetMaterialMode(
            rightPreviewMaterial,
            true
        );
    }


    private void SetMaterialMode(
        Material material,
        bool previewMode
    )
    {
        if (material == null)
        {
            return;
        }


        material.SetFloat(
            PreviewModeProperty,
            previewMode
                ? 1.0f
                : 0.0f
        );


        material.SetInt(
            GuardBandPixelsProperty,
            guardBandPixels
        );
    }


    // =========================================================
    // Material Validation
    // =========================================================

    private void ValidateMaterials()
    {
        if (leftMaterial != null &&
            leftPreviewMaterial != null &&
            leftMaterial == leftPreviewMaterial)
        {
            Debug.LogError(
                "Left Material と Left Preview Material に" +
                "同じMaterialが設定されています。\n" +
                "Main用とPreview用は別Materialにしてください。",
                this
            );
        }


        if (rightMaterial != null &&
            rightPreviewMaterial != null &&
            rightMaterial == rightPreviewMaterial)
        {
            Debug.LogError(
                "Right Material と Right Preview Material に" +
                "同じMaterialが設定されています。\n" +
                "Main用とPreview用は別Materialにしてください。",
                this
            );
        }
    }


    // =========================================================
    // Image Shift
    // =========================================================

    private void ApplyImageShift()
    {
        ApplyShiftToMaterial(
            leftMaterial,
            shiftPixels
        );


        ApplyShiftToMaterial(
            rightMaterial,
            -shiftPixels
        );


        ApplyShiftToMaterial(
            leftPreviewMaterial,
            shiftPixels
        );


        ApplyShiftToMaterial(
            rightPreviewMaterial,
            -shiftPixels
        );
    }


    private void ApplyShiftToMaterial(
        Material material,
        int shift
    )
    {
        if (material == null)
        {
            return;
        }


        material.SetInt(
            ShiftPixelsProperty,
            shift
        );


        material.SetInt(
            GuardBandPixelsProperty,
            guardBandPixels
        );
    }


    // =========================================================
    // Baseline
    // =========================================================

    private void ApplyBaseline()
    {
        if (leftCamera == null ||
            rightCamera == null)
        {
            return;
        }


        float halfBaseline =
            baseline *
            0.5f *
            0.001f;


        Vector3 leftPosition =
            leftCamera
                .transform
                .localPosition;


        leftPosition.x =
            -halfBaseline;


        leftCamera
            .transform
            .localPosition =
                leftPosition;


        Vector3 rightPosition =
            rightCamera
                .transform
                .localPosition;


        rightPosition.x =
            halfBaseline;


        rightCamera
            .transform
            .localPosition =
                rightPosition;
    }


    // =========================================================
    // Focal Length
    // =========================================================

    private void ApplyFocalLength()
    {
        if (leftCamera != null)
        {
            leftCamera.usePhysicalProperties =
                true;


            leftCamera.focalLength =
                focalLength;
        }


        if (rightCamera != null)
        {
            rightCamera.usePhysicalProperties =
                true;


            rightCamera.focalLength =
                focalLength;
        }


        ApplyGuardBandSensorSize();
    }


    // =========================================================
    // Stereo Camera Transform
    // =========================================================

    private void ApplyStereoCameraTransform()
    {
        if (stereoCameraRoot == null)
        {
            return;
        }


        stereoCameraRoot.localPosition =
            stereoCameraPosition;


        Vector3 currentEulerAngles =
            stereoCameraRoot
                .localEulerAngles;


        currentEulerAngles.x =
            stereoCameraRotationX;


        currentEulerAngles.y =
            stereoCameraRotationY;


        stereoCameraRoot.localEulerAngles =
            currentEulerAngles;
    }


    // =========================================================
    // Cartesian Position Control
    // =========================================================

    /// <summary>
    /// XYZ UI用。
    ///
    /// XYZ位置を変更したあと、
    /// その位置からr / Thetaを再計算する。
    ///
    /// XYZ操作ではLookAtは行わないため、
    /// 従来通りPitchを個別に操作できる。
    /// </summary>
    public void SetStereoCameraPosition(
        Vector3 localPosition
    )
    {
        stereoCameraPosition =
            localPosition;


        ApplyStereoCameraTransform();


        SynchronizePolarFromCartesian();
    }


    /// <summary>
    /// Pitch UI用。
    /// </summary>
    public void SetStereoCameraPitch(
        float pitchDeg
    )
    {
        stereoCameraRotationX =
            pitchDeg;


        ApplyStereoCameraTransform();
    }


    // =========================================================
    // Polar Coordinate Control
    // =========================================================

    /// <summary>
    /// 半径 r を変更。
    ///
    /// Thetaと軌道方向を維持したまま
    /// ネット中心からの距離だけ変更する。
    /// </summary>
    public void SetOrbitRadius(
        float radius
    )
    {
        if (!EnsureOrbitInitialized())
        {
            return;
        }


        orbitRadius =
            Mathf.Max(
                0.001f,
                radius
            );


        ApplyPolarToStereoCamera();
    }


    /// <summary>
    /// Thetaを変更。
    ///
    /// r一定なので
    /// ネット中心を中心とした円軌道を移動する。
    /// </summary>
    public void SetOrbitTheta(
        float thetaDeg
    )
    {
        if (!EnsureOrbitInitialized())
        {
            return;
        }


        orbitThetaDeg =
            NormalizeAngle(
                thetaDeg
            );


        ApplyPolarToStereoCamera();
    }


    /// <summary>
    /// 現在のXYZ位置から
    ///
    /// r
    /// Theta
    /// Azimuth
    ///
    /// を計算する。
    /// </summary>
    public void SynchronizePolarFromCartesian()
    {
        if (stereoCameraRoot == null ||
            orbitCenter == null)
        {
            orbitInitialized =
                false;

            return;
        }


        Vector3 cameraWorldPosition =
            stereoCameraRoot.position;


        Vector3 offset =
            cameraWorldPosition -
            orbitCenter.position;


        float radius =
            offset.magnitude;


        if (radius <= 0.000001f)
        {
            orbitRadius =
                0.001f;

            orbitThetaDeg =
                0.0f;

            orbitInitialized =
                true;

            return;
        }


        // -----------------------------
        // r
        // -----------------------------

        orbitRadius =
            radius;


        // -----------------------------
        // 水平方向距離
        // -----------------------------

        float horizontalDistance =
            new Vector2(
                offset.x,
                offset.z
            ).magnitude;


        // -----------------------------
        // Theta
        //
        // atan2(
        //     高さ差,
        //     水平距離
        // )
        // -----------------------------

        orbitThetaDeg =
            Mathf.Atan2(
                offset.y,
                horizontalDistance
            ) *
            Mathf.Rad2Deg;


        // -----------------------------
        // Azimuth
        //
        // Thetaを動かすときに
        // どの縦平面を円軌道にするか。
        //
        // 現在のCameraのXZ方向を使う。
        // -----------------------------

        if (horizontalDistance >
            0.000001f)
        {
            orbitAzimuthDeg =
                Mathf.Atan2(
                    offset.x,
                    offset.z
                ) *
                Mathf.Rad2Deg;
        }


        orbitInitialized =
            true;
    }


    /// <summary>
    /// r / Thetaから
    /// StereoCameraのXYZ位置を計算する。
    /// </summary>
    private void ApplyPolarToStereoCamera()
    {
        if (stereoCameraRoot == null ||
            orbitCenter == null)
        {
            return;
        }


        float radius =
            Mathf.Max(
                orbitRadius,
                0.001f
            );


        float thetaRad =
            orbitThetaDeg *
            Mathf.Deg2Rad;


        float azimuthRad =
            orbitAzimuthDeg *
            Mathf.Deg2Rad;


        /*
         * rを水平成分と垂直成分に分解
         *
         * horizontal = r cosθ
         * vertical   = r sinθ
         */

        float horizontalRadius =
            radius *
            Mathf.Cos(
                thetaRad
            );


        float verticalOffset =
            radius *
            Mathf.Sin(
                thetaRad
            );


        /*
         * 水平成分を
         * X / Zへ分解する。
         */

        Vector3 offset =
            new Vector3(

                horizontalRadius *
                Mathf.Sin(
                    azimuthRad
                ),

                verticalOffset,

                horizontalRadius *
                Mathf.Cos(
                    azimuthRad
                )
            );


        Vector3 targetWorldPosition =
            orbitCenter.position +
            offset;


        // -----------------------------
        // Camera移動
        // -----------------------------

        stereoCameraRoot.position =
            targetWorldPosition;


        /*
         * 既存のXYZ UIなどは
         * localPositionを使用しているので同期。
         */

        stereoCameraPosition =
            stereoCameraRoot.localPosition;


        // -----------------------------
        // 光軸をネット中心へ向ける
        // -----------------------------

        LookAtOrbitCenter();


        orbitInitialized =
            true;
    }


    // =========================================================
    // Look At Net Center
    // =========================================================

    private void LookAtOrbitCenter()
    {
        if (stereoCameraRoot == null ||
            orbitCenter == null)
        {
            return;
        }


        Vector3 direction =
            orbitCenter.position -
            stereoCameraRoot.position;


        if (direction.sqrMagnitude <=
            0.00000001f)
        {
            return;
        }


        Vector3 forward =
            direction.normalized;


        Vector3 up =
            Vector3.up;


        /*
         * CameraがNetCenterの真上/真下付近に来ると
         * forwardとVector3.upがほぼ平行になる。
         *
         * LookRotationが不安定になるのを避ける。
         */

        if (Mathf.Abs(
            Vector3.Dot(
                forward,
                up
            )
        ) > 0.999f)
        {
            up =
                Vector3.forward;
        }


        stereoCameraRoot.rotation =
            Quaternion.LookRotation(
                forward,
                up
            );


        /*
         * LookRotationで決まった回転を
         * 既存Pitch / Yawパラメータへ同期。
         */

        Vector3 localEuler =
            stereoCameraRoot.localEulerAngles;


        stereoCameraRotationX =
            NormalizeAngle(
                localEuler.x
            );


        stereoCameraRotationY =
            NormalizeAngle(
                localEuler.y
            );
    }


    // =========================================================
    // Orbit initialization
    // =========================================================

    private bool EnsureOrbitInitialized()
    {
        if (stereoCameraRoot == null)
        {
            Debug.LogWarning(
                "Stereo Camera Root が設定されていません。",
                this
            );

            return false;
        }


        if (orbitCenter == null)
        {
            Debug.LogWarning(
                "Polar / Orbit Camera の Orbit Center に" +
                "ネット中心Transformを設定してください。",
                this
            );

            return false;
        }


        if (!orbitInitialized)
        {
            SynchronizePolarFromCartesian();
        }


        return orbitInitialized;
    }


    // =========================================================
    // Court Change
    // =========================================================

    public void ToggleCourt()
    {
        Vector3 position =
            stereoCameraPosition;


        position.z =
            -position.z;


        stereoCameraPosition =
            position;


        stereoCameraRotationY =
            NormalizeAngle(
                stereoCameraRotationY +
                180.0f
            );


        ApplyStereoCameraTransform();


        // XYZ変更後なので極座標も同期
        SynchronizePolarFromCartesian();


        Debug.Log(
            "[ImageController] Court Changed\n" +
            $"Position Z = {stereoCameraPosition.z:F3}\n" +
            $"Rotation Y = {stereoCameraRotationY:F1} deg",
            this
        );
    }


    // =========================================================
    // Apply All
    // =========================================================

    private void ApplyAllParameters()
    {
        // ApplyMaterialModes();

        ApplyImageShift();

        ApplyBaseline();

        ApplyFocalLength();

        ApplyStereoCameraTransform();
    }


    // =========================================================
    // Restore
    // =========================================================

    private void RestoreOriginalCameraSettings()
    {
        if (leftCamera != null)
        {
            leftCamera.targetTexture =
                originalLeftTexture;


            leftCamera.sensorSize =
                originalLeftSensorSize;
        }


        if (rightCamera != null)
        {
            rightCamera.targetTexture =
                originalRightTexture;


            rightCamera.sensorSize =
                originalRightSensorSize;
        }
    }


    // =========================================================
    // Release Runtime Textures
    // =========================================================

    private void ReleaseGuardBandTextures()
    {
        if (leftGuardTexture != null)
        {
            leftGuardTexture.Release();


            Destroy(
                leftGuardTexture
            );


            leftGuardTexture =
                null;
        }


        if (rightGuardTexture != null)
        {
            rightGuardTexture.Release();


            Destroy(
                rightGuardTexture
            );


            rightGuardTexture =
                null;
        }
    }


    // =========================================================
    // Utility
    // =========================================================

    private static float NormalizeAngle(
        float angle
    )
    {
        angle %=
            360.0f;


        if (angle > 180.0f)
        {
            angle -=
                360.0f;
        }


        if (angle <= -180.0f)
        {
            angle +=
                360.0f;
        }


        return angle;
    }
}