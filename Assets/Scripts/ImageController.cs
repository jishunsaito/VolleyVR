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

    [Tooltip(
        "左右画像を逆方向にシフトする量[pixel]"
    )]
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

    public float stereoCameraRotationX =
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
    }


    private void Awake()
    {
        CacheOriginalCameraSettings();
    }


    private void Start()
    {
        CreateGuardBandRenderTextures();

        ApplyTexturesToMaterials();

        ApplyMaterialModes();

        ApplyAllParameters();
    }


    private void Update()
    {
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


        // Cameraの出力先を
        // Guard Band付きRenderTextureへ変更
        leftCamera.targetTexture =
            leftGuardTexture;

        rightCamera.targetTexture =
            rightGuardTexture;


        // -----------------------------------------------------
        // CameraのSensor Widthを拡張
        // -----------------------------------------------------

        ApplyGuardBandSensorSize();
    }


    private RenderTexture CreateGuardTexture(
        RenderTexture source,
        string textureName
    )
    {
        RenderTextureDescriptor descriptor =
            source.descriptor;


        // 元の表示幅
        int visibleWidth =
            source.width;


        // 左右Guard Bandを追加
        descriptor.width =
            visibleWidth +
            guardBandPixels * 2;


        // 高さは変更しない
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
        // Preview Material
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
        // RawImage
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
        /*
         * Main Display
         *
         * PreviewMode = 0
         *
         * Shader側で水平反転。
         * Wheatstoneの鏡を通したとき
         * 正しい向きになる。
         */

        SetMaterialMode(
            leftMaterial,
            false
        );

        SetMaterialMode(
            rightMaterial,
            false
        );


        /*
         * UI Preview
         *
         * PreviewMode = 1
         *
         * 水平反転しない。
         * RawImageのAlphaも使用する。
         */

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


        material.SetFloat(
            GuardBandPixelsProperty,
            guardBandPixels
        );
    }


    // =========================================================
    // Image Shift
    // =========================================================

    private void ApplyImageShift()
    {
        /*
         * 論理上のShift方向は
         * PreviewとMain Displayで同じ。
         *
         * Main DisplayはShader側で
         * 事前にMirrorしているので、
         * Wheatstoneの物理Mirrorを通した後に
         * Previewと同じ方向に見える。
         */

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
        float shift
    )
    {
        if (material == null)
        {
            return;
        }


        material.SetFloat(
            ShiftPixelsProperty,
            shift
        );


        material.SetFloat(
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


        // Left
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


        // Right
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


        /*
         * 焦点距離を変更しても
         * Guard Band用Sensor Widthは維持。
         */
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


        stereoCameraRoot.localEulerAngles =
            currentEulerAngles;
    }


    // =========================================================
    // Apply All
    // =========================================================

    private void ApplyAllParameters()
    {
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
        if (angle > 180.0f)
        {
            angle -=
                360.0f;
        }


        return angle;
    }
}