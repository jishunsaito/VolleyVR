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

    [Tooltip("UIのLeft RawImageに使用するMaterial")]
    [SerializeField]
    private Material leftPreviewMaterial;

    [Tooltip("UIのRight RawImageに使用するMaterial")]
    [SerializeField]
    private Material rightPreviewMaterial;


    // =========================================================
    // Preview RawImages
    // =========================================================

    [Header("Preview RawImages")]

    [Tooltip("左目映像確認用のRawImage")]
    [SerializeField]
    private RawImage leftPreviewRawImage;

    [Tooltip("右目映像確認用のRawImage")]
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


        // -----------------------------------------------------
        // Left
        // -----------------------------------------------------

        leftGuardTexture =
            CreateGuardTexture(
                originalLeftTexture,
                "LeftEye_GuardBand"
            );


        // -----------------------------------------------------
        // Right
        // -----------------------------------------------------

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
        // -----------------------------------------------------
        // Main Display
        // -----------------------------------------------------

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


        // -----------------------------------------------------
        // Preview Materials
        // -----------------------------------------------------

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


        // -----------------------------------------------------
        // Preview RawImages
        // -----------------------------------------------------

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
        // -----------------------------------------------------
        // Main Display
        // -----------------------------------------------------

        ApplyShiftToMaterial(
            leftMaterial,
            shiftPixels
        );


        ApplyShiftToMaterial(
            rightMaterial,
            -shiftPixels
        );


        // -----------------------------------------------------
        // Preview
        // -----------------------------------------------------

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


        // -----------------------------------------------------
        // Left
        // -----------------------------------------------------

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


        // -----------------------------------------------------
        // Right
        // -----------------------------------------------------

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


        // -----------------------------------------------------
        // Position
        // -----------------------------------------------------

        stereoCameraRoot.localPosition =
            stereoCameraPosition;


        // -----------------------------------------------------
        // Rotation
        //
        // X = Pitch
        // Y = Yaw / Court Side
        // Z = 現在値を維持
        // -----------------------------------------------------

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
    // Court Change
    // =========================================================

    /// <summary>
    /// StereoCameraを反対側のコートへ移動する。
    ///
    /// Position Z:
    /// z -> -z
    ///
    /// Rotation Y:
    /// y -> y + 180°
    ///
    /// もう一度呼ぶと元に戻る。
    /// </summary>
    public void ToggleCourt()
    {
        // -----------------------------------------------------
        // Z座標反転
        // -----------------------------------------------------

        Vector3 position =
            stereoCameraPosition;


        position.z =
            -position.z;


        stereoCameraPosition =
            position;


        // -----------------------------------------------------
        // Y回転 180°
        // -----------------------------------------------------

        stereoCameraRotationY =
            NormalizeAngle(
                stereoCameraRotationY +
                180.0f
            );


        // -----------------------------------------------------
        // 即時反映
        // -----------------------------------------------------

        ApplyStereoCameraTransform();


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