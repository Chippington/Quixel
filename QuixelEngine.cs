using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System;
using System.IO;

namespace Quixel
{
    public static class QuixelEngine
    {
        public static Material[] materials;
        private static GameObject cameraObj;
        private static bool active = true;

        /// <summary>
        /// Initializes the Quixel Engine
        /// </summary>
        /// <param name="mats">Array of materials.</param>
        /// <param name="terrainObj">Parent terrain object. (empty)</param>
        /// <param name="worldName">Name of the world. Used for paging. (empty)</param>
        public static void init(Material[] mats, GameObject terrainObj, string worldName)
        {
            MeshFactory.terrainObj = terrainObj;

            materials = mats;
            Debug.Log("Materials: " + mats.Length);
            DensityPool.init();
            MeshFactory.start();
            NodeManager.init(worldName);
        }

        /// <summary>
        /// Sets the width/length/height of the smallest LOD voxel.
        /// The width of a single voxel will be 2^(size + LOD)
        /// </summary>
        /// <param name="size">The size (units).</param>
        public static void setVoxelSize(int size, int maxLOD)
        {
            NodeManager.maxLOD = maxLOD;
            NodeManager.nodeCount = new int[maxLOD+1];
            NodeManager.LODSize = new int[maxLOD+1];
            for (int i = 0; i <= maxLOD; i++)
            {
                NodeManager.LODSize[i] = (int)Mathf.Pow(2, i + size);
            }
        }
		
		/// <summary>
        /// Sets the terrain generator to use when generating terrain.
        /// </summary>
		public static void setTerrainGenerator(IGenerator gen)
		{
			MeshFactory.terrainGenerator = gen;
		}
		
        /// <summary>
        /// Updates the Quixel system. Should be called every step.
        /// </summary>
        public static void update()
        {
            DensityPool.update();
            MeshFactory.update();

            if (cameraObj != null)
                NodeManager.setViewPosition(cameraObj.transform.position);

            if (!Application.isPlaying)
                active = false;
        }

        /// <summary>
        /// Sets the object to follow for the LOD system.
        /// </summary>
        /// <param name="obj"></param>
        public static void setCameraObj(GameObject obj)
        {
            cameraObj = obj;
        }

        /// <summary>
        /// Returns true if the player is still active.
        /// </summary>
        /// <returns></returns>
        public static bool isActive()
        {
            return active;
        }

        /// <summary>
        /// Terminates the engine.
        /// </summary>
        /// <returns></returns>
        public static void terminate()
        {
            active = false;
        }
    }
}
