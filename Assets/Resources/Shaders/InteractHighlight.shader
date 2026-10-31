// Soft glow drawn over the object the player can interact with (InteractHighlighter).
// Unlit and additive, two passes of one object only:
//   1. a thin outline a few pixels wide around the silhouette (an expanded back-face shell), so flat low-poly faces read too;
//   2. a rim brightest along the silhouette with a faint fill elsewhere.
// Queued just before transparent objects, so world-space UI (the computer's email screen) still draws on top of it.
Shader "Scam Sense/Interact Highlight"
{
    Properties
    {
        _Color ("Glow Colour", Color) = (0.55, 0.85, 1, 1)
        _Power ("Rim Sharpness", Range(0.5, 8)) = 2.2
        _Fill ("Face Fill", Range(0, 1)) = 0.08
        _OutlinePx ("Outline Width (pixels)", Range(0, 8)) = 3
        _OutlineBoost ("Outline Brightness", Range(0, 3)) = 1.4
        _Strength ("Strength", Range(0, 2)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Outline"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _OutlinePx, _OutlineBoost, _Strength;

            float4 vert (appdata_base v) : SV_POSITION
            {
                float4 pos = UnityObjectToClipPos(v.vertex);
                // Push each vertex outward on screen along its view-space normal (no normalize: faces pointing at or away
                // from the camera stay put, so flat sheets stay stable), keeping the width constant in pixels.
                float2 n = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal).xy;
                float len = length(n);
                if (len > 1) n /= len;
                pos.xy += n * (_OutlinePx * 2 / _ScreenParams.xy) * pos.w;
                return pos;
            }

            fixed4 frag () : SV_Target
            {
                return _Color * (_Strength * _OutlineBoost);
            }
            ENDCG
        }

        Pass
        {
            Name "Rim"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Power, _Fill, _Strength;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half rim = pow(1 - saturate(dot(normalize(i.normal), normalize(i.viewDir))), _Power);
                return _Color * ((rim + _Fill) * _Strength);
            }
            ENDCG
        }
    }
    Fallback Off
}
