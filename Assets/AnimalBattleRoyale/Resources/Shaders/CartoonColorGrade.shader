Shader "Hidden/AnimalBattleRoyale/CartoonColorGrade"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Saturation ("Vibrance", Range(0.5, 1.6)) = 1.24
        _Contrast ("Contrast", Range(0.7, 1.4)) = 1.05
        _Brightness ("Brightness", Range(0.7, 1.2)) = 0.95
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half _Saturation;
            half _Contrast;
            half _Brightness;

            fixed4 frag(v2f_img input) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, input.uv);
                half3 color = max(sample.rgb, 0.0h);
                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                half highest = max(color.r, max(color.g, color.b));
                half lowest = min(color.r, min(color.g, color.b));
                half chroma = highest - lowest;
                half lowSaturationWeight = 1.0h - saturate(chroma * 1.25h);
                half adaptiveSaturation = 1.0h + (_Saturation - 1.0h) * lowSaturationWeight;
                color = lerp(luminance.xxx, color, adaptiveSaturation);
                color = (color - 0.5h) * _Contrast + 0.5h;
                color *= _Brightness;
                return fixed4(saturate(color), sample.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
