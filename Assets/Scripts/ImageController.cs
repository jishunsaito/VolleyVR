using UnityEngine;

public class ImageController : MonoBehaviour
{
    // =========================================================
    // 画像シフト
    // =========================================================

    [Header("Shift Materials")]

    [SerializeField]
    private Material leftMaterial;

    [SerializeField]
    private Material rightMaterial;

    [Header("Image Shift")]

    [Tooltip("正の値で左画像と右画像を逆方向にシフトします")]
    public float shiftPixels = 0.0f;


    // =========================================================
    // ステレオカメラ
    // =========================================================

    [Header("Stereo Camera Objects")]

    [Tooltip("左右カメラを子に持つ親オブジェクト")]
    [SerializeField]
    private Transform stereoCameraRoot;

    [SerializeField]
    private Camera leftCamera;

    [SerializeField]
    private Camera rightCamera;


    // =========================================================
    // 基線長
    // =========================================================

    [Header("Baseline")]

    [Tooltip("基線長。例えば100なら左右をそれぞれ50ずつ離します")]
    [Min(0.0f)]
    public float baseline = 100.0f;

    [Tooltip(
        "基線長をUnity座標へ変換する倍率。" +
        "1 Unity Unit = 1 mなら0.001、" +
        "1 Unity Unit = 1 mmなら1を指定します"
    )]
    public float baselineUnitScale = 0.001f;


    // =========================================================
    // 焦点距離
    // =========================================================

    [Header("Focal Length")]

    [Tooltip("左右カメラに設定する焦点距離［mm］")]
    [Min(0.1f)]
    public float focalLength = 90.0f;


    // =========================================================
    // ステレオカメラ親の位置と回転
    // =========================================================

    [Header("Stereo Camera Root Transform")]

    [Tooltip("ステレオカメラ親オブジェクトのローカル座標")]
    public Vector3 stereoCameraPosition = Vector3.zero;

    [Tooltip("ステレオカメラ親オブジェクトのX軸回転［deg］")]
    public float stereoCameraRotationX = 0.0f;


    // =========================================================
    // Shaderプロパティ
    // =========================================================

    private static readonly int ShiftPixelsProperty =
        Shader.PropertyToID("_ShiftPixels");


    // =========================================================
    // Unityイベント
    // =========================================================

    private void Reset()
    {
        // このスクリプトをStereo Camera親に付けた場合は、
        // 自分自身を親オブジェクトとして登録
        stereoCameraRoot = transform;

        stereoCameraPosition = transform.localPosition;
        stereoCameraRotationX = NormalizeAngle(
            transform.localEulerAngles.x
        );
    }

    private void Update()
    {
        ApplyImageShift();
        ApplyBaseline();
        ApplyFocalLength();
        ApplyStereoCameraTransform();
    }


    // =========================================================
    // 画像シフト
    // =========================================================

    private void ApplyImageShift()
    {
        if (leftMaterial != null)
        {
            leftMaterial.SetFloat(
                ShiftPixelsProperty,
                shiftPixels
            );
        }

        if (rightMaterial != null)
        {
            rightMaterial.SetFloat(
                ShiftPixelsProperty,
                -shiftPixels
            );
        }
    }


    // =========================================================
    // 基線長
    // =========================================================

    private void ApplyBaseline()
    {
        if (leftCamera == null || rightCamera == null)
        {
            return;
        }

        /*
         * 例：
         * baseline = 100 mm
         * baselineUnitScale = 0.001
         *
         * 左カメラX = -0.05 Unity Unit
         * 右カメラX = +0.05 Unity Unit
         *
         * mm表記では左-50 mm、右+50 mm
         */
        float halfBaseline =
            baseline * 0.5f * baselineUnitScale;

        // 左カメラ
        Vector3 leftPosition =
            leftCamera.transform.localPosition;

        leftPosition.x = -halfBaseline;

        leftCamera.transform.localPosition =
            leftPosition;

        // 右カメラ
        Vector3 rightPosition =
            rightCamera.transform.localPosition;

        rightPosition.x = halfBaseline;

        rightCamera.transform.localPosition =
            rightPosition;
    }


    // =========================================================
    // 焦点距離
    // =========================================================

    private void ApplyFocalLength()
    {
        if (leftCamera != null)
        {
            leftCamera.usePhysicalProperties = true;
            leftCamera.focalLength = focalLength;
        }

        if (rightCamera != null)
        {
            rightCamera.usePhysicalProperties = true;
            rightCamera.focalLength = focalLength;
        }
    }


    // =========================================================
    // 親オブジェクトの位置・回転
    // =========================================================

    private void ApplyStereoCameraTransform()
    {
        if (stereoCameraRoot == null)
        {
            return;
        }

        // 親オブジェクト全体のXYZ座標
        stereoCameraRoot.localPosition =
            stereoCameraPosition;

        // Y軸、Z軸回転は現在値を維持し、X軸だけ変更
        Vector3 currentEulerAngles =
            stereoCameraRoot.localEulerAngles;

        currentEulerAngles.x =
            stereoCameraRotationX;

        stereoCameraRoot.localEulerAngles =
            currentEulerAngles;
    }


    // =========================================================
    // 角度表示用
    // =========================================================

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180.0f)
        {
            angle -= 360.0f;
        }

        return angle;
    }
}