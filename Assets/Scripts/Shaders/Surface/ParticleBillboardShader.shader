Shader "Custom/ParticleBillboardShader" {

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
	float _SizeByVelocity = 2.0f;
	float3 _worldPos;

	struct particle
	{
		float3 pos;
		float3 vel;
		float4 col;
		float timeElapsed;
		float lifeSpan;
		float3 spawnPosition;
	};

	// buffer containing array of points we want to draw
	StructuredBuffer<particle> particles;

	struct input
	{
		float4 pos : SV_POSITION;
		float4 col : COLOR;
		float3 vel : TEXCOORD1;
		UNITY_FOG_COORDS(1)
	};

	struct output
	{
		float4 pos : SV_POSITION;
		float4 col : COLOR;
		float2 uv : TEXCOORD0;
		UNITY_FOG_COORDS(1)
	};

	input vert(uint id : SV_VertexID)
	{
		input o;
		o.pos = float4(particles[id].pos + _worldPos, 1.0f);
		o.vel = particles[id].vel;
		o.col = particles[id].col;
		//particles[id].col.r, particles[id].col.g, particles[id].col.b
		return o;
	}

	float4 RotPoint(float4 p, float3 offset, float3 sideVector, float3 upVector) {
		float3 finalPos = p.xyz;

		finalPos += offset.x * sideVector;
		finalPos += offset.y * upVector;

		return float4(finalPos, 1);
	}

	[maxvertexcount(4)]
	void geom(point input p[1], inout TriangleStream<output> triStream)
	{
		float2 halfS = _Size;

		float4 v[4];

		float3 up = normalize(float3(p[0].vel.x, p[0].vel.y, p[0].vel.z));
		float3 look = _WorldSpaceCameraPos - p[0].pos.xyz;

		look = normalize(look);
		float3 right = normalize(cross(look, up));
		up = normalize(cross(right, look)) * length(p[0].vel) * _SizeByVelocity;

		v[0] = RotPoint(p[0].pos, float3(-halfS.x, -halfS.y, 0), right, up);
		v[1] = RotPoint(p[0].pos, float3(-halfS.x, halfS.y, 0), right, up);
		v[2] = RotPoint(p[0].pos, float3(halfS.x, -halfS.y, 0), right, up);
		v[3] = RotPoint(p[0].pos, float3(halfS.x, halfS.y, 0), right, up);

		output pIn;

		pIn.col = p[0].col;

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

	float4 frag(output i) : COLOR
	{
		fixed4 col = tex2D(_Sprite, i.uv) * (_Tint * i.col);	// multiplactive colour blending
		UNITY_APPLY_FOG(i.fogCoord, col);

		return col;
	}

		ENDCG
	}
	}
		Fallback Off
}
