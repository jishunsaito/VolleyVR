using UnityEngine;

public class ImageController : MonoBehaviour
{
    [Header("Shift Materials")]
    [SerializeField]
    private Material leftMaterial;

    [SerializeField]
    private Material rightMaterial;


    public int ShiftPixel = 0;

    private static readonly int ShiftPixelsProperty =
        Shader.PropertyToID("_ShiftPixels");

    private void Update()
    {
        if (leftMaterial == null || rightMaterial == null)
        {
            return;
        }

        leftMaterial.SetFloat(
            ShiftPixelsProperty,
            ShiftPixel
        );

        rightMaterial.SetFloat(
            ShiftPixelsProperty,
            -ShiftPixel
        );
    }
}