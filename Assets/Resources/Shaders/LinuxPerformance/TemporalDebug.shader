Shader "Hidden/Unturned/LinuxPerformance/TemporalDebug"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "black" {}
	}
	SubShader
	{
		Cull Off ZWrite Off ZTest Always
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			int _DebugMode;
			float4 _TemporalJitter;
			float4 _TemporalNearFar;

			fixed4 frag(v2f_img input) : SV_Target
			{
				float4 sampleValue = tex2D(_MainTex, input.uv);
				if (_DebugMode == 1)
				{
					float linearDepth = Linear01Depth(sampleValue.r);
					return float4(linearDepth, linearDepth, linearDepth, 1.0);
				}
				if (_DebugMode == 2)
					return float4(sampleValue.rg * 0.5 + 0.5, 0.0, 1.0);
				if (_DebugMode == 3)
				{
					float magnitude = saturate(length(sampleValue.rg) * 32.0);
					return float4(magnitude, magnitude, magnitude, 1.0);
				}
				if (_DebugMode == 4)
					return float4(sampleValue.rrr, 1.0);
				if (_DebugMode == 5)
				{
					float2 jitterPixels = _TemporalJitter.xy * _TemporalJitter.zw;
					return float4(jitterPixels * 0.5 + 0.5, 0.0, 1.0);
				}
				return sampleValue;
			}
			ENDCG
		}
	}
}
