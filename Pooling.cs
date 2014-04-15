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
    internal static class ChunkPool
    {
        /// <summary>A list of unused chunk objects</summary>
        public static List<GameObject> chunkList = new List<GameObject>();
        public static int totalCreated = 0;

        /// <summary>
        /// Gets a chunk from the chunk pool, creates one if the chunk pool is empty
        /// </summary>
        /// <returns></returns>
        public static GameObject getChunk()
        {
            if (chunkList.Count == 0)
            {
                totalCreated++;
                GameObject obj = (GameObject)GameObject.Instantiate(MeshFactory.chunkObj);
                obj.transform.parent = MeshFactory.terrainObj.transform;
                return obj;
            }

            GameObject chunk = chunkList[0];
            chunkList.RemoveAt(0);
            chunk.SetActive(true);
            return chunk;
        }

        /// <summary>
        /// Recycles a chunk gameobject to the chunk pool
        /// </summary>
        /// <param name="chunk"></param>
        public static void recycleChunk(GameObject chunk)
        {
            chunk.GetComponent<MeshFilter>().sharedMesh = null;
            chunk.GetComponent<MeshCollider>().sharedMesh = null;
            chunk.SetActive(false);
            chunkList.Add(chunk);
        }
    }

    internal static class DensityPool
    {
        /// <summary>
        /// A pool of reusable density arrays.
        /// </summary>
        //private static Queue<DensityData> densityPool = new Queue<DensityData>();
        //private static Queue<DensityData> densityRecycleList = new Queue<DensityData>();
        private static DensityThread densityThread;

        /// <summary>
        /// Initializes the density pool, starts the recycle thread.
        /// </summary>
        public static void init()
        {
            //densityThread = new DensityThread();
        }

        /// <summary>
        /// Updates the density pool.
        /// </summary>
        public static void update()
        {
            return;

            /*
            DensityData d;
            while ((d = densityThread.getFinishedDensity()) != null)
            {
                lock(densityPool)
                    densityPool.Enqueue(d);
            }
            */
        }

        /// <summary>
        /// Attempts to pull a recycled array of densities from the pool. Creates one if none found.
        /// </summary>
        /// <returns></returns>
        public static DensityData getDensityData()
        {
            return new DensityData();

            //See: recycleDensityData
            /*
            if (densityPool.Count == 0)
                return new DensityData();

            lock (densityPool)
                return densityPool.Dequeue();
            */
        }

        /// <summary>
        /// Recycles (threaded) a density data array.
        /// </summary>
        /// <param name="arr"></param>
        public static void recycleDensityData(DensityData arr)
        {
            //Visual bug caused by recycling densities
            //densityThread.queueRecycleDensity(arr);
            return;
        }
    }
}