Shader "Custom/ImageController"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        _ShiftPixels ("Horizontal Shift Pixels", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        LOD 100

        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            float _ShiftPixels;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;

                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.uv;


                uv.x += _ShiftPixels * _MainTex_TexelSize.x;

                // îÕàÕäOÇ≈ÇÕí[ÇÃâÊëfÇéQè∆
                uv.x = clamp(uv.x, 0.0, 1.0);

                fixed4 color = tex2D(_MainTex, uv);
                color.a = 1.0;

                return color;
            }

            ENDCG
        }
    }

    FallBack Off
}