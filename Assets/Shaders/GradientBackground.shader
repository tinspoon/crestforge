Shader "Crestforge/GradientBackground"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.15, 0.12, 0.2, 1)
        _BottomColor ("Bottom Color", Color) = (0.08, 0.06, 0.1, 1)
        _GradientOffset ("Gradient Offset", Range(-1, 1)) = 0
        _GradientScale ("Gradient Scale", Range(0.5, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" }
        ZWrite Off
        Cull Off

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
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _TopColor;
            float4 _BottomColor;
            float _GradientOffset;
            float _GradientScale;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Calculate gradient based on vertical position
                float t = saturate((i.uv.y + _GradientOffset) * _GradientScale);

                // Smooth step for nicer blend
                t = smoothstep(0, 1, t);

                // Lerp between colors
                float4 color = lerp(_BottomColor, _TopColor, t);

                return color;
            }
            ENDCG
        }
    }
}
