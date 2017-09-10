Shader "Flow/VectorSurfaceShader"
{
	Properties
	{
		_PaintBrushSize("PaintBrushSize", Float) = 10.0
		_PaintSourceDistance("PaintSourceDistance", Float) = 150.0
	}

		SubShader
	{

		Blend SrcAlpha OneMinusSrcAlpha

		Cull off
		ZWrite off

		Pass
	{

		CGPROGRAM
#pragma target 5.0

#pragma vertex vert
#pragma fragment frag

#include "UnityCG.cginc"


	float _PaintBrushSize = 10.0f;
	float _PaintSourceDistance = 150.0f;
	struct vectorStruct
	{
		float3 pos;
		float4 col;
	};

	// The buffer containing an array of the points we want to draw.
	StructuredBuffer<vectorStruct> buf_Points;

	// input struct representing a single vector's data (position and colour)
	struct ps_input {
		float4 pos : SV_POSITION;
		float4 col : COLOR;
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
		float dist = distance(_WorldSpaceCameraPos, worldPos);
		float alpha = saturate(_PaintSourceDistance + _PaintBrushSize - dist) * saturate(dist - _PaintSourceDistance + _PaintBrushSize);
		o.col = buf_Points[id].col * float4(alpha, 1.0f, alpha, saturate(alpha+0.2f));

		return o;
	}

	// fragment/pixel shader returns a color for each point.
	float4 frag(ps_input i) : COLOR
	{
		return i.col;
	}

		ENDCG
	}
	}

		Fallback Off
}