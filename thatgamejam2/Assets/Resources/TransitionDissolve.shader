// 화면 전환용 UI 디졸브 셰이더.
// 반드시 Resources 폴더 아래에 둘 것 (예: Assets/Resources/Shaders/TransitionDissolve.shader)
// → 빌드에 포함돼야 SceneLoader가 Shader.Find로 찾을 수 있음.
// Built-in / URP 둘 다 Screen Space - Overlay 캔버스에서 동작.

Shader "UI/TransitionDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Progress ("Progress (0 = 투명, 1 = 완전히 덮음)", Range(0, 1)) = 0
        _NoiseScale ("Noise Scale", Float) = 6
        _Softness ("Edge Softness", Range(0.001, 0.5)) = 0.04
        _Aspect ("Screen Aspect", Float) = 1.7778
        _NoiseOffset ("Noise Offset", Vector) = (0, 0, 0, 0)

        // UI 표준 스텐실 프로퍼티 (Mask 컴포넌트 호환용)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float  _Progress;
            float  _NoiseScale;
            float  _Softness;
            float  _Aspect;
            float4 _NoiseOffset;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            // ── 절차적 노이즈 (텍스처 불필요) ──
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += amp * valueNoise(p);
                    p *= 2.0;
                    amp *= 0.5;
                }
                return v / 0.9375; // 0~1로 정규화
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 p = IN.texcoord * float2(_Aspect, 1.0) * _NoiseScale + _NoiseOffset.xy;
                float n = fbm(p);

                // Progress 0 → 완전 투명, 1 → 완전 덮음 (양 끝이 정확히 맞도록 보정)
                float a = saturate((_Progress * (1.0 + _Softness) - n) / _Softness);

                fixed4 col = IN.color;
                col.a *= a;
                return col;
            }
            ENDCG
        }
    }
}
