// Cap pass of the meat cut face (step 3 of 3, see MeatBiteCaps). Draws the back faces of the invisible bite spheres, but only
// where the stencil bit is 1, i.e. where the sphere surface lies inside the meat. That part of the sphere is the cut face:
// a concave dent with a flesh pattern. The pattern is a 3D pattern in the meat's own space, so it stays on the meat.
Shader "RaisingStackChan/MangaMeatBiteCap"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.93, 0.45, 0.42, 1)
        _InnerColor2 ("Inner Color 2 (marbling)", Color) = (0.98, 0.7, 0.62, 1)
        _FiberStretch ("Fiber Stretch (object X)", Float) = 0.4
        _ShadeMin ("Darkest Shade", Range(0, 1)) = 0.4
        _MetersPerUnit ("Meters Per Unit", Float) = 1
        _BiteCount ("Bite Count", Float) = 0
        [HideInInspector] _MeatW2L0 ("Meat World To Local 0", Vector) = (1, 0, 0, 0)
        [HideInInspector] _MeatW2L1 ("Meat World To Local 1", Vector) = (0, 1, 0, 0)
        [HideInInspector] _MeatW2L2 ("Meat World To Local 2", Vector) = (0, 0, 1, 0)
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
            "Queue" = "Geometry+3"
        }

        Pass
        {
            Name "Cap"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual

            // Draw only where the counting pass left 1, and clear the bit for the next frame / bite.
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp Equal
                Pass Zero
                Fail Zero
                ZFail Zero
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _InnerColor;
                half4 _InnerColor2;
                float _FiberStretch;
                float _ShadeMin;
                float _MetersPerUnit;
                float _BiteCount;
                float4 _MeatW2L0;
                float4 _MeatW2L1;
                float4 _MeatW2L2;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 pw = float4(input.positionWS, 1.0);
                float3 pl = float3(dot(_MeatW2L0, pw), dot(_MeatW2L1, pw), dot(_MeatW2L2, pw));
                float3 pm = pl * _MetersPerUnit;

                float cut;
                RSC_BITE_CUT(pm, 0.985, cut);
                clip(-cut);

                // Flesh: marbling stretched along the meat's long axis (object X) so it reads as muscle fibers.
                float3 pf = pm * float3(_FiberStretch, 1.0, 1.0);
                half marble = ValueNoise(pf * 70.0) * 0.55 + ValueNoise(pf * 170.0) * 0.45;
                half3 albedo = lerp(_InnerColor.rgb, _InnerColor2.rgb, smoothstep(0.35, 0.75, marble));

                // The sphere is seen from inside, so the surface faces the opposite way of the mesh normal.
                float3 n = -normalize(input.normalWS);
                float3 v = normalize(GetCameraPositionWS() - input.positionWS);
                half ndl = saturate(dot(n, normalize(float3(0.3, 0.8, 0.45))) * 0.5 + 0.5);
                half facing = saturate(dot(n, v));
                half shade = lerp(_ShadeMin, 1.0, ndl) * lerp(0.7, 1.0, facing);
                return half4(albedo * shade, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
