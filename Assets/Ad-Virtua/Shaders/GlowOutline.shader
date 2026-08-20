Shader "Unlit/AdvirtuaGlowOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _GlowColor ("Glow Color", Color) = (0, 0.5, 1, 1)
        _GlowWidth ("Glow Width", Range(0, 0.2)) = 0.05
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2.0
        _Alpha ("Alpha", Range(0, 1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _GlowColor;
            float _GlowWidth;
            float _GlowIntensity;
            float _Alpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 中心のテクスチャをサンプリング
                fixed4 centerCol = tex2D(_MainTex, i.uv);

                // Glow効果の計算
                float glowAlpha = 0;

                // 周囲をサンプリングしてGlowを計算（16方向サンプリング）
                const int samples = 16;
                for (int s = 0; s < samples; s++)
                {
                    float angle = (float)s / (float)samples * 6.28318530718; // 2π

                    // 複数の距離でサンプリング（より滑らかなGlow）
                    for (float dist = 0.2; dist <= 1.0; dist += 0.2)
                    {
                        float2 offset = float2(cos(angle), sin(angle)) * _GlowWidth * dist;
                        float sampledAlpha = tex2D(_MainTex, i.uv + offset).a;

                        // smoothstepで自然な減衰（外側ほど薄くなる）
                        float falloff = smoothstep(1.0, 0.0, dist);
                        glowAlpha = max(glowAlpha, sampledAlpha * falloff);
                    }
                }

                // smoothstepで滑らかなGlowマスクを生成
                float glowMask = smoothstep(0.0, 1.0, glowAlpha) * (1.0 - centerCol.a);

                // Glow色を計算（中心に近いほど明るく）
                float4 glowCol = float4(_GlowColor.rgb * _GlowIntensity, glowMask * _GlowColor.a);

                // 元のテクスチャとGlowを滑らかに合成
                float4 finalCol;
                float blendFactor = smoothstep(0.0, 1.0, centerCol.a);
                finalCol.rgb = lerp(glowCol.rgb, centerCol.rgb, blendFactor);
                finalCol.a = saturate(centerCol.a + glowCol.a * smoothstep(0.0, 0.5, glowMask));

                // 全体の透明度を適用
                finalCol.a *= _Alpha;

                return finalCol;
            }
            ENDCG
        }
    }
}
