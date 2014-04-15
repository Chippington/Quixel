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

            float d = yy - (-50f);

            return new VoxelData()
            {
                density = d,
                material = 0
            };
        }
    }
}