// A stored Charge card burning: one rising fire raymarched inside a box that stands on
// the card face. The fire is defined in the card's own space, so every seat sees the
// same flame in the same place, and it cannot show outside the card's footprint.
//
// The box mesh is drawn front faces only; each pixel marches from where the view ray
// enters the box to where it leaves, adding up glowing fire density along the way.
Shader "Shields/Charge Card Flame Volume"
{
    Properties
    {
        [HDR] _CoreColor ("Core Colour", Color) = (2.4, 1.7, 3.2, 1)
        [HDR] _FlameColor ("Flame Colour", Color) = (1.1, 0.32, 2.6, 1)
        [HDR] _TipColor ("Tip Colour", Color) = (0.3, 0.04, 0.85, 1)

        _Intensity ("Intensity", Float) = 2.2
        _BedGlow ("Burning Surface Glow", Float) = 0.9
        _BedHeight ("Burning Surface Height (fraction of flame)", Range(0.02, 0.5)) = 0.1

        _NoiseScale ("Noise Scale (per metre)", Float) = 60
        _Stretch ("Vertical Stretch", Range(0.2, 1)) = 0.45
        _RiseSpeed ("Rise Speed (metres per second)", Float) = 0.09
        _Taper ("Taper Toward The Top", Range(0, 0.9)) = 0.6
        _EdgeSoftness ("Edge Softness (metres)", Float) = 0.007

        // Set per card by ChargeCardFlame.
        _Size ("Box Size (x, height, z)", Vector) = (0.063, 0.07, 0.088, 0)
        _Ignite ("Ignite", Range(0, 1)) = 1
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ChargeFlame"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _FlameColor;
                float4 _TipColor;
                float _Intensity;
                float _BedGlow;
                float _BedHeight;
                float _NoiseScale;
                float _Stretch;
                float _RiseSpeed;
                float _Taper;
                float _EdgeSoftness;
                float4 _Size;
                float _Ignite;
                float _Seed;
            CBUFFER_END

            #define STEPS 40

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
                         lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                         lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float v = 0.5 * Noise(p);
                p = p * 2.03 + 17.1;
                v += 0.25 * Noise(p);
                p = p * 2.01 + 9.7;
                v += 0.125 * Noise(p);
                return v / 0.875;
            }

            // Fire density at a card-space point; also returns how far up the flame it is.
            float Density(float3 p, float height, float time, out float h)
            {
                h = p.y / height;
                if (h >= 1.0) return 0.0;

                // The burning area narrows as it rises, always inside the card footprint.
                float2 halfSize = 0.5 * _Size.xz * (1.0 - _Taper * h);
                float edge = min(halfSize.x - abs(p.x), halfSize.y - abs(p.z));
                float mask = saturate(edge / _EdgeSoftness);
                if (mask <= 0.0) return 0.0;

                float3 q = float3(p.x, (p.y - time * _RiseSpeed) * _Stretch, p.z) * _NoiseScale + _Seed;

                // Licks bend sideways more the higher they get, but stay inside the mask.
                float sway = Noise(q * 0.35 + 3.1) - 0.5;
                q.xz += sway * h * 2.5;

                float n = Fbm(q);

                // Tongues: only the strongest noise survives higher up, with crisp edges.
                float threshold = lerp(0.46, 0.84, h);
                float tongues = smoothstep(threshold, threshold + 0.05, n) * (1.0 - h * h);

                // The card face itself burning: a thin, patchy, flickering bed of fire.
                float bed = exp2(-h / _BedHeight) * (0.35 + 0.65 * smoothstep(0.25, 0.7, n)) * _BedGlow;

                return (tongues + bed) * mask;
            }

            float3 Ramp(float heat)
            {
                float3 c = lerp(_TipColor.rgb, _FlameColor.rgb, smoothstep(0.0, 0.45, heat));
                return lerp(c, _CoreColor.rgb, smoothstep(0.45, 1.0, heat));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = max(_Size.y * _Ignite, 1e-4);
                float3 boxMin = float3(-0.5 * _Size.x, 0.0, -0.5 * _Size.z);
                float3 boxMax = float3(0.5 * _Size.x, height, 0.5 * _Size.z);

                float3 origin = TransformWorldToObject(GetCameraPositionWS());
                float3 dir = normalize(input.positionOS - origin);

                // Where the view ray enters and leaves the box.
                float3 inv = 1.0 / (dir + (dir == 0.0) * 1e-6);
                float3 t0 = (boxMin - origin) * inv;
                float3 t1 = (boxMax - origin) * inv;
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                float tNear = max(max(max(tMin.x, tMin.y), tMin.z), 0.0);
                float tFar = min(min(tMax.x, tMax.y), tMax.z);
                if (tFar <= tNear) return 0;

                float stepLength = (tFar - tNear) / STEPS;

                // Per-pixel offset so the fixed steps do not band.
                float jitter = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));

                float time = _Time.y;
                float3 colour = 0.0;

                [loop]
                for (int s = 0; s < STEPS; s++)
                {
                    float3 p = origin + dir * (tNear + (s + jitter) * stepLength);

                    float h;
                    float d = Density(p, height, time, h);
                    if (d <= 0.0) continue;

                    // Hottest at the card face, cooling as the fire rises.
                    float heat = saturate(d) * (1.0 - h) * (1.0 - h);
                    colour += Ramp(heat) * d;
                }

                colour *= stepLength / _Size.y * _Intensity * _Ignite;
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
