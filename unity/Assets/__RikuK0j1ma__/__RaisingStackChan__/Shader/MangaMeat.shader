// Toon-lit shader for the manga meat. Bites are spherical holes carved from the surface (up to 8):
// - A fragment inside a bite is discarded, so a bite takes a chunk out of the meat.
// - The mesh is two-sided. Looking through a bite you see the back faces of the shell, drawn as the inside of the meat
//   (flesh color with a marbled pattern). The front faces near the cut get a lighter "cooked edge" color.
// Bites are passed from script as _Bite0.._Bite7: xyz is the center in object space, w is the radius in meters.
// They are separate vectors in the material buffer (an array outside it is not filled when the SRP Batcher is used).
// _MetersPerUnit converts object space units to meters (the lossy scale of the renderer).
// Lighting is a fixed toon shade so the look does not depend on the scene lights (passthrough MR has none that fit).
Shader "RaisingStackChan/MangaMeat"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _InnerColor ("Inner Color", Color) = (0.93, 0.45, 0.42, 1)
        _InnerColor2 ("Inner Color 2 (marbling)", Color) = (0.98, 0.7, 0.62, 1)
        _RimColor ("Cut Edge Color", Color) = (1.0, 0.8, 0.5, 1)
        _RimWidth ("Cut Edge Width (m)", Float) = 0.006
        _EdgeNoise ("Bite Edge Roughness", Range(0, 1)) = 0.35
        _ShadeMin ("Darkest Shade", Range(0, 1)) = 0.6
        _MetersPerUnit ("Meters Per Unit", Float) = 1
        _BiteCount ("Bite Count", Float) = 0
        [HideInInspector] _Bite0 ("Bite 0", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Bite1 ("Bite 1", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Bite2 ("Bite 2", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Bite3 ("Bite 3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Bite4 ("Bite 4", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Bite5 ("Bite 5", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Bite6 ("Bite 6", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Bite7 ("Bite 7", Vector) = (0, 0, 0, 0)
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
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _InnerColor;
                half4 _InnerColor2;
                half4 _RimColor;
                float _RimWidth;
                float _EdgeNoise;
                float _ShadeMin;
                float _MetersPerUnit;
                float _BiteCount;
                float4 _Bite0;
                float4 _Bite1;
                float4 _Bite2;
                float4 _Bite3;
                float4 _Bite4;
                float4 _Bite5;
                float4 _Bite6;
                float4 _Bite7;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // Value noise in [0, 1].
            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = Hash31(i);
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                            lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 pm = input.positionOS * _MetersPerUnit;

                // Distance (m) inside the deepest bite. Positive means this point is carved away.
                float cut = -1.0;
                int count = (int)_BiteCount;
                float4 bites[8] = { _Bite0, _Bite1, _Bite2, _Bite3, _Bite4, _Bite5, _Bite6, _Bite7 };
                float rough = (ValueNoise(pm * 90.0) - 0.5) * 2.0;
                [unroll]
                for (int k = 0; k < 8; k++)
                {
                    if (k < count)
                    {
                        float d = distance(input.positionOS, bites[k].xyz) * _MetersPerUnit;
                        float r = bites[k].w * (1.0 + rough * _EdgeNoise);
                        cut = max(cut, r - d);
                    }
                }
                clip(-cut);

                bool isFront = IS_FRONT_VFACE(face, true, false);
                float3 n = normalize(input.normalWS);
                if (!isFront) n = -n;

                half3 albedo;
                if (isFront)
                {
                    albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                    // Lighter cooked edge right around the cut.
                    half edge = saturate(1.0 + cut / max(_RimWidth, 1e-5));
                    albedo = lerp(albedo, _RimColor.rgb, edge * step(-0.5, cut));
                }
                else
                {
                    // Inside of the meat: flesh with a marbled pattern.
                    half marble = ValueNoise(pm * float3(60.0, 60.0, 60.0)) * 0.6 + ValueNoise(pm * 160.0) * 0.4;
                    albedo = lerp(_InnerColor.rgb, _InnerColor2.rgb, smoothstep(0.35, 0.75, marble));
                }

                float3 lightDir = normalize(float3(0.3, 0.8, 0.45));
                half ndl = dot(n, lightDir) * 0.5 + 0.5;
                half shade = lerp(_ShadeMin, 1.0, ndl);
                return half4(albedo * shade, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
