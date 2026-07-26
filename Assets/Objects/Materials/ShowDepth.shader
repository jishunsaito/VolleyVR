Shader "Custom/ShowDepth"
{
    Properties
    {
        _MinDepth ("Min Depth", Float) = 20
        _MaxDepth ("Max Depth", Float) = 80
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _CameraDepthTexture;
            float _MinDepth;
            float _MaxDepth;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float eyeDepth = LinearEyeDepth(rawDepth); // 実距離っぽい値

                float d = saturate((eyeDepth - _MinDepth) / (_MaxDepth - _MinDepth));

                return float4(d, d, d, 1);
            }
            ENDCG
        }
    }
}