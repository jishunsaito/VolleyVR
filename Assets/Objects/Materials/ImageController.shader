Shader "Custom/ImageController"
{
    Properties
    {
        [MainTexture]
        _MainTex ("Texture", 2D) = "white" {}

        _ShiftPixels (
            "Horizontal Shift Pixels",
            Float
        ) = 0

        [Toggle]
        _PreviewMode (
            "Preview Mode",
            Float
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

        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;

            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            float _ShiftPixels;
            float _PreviewMode;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                // RawImage��Color
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata input)
            {
                v2f output;

                output.vertex =
                    UnityObjectToClipPos(input.vertex);

                output.uv =
                    TRANSFORM_TEX(
                        input.uv,
                        _MainTex
                    );

                // RawImage��Color��Alpha���󂯎��
                output.color = input.color;

                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.uv;

                uv.x +=
                    _ShiftPixels *
                    _MainTex_TexelSize.x;

                uv.x = clamp(
                    uv.x,
                    0.0,
                    1.0
                );

                fixed4 color =
                    tex2D(_MainTex, uv);

                // RawImage��RGB�F�͔��f
                color.rgb *= input.color.rgb;

                /*
                 * Preview Mode = 0
                 * RawImage��Alpha�𖳎����ĕs����
                 *
                 * Preview Mode = 1
                 * RawImage��Alpha���g�p
                 */
                float previewEnabled =
                    step(0.5, _PreviewMode);

                color.a = lerp(
                    1.0,
                    input.color.a,
                    previewEnabled
                );

                return color;
            }

            ENDCG
        }
    }

    FallBack Off
}