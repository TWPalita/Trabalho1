Shader "Custom/CockpitHologramVisor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PrimaryColor ("Primary Hologram Color", Color) = (0.18, 0.92, 1.0, 1.0)
        _SecondaryColor ("Secondary Hologram Color", Color) = (0.10, 0.45, 0.98, 1.0)
        _BgColor ("Hologram Field Color", Color) = (0.015, 0.08, 0.18, 0.42)
        _VisorMode ("Mode (0=MathVisor, 1=ProjectionBeam, 2=EmitterLens, 3=GrandVictoryPanel, 4=ControlPanel)", Float) = 0
        _RoundProgress ("Round Progress (0..1)", Range(0, 1)) = 0.1
        _PulseBoost ("Pulse Boost", Range(0, 1)) = 0.0

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
                float2 uv       : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _PrimaryColor;
            float4 _SecondaryColor;
            float4 _BgColor;
            float _VisorMode;
            float _RoundProgress;
            float _PulseBoost;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.uv = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // SDF de octógono/caixa chanfrada nos 4 cantos
            float sdOctagonBox(float2 p, float2 b, float chamfer)
            {
                float2 d = abs(p) - b;
                float boxDist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
                float c = (abs(p.x) - (b.x - chamfer)) + (abs(p.y) - (b.y - chamfer));
                return max(boxDist, c * 0.70710678);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = saturate(IN.uv);
                float3 pri = _PrimaryColor.rgb;
                float3 sec = _SecondaryColor.rgb;

                // =====================================================================
                // MODO 1: FEIXE CÔNICO DE PROJEÇÃO HOLOGRÁFICA (HoloProjection_Beam)
                // =====================================================================
                if (_VisorMode > 0.5 && _VisorMode < 1.5)
                {
                    float xCentered = (uv.x - 0.5) * 2.0; // -1..1
                    // Largura do cone expande da base (uv.y=0, mais estreito) até o visor (uv.y=1, mais largo)
                    float coneHalfWidth = lerp(0.42, 0.94, uv.y);
                    float normX = abs(xCentered) / max(0.001, coneHalfWidth);

                    float coneMask = 1.0 - smoothstep(0.68, 1.0, normX);
                    float edgeBeam = smoothstep(0.55, 0.86, normX) * (1.0 - smoothstep(0.86, 1.0, normX));

                    // Ondas de energia subindo do emissor para o visor
                    float upwardWave = 0.55 + 0.45 * sin(uv.y * 22.0 - _Time.y * 9.5);
                    float fineRays = 0.65 + 0.35 * sin(xCentered * 42.0);
                    float vertFade = smoothstep(0.0, 0.18, uv.y) * (1.0 - smoothstep(0.78, 1.0, uv.y) * 0.35);

                    float3 beamCol = lerp(sec, pri, uv.y * 0.7 + edgeBeam * 0.3) * (1.1 + _PulseBoost * 0.4);
                    float alpha = (coneMask * 0.28 * upwardWave * fineRays + edgeBeam * 0.48) * vertFade;

                    return float4(beamCol, saturate(alpha));
                }

                // =====================================================================
                // MODO 2: LENTE EMISSORA DE HOLOGRAMA (HoloEmitter_Lens)
                // =====================================================================
                if (_VisorMode >= 1.5 && _VisorMode < 2.5)
                {
                    float2 p = (uv - 0.5) * 2.0;
                    float horizFade = 1.0 - smoothstep(0.55, 1.0, abs(p.x));
                    float core = exp(-abs(p.y) * 2.8) * horizFade;
                    float pulse = 0.85 + 0.15 * sin(_Time.y * 5.0);
                    float3 col = lerp(pri, float3(1, 1, 1), core * 0.65) * pulse;
                    return float4(col, saturate(core * 0.95));
                }

                // =====================================================================
                // MODO 0: VISOR HOLOGRÁFICO DA RODADA E CONTA (MathVisor)
                // =====================================================================
                if (_VisorMode < 0.5)
                {
                    float2 size = float2(500.0, 126.0);
                    float2 p = (uv - 0.5) * size;
                    float2 halfSize = size * 0.5 - float2(4.0, 4.0);

                    float dist = sdOctagonBox(p, halfSize, 22.0);
                    float aa = max(fwidth(dist), 0.75);
                    float insideMask = 1.0 - smoothstep(-aa, aa, dist);

                    // 1. Campo de plasma holográfico translúcido (permite ver as estrelas ao fundo!)
                    float centerDist = length((uv - 0.5) * float2(1.1, 1.5));
                    float radialGlass = smoothstep(0.0, 0.85, centerDist);
                    float3 bgRGB = lerp(_BgColor.rgb, sec * 0.32, radialGlass * 0.65);

                    // Faixa superior dedicada ao cabeçalho "RODADA X / 10" (em degradê ciano luminoso)
                    float headerZone = smoothstep(0.64, 0.70, uv.y);
                    float headerGlow = exp(-abs(uv.y - 0.67) * 28.0) * (1.0 - smoothstep(0.32, 0.48, abs(uv.x - 0.5)));
                    bgRGB = lerp(bgRGB, pri * 0.30, headerZone * 0.65);
                    bgRGB += pri * (headerGlow * 0.55);

                    // 2. Scanlines holográficas + raios verticais de projeção
                    float scanline = 0.5 + 0.5 * sin(uv.y * 126.0 * 1.4 - _Time.y * 4.5);
                    float vertRays = 0.5 + 0.5 * sin(uv.x * 90.0);
                    bgRGB += pri * (scanline * 0.045 + vertRays * 0.025 * (1.0 - uv.y));

                    // Varredura horizontal de luz holográfica (Sheen)
                    float sweepPos = frac(_Time.y * 0.28) * 1.6 - 0.3;
                    float sweep = smoothstep(0.16, 0.0, abs((uv.x + uv.y * 0.2) - sweepPos)) * 0.14;
                    bgRGB += lerp(pri, float3(1, 1, 1), 0.5) * sweep;

                    // 3. Borda dupla laser neon + cantoneiras táticas (Corner Brackets)
                    float outerBorder = 1.0 - smoothstep(0.0, 2.2 + aa, abs(dist + 1.2));
                    float innerBorder = (1.0 - smoothstep(0.0, 1.4 + aa, abs(dist + 7.5))) * 0.42;
                    float edgeBloom = exp(-max(0.0, -dist) * 0.12) * 0.42;

                    // Reforço luminoso nos 4 cantos chanfrados e nas laterais da rodada
                    float cornerWeight = smoothstep(0.28, 0.46, abs(uv.x - 0.5)) * smoothstep(0.22, 0.42, abs(uv.y - 0.5));
                    float bracketGlow = (1.0 - smoothstep(0.0, 3.8 + aa, abs(dist + 1.5))) * cornerWeight;

                    float3 col = bgRGB;
                    col = lerp(col, pri, edgeBloom * 0.65);
                    col = lerp(col, sec * 1.3, innerBorder);
                    col = lerp(col, pri * 1.25, outerBorder * 0.90);
                    col = lerp(col, lerp(pri, float3(1, 1, 1), 0.45), bracketGlow);

                    // Pulso energético quando uma nova rodada começa
                    col += pri * (_PulseBoost * 0.30);

                    // Transparência calibrada de holograma real (translúcido no centro, brilhante nas bordas)
                    float baseAlpha = lerp(_BgColor.a, _BgColor.a * 1.38, radialGlass);
                    baseAlpha = max(baseAlpha, headerZone * 0.48);
                    float alpha = baseAlpha * insideMask;
                    alpha = max(alpha, outerBorder * 0.92 * insideMask);
                    alpha = max(alpha, innerBorder * 0.65 * insideMask);
                    alpha = max(alpha, bracketGlow * 0.98 * insideMask);
                    alpha = max(alpha, headerGlow * 0.72 * insideMask);

                    return float4(col, saturate(alpha));
                }

                // =====================================================================
                // MODO 3 E 4: GRAND HOLOGRAM CANVAS (VITÓRIA / FIM DE JOGO / CONSOLE)
                // =====================================================================
                {
                    bool isVictoryPanel = (_VisorMode >= 2.5 && _VisorMode < 3.5);
                    float2 size = isVictoryPanel ? float2(880.0, 600.0) : float2(860.0, 175.0);
                    float2 p = (uv - 0.5) * size;
                    float2 halfSize = size * 0.5 - float2(4.0, 4.0);
                    float chamfer = isVictoryPanel ? 36.0 : 22.0;

                    float dist = sdOctagonBox(p, halfSize, chamfer);
                    float aa = max(fwidth(dist), 0.8);
                    float insideMask = 1.0 - smoothstep(-aa, aa, dist);

                    // Degradê espacial profundo com bordas luminosas translúcidas
                    float radial = length((uv - 0.5) * float2(1.0, 1.1));
                    float3 bgRGB = lerp(_BgColor.rgb, sec * 0.26, saturate(radial * 0.85));

                    // Brilho de cabeçalho no topo
                    float topGlow = exp(-abs(1.0 - uv.y) * (isVictoryPanel ? 6.5 : 4.5)) * 0.28;
                    bgRGB += pri * topGlow;

                    // Scanlines holográficas e grade sutil
                    float scanFreq = isVictoryPanel ? 220.0 : 90.0;
                    float scanline = 0.5 + 0.5 * sin(uv.y * scanFreq - _Time.y * 3.8);
                    bgRGB += pri * (scanline * 0.032);

                    float2 gridUV = frac(p / 36.0);
                    float grid = step(0.96, max(gridUV.x, gridUV.y));
                    bgRGB += pri * (grid * 0.024);

                    // Borda neon dupla + cantoneiras
                    float outerRing = 1.0 - smoothstep(0.0, 2.6 + aa, abs(dist + 1.5));
                    float innerRing = (1.0 - smoothstep(0.0, 1.5 + aa, abs(dist + 10.0))) * 0.40;
                    float innerBloom = exp(-max(0.0, -dist) * 0.08) * 0.36;

                    float cornerMask = smoothstep(0.32, 0.45, abs(uv.x - 0.5)) * smoothstep(0.32, 0.45, abs(uv.y - 0.5));
                    float cornerBracket = (1.0 - smoothstep(0.0, 4.5 + aa, abs(dist + 2.0))) * cornerMask;

                    float3 col = bgRGB;
                    col = lerp(col, sec, innerBloom);
                    col = lerp(col, sec * 1.2, innerRing);
                    col = lerp(col, pri * 1.25, outerRing * 0.92);
                    col = lerp(col, lerp(pri, float3(1, 1, 1), 0.5), cornerBracket);

                    float alpha = _BgColor.a * insideMask;
                    alpha = max(alpha, outerRing * 0.95 * insideMask);
                    alpha = max(alpha, innerRing * 0.65 * insideMask);
                    alpha = max(alpha, cornerBracket * 0.98 * insideMask);

                    return float4(col, saturate(alpha));
                }
            }
            ENDCG
        }
    }
}
