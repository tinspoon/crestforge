Shader "Crestforge/GrassHex"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0.45, 0.65, 0.3, 1)
        _EdgeColor ("Edge Color", Color) = (0.35, 0.55, 0.25, 1)
        _OutlineColor ("Outline Color", Color) = (0.3, 0.5, 0.2, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
        _EdgeWidth ("Edge Width", Range(0, 0.3)) = 0.15
        _Brightness ("Brightness", Range(0.5, 1.5)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            float4 _MainColor;
            float4 _EdgeColor;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _EdgeWidth;
            float _Brightness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.localPos = v.vertex.xyz;
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Calculate distance from center for hex edge effect
                float2 hexCenter = float2(0, 0);
                float distFromCenter = length(i.localPos.xz);

                // Normalize to hex radius (approximately 0.5)
                float normalizedDist = distFromCenter / 0.5;

                // Create edge gradient
                float edgeFactor = smoothstep(1.0 - _EdgeWidth - _OutlineWidth, 1.0 - _OutlineWidth, normalizedDist);
                float outlineFactor = smoothstep(1.0 - _OutlineWidth, 1.0, normalizedDist);

                // Blend colors
                float3 color = lerp(_MainColor.rgb, _EdgeColor.rgb, edgeFactor);
                color = lerp(color, _OutlineColor.rgb, outlineFactor);

                // Simple lighting
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = dot(i.worldNormal, lightDir) * 0.5 + 0.5;
                float lighting = lerp(0.7, 1.0, NdotL);

                color *= lighting * _Brightness;

                return float4(color, 1);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
