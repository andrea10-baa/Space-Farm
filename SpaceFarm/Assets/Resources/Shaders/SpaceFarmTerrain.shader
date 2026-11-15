Shader "SpaceFarm/TerrainDust"
{
    Properties
    {
        [PerRendererData] _MainTex ("Relieve", 2D) = "white" {}
        _DustTex ("Polvo marciano", 2D) = "white" {}
        _TileCount ("Repeticiones y desplazamiento", Vector) = (1, 1, 0, 0)
        _DustStrength ("Detalle del polvo", Range(0, 1)) = 0.82
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _DustTex;
            float4 _TileCount;
            fixed4 _Color;
            float _DustStrength;

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            VertexOutput Vert(VertexInput input)
            {
                VertexOutput output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Frag(VertexOutput input) : SV_Target
            {
                fixed4 relief = tex2D(_MainTex, input.uv);
                float2 repeated = input.uv * _TileCount.xy + _TileCount.zw;
                // Reflect alternating tiles so their edges join without a visible seam.
                float2 dustUv = 1.0 - abs(frac(repeated * 0.5) * 2.0 - 1.0);
                fixed3 dust = tex2D(_DustTex, dustUv).rgb;
                fixed3 light = clamp(relief.rgb / fixed3(0.549, 0.333, 0.267), 0.38, 1.6);
                fixed3 detailed = saturate(dust * light);
                fixed3 result = lerp(relief.rgb, detailed, _DustStrength);
                return fixed4(result, relief.a) * input.color;
            }
            ENDCG
        }
    }
}
