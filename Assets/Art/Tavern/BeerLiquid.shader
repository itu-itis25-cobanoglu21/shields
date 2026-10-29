// Beer inside a mug, without any fluid simulation.
//
// The mesh is a closed volume that sits just inside the mug's inner wall (bottom and side,
// no top). Every pixel above a world horizontal level (_FillLevelWS, set per mug by
// BeerMugVisual) is discarded. Seen through the open top, what remains of the volume's
// inside (its back faces) is shaded as the flat liquid surface, so the beer reads as a
// level surface at any mug tilt, and since clipping only removes, it can never show
// outside the mug.
Shader "Shields/Beer Liquid"
{
    Properties
    {
        _LiquidColor ("Liquid Colour", Color) = (0.80, 0.50, 0.10, 1)
        _SurfaceColor ("Surface Colour", Color) = (0.96, 0.72, 0.24, 1)
        _FoamColor ("Foam Ring Colour", Color) = (1.0, 0.92, 0.72, 1)

        _FoamWidth ("Foam Ring Width (object units)", Float) = 0.014
        _SurfaceRadius ("Surface Radius (object units)", Float) = 0.078
        _AxisOS ("Mug Axis (object x, z)", Vector) = (-0.0012, 0.003, 0, 0)

        _SelfLight ("Self Light (keeps it readable in a dim tavern)", Range(0, 1)) = 0.28
        _Specular ("Surface Glint", Range(0, 1)) = 0.25

        // Set per mug by BeerMugVisual. Very high means "full to the brim of the mesh".
        _FillLevelWS ("Fill Level (world Y)", Float) = 1000
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
            "RenderType" = "Opaque"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _LiquidColor;
            float4 _SurfaceColor;
            float4 _FoamColor;
            float _FoamWidth;
            float _SurfaceRadius;
            float4 _AxisOS;
            float _SelfLight;
            float _Specular;
            float _FillLevelWS;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "BeerLiquid"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float3 Shade(float3 albedo, float3 normalWS, float3 positionWS, float4 positionCS)
            {
                float3 light = SampleSH(normalWS);

                Light mainLight = GetMainLight();
                light += mainLight.color * saturate(dot(normalWS, mainLight.direction)) * mainLight.distanceAttenuation;

            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = normalWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light additional = GetAdditionalLight(lightIndex, positionWS);
                    light += additional.color * additional.distanceAttenuation * saturate(dot(normalWS, additional.direction));
                LIGHT_LOOP_END
            #endif

                return albedo * (light + _SelfLight);
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // Only the beer below the level exists.
                clip(_FillLevelWS - input.positionWS.y);

                bool front = IS_FRONT_VFACE(face, true, false);
                if (front)
                {
                    // The volume's outside: only ever seen from right at the rim.
                    float3 n = normalize(input.normalWS);
                    return half4(Shade(_LiquidColor.rgb, n, input.positionWS, input.positionCS), 1);
                }

                // The inside, seen through the cut: shade it as the flat surface. Find where
                // the view ray actually crosses the level, for the foam ring at the wall.
                float3 viewDir = input.positionWS - GetCameraPositionWS();
                float t = abs(viewDir.y) > 1e-5 ? (_FillLevelWS - GetCameraPositionWS().y) / viewDir.y : 1.0;
                float3 surfaceWS = GetCameraPositionWS() + viewDir * saturate(t);
                float3 surfaceOS = TransformWorldToObject(surfaceWS);
                float radial = length(surfaceOS.xz - _AxisOS.xy);
                float foam = smoothstep(_SurfaceRadius - _FoamWidth, _SurfaceRadius, radial);

                float3 albedo = lerp(_SurfaceColor.rgb, _FoamColor.rgb, foam * 0.85);
                float3 up = float3(0, 1, 0);
                float3 color = Shade(albedo, up, surfaceWS, input.positionCS);

                // A soft glint so the surface reads as liquid.
                Light mainLight = GetMainLight();
                float3 toEye = normalize(-viewDir);
                float glint = pow(saturate(dot(up, normalize(toEye + mainLight.direction))), 48.0);
                color += mainLight.color * glint * _Specular;

                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(_FillLevelWS - input.positionWS.y);
                return 0;
            }
            ENDHLSL
        }

    }
}
