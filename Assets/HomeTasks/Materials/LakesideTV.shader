Shader "Home Focus/Lakeside TV"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            float Oval(float2 p, float2 center, float2 radius)
            {
                return 1.0 - smoothstep(0.96, 1.04, length((p - center) / radius));
            }

            float Ridge(float x, float offset)
            {
                return 0.12 * abs(sin(x * 6.0 + offset)) + 0.065 * abs(cos(x * 15.0 + offset));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv;
                float t = _Time.y;
                float daylight = 0.5 + 0.5 * sin(t * 0.10);
                const float horizon = 0.45;
                float3 sky = lerp(float3(0.85, 0.39, 0.19), float3(0.34, 0.69, 0.85), daylight);
                float3 color = lerp(float3(0.99, 0.78, 0.45), sky, saturate((p.y - horizon) / 0.55));

                float2 sun = float2(0.75, 0.67 + 0.13 * daylight);
                color = lerp(color, float3(1.0, 0.91, 0.59), Oval(p, sun, float2(0.038, 0.071)));
                float2 c = p - float2(frac(t * 0.018 + 0.19) * 1.4 - 0.2, 0.80);
                float cloud = max(Oval(c, float2(0, 0), float2(0.12, 0.023)),
                    Oval(c, float2(-0.020, 0.015), float2(0.055, 0.027)));
                float2 d = p - float2(frac(t * 0.012 + 0.72) * 1.4 - 0.2, 0.90);
                cloud = max(cloud, Oval(d, float2(0, 0), float2(0.080, 0.014)));
                color = lerp(color, float3(1.0, 0.91, 0.76), cloud * 0.62);

                float distant = horizon + 0.05 + Ridge(p.x, 1.9);
                float middle = horizon + 0.018 + Ridge(p.x * 1.34, 0.9) * 0.72;
                color = lerp(color, float3(0.30, 0.44, 0.49), 1.0 - smoothstep(distant - 0.003, distant + 0.003, p.y));
                color = lerp(color, float3(0.11, 0.29, 0.28), 1.0 - smoothstep(middle - 0.003, middle + 0.003, p.y));

                if (p.y < horizon)
                {
                    float depth = (horizon - p.y) / horizon;
                    float3 lake = lerp(float3(0.19, 0.39, 0.39), float3(0.025, 0.17, 0.21), depth);
                    float waves = sin(p.y * 205 + sin(p.x * 25.0 + t * 0.7) * 1.4 - t * 1.65);
                    float glimmer = saturate(1.0 - abs(p.x - sun.x) * (8.0 - 4.0 * depth));
                    color = lake + float3(0.42, 0.27, 0.13) * glimmer * (1.0 - depth) * smoothstep(0.3, 0.95, waves) * 0.55;
                    color += 0.027 * waves * (1.0 - depth * 0.45);
                }

                float2 boat = p - float2(0.50 + sin(t * 0.13) * 0.29, 0.295 + sin(t * 1.8) * 0.006);
                float hull = step(-0.020, boat.x) * step(boat.x, 0.022) * step(-0.012, boat.y) * step(boat.y, 0.001);
                float sail = step(0.002, boat.y) * step(boat.y, 0.086) * step(-0.004, boat.x) * step(boat.x, 0.026 - boat.y * 0.30);
                color = lerp(color, float3(0.65, 0.24, 0.12), hull);
                color = lerp(color, float3(0.98, 0.87, 0.66), sail);

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
