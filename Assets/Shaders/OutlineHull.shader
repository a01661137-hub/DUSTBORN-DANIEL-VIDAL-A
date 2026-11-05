// Contorno estilo caricatura para URP.
// Dibuja una copia del modelo inflada por detrás, en un solo color (negro por defecto).
// El grosor está en PIXELES de pantalla, así se ve parecido en modelos chicos y en modelos grandes.
Shader "Custom/OutlineHull"
{
    Properties
    {
        _OutlineColor ("Color del contorno", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Grosor (pixeles)", Range(0, 10)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // Empuja el vertice hacia afuera en pantalla, con grosor constante en pixeles.
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                float2 direction = normalize(normalCS + 1e-5);

                positionCS.xy += direction * _OutlineWidth * 2.0 / _ScreenParams.xy * positionCS.w;

                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
