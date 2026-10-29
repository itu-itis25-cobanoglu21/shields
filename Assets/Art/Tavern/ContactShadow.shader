// A soft round contact shadow on the floor, for characters without visible feet (the
// waiter). Drawn on a flat quad lying on the floor: darkest in the middle, fading to
// nothing at the quad's edge. _Strength is driven per character so the shadow tightens
// when the body is at its lowest (grounded) and fades a touch as it lifts.
Shader "Shields/Contact Shadow"
{
    Properties
    {
        _Color ("Shadow Colour", Color) = (0.02, 0.015, 0.01, 1)
        _Opacity ("Opacity At The Centre", Range(0, 1)) = 0.5
        _Softness ("Edge Softness", Range(0.05, 1)) = 0.75
        _Strength ("Strength (set per character)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-50"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ContactShadow"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Opacity;
                float _Softness;
                float _Strength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv * 2.0 - 1.0);
                float a = 1.0 - smoothstep(1.0 - _Softness, 1.0, r);
                return half4(_Color.rgb, a * _Opacity * _Strength);
            }
            ENDHLSL
        }
    }
}
