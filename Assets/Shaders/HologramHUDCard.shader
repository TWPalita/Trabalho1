Shader "Custom/HologramHUDCard"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AccentColor ("Accent Color", Color) = (0.22, 0.78, 0.98, 1)
        _SecondaryColor ("Secondary Gradient Color", Color) = (0.08, 0.35, 0.85, 1)
        _BgLeftColor ("Background Left Color", Color) = (0.015, 0.045, 0.11, 0.95)
        _BgRightColor ("Background Right Color", Color) = (0.03, 0.12, 0.24, 0.62)
        _Dissolve ("Dissolve Progress", Range(0, 1)) = 0.0
        _CardMode ("Card Element Mode (0=MainFrame, 1=FlatGradientElement, 2=GlowSweep)", Float) = 0

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
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            #include "UnityUI.cginc"

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
                float2 hudUV    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _AccentColor;
            float4 _SecondaryColor;
            float4 _BgLeftColor;
            float4 _BgRightColor;
            float _Dissolve;
            float _CardMode;

            float4x4 _HoloHudWorldToLocal;
            float _HoloHudDissolve;
            float4 _HoloHudAccentColor;
            float4 _HoloHudSecondaryColor;
            float4 _HoloHudBgLeftColor;
            float4 _HoloHudBgRightColor;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float2 hudLocal = mul(_HoloHudWorldToLocal, float4(worldPos, 1.0)).xy;
                OUT.hudUV = (hudLocal + float2(320.0, 115.0)) / float2(640.0, 230.0);

                return OUT;
            }

            // SDF de caixa com cantos chanfrados a 45 graus (estilo HUD Sci-Fi AAA)
            float sdChamferBox(float2 p, float2 b, float chamferTopRight, float chamferBottomRight, float chamferLeft)
            {
                float2 d = abs(p) - b;
                float boxDist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);

                // Chanfro superior direito
                float tr = (p.x - (b.x - chamferTopRight)) + (p.y - (b.y - chamferTopRight));
                boxDist = max(boxDist, tr * 0.70710678);

                // Chanfro inferior direito (maior, estilo visor tático)
                float br = (p.x - (b.x - chamferBottomRight)) + (-p.y - (b.y - chamferBottomRight));
                boxDist = max(boxDist, br * 0.70710678);

                // Micro-chanfro nos cantos esquerdos
                float tl = (-p.x - (b.x - chamferLeft)) + (p.y - (b.y - chamferLeft));
                float bl = (-p.x - (b.x - chamferLeft)) + (-p.y - (b.y - chamferLeft));
                boxDist = max(boxDist, max(tl, bl) * 0.70710678);

                return boxDist;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = saturate(IN.hudUV);
                float dissolve = max(_Dissolve, _HoloHudDissolve);
                float4 accent = (_HoloHudAccentColor.a > 0.01) ? _HoloHudAccentColor : _AccentColor;
                float4 secondary = (_HoloHudSecondaryColor.a > 0.01) ? _HoloHudSecondaryColor : _SecondaryColor;
                float4 bgLeft = (_HoloHudBgLeftColor.a > 0.01) ? _HoloHudBgLeftColor : _BgLeftColor;
                float4 bgRight = (_HoloHudBgRightColor.a > 0.01) ? _HoloHudBgRightColor : _BgRightColor;

                float4 finalCol = float4(0, 0, 0, 0);

                if (_CardMode < 0.5)
                {
                    // === MODO 0: PAINEL PRINCIPAL HOLOGRÁFICO EM DEGRADÊ ===
                    // Coordenadas em pixels do card (640 x 230, centro em 0,0)
                    float2 p = (IN.hudUV - 0.5) * float2(640.0, 230.0);
                    float2 halfSize = float2(316.0, 111.0);

                    float dist = sdChamferBox(p, halfSize, 26.0, 38.0, 8.0);
                    float aa = max(fwidth(dist), 0.75);

                    // Máscara interna suavizada do card chanfrado
                    float insideMask = 1.0 - smoothstep(-aa, aa, dist);

                    // 1. Fundo em degradê horizontal + diagonal (Glassmorphism espacial)
                    float horizGrad = smoothstep(0.0, 1.0, uv.x);
                    float vertGrad = smoothstep(0.0, 1.0, uv.y);
                    float4 bgCol = lerp(bgLeft, bgRight, horizGrad * 0.82 + (1.0 - vertGrad) * 0.18);

                    // Brilho radial suave de plasma no canto superior esquerdo (próximo à barra de energia)
                    float2 glowOrigin = float2(0.08, 0.78);
                    float radialDist = length((uv - glowOrigin) * float2(1.4, 0.85));
                    float plasmaGlow = exp(-radialDist * 3.2) * 0.28;
                    bgCol.rgb += accent.rgb * plasmaGlow;

                    // Degradê luminoso sutil na borda superior e esquerda
                    float topHighlight = exp(-abs(1.0 - uv.y) * 9.0) * 0.16;
                    bgCol.rgb += lerp(accent.rgb, secondary.rgb, uv.x) * topHighlight;

                    // 2. Scanlines holográficas finíssimas + micro-grade tática
                    float scanline = 0.5 + 0.5 * sin(uv.y * 230.0 * 1.35 - _Time.y * 3.5);
                    bgCol.rgb += accent.rgb * (scanline * 0.028);

                    float2 gridUV = frac(p / 28.0);
                    float gridLine = step(0.95, max(gridUV.x, gridUV.y));
                    bgCol.rgb += accent.rgb * (gridLine * 0.025 * (1.0 - uv.x * 0.6));

                    // 3. Varredura de brilho especular (Holo Sheen) contínua no vidro
                    float sheenPos = frac(_Time.y * 0.22) * 1.8 - 0.4;
                    float sheenDist = abs((uv.x + uv.y * 0.28) - sheenPos);
                    float sheen = smoothstep(0.14, 0.0, sheenDist) * 0.10;
                    bgCol.rgb += lerp(accent.rgb, float3(1, 1, 1), 0.5) * sheen;

                    // 4. Borda neon em degradê (mais intensa à esquerda, mais translúcida à direita)
                    float borderRing = 1.0 - smoothstep(0.0, 2.2 + aa, abs(dist + 1.2));
                    float innerGlow = exp(-max(0.0, -dist) * 0.14) * 0.35;

                    // Cantoneiras táticas reforçadas (Corner Brackets)
                    float cornerMask = 0.0;
                    if (uv.x < 0.16 && (uv.y > 0.78 || uv.y < 0.22)) cornerMask = 1.0;
                    if (uv.x > 0.82 && uv.y > 0.72) cornerMask = 0.85;
                    if (uv.x > 0.80 && uv.y < 0.28) cornerMask = 0.85;

                    float cornerRing = (1.0 - smoothstep(0.0, 3.6 + aa, abs(dist + 1.6))) * cornerMask;

                    // Barra lateral esquerda integrada com degradê vertical
                    float leftBarMask = smoothstep(14.0, 11.5, p.x + halfSize.x) * insideMask;
                    float3 leftBarCol = lerp(secondary.rgb, accent.rgb, smoothstep(0.0, 0.85, uv.y));
                    leftBarCol = lerp(leftBarCol, float3(1, 1, 1), 0.22 * exp(-abs(uv.y - 0.75) * 6.0));

                    float3 borderCol = lerp(accent.rgb, secondary.rgb, uv.x * 0.65);
                    float borderAlpha = lerp(0.85, 0.38, uv.x);

                    float3 composedRGB = bgCol.rgb;
                    composedRGB = lerp(composedRGB, borderCol, innerGlow);
                    composedRGB = lerp(composedRGB, leftBarCol, leftBarMask);
                    composedRGB = lerp(composedRGB, borderCol * 1.25, borderRing * borderAlpha);
                    composedRGB = lerp(composedRGB, lerp(accent.rgb, float3(1,1,1), 0.35), cornerRing);

                    float composedAlpha = bgCol.a * insideMask;
                    composedAlpha = max(composedAlpha, borderRing * borderAlpha * insideMask);
                    composedAlpha = max(composedAlpha, cornerRing * 0.95 * insideMask);
                    composedAlpha = max(composedAlpha, leftBarMask * 0.98);

                    finalCol = float4(composedRGB, composedAlpha) * IN.color;
                }
                else
                {
                    // === MODO 1: ELEMENTOS INTERNOS (Divisória em Degradê / Badges de Progresso) ===
                    half4 texCol = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                    // Degradê horizontal suave para a linha divisória
                    if (_CardMode > 1.5)
                    {
                        float divFade = smoothstep(0.0, 0.08, IN.texcoord.x) * (1.0 - smoothstep(0.35, 1.0, IN.texcoord.x));
                        texCol.a *= divFade;
                        texCol.rgb = lerp(accent.rgb, secondary.rgb, IN.texcoord.x);
                    }
                    finalCol = texCol;
                }

                // =========================================================================
                // DISSOLUÇÃO HOLOGRÁFICA EM DEGRADÊ CONTÍNUO (DIREITA -> ESQUERDA / DIAGONAL)
                // =========================================================================
                if (dissolve > 0.0001)
                {
                    // Coordenada do degradê: 0.0 na direita-baixo -> 1.0 na esquerda-topo
                    float wave = sin(uv.y * 14.0 - _Time.y * 8.0) * 0.016;
                    float gradCoord = (1.0 - uv.x) * 0.86 + uv.y * 0.14 + wave;

                    // Largura do degradê = 0.62 (62% do card fica em transição suave simultânea!)
                    float feather = 0.62;
                    float sweep = lerp(-feather, 1.05, dissolve);
                    float alphaGrad = smoothstep(sweep, sweep + feather, gradCoord);

                    // Curva suave de transparência (degradê sedoso sem borda dura)
                    float softAlpha = alphaGrad * alphaGrad * (3.0 - 2.0 * alphaGrad);

                    // Brilho de energia ciano/azul-celeste ao longo da zona de degradê
                    float edgeBand = sin(alphaGrad * 3.14159265);
                    float3 holoCyan = float3(0.25, 0.96, 1.0);
                    float3 holoAzure = float3(0.35, 0.58, 1.0);
                    float3 degradeTint = lerp(holoAzure, holoCyan, alphaGrad);

                    finalCol.rgb = lerp(finalCol.rgb, degradeTint * 1.35, edgeBand * 0.72 * smoothstep(0.0, 0.18, dissolve));
                    finalCol.a *= softAlpha;
                }

                return finalCol;
            }
            ENDCG
        }
    }
}
