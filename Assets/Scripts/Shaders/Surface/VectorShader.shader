Shader "DX11/VectorShader"
{
	SubShader
	{
		Pass
	{

		CGPROGRAM
#pragma target 5.0

#pragma vertex vert
#pragma fragment frag

#include "UnityCG.cginc"

		struct particleStruct
	{
		float3 pos;
	};

	// The buffer containing an array of the points we want to draw.
	StructuredBuffer<particleStruct> buf_Points;

	// input struct representing a single particle's data (position and colour)
	struct ps_input {
		float4 pos : SV_POSITION;
	};

	// fetch a point from the buffer corresponding to the vertex index
	// fransform with the view-projection matrix before passing to the fragment shader
	ps_input vert(uint id : SV_VertexID)
	{
		// output struct, which is sent to the fragment shader
		ps_input o;

		float3 worldPos = buf_Points[id].pos;

		// transform the position
		o.pos = mul(UNITY_MATRIX_VP, float4(worldPos,1.0f));

		return o;
	}

	// fragment/pixel shader returns a color for each point.
	float4 frag(ps_input i) : COLOR
	{
		return float4(1.0,1.0,1.0,1.0);
	}

		ENDCG
	}
	}

		Fallback Off
}