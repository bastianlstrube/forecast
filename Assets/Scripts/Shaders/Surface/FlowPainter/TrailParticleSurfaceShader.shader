Shader "Flow/TrailParticleShader" 
{

	Properties
	{
		_Sprite("Sprite", 2D) = "white" {}
		_Tint("Tint", Color) = (1,1,1,1)
		_Size("Size", Vector) = (1,1,0,0)
		_SizeByVelocity("Size By Velocity", Float) = 2.0
	}


	SubShader
	{
		Tags{ "Queue" = "Overlay+100" "RenderType" = "Transparent" }

		LOD 100
		Blend SrcAlpha One
		
		Cull off
		ZWrite off

		Pass
		{

			CGPROGRAM
			#pragma target 5.0

			#pragma vertex vert
			//#pragma geometry geom
			#pragma	fragment frag

			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			sampler2D _Sprite;
			float4 _Tint = float4(1.0f, 1.0f, 1.0f, 1.0f);
			float2 _Size = float2(1.0f, 1.0f);
			float3 _worldPos;
			float3 _localScale;
			
			struct trailParticle
			{
				float3 position;
				float3 albedo;
				float3 emissive;
				float alpha;
				float scale;
			};

			// buffer containing array of points we want to draw
			StructuredBuffer<trailParticle> trailParticles;

			struct input
			{
				float4 position : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 albedo : COLOR0;
				float4 emissive : COLOR1;
				float scale: TEXCOORD3;
				UNITY_FOG_COORDS(1)
			};

			input vert(uint id : SV_VertexID)
			{
				input i;
				i.position = float4(_worldPos + (trailParticles[id].position) * _localScale, 1.0f);
				i.uv = float2(0, 0);
				i.albedo = float4(trailParticles[id].albedo, trailParticles[id].alpha);
				i.emissive = float4(trailParticles[id].emissive, 0.0f);
				i.scale= trailParticles[id].scale;

				return i;
			}

			float4 RotPoint(float4 p, float3 offset, float3 sideVector, float3 upVector) {
				float3 finalPos = p.xyz;

				finalPos += offset.x * sideVector;
				finalPos += offset.y * upVector;

				return float4(finalPos, 1);
			}

			[maxvertexcount(4)]
			void geom(point input p[1], inout TriangleStream<input> triStream)
			{
				float2 halfS = _Size * p[0].scale;

				float4 v[4];

				float3 up = float3(1.0f, 0.0f, 0.0f);
				float3 look = _WorldSpaceCameraPos - p[0].position.xyz;

				//float4x4 vp = mul(UNITY_MATRIX_MVP, _World2Object);

				look = normalize(look);
				float3 right = normalize(cross(look, up));
				up = normalize(cross(right, look));
				
				v[0] = RotPoint(p[0].position, float3(-halfS.x, -halfS.y, 0), right, up);
				v[1] = RotPoint(p[0].position, float3(-halfS.x, halfS.y, 0), right, up);
				v[2] = RotPoint(p[0].position, float3(halfS.x, -halfS.y, 0), right, up);
				v[3] = RotPoint(p[0].position, float3(halfS.x, halfS.y, 0), right, up);
				
				/*
				v[0] = p[0].position + float4(0.01f, 0, 0, 0);
				v[1] = p[0].position;
				v[2] = p[1].position + float4(0.01f, 0, 0, 0);
				v[3] = p[1].position;
				*/

				input pIn;
				pIn.albedo = p[0].albedo;
				pIn.emissive = p[0].emissive;
				pIn.scale = p[0].scale;

				pIn.position = mul(UNITY_MATRIX_VP, v[0]);
				pIn.uv = float2(0.0f, 0.0f);
				UNITY_TRANSFER_FOG(pIn, pIn.position);
				triStream.Append(pIn);

				pIn.position = mul(UNITY_MATRIX_VP, v[1]);
				pIn.uv = float2(0.0f, 1.0f);
				UNITY_TRANSFER_FOG(pIn, pIn.position);
				triStream.Append(pIn);

				pIn.position = mul(UNITY_MATRIX_VP, v[2]);
				pIn.uv = float2(1.0f, 0.0f);
				UNITY_TRANSFER_FOG(pIn, pIn.position);
				triStream.Append(pIn);

				pIn.position = mul(UNITY_MATRIX_VP, v[3]);
				pIn.uv = float2(1.0f, 1.0f);
				UNITY_TRANSFER_FOG(pIn, pIn.position);
				triStream.Append(pIn);
			}

			float4 frag(input i) : COLOR
			{
				
				fixed4 colour = tex2D(_Sprite, i.uv) * (_Tint) * i.albedo + i.emissive;
				UNITY_APPLY_FOG(i.fogCoord, colour);

				return colour;
			}

			ENDCG
		}
	}

	Fallback Off
}
