Shader "DarkSweeper/CellOverlay"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.22, 0.22, 0.25, 1)
        _Brightness ("Brightness", Range(0, 1)) = 0
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _BorderColor ("Border Color", Color) = (1, 1, 1, 0)
        _BorderWidth ("Border Width", Range(0, 0.15)) = 0.06
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
        }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Brightness;
                half4 _EmissionColor;
                half4 _BorderColor;
                half _BorderWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ---- Border detection ----
                half2 distToEdge = min(input.uv, 1.0 - input.uv);
                half minDist = min(distToEdge.x, distToEdge.y);
                half borderMask = 1.0 - smoothstep(_BorderWidth * 0.7, _BorderWidth, minDist);

                // ---- Base fill (interior) ----
                half3 baseColor = _BaseColor.rgb + _EmissionColor.rgb;
                half emissionAlpha = max(_EmissionColor.r, max(_EmissionColor.g, _EmissionColor.b));
                half baseAlpha = max(_BaseColor.a * _Brightness, emissionAlpha);

                // ---- Border ----
                // Border alpha uses max(_Brightness, emissionAlpha) so it's visible
                // when either the cell is lit OR has emission (hover in the dark).
                half borderVis = max(_Brightness, emissionAlpha);
                half borderAlpha = borderMask * _BorderColor.a * borderVis;

                // ---- Composite ----
                half3 finalColor = lerp(baseColor, _BorderColor.rgb, borderMask * _BorderColor.a);
                half finalAlpha = max(baseAlpha, borderAlpha);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
