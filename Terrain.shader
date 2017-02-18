Shader "Custom/Terrain" 
{
	Properties 
	{	_CliffTex("Grass", 2D) = "white" {}
		_SandTex("Sand", 2D) = "white" {}
		_GravelTex("GravelTex", 2D) = "white" {}
		_RockTex("RockTex", 2D) = "white" {}

		_IronTex("IronTex", 2D) = "white" {}
		_GoldTex("GoldTex", 2D) = "white" {}
		_GunPowderTex("GunPowderTex", 2D) = "white" {}
		_TungstenTex("TungstenTex", 2D) = "white" {}
		_Blending ("Blending" , Range( 0.0001 ,0.5 ))= 0.5
		_FBlending ("Sharpness" , Range( 0.0001 ,0.25 ))= 0.5
		_Fuzzy ("Fuzzy Blending" , Range( 0.1 ,16.0 ))= 0.5
	}
	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		LOD 200
		//Cull off
				

		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows
		#pragma target 3.0

		sampler2D _SandTex, _GravelTex, _RockTex,_CliffTex,_IronTex,_GoldTex,_GunPowderTex,_TungstenTex;
		float _Blending;
		float _FBlending;
		float _Fuzzy;
		struct Input 
		{
			fixed3 worldPos;
			fixed3 worldNormal;
			float4 color : COLOR;
		};
		
	
		fixed4 TriplanarSample(sampler2D tex, fixed3 worldPosition, fixed3 projNormal, float scale)
		{
			fixed4 cZY = tex2D(tex, worldPosition.zy * scale);
			fixed4 cXZ = tex2D(tex, worldPosition.xz * scale);
			fixed4 cXY = tex2D(tex, worldPosition.xy * scale);
			
			cXY = lerp(cXY, cXZ, projNormal.y);
			return lerp(cXY, cZY, projNormal.x);
		}
		

		
		
		float4 blend(float4 texture1, float a1, float4 texture2, float a2,float b)
		{
	
    	float depth = _Blending;
    	float ma = max(texture1.a * a1, texture2.a * a2) - depth;

    	float b1 = max(texture1.a * a1 - ma, 0);
    	float b2 = max(texture2.a * a2 - ma, 0);

    	return ((texture1.rgba * b1) + (texture2.rgba * b2)*b) / (b1 + b2);
		}
		
		
		
		void surf(Input IN, inout SurfaceOutputStandard o) 
		
		{	
			fixed3 projNormal = saturate(pow(IN.worldNormal * 1.5, 4));
			
			
			float4 sand = TriplanarSample(_SandTex, IN.worldPos, projNormal, 1.0);
			float4 gravel = TriplanarSample(_GravelTex, IN.worldPos, projNormal, 1.0);
			float4 rock = TriplanarSample(_RockTex, IN.worldPos, projNormal, 0.1);
			float4 cliff = TriplanarSample(_CliffTex, IN.worldPos, projNormal, 1.0);
			
			float4 Iron = TriplanarSample(_IronTex, IN.worldPos, projNormal, 1.0);
			
			float4 Gold = TriplanarSample(_GoldTex, IN.worldPos, projNormal, 1.0);
			
			float4 Gunpowder = TriplanarSample(_GunPowderTex, IN.worldPos, projNormal, 0.1);

			float4 Tungsten = TriplanarSample(_TungstenTex, IN.worldPos, projNormal, 1.0);
		
			float4 controlMap = IN.color;
			float4 col= 0;
			
			if(controlMap.g<0.71f&&controlMap.g>_FBlending){
			col =	lerp(col,blend(sand,sand.a*2,sand ,sand.a*2, controlMap.g*2),controlMap.g*_Fuzzy);
			}
			
			if(controlMap.r<0.71f&&controlMap.r>_FBlending){
			col = lerp(col,blend(col,col.a*2, rock,rock.a*2,controlMap.r*2 ),controlMap.r*_Fuzzy);
			}
			if(controlMap.r=0.0f&&controlMap.g==0.0f&&controlMap.b==0.0f&&controlMap.a==0.0f)
			col = lerp(col,blend(col,col.a*2, rock,rock.a*2,1 ),controlMap.g*_Fuzzy);
			
			if(controlMap.b<0.71f&&controlMap.b>_FBlending){
			col = lerp(col,blend(col,col.a*2, gravel, gravel.a*2,controlMap.b*2),controlMap.b*_Fuzzy);
			}
			if(controlMap.a<0.71f&&controlMap.a>_FBlending){
			col = lerp(col,blend(col,col.a*2, cliff,cliff.a*2,controlMap.a*2), controlMap.a*_Fuzzy);

			}
			
			if(controlMap.r>0.5f)
			col = lerp(col,blend(col,col.a, Iron, Iron.a,Iron.a),controlMap.r*_Fuzzy);
			
			
			if(controlMap.g>0.5f)
			col = lerp(col,blend(col,col.a, Gold,Gold.a, controlMap.g),controlMap.g*_Fuzzy);
			
				if(controlMap.b>0.5f)
			col = lerp(col,blend(col,col.a, Gunpowder,Gunpowder.a, controlMap.b*2),controlMap.b*_Fuzzy);
			
			if(controlMap.a>0.5f)
			col = lerp(col,blend(col,col.a ,Tungsten , Tungsten.a,controlMap.a*2),controlMap.a*_Fuzzy);
			

			
			
			
			
						

			o.Metallic = 0;
            o.Smoothness = 0;
			
			o.Albedo = col/_Fuzzy;
			//o.Normal = normalize(coln.rgb) ;
			//o.Albedo = controlMap;
			
		}
		ENDCG
	} 
	FallBack "Diffuse"
}
