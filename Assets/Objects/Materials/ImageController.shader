Shader "Custom/ImageController"
{
    Properties
    {
        [MainTexture]
        _MainTex (
            "Texture",
            2D
        ) = "white" {}


        _ShiftPixels (
            "Horizontal Shift Pixels",
            Float
        ) = 0


        [Toggle]
        _PreviewMode (
            "Preview Mode",
            Float
        ) = 0


        _GuardBandPixels (
            "Guard Band Pixels Per Side",
            Float
        ) = 0


        _OutOfRangeColor (
            "Out Of Range Color",
            Color
        ) = (0, 0, 0, 1)
    }


    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }


        Cull Off

        ZWrite Off

        ZTest Always


        Blend
            SrcAlpha
            OneMinusSrcAlpha


        Pass
        {
            CGPROGRAM


            #pragma vertex vert
            #pragma fragment frag


            #include "UnityCG.cginc"


            sampler2D _MainTex;


            float4 _MainTex_TexelSize;


            float _ShiftPixels;

            float _PreviewMode;

            float _GuardBandPixels;

            fixed4 _OutOfRangeColor;


            // =================================================
            // Vertex Input
            // =================================================

            struct appdata
            {
                float4 vertex :
                    POSITION;

                float2 uv :
                    TEXCOORD0;

                fixed4 color :
                    COLOR;
            };


            // =================================================
            // Vertex -> Fragment
            // =================================================

            struct v2f
            {
                float4 vertex :
                    SV_POSITION;

                float2 uv :
                    TEXCOORD0;

                fixed4 color :
                    COLOR;
            };


            // =================================================
            // Vertex Shader
            // =================================================

            v2f vert(
                appdata input
            )
            {
                v2f output;


                output.vertex =
                    UnityObjectToClipPos(
                        input.vertex
                    );


                output.uv =
                    input.uv;


                output.color =
                    input.color;


                return output;
            }


            // =================================================
            // Fragment Shader
            // =================================================

            fixed4 frag(
                v2f input
            ) : SV_Target
            {
                // =============================================
                // Texture information
                // =============================================

                float sourceWidth =
                    _MainTex_TexelSize.z;


                float sourceHeight =
                    _MainTex_TexelSize.w;


                float guardPixels =
                    max(
                        _GuardBandPixels,
                        0.0
                    );


                /*
                 * Guard Bandを除いた、
                 * 本来表示する画像幅
                 */
                float visibleWidth =
                    sourceWidth -
                    2.0 *
                    guardPixels;


                if (visibleWidth <= 1.0)
                {
                    return
                        _OutOfRangeColor;
                }


                // =============================================
                // Preview Mode
                // =============================================

                /*
                 * PreviewMode = 1
                 *
                 * UI RawImage
                 * → 左右反転しない
                 *
                 *
                 * PreviewMode = 0
                 *
                 * Wheatstone Display
                 * → 水平反転して出力
                 *
                 * その後、実際のMirrorで
                 * もう一度反転されるため、
                 * 観察者には通常方向で見える。
                 */

                float previewEnabled =
                    step(
                        0.5,
                        _PreviewMode
                    );


                // =============================================
                // Horizontal coordinate
                // =============================================

                float logicalU;


                if (previewEnabled > 0.5)
                {
                    // UI Preview
                    logicalU =
                        input.uv.x;
                }
                else
                {
                    // Wheatstone Display
                    logicalU =
                        1.0 -
                        input.uv.x;
                }


                // =============================================
                // Output pixel -> Source pixel
                // =============================================

                /*
                 * Shift = 0:
                 *
                 * [Guard][ Visible Image ][Guard]
                 *          ↑ここだけ表示
                 *
                 *
                 * Shiftすると、
                 * Guard側へサンプリング領域が移動する。
                 */

                float sourcePixelX =
                    guardPixels +
                    logicalU *
                    visibleWidth +
                    _ShiftPixels;


                // =============================================
                // Out of Guard Band
                // =============================================

                /*
                 * 正常なShift範囲では
                 * Guard Band内の実際のSceneが表示される。
                 *
                 * Guard Bandを超えた場合のみ
                 * 黒などの指定色を表示。
                 */

                if (sourcePixelX < 0.0 ||
                    sourcePixelX >= sourceWidth)
                {
                    fixed4 outColor =
                        _OutOfRangeColor;


                    if (previewEnabled > 0.5)
                    {
                        outColor.a *=
                            input.color.a;
                    }
                    else
                    {
                        outColor.a =
                            1.0;
                    }


                    return
                        outColor;
                }


                // =============================================
                // Pixel -> UV
                // =============================================

                /*
                 * +0.5はPixel Centerを読むため。
                 */

                float sampleU =
                    (
                        sourcePixelX +
                        0.5
                    ) /
                    sourceWidth;


                float2 sampleUV =
                    float2(
                        sampleU,
                        input.uv.y
                    );


                // =============================================
                // Sample
                // =============================================

                fixed4 color =
                    tex2D(
                        _MainTex,
                        sampleUV
                    );


                // RawImageなどのVertex Color
                color.rgb *=
                    input.color.rgb;


                // =============================================
                // Alpha
                // =============================================

                /*
                 * Preview:
                 * RawImage側のAlphaを使用
                 *
                 * Main Display:
                 * 常に不透明
                 */

                if (previewEnabled > 0.5)
                {
                    color.a *=
                        input.color.a;
                }
                else
                {
                    color.a =
                        1.0;
                }


                return
                    color;
            }


            ENDCG
        }
    }


    FallBack Off
}