// Toon-lit shader for the manga meat. Bites are spherical chunks taken out of a solid meat (up to 8):
// - The mesh is only a shell, so the cut face is made here: a front-face fragment inside a bite is replaced by the
//   point where its view ray leaves the bite sphere (a concave dent), drawn as marbled flesh and written to depth.
//   If that point is outside the meat (_MeatCenter/_MeatExtents ellipsoid) the bite went through, so it is discarded.
// - The front faces near the cut get a lighter "cooked edge" color.
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
        [HideInInspector] _MeatCenter ("Meat Center (object space)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _MeatExtents ("Meat Extents (object space)", Vector) = (1, 1, 1, 0)
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
                float4 _MeatCenter;
                float4 _MeatExtents;
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

            struct FragOut
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

            FragOut frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float mpu = _MetersPerUnit;
                float3 pm = input.positionOS * mpu;

                // Bites in meters. Rough edge: the radius wobbles with the position.
                int count = (int)_BiteCount;
                float4 bites[8] = { _Bite0, _Bite1, _Bite2, _Bite3, _Bite4, _Bite5, _Bite6, _Bite7 };
                float rough = (ValueNoise(pm * 45.0) - 0.5) * 2.0;
                float3 bc[8];
                float br[8];
                // Distance (m) inside the deepest bite. Positive means this point is carved away.
                float cut = -1.0;
                [unroll]
                for (int k = 0; k < 8; k++)
                {
                    bc[k] = bites[k].xyz * mpu;
                    br[k] = bites[k].w * (1.0 + rough * _EdgeNoise);
                    if (k < count) cut = max(cut, br[k] - distance(pm, bc[k]));
                }

                bool isFront = IS_FRONT_VFACE(face, true, false);
                FragOut o;
                o.depth = input.positionCS.z;

                if (cut > 0.0)
                {
                    // The shell is hollow, so only the front face stands in for the cut face. Back faces in a bite are gone.
                    if (!isFront) discard;

                    float3 camOS = TransformWorldToObject(GetCameraPositionWS()) * mpu;
                    float3 dir = normalize(pm - camOS);

                    // Where the view ray leaves the union of the bites (a few passes for overlapping bites).
                    float3 q = pm;
                    [unroll]
                    for (int it = 0; it < 3; it++)
                    {
                        float tMax = 0.0;
                        [unroll]
                        for (int k2 = 0; k2 < 8; k2++)
                        {
                            if (k2 < count)
                            {
                                float3 v = q - bc[k2];
                                float cc = dot(v, v) - br[k2] * br[k2];
                                if (cc < 0.0)
                                {
                                    float b = dot(v, dir);
                                    tMax = max(tMax, -b + sqrt(max(b * b - cc, 0.0)));
                                }
                            }
                        }
                        q += dir * (tMax + 1e-5);
                    }

                    // Outside the meat: the bite went through that side, so there is no cut face to see.
                    float3 e = (q / mpu - _MeatCenter.xyz) / max(_MeatExtents.xyz, 1e-5);
                    if (dot(e, e) > 1.3) discard;

                    // Normal of the dent where the ray leaves: the sphere whose surface is nearest to q, pointing at its center.
                    float best = 1e9;
                    float3 n = float3(0, 1, 0);
                    [unroll]
                    for (int k3 = 0; k3 < 8; k3++)
                    {
                        if (k3 < count)
                        {
                            float3 v = q - bc[k3];
                            float gap = abs(length(v) - br[k3]);
                            if (gap < best)
                            {
                                best = gap;
                                n = -normalize(v);
                            }
                        }
                    }

                    half marble = ValueNoise(q * 60.0) * 0.6 + ValueNoise(q * 160.0) * 0.4;
                    half3 albedo = lerp(_InnerColor.rgb, _InnerColor2.rgb, smoothstep(0.35, 0.75, marble));
                    // Lit like a dent: darker the deeper it is and where it faces away from the light.
                    half ndlCut = saturate(dot(n, normalize(float3(0.3, 0.8, 0.45))) * 0.5 + 0.5);
                    half depthFac = saturate(length(q - pm) / 0.07);
                    o.color = half4(albedo * lerp(0.4, 1.0, ndlCut) * lerp(1.0, 0.65, depthFac), 1.0);

                    float4 clip = TransformWorldToHClip(TransformObjectToWorld(q / mpu));
                    o.depth = clip.z / clip.w;
                    return o;
                }

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
                    // Inside of the shell (only seen when a bite went through): flesh with a marbled pattern.
                    half marble = ValueNoise(pm * 60.0) * 0.6 + ValueNoise(pm * 160.0) * 0.4;
                    albedo = lerp(_InnerColor.rgb, _InnerColor2.rgb, smoothstep(0.35, 0.75, marble));
                }

                half ndl = dot(n, normalize(float3(0.3, 0.8, 0.45))) * 0.5 + 0.5;
                o.color = half4(albedo * lerp(_ShadeMin, 1.0, ndl), 1.0);
                return o;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
