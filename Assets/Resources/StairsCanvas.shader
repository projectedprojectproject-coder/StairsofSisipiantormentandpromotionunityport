// WebCanvas가 쓰는 셰이더: 웹 캔버스의 drawImage/fillRect/fillText와 CSS filter "grayscale(1) brightness(0.85) contrast(0.85)".
// Resources 폴더에 있어 빌드에 늘 들어간다.
Shader "Hidden/StairsCanvas"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Gray ("Grayscale filter", Float) = 0
        _Text ("Alpha-only font texture", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest Always Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Gray, _Text;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            float4 frag (v2f i) : SV_Target
            {
                float4 t = tex2D(_MainTex, i.uv);
                float4 c = _Text > 0.5 ? float4(i.color.rgb, i.color.a * t.a) : t * i.color;
                if (_Gray > 0.5)
                {
                    float l = dot(c.rgb, float3(0.2126, 0.7152, 0.0722)) * 0.85;   // grayscale(1) brightness(0.85)
                    l = (l - 0.5) * 0.85 + 0.5;                                    // contrast(0.85)
                    c.rgb = saturate(l.xxx);
                }
                return c;
            }
            ENDCG
        }
    }
}
