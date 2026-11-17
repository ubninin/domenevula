// 스프라이트의 모양(알파)만 남기고 색은 전부 흰색(틴트 색)으로 그리는 셰이더.
// 상호작용 인디케이터(흰 테두리)에서 쓴다. URP 2D 전용.
Shader "Dome/SpriteWhiteSilhouette"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
        CBUFFER_END
        struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
        Varyings vert(Attributes v)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS);
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            o.color = v.color * _Color;
            return o;
        }
        half4 frag(Varyings i) : SV_Target
        {
            half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
            return half4(i.color.rgb, a);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
