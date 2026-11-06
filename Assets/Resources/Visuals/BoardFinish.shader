Shader "ManuEtMente/BoardFinish"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Piece ("Piece finish", Float) = 0
        _Marker ("Move marker", Float) = 0
        _Capture ("Capture marker", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            float _Piece, _Marker, _Capture;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, i.uv);
                if (_Marker > .5)
                {
                    float2 offset = i.uv - .5;
                    float angle = atan2(offset.y, offset.x);
                    float segments = _Capture > .5 ? smoothstep(.15, .3, abs(sin(angle * 4))) : 1;
                    return fixed4(i.color.rgb, source.a * i.color.a * segments);
                }
                float light = dot(source.rgb, float3(.299,.587,.114));
                float3 color;
                if (_Piece > .5)
                {
                    float3 team = source.r > source.g ? float3(1.0,.36,.025) : float3(.045,.70,.32);
                    color = lerp(float3(.018,.04,.05), team, smoothstep(.025,.24,light));
                    color *= .94 + .10 * i.uv.y;
                }
                else
                {
                    color = lerp(float3(.055,.19,.24), float3(.95,.83,.58), light);
                    float red = saturate((source.r - max(source.g, source.b)) * 4);
                    color = lerp(color, float3(.91,.61,.16), red);
                    color *= .94 + .09 * i.uv.y;
                }
                // Palette values are authored in sRGB. In a linear project the framebuffer
                // performs the final sRGB conversion; decode first to avoid washed-out colors.
                color = saturate(color);
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = GammaToLinearSpace(color);
                #endif
                return fixed4(color * i.color.rgb, source.a * i.color.a);
            }
            ENDCG
        }
    }
}
