using UnityEngine;
using System.IO;

public class DepthMapSaver : MonoBehaviour
{
    public Camera targetCamera;
    public Material depthMaterial;
    public int width = 1920;
    public int height = 1080;

    public void SaveDepth()
    {
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture temp = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);

        targetCamera.depthTextureMode = DepthTextureMode.Depth;
        targetCamera.targetTexture = rt;
        targetCamera.Render();

        Graphics.Blit(rt, temp, depthMaterial);

        RenderTexture.active = temp;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(Application.dataPath + "/depth.png", bytes);

        targetCamera.targetTexture = null;
        RenderTexture.active = null;

        Destroy(rt);
        Destroy(temp);
        Destroy(tex);

        Debug.Log("depth saved");
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            SaveDepth();
        }
        
    }
}