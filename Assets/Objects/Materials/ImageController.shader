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


        _FadeAmount (
            "Experiment Fade Amount",
            Range(0, 1)
        ) = 0
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

            float _FadeAmount;


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
            // Experiment Fade
            // =================================================

            fixed4 ApplyExperimentFade(
                fixed4 inputColor
            )
            {
                float amount =
                    saturate(
                        _FadeAmount
                    );


                inputColor.rgb =
                    lerp(
                        inputColor.rgb,
                        fixed3(
                            0.0,
                            0.0,
                            0.0
                        ),
                        amount
                    );


                inputColor.a =
                    lerp(
                        inputColor.a,
                        1.0,
                        amount
                    );


                return
                    inputColor;
            }


            // =================================================
            // Fragment Shader
            // =================================================

            fixed4 frag(
                v2f input
            ) : SV_Target
            {
                float sourceWidth =
                    _MainTex_TexelSize.z;


                float sourceHeight =
                    _MainTex_TexelSize.w;


                float guardPixels =
                    max(
                        _GuardBandPixels,
                        0.0
                    );


                float visibleWidth =
                    sourceWidth -
                    2.0 *
                    guardPixels;


                if (visibleWidth <= 1.0)
                {
                    return
                        ApplyExperimentFade(
                            _OutOfRangeColor
                        );
                }


                float previewEnabled =
                    step(
                        0.5,
                        _PreviewMode
                    );


                float logicalU;


                if (previewEnabled > 0.5)
                {
                    logicalU =
                        input.uv.x;
                }
                else
                {
                    logicalU =
                        1.0 -
                        input.uv.x;
                }


                float sourcePixelX =
                    guardPixels +
                    logicalU *
                    visibleWidth +
                    _ShiftPixels;


                if (
                    sourcePixelX < 0.0 ||
                    sourcePixelX >= sourceWidth
                )
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
                        ApplyExperimentFade(
                            outColor
                        );
                }


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


                fixed4 color =
                    tex2D(
                        _MainTex,
                        sampleUV
                    );


                color.rgb *=
                    input.color.rgb;


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
                    ApplyExperimentFade(
                        color
                    );
            }


            ENDCG
        }
    }


    FallBack Off
}