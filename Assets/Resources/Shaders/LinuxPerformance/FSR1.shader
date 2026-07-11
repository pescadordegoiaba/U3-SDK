// AMD FidelityFX Super Resolution 1.0 - EASU + RCAS
// Origem do algoritmo: AMD FidelityFX Super Resolution [FSR 1] ffx_fsr1.h v1.20210629.
// Licenca do algoritmo AMD: MIT. Copyright (c) 2021 Advanced Micro Devices, Inc.
// Adaptacoes nesta versao:
// - Port manual para Unity Built-in Render Pipeline / CGPROGRAM.
// - Constantes calculadas no shader a partir de _SourceSize e _OutputSize.
// - Amostragem 12-tap equivalente ao caminho EASU nao empacotado.
// - RCAS em passe separado com alpha preservado.
// - Sem dependencia de plugin nativo.
Shader "Hidden/Unturned/LinuxPerformance/FSR1"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_Sharpness("RCAS Sharpness", Range(0, 1)) = 0.2
	}

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		CGINCLUDE
		#include "UnityCG.cginc"

		#define FSR_RCAS_LIMIT (0.25 - (1.0 / 16.0))

		sampler2D _MainTex;
		float4 _SourceSize; // xy = input size, zw = input texel size
		float4 _OutputSize; // xy = output size, zw = output texel size
		float _Sharpness;

		struct appdata
		{
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};

		struct v2f
		{
			float4 vertex : SV_POSITION;
			float2 uv : TEXCOORD0;
		};

		v2f vert(appdata v)
		{
			v2f o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.uv = v.uv;
			return o;
		}

		float ApproxRcp(float v)
		{
			return rcp(max(abs(v), 1.0e-6));
		}

		float3 Min3(float3 a, float3 b, float3 c)
		{
			return min(a, min(b, c));
		}

		float3 Max3(float3 a, float3 b, float3 c)
		{
			return max(a, max(b, c));
		}

		float Min3Scalar(float a, float b, float c)
		{
			return min(a, min(b, c));
		}

		float Max3Scalar(float a, float b, float c)
		{
			return max(a, max(b, c));
		}

		float4 SamplePixel(float2 pixel)
		{
			float2 clampedPixel = clamp(pixel, 0.0, _SourceSize.xy - 1.0);
			return tex2D(_MainTex, (clampedPixel + 0.5) * _SourceSize.zw);
		}

		float Luma2(float3 color)
		{
			return color.b * 0.5 + (color.r * 0.5 + color.g);
		}

		void EasuSet(inout float2 dir, inout float len, float2 pp, bool biS, bool biT, bool biU, bool biV, float lA, float lB, float lC, float lD, float lE)
		{
			float w = 0.0;
			if (biS)
				w = (1.0 - pp.x) * (1.0 - pp.y);
			if (biT)
				w = pp.x * (1.0 - pp.y);
			if (biU)
				w = (1.0 - pp.x) * pp.y;
			if (biV)
				w = pp.x * pp.y;

			float dc = lD - lC;
			float cb = lC - lB;
			float lenX = max(abs(dc), abs(cb));
			lenX = ApproxRcp(lenX);
			float dirX = lD - lB;
			dir.x += dirX * w;
			lenX = saturate(abs(dirX) * lenX);
			lenX *= lenX;
			len += lenX * w;

			float ec = lE - lC;
			float ca = lC - lA;
			float lenY = max(abs(ec), abs(ca));
			lenY = ApproxRcp(lenY);
			float dirY = lE - lA;
			dir.y += dirY * w;
			lenY = saturate(abs(dirY) * lenY);
			lenY *= lenY;
			len += lenY * w;
		}

		void EasuTap(inout float3 aC, inout float aW, float2 off, float2 dir, float2 len2, float lob, float clp, float3 color)
		{
			float2 v;
			v.x = off.x * dir.x + off.y * dir.y;
			v.y = off.x * -dir.y + off.y * dir.x;
			v *= len2;
			float d2 = min(dot(v, v), clp);
			float wB = 2.0 / 5.0 * d2 - 1.0;
			float wA = lob * d2 - 1.0;
			wB *= wB;
			wA *= wA;
			wB = 25.0 / 16.0 * wB - 25.0 / 16.0;
			float w = wB * wA;
			aC += color * w;
			aW += w;
		}

		float4 FsrEasu(float2 uv)
		{
			float2 outputPixel = uv * _OutputSize.xy;

			float inputViewportX = _SourceSize.x;
			float inputViewportY = _SourceSize.y;
			float2 con0xy = float2(inputViewportX / _OutputSize.x, inputViewportY / _OutputSize.y);
			float2 con0zw = float2(0.5 * inputViewportX / _OutputSize.x - 0.5, 0.5 * inputViewportY / _OutputSize.y - 0.5);

			float2 pp = outputPixel * con0xy + con0zw;
			float2 fp = floor(pp);
			pp -= fp;

			float4 b = SamplePixel(fp + float2(0.0, -1.0));
			float4 c = SamplePixel(fp + float2(1.0, -1.0));
			float4 e = SamplePixel(fp + float2(-1.0, 0.0));
			float4 f = SamplePixel(fp + float2(0.0, 0.0));
			float4 g = SamplePixel(fp + float2(1.0, 0.0));
			float4 h = SamplePixel(fp + float2(2.0, 0.0));
			float4 i = SamplePixel(fp + float2(-1.0, 1.0));
			float4 j = SamplePixel(fp + float2(0.0, 1.0));
			float4 k = SamplePixel(fp + float2(1.0, 1.0));
			float4 l = SamplePixel(fp + float2(2.0, 1.0));
			float4 n = SamplePixel(fp + float2(0.0, 2.0));
			float4 o = SamplePixel(fp + float2(1.0, 2.0));

			float bL = Luma2(b.rgb);
			float cL = Luma2(c.rgb);
			float eL = Luma2(e.rgb);
			float fL = Luma2(f.rgb);
			float gL = Luma2(g.rgb);
			float hL = Luma2(h.rgb);
			float iL = Luma2(i.rgb);
			float jL = Luma2(j.rgb);
			float kL = Luma2(k.rgb);
			float lL = Luma2(l.rgb);
			float nL = Luma2(n.rgb);
			float oL = Luma2(o.rgb);

			float2 dir = 0.0;
			float len = 0.0;
			EasuSet(dir, len, pp, true, false, false, false, bL, eL, fL, gL, jL);
			EasuSet(dir, len, pp, false, true, false, false, cL, fL, gL, hL, kL);
			EasuSet(dir, len, pp, false, false, true, false, fL, iL, jL, kL, nL);
			EasuSet(dir, len, pp, false, false, false, true, gL, jL, kL, lL, oL);

			float dirR = dot(dir, dir);
			bool zeroDir = dirR < (1.0 / 32768.0);
			dirR = rsqrt(max(dirR, 1.0e-6));
			dir = zeroDir ? float2(1.0, 0.0) : dir * dirR;

			len *= 0.5;
			len *= len;

			float stretch = (dir.x * dir.x + dir.y * dir.y) * ApproxRcp(max(abs(dir.x), abs(dir.y)));
			float2 len2 = float2(1.0 + (stretch - 1.0) * len, 1.0 - 0.5 * len);
			float lob = 0.5 + ((0.25 - 0.04) - 0.5) * len;
			float clp = ApproxRcp(lob);

			float3 min4 = min(Min3(f.rgb, g.rgb, j.rgb), k.rgb);
			float3 max4 = max(Max3(f.rgb, g.rgb, j.rgb), k.rgb);

			float3 aC = 0.0;
			float aW = 0.0;
			EasuTap(aC, aW, float2(0.0, -1.0) - pp, dir, len2, lob, clp, b.rgb);
			EasuTap(aC, aW, float2(1.0, -1.0) - pp, dir, len2, lob, clp, c.rgb);
			EasuTap(aC, aW, float2(-1.0, 1.0) - pp, dir, len2, lob, clp, i.rgb);
			EasuTap(aC, aW, float2(0.0, 1.0) - pp, dir, len2, lob, clp, j.rgb);
			EasuTap(aC, aW, float2(0.0, 0.0) - pp, dir, len2, lob, clp, f.rgb);
			EasuTap(aC, aW, float2(-1.0, 0.0) - pp, dir, len2, lob, clp, e.rgb);
			EasuTap(aC, aW, float2(1.0, 1.0) - pp, dir, len2, lob, clp, k.rgb);
			EasuTap(aC, aW, float2(2.0, 1.0) - pp, dir, len2, lob, clp, l.rgb);
			EasuTap(aC, aW, float2(2.0, 0.0) - pp, dir, len2, lob, clp, h.rgb);
			EasuTap(aC, aW, float2(1.0, 0.0) - pp, dir, len2, lob, clp, g.rgb);
			EasuTap(aC, aW, float2(1.0, 2.0) - pp, dir, len2, lob, clp, o.rgb);
			EasuTap(aC, aW, float2(0.0, 2.0) - pp, dir, len2, lob, clp, n.rgb);

			float3 easuRgb = aW > 1.0e-6 ? aC * ApproxRcp(aW) : f.rgb;
			float3 rgb = max(0.0, min(max4, max(min4, easuRgb)));
			float alpha = saturate((f.a + g.a + j.a + k.a) * 0.25);
			return float4(rgb, alpha);
		}

		float4 fragEasu(v2f i) : SV_Target
		{
			return FsrEasu(i.uv);
		}

		float4 LoadRcas(int2 p)
		{
			int2 clampedPixel = clamp(p, int2(0, 0), int2((int)_SourceSize.x - 1, (int)_SourceSize.y - 1));
			return tex2D(_MainTex, ((float2)clampedPixel + 0.5) * _SourceSize.zw);
		}

		float4 fragRcas(v2f i) : SV_Target
		{
			int2 ip = (int2)floor(i.uv * _SourceSize.xy);
			float4 b4 = LoadRcas(ip + int2(0, -1));
			float4 d4 = LoadRcas(ip + int2(-1, 0));
			float4 e4 = LoadRcas(ip);
			float4 f4 = LoadRcas(ip + int2(1, 0));
			float4 h4 = LoadRcas(ip + int2(0, 1));

			float bL = Luma2(b4.rgb);
			float dL = Luma2(d4.rgb);
			float eL = Luma2(e4.rgb);
			float fL = Luma2(f4.rgb);
			float hL = Luma2(h4.rgb);
			float nz = 0.25 * bL + 0.25 * dL + 0.25 * fL + 0.25 * hL - eL;
			nz = saturate(abs(nz) * ApproxRcp(Max3Scalar(Max3Scalar(bL, dL, eL), fL, hL) - Min3Scalar(Min3Scalar(bL, dL, eL), fL, hL)));
			nz = -0.5 * nz + 1.0;

			float3 mn4 = min(Min3(b4.rgb, d4.rgb, f4.rgb), h4.rgb);
			float3 mx4 = max(Max3(b4.rgb, d4.rgb, f4.rgb), h4.rgb);
			float3 hitMin = min(mn4, e4.rgb) * rcp(max(4.0 * mx4, 1.0e-6));
			float3 hitMax = (1.0 - max(mx4, e4.rgb)) / min(4.0 * mn4 - 4.0, -1.0e-6);
			float3 lobeRgb = max(-hitMin, hitMax);

			float sharpnessStops = lerp(2.0, 0.0, saturate(_Sharpness));
			float sharpness = exp2(-sharpnessStops);
			float lobe = max(-FSR_RCAS_LIMIT, min(max(lobeRgb.r, max(lobeRgb.g, lobeRgb.b)), 0.0)) * sharpness * nz;
			float rcpL = rcp(4.0 * lobe + 1.0);
			float3 rgb = max(0.0, (lobe * b4.rgb + lobe * d4.rgb + lobe * h4.rgb + lobe * f4.rgb + e4.rgb) * rcpL);
			return float4(rgb, e4.a);
		}
		ENDCG

		Pass
		{
			Name "EASU"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragEasu
			#pragma target 3.0
			ENDCG
		}

		Pass
		{
			Name "RCAS"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragRcas
			#pragma target 3.0
			ENDCG
		}
	}

	Fallback Off
}
