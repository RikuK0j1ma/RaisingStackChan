// Toon-lit shader for the manga meat (the visible shell). Bites are spherical chunks taken out of the solid meat (up to 8):
// - A fragment inside a bite is discarded (front and back faces), so a bite takes a chunk out of the meat.
// - The cut face is not drawn here. MangaMeatBiteDepth / MangaMeatVolume / MangaMeatBiteCap draw it with the stencil
//   (see MeatBiteCaps). The front faces right around the cut get a lighter "cooked edge" color here.
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
        _RimWidth ("Cut Edge Width (m)", Float) = 0.018
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

            #include "MangaMeatCommon.hlsl"

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
                float cut;
                RSC_BITE_CUT(pm, 1.0, cut);
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
                    // Inside of the shell (only seen through a hole): flesh with a marbled pattern.
                    half marble = ValueNoise(pm * 60.0) * 0.6 + ValueNoise(pm * 160.0) * 0.4;
                    albedo = lerp(_InnerColor.rgb, _InnerColor2.rgb, smoothstep(0.35, 0.75, marble));
                }

                half ndl = dot(n, normalize(float3(0.3, 0.8, 0.45))) * 0.5 + 0.5;
                return half4(albedo * lerp(_ShadeMin, 1.0, ndl), 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
