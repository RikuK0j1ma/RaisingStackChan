// Depth pass of the meat cut face (step 1 of 3, see MeatBiteCaps). Draws the back faces of the invisible bite spheres into the
// depth buffer only. Where a bite carved the shell, the depth is now where the view ray leaves the bite sphere.
// Points inside another bite are discarded, so only the outline of the union of the bites is left.
Shader "RaisingStackChan/MangaMeatBiteDepth"
{
    Properties
    {
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
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "Depth"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
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
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 pw = float4(input.positionWS, 1.0);
                float3 pm = float3(dot(_MeatW2L0, pw), dot(_MeatW2L1, pw), dot(_MeatW2L2, pw)) * _MetersPerUnit;
                float cut;
                RSC_BITE_CUT(pm, 0.985, cut);
                clip(-cut);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
