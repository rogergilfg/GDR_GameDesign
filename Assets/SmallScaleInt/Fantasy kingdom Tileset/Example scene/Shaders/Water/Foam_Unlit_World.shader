Shader "SmallScale/URP2D/Foam_Unlit_World"
{
    Properties
    {
        // Colors & depth tint (kept so you can tint toward your water palette if you want)
        _ShallowColor ("Shallow Color", Color) = (0.14, 0.55, 0.78, 1)
        _DeepColor    ("Deep Color",    Color) = (0.02, 0.22, 0.40, 1)
        _DepthBlendStrength ("Depth Blend Strength (world-Y)", Range(0,2)) = 0.6

        // World-UV tiling & flow (detail)
        _UVScale  ("Global UV Scale (1/units)", Float) = 0.25
        _DetailTex ("Detail/Base (repeatable)", 2D) = "white" {}
        _DetailInfluence ("Detail Influence", Range(0,1)) = 0.35
        _DetailPan ("Detail Flow (XY)", Vector) = (0.005, 0.004, 0, 0)

        // Ripple / wobble (world UV)
        _RippleTex  ("Ripple Noise (repeatable)", 2D) = "gray" {}
        _RipplePan  ("Ripple Flow (XY)", Vector) = (0.02, 0.015, 0, 0)
        _RippleTilingMul ("Ripple Tiling Multiplier", Float) = 1.0
        _RippleAmount ("Ripple Albedo Wobble", Range(0,0.2)) = 0.05
        _UVWobbleAmount ("UV Wobble (distort everything)", Range(0,0.1)) = 0.02

        // Dual “normals” used for distortion (world UV)
        _NormalA ("Normal A", 2D) = "bump" {}
        _NormalB ("Normal B", 2D) = "bump" {}
        _PanA    ("Normal A Flow (XY)", Vector) = (0.035, 0.010, 0, 0)
        _PanB    ("Normal B Flow (XY)", Vector) = (-0.015, 0.028, 0, 0)
        _NormalTilingMul ("Normals Tiling Multiplier", Float) = 1.0
        _NormalScale ("Distortion Strength", Range(0,2)) = 0.9

        // Screen-edge sheen (fake spec)
        _SheenStrength ("Sheen Strength", Range(0,1)) = 0.25
        _SheenSharpness ("Sheen Sharpness", Range(0.5,8)) = 2.2

        // Transparency controls
        _OverallAlpha ("Overall Alpha", Range(0,1)) = 0.75
        _AlphaDepthStrength ("Alpha Depth Strength", Range(-1,1)) = 0.0

        // *** Edge mask / sprite alpha ***
        _AlphaCutoff ("Alpha Cutoff (edge clip)", Range(0,0.5)) = 0.02

        // Sprite compat — _MainTex is the sprite (mask only)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [PerRendererData] _MainTex ("Sprite Texture (mask only)", 2D) = "white" {}
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        // -------- NEW: Foam tint & “rock-hit” rim animation --------
        _FoamTint ("Foam Tint", Color) = (1,1,1,1)
        _RimWidthPixels  ("Rim Width (px)", Range(0,12)) = 4
        _RimSoftPixels   ("Rim Soft (px)",  Range(0,4))  = 1
        _RimPulseSpeed   ("Rim Pulse Speed", Range(0,6)) = 1.6
        _RimPulsePixels  ("Rim Pulse Amplitude (px)", Range(0,6)) = 1.0
        _RimNoiseTex     ("Rim Noise (repeat)", 2D) = "gray" {}
        _RimNoiseScale   ("Rim Noise Scale (1/units)", Float) = 0.8
        _RimNoiseInfluence ("Rim Noise Influence", Range(0,1)) = 0.6
        _FoamContrast    ("Foam Contrast", Range(0.5,2)) = 1.1
    }

    SubShader
    {
        Tags{
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Sprite"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "Unlit2D"
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // --- Material params
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _DepthBlendStrength;

                float  _UVScale;
                float4 _DetailPan;
                float  _DetailInfluence;

                float4 _RipplePan;
                float  _RippleTilingMul;
                float  _RippleAmount;
                float  _UVWobbleAmount;

                float4 _PanA;
                float4 _PanB;
                float  _NormalTilingMul;
                float  _NormalScale;

                float  _SheenStrength;
                float  _SheenSharpness;

                float  _OverallAlpha;
                float  _AlphaDepthStrength;

                float  _AlphaCutoff;

                // NEW
                float4 _FoamTint;
                float  _RimWidthPixels;
                float  _RimSoftPixels;
                float  _RimPulseSpeed;
                float  _RimPulsePixels;
                float  _RimNoiseScale;
                float  _RimNoiseInfluence;
                float  _FoamContrast;
            CBUFFER_END

            float4 _RendererColor;
            float  _EnableExternalAlpha;

            // Textures
            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_AlphaTex);   SAMPLER(sampler_AlphaTex);

            TEXTURE2D(_DetailTex);  SAMPLER(sampler_DetailTex);
            TEXTURE2D(_RippleTex);  SAMPLER(sampler_RippleTex);
            TEXTURE2D(_NormalA);    SAMPLER(sampler_NormalA);
            TEXTURE2D(_NormalB);    SAMPLER(sampler_NormalB);

            // NEW rim noise
            TEXTURE2D(_RimNoiseTex); SAMPLER(sampler_RimNoiseTex);

            float4 _MainTex_TexelSize;

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0; // sprite UV (mask)
                float4 color      : COLOR;
            };
            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 spriteUV : TEXCOORD0;
                float4 col      : COLOR;
                float3 worldPos : TEXCOORD1;
                float2 screenUV : TEXCOORD2;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 world = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(world);
                OUT.spriteUV = IN.uv;
                OUT.col      = IN.color * _RendererColor;
                OUT.worldPos = world;

                float4 clip = OUT.positionHCS;
                OUT.screenUV = clip.xy / max(1e-5, clip.w);
                OUT.screenUV = OUT.screenUV * 0.5f + 0.5f;
                return OUT;
            }

            float3 unpackRGToNormal(float2 rg)
            {
                float2 xy = rg * 2.0 - 1.0;
                float z = sqrt(saturate(1.0 - dot(xy, xy)));
                return normalize(float3(xy, z));
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // --- 1) SPRITE EDGE MASK (hard clip to stop overlap)
                float spriteA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.spriteUV).a;
                if (_EnableExternalAlpha > 0.5)
                {
                    float aExt = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, IN.spriteUV).r;
                    spriteA *= aExt;
                }
                clip(spriteA - _AlphaCutoff);

                // --- 2) CONTINUOUS WORLD-SPACE BASE (kept from your shader)
                float2 worldUV = IN.worldPos.xy * _UVScale;

                // Ripple & wobble
                float2 uvRipple = worldUV * _RippleTilingMul + _RipplePan.xy * t;
                float ripple = SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex, uvRipple).r;
                float2 wobble = (ripple - 0.5) * _UVWobbleAmount;

                // “Normals” for distortion
                float2 uvA = worldUV * _NormalTilingMul + _PanA.xy * t + wobble * 1.5;
                float2 uvB = worldUV * _NormalTilingMul + _PanB.xy * t - wobble * 1.2;
                float2 nA = SAMPLE_TEXTURE2D(_NormalA, sampler_NormalA, uvA).rg;
                float2 nB = SAMPLE_TEXTURE2D(_NormalB, sampler_NormalB, uvB).rg;
                float2 nXY = normalize((nA * 2 - 1) + (nB * 2 - 1));
                float3 n   = unpackRGToNormal(nXY * 0.5 + 0.5);
                n.xy *= _NormalScale;

                // Detail albedo in world space (used as subtle under-tint for foam)
                float2 uvDetail = worldUV + _DetailPan.xy * t + n.xy * 0.02 + wobble;
                float3 detail = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, uvDetail).rgb;

                // Optional water-ish tint you already had (very subtle under foam)
                float depthLerp = saturate((IN.worldPos.y * 0.25) * _DepthBlendStrength * 0.5 + 0.5);
                float3 baseColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthLerp);
                float3 albedo = lerp(baseColor, baseColor * detail, _DetailInfluence);
                albedo += (ripple - 0.5) * _RippleAmount;

                // Screen-edge sheen (very subtle for foam)
                float2 toEdge = abs(IN.screenUV - 0.5) * 2.0;
                float edgeFactorSheen = pow(saturate(max(toEdge.x, toEdge.y)), _SheenSharpness);
                float sheen = edgeFactorSheen * _SheenStrength * 0.5; // halve for foam
                float3 waterUnder = saturate(albedo + sheen);

                // --- 3) ROCK-HIT FOAM RIM (animated inward band along sprite edge)
                // Approximate inward edge normal from sprite alpha gradient (sprite space)
                float2 texel = _MainTex_TexelSize.xy;
                float aR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.spriteUV + float2(texel.x,0)).a;
                float aL = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.spriteUV - float2(texel.x,0)).a;
                float aU = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.spriteUV + float2(0,texel.y)).a;
                float aD = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.spriteUV - float2(0,texel.y)).a;
                float2 grad = float2(aR - aL, aU - aD);

                // Alpha usually rises from 0 (outside) to 1 (inside), so inward normal is -grad
                float2 nEdge = -normalize(grad + 1e-6);

                // Rim thickness/pulse (in *pixels* along inward normal)
                float rimNoise = SAMPLE_TEXTURE2D(_RimNoiseTex, sampler_RimNoiseTex, IN.worldPos.xy * _RimNoiseScale).r;
                float pulse = sin(t * _RimPulseSpeed + rimNoise * 6.28318) * _RimPulsePixels;
                float rimPx = max(0.0, _RimWidthPixels + pulse);

                // Sample alpha a little way *inside* the sprite to form a band
                float2 inwardUV = IN.spriteUV - nEdge * texel * rimPx;
                float aInside = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, inwardUV).a;
                if (_EnableExternalAlpha > 0.5)
                {
                    float aExtIn = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, inwardUV).r;
                    aInside *= aExtIn;
                }

                // Feather at the outer edge so band is soft
                float feather = max(1e-5, max(texel.x, texel.y) * _RimSoftPixels);
                float edgeSoft = smoothstep(_AlphaCutoff, _AlphaCutoff + feather, spriteA);
                float innerSoft = smoothstep(_AlphaCutoff, _AlphaCutoff + feather, aInside);

                // Band mask: inside minus edge → thin animated strip that advances/retreats
                float rimBand = saturate(innerSoft - edgeSoft);

                // Add small “boil” from ripple noise
                float boil = 0.85 + 0.15 * sin(t * 2.1 + ripple * 6.28318);
                float foamMask = pow(saturate(rimBand * boil), _FoamContrast);

                // --- 4) Compose FOAM (white-ish over water under-tint)
                float3 foamCol = _FoamTint.rgb;
                float3 color = lerp(waterUnder, foamCol, foamMask); // foam rides on top visually

                // Final alpha (respect original sprite silhouette)
                float alpha = spriteA;
                alpha = saturate(alpha + (depthLerp - 0.5) * _AlphaDepthStrength);
                alpha *= _OverallAlpha * IN.col.a;

                // Multiply foam opacity by foam mask so only the rim pops
                alpha *= saturate(foamMask + 0.0001);

                return float4(color * IN.col.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
