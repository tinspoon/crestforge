Shader "Crestforge/MedievalToon"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0.3, 0.3, 0.4, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSoftness ("Shadow Softness", Range(0.001, 0.5)) = 0.05

        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 0.5

        [Header(Specular)]
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularIntensity ("Specular Intensity", Range(0, 1)) = 0.3
        _SpecularSize ("Specular Size", Range(0, 1)) = 0.1

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.1, 0.1, 0.1, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.05)) = 0.01

        [Header(Emission)]
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionIntensity ("Emission Intensity", Range(0, 3)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        // Main pass
        Pass
        {
            Name "MAIN"
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                SHADOW_COORDS(3)
            };

            float4 _MainColor;
            float4 _ShadowColor;
            float _ShadowThreshold;
            float _ShadowSoftness;
            float4 _RimColor;
            float _RimPower;
            float _RimIntensity;
            float4 _SpecularColor;
            float _SpecularIntensity;
            float _SpecularSize;
            float4 _EmissionColor;
            float _EmissionIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                TRANSFER_SHADOW(o);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Normalize vectors
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);

                // Basic NdotL for toon shading
                float NdotL = dot(normal, lightDir);

                // Stepped shadow with configurable softness
                float shadow = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, NdotL * 0.5 + 0.5);

                // Apply Unity shadows
                float atten = SHADOW_ATTENUATION(i);
                shadow *= atten;

                // Blend between shadow and lit color
                float3 diffuse = lerp(_ShadowColor.rgb * _MainColor.rgb, _MainColor.rgb, shadow);

                // Specular (toon style - hard edge)
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = dot(normal, halfDir);
                float specular = smoothstep(1 - _SpecularSize - 0.01, 1 - _SpecularSize + 0.01, NdotH);
                specular *= _SpecularIntensity * shadow;

                // Rim lighting
                float rim = 1.0 - saturate(dot(viewDir, normal));
                rim = pow(rim, _RimPower) * _RimIntensity;
                float3 rimColor = _RimColor.rgb * rim;

                // Combine
                float3 finalColor = diffuse + _SpecularColor.rgb * specular + rimColor;

                // Add emission
                finalColor += _EmissionColor.rgb * _EmissionIntensity;

                // Add ambient
                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * _MainColor.rgb * 0.5;
                finalColor += ambient;

                return float4(finalColor, 1);
            }
            ENDCG
        }

        // Outline pass
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float _OutlineWidth;
            float4 _OutlineColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                // Expand vertices along normals
                float3 norm = normalize(v.normal);
                v.vertex.xyz += norm * _OutlineWidth;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            v2f vert(appdata v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
