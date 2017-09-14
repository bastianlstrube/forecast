Shader "Flow/ParticleSurfaceShader" 
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
			#pragma geometry geom
			#pragma	fragment frag

			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			sampler2D _Sprite;
			float4 _Tint = float4(1.0f, 1.0f, 1.0f, 1.0f);
			float2 _Size = float2(1.0f, 1.0f);
			float3 _worldPos;
			float3 _localScale;
			float _SizeByVelocity = 2.0f;
			
			struct particle
			{
				float3 pos;
				float3 direction;
				float4 col;
				float timeElapsed;
				float lifeSpan;
				float3 spawnPosition;
				float drag;
				float velocity;
				float baseVelocity;
				float scale;
				float4 emissive;
			};

			// buffer containing array of points we want to draw
			StructuredBuffer<particle> particles;

			struct input
			{
				float4 pos : SV_POSITION;
				float3 dir : TEXCOORD2;
				float2 uv : TEXCOORD0;
				float drag : BLENDWEIGHT;
				float sizeOverLifetime : PSIZE;
				float4 col : COLOR0;
				float velocity : TEXCOORD3;
				float4 emissive : COLOR1;
				UNITY_FOG_COORDS(1)
			};

			input vert(uint id : SV_VertexID)
			{
				input i;
				i.pos = float4(_worldPos + (particles[id].pos)* _localScale, 1.0f);
				i.uv = float2(0, 0);
				i.dir = particles[id].direction;
				i.drag = particles[id].drag;
				i.sizeOverLifetime = 2.0f * (0.5f - abs((particles[id].timeElapsed / particles[id].lifeSpan) - 0.5f)) * particles[id].scale;
				i.col = particles[id].col;
				i.velocity = particles[id].velocity;
				i.emissive = particles[id].emissive;
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
				float2 halfS = _Size * p[0].sizeOverLifetime;

				float4 v[4];

				float3 up = normalize(float3(p[0].dir.x, p[0].dir.y, p[0].dir.z));
				float3 look = _WorldSpaceCameraPos - p[0].pos.xyz;

				look = normalize(look);
				float3 right = normalize(cross(look, up));
				up = normalize(cross(right, look));// *((p[0].velocity) * (_SizeByVelocity)+(1.0f)); // - p[0].drag));

				v[0] = RotPoint(p[0].pos, float3(-halfS.x, -halfS.y, 0), right, up);
				v[1] = RotPoint(p[0].pos, float3(-halfS.x, halfS.y, 0), right, up);
				v[2] = RotPoint(p[0].pos, float3(halfS.x, -halfS.y, 0), right, up);
				v[3] = RotPoint(p[0].pos, float3(halfS.x, halfS.y, 0), right, up);

				input pIn;
				pIn.velocity = 0;
				pIn.col = p[0].col;
				pIn.emissive = p[0].emissive;

				pIn.dir = float3(0, 0, 0);
				pIn.drag = 0;
				pIn.sizeOverLifetime = 0;

				pIn.pos = mul(UNITY_MATRIX_VP, v[0]);
				pIn.uv = float2(0.0f, 0.0f);
				UNITY_TRANSFER_FOG(pIn, pIn.pos);
				triStream.Append(pIn);

				pIn.pos = mul(UNITY_MATRIX_VP, v[1]);
				pIn.uv = float2(0.0f, 1.0f);
				UNITY_TRANSFER_FOG(pIn, pIn.pos);
				triStream.Append(pIn);

				pIn.pos = mul(UNITY_MATRIX_VP, v[2]);
				pIn.uv = float2(1.0f, 0.0f);
				UNITY_TRANSFER_FOG(pIn, pIn.pos);
				triStream.Append(pIn);

				pIn.pos = mul(UNITY_MATRIX_VP, v[3]);
				pIn.uv = float2(1.0f, 1.0f);
				UNITY_TRANSFER_FOG(pIn, pIn.pos);
				triStream.Append(pIn);
			}

			float4 frag(input i) : COLOR
			{
				
				fixed4 col = tex2D(_Sprite, i.uv) * (_Tint) * i.col + i.emissive + float4(0.0f,0.0f,3.0f,2.0f);	// multiplactive colour blending
				UNITY_APPLY_FOG(i.fogCoord, col);

				return col;
			}

			ENDCG
		}
	}

	Fallback Off
}
