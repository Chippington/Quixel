/*
	Quick Voxel Terrain Engine
    Copyright (C) 2013  Gerrit Jamerson

    This program is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License along
    with this program; if not, write to the Free Software Foundation, Inc.,
    51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
	
	Email: scythix@hotmail.com
*/

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

            float d = yy - 50f;

            float warp = SimplexNoise.Noise.Generate(xx / 100f, yy / 100f, zz / 100f);
            xx += warp;
            yy += warp;
            zz += warp;

            float noise = SimplexNoise.Noise.Generate(xx / 500f, 0f, zz / 500f);
            d += noise * 70.5f;

            noise = SimplexNoise.Noise.Generate(xx / 100f, 0f, zz / 100f);
            d += noise * SimplexNoise.Noise.Generate(xx / 200f, 0f, zz / 200f) * 10f;

            noise = SimplexNoise.Noise.Generate(xx / 4000f, 0f, zz / 4000f);
            d += noise * 300f;

            noise = SimplexNoise.Noise.Generate(xx / 10000f, 0f, zz / 10000f);
            d += noise * 1400f;

            if (yy < -150f)
                d -= (SimplexNoise.Noise.Generate(xx / 680f, yy / 600f, zz / 680f) * 190f) * ((yy + 150f) / 300f);


            return new VoxelData()
            {
                density = d,
                material = 0
            };
        }
    }
}