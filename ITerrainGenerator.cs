using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System;
using System.IO;

namespace Quixel
{
    public interface IGenerator
    {
        /// <summary>
        /// Calculate the density at a given point.
        /// </summary>
        /// <param name="pos">The (real world) position</param>
        /// <returns></returns>
        VoxelData calculateDensity(Vector3 pos);
    }

    public struct VoxelData
    {
        public float density;
        public byte material;
    }

    internal class BasicTerrain : IGenerator
    {
        VoxelData IGenerator.calculateDensity(Vector3 pos)
        {
            float xx, yy, zz;
            xx = pos.x;
            yy = pos.y;
            zz = pos.z;

            float d = yy - (-100f);
			float D = 0;
					float warp = SimplexNoise.Noise.Generate(xx / 100f, yy / 100f, zz / 100f);
		xx += warp;
		yy += warp;
		zz += warp;
		
		float noise = SimplexNoise.Noise.Generate(xx / 500f, 0f, zz / 500f);
		d += noise * 8f;
			D+=noise ;
			noise = SimplexNoise.Noise.Generate(xx / 100f, 0f, zz / 100f)* SimplexNoise.Noise.Generate(xx / 200f, 0f, zz / 200f);
		d += noise *10;
			D+=noise;
			noise = SimplexNoise.Noise.Generate(xx / 400f, 0f, zz / 400f);
		d += noise * 30f;
			D+=noise ;
			//D=SimplexNoise.Noise.Generate(xx/20,yy/20,zz/20);
		noise = SimplexNoise.Noise.Generate(xx / 1000f, 0f, zz / 1000f);
		d += noise * 80f;
			D+=noise ;

		//Uncomment for some silly looking caves/hills
	//	if (yy < -150f)
			//noise = (SimplexNoise.Noise.Generate(xx / 680f, yy / 600f, zz / 680f) * 190f) * ((yy + 150f) / 300f);
			//D-=noise;
			//d-=noise;
			D*=5;
			D = Mathf.Clamp(D,-0.5f,0.5f);
			D+=0.5f;
			D *= 7;
			Mathf.Clamp(D,0,7);
			return new VoxelData()
            {	

                density = d,
			//	material =0
                material = (byte)D
            };
        }
    }
}