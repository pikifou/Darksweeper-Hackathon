Shader "DarkSweeper/FogOfWar"
{
    Properties
    {
        _MainTex ("Background Texture", 2D) = "white" {}
        _LightmapTex ("Lightmap (R=mask, G=distance)", 2D) = "black" {}
        _FogColor ("Fog Color", Color) = (0, 0, 0, 1)
        _LitTint ("Lit Area Tint", Color) = (1, 1, 1, 1)

        [Header(Light Gradient)]
        _MaxLight ("Max Light (center of lit area)", Range(0, 1)) = 1.0
        _MinLight ("Min Light (near fog edge)", Range(0, 1)) = 0.25
        _LightCurve ("Light Curve (1=linear, <1=bright near edge, >1=dark near edge)", Range(0.2, 4.0)) = 1.0

        [Header(Fog Edge)]
        _FogNoiseScale ("Noise Scale", Float) = 2.0
        _FogNoiseStrength ("Noise Strength", Range(0.0, 0.3)) = 0.06
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_LightmapTex);
            SAMPLER(sampler_LightmapTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _FogColor;
                float4 _LitTint;
                float _MaxLight;
                float _MinLight;
                float _LightCurve;
                float _FogNoiseScale;
                float _FogNoiseStrength;
            CBUFFER_END

            // Global — set from C# via Shader.SetGlobalVector
            float4 _DSGridBounds; // xy = grid min corner (world XZ), zw = grid world size

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 mainColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Compute lightmap UV from world position
                float2 lmUV = (input.positionWS.xz - _DSGridBounds.xy) / _DSGridBounds.zw;

                // ============================================================
                // Read two-channel lightmap:
                //   R = raw light mask (0=dark, 1=lit, bilinear-interpolated at edges)
                //   G = normalized distance to nearest dark cell
                //       (0 at fog edge, 1 deep inside lit area)
                // ============================================================
                half2 lm = SAMPLE_TEXTURE2D(_LightmapTex, sampler_LightmapTex, lmUV).rg;
                half rawMask = lm.r;
                half distGradient = lm.g;

                // ============================================================
                // BRIGHTNESS GRADIENT
                // Cells deep inside lit areas → MaxLight (e.g. 1.0)
                // Cells near the fog edge   → MinLight (e.g. 0.25)
                // LightCurve controls the shape:
                //   1.0 = linear
                //   >1  = stays darker longer, brightens near center
                //   <1  = brightens quickly, stays bright
                // ============================================================
                half curvedDist = pow(saturate(distGradient), _LightCurve);
                half gradient = lerp(_MinLight, _MaxLight, curvedDist);

                // ============================================================
                // COMBINE: raw mask × gradient
                // - Dark cells (rawMask=0): 0 → fully dark
                // - Lit cells near edge: 1.0 × MinLight → dim
                // - Lit cells deep inside: 1.0 × MaxLight → full bright
                // - Bilinear boundary: smooth transition
                // ============================================================
                half brightness = rawMask * gradient;

                // ============================================================
                // ORGANIC NOISE EDGE
                // Small subtractive noise at the transition zone.
                // Only darkens — fog expands slightly into lit areas.
                // ============================================================
                float2 noiseUV = input.positionWS.xz * _FogNoiseScale;
                half noise = frac(sin(dot(noiseUV, float2(12.9898, 78.233))) * 43758.5453);
                brightness = saturate(brightness - noise * _FogNoiseStrength);

                // ============================================================
                // FINAL COMPOSITE
                // brightness is a continuous 0..1 value acting as true
                // light intensity — NOT a binary mask.
                // ============================================================
                half3 finalColor = lerp(_FogColor.rgb, mainColor.rgb * _LitTint.rgb, brightness);
                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
