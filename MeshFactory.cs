using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System;
using System.IO;

namespace Quixel
{
	/// <summary>
    /// Controls mesh generation (threaded)
    /// </summary>
    internal static class MeshFactory
    {
        #region Fields
        public static IGenerator terrainGenerator = new BasicTerrain();
        public static float isolevel = 5f;

        public static Queue<MeshRequest> setMeshQueue = new Queue<MeshRequest>();
        public static GeneratorThread[] generatorThreads = new GeneratorThread[4];
        public static FileThread fileThread;

        //[DEBUG] used to keep track of debug info
        public static int requestNum = 0;
        public static int threadNum = 0;
        public static int chunksFinished = 0;
        public static int nodesSaved = 0;
        public static int nodesLoaded = 0;

        public static GameObject chunkObj;
        public static GameObject terrainObj;
        #endregion

        public static void start()
        {
            chunkObj = new GameObject();
            chunkObj.AddComponent<MeshFilter>();
            chunkObj.AddComponent<MeshRenderer>();
            chunkObj.AddComponent<MeshCollider>();

            for (int i = 0; i < requestArray.Length; i++)
                requestArray[i] = new Queue<MeshRequest>();
            for (int i = 0; i < generatorThreads.Length; i++)
                generatorThreads[i] = new GeneratorThread();
            fileThread = new FileThread();
        }

        public static void update()
        {
            if (setMeshQueue.Count > 0)
            {
                MeshRequest req = setMeshQueue.Dequeue();
                req.node.setMesh(req.meshData);
            }

            for (int i = 0; i < generatorThreads.Length; i++)
            {
                MeshRequest req;
                while ((req = generatorThreads[i].getFinishedMesh()) != null)
                {
                    chunksFinished++;
                    if (req.node.disposed == false)
                    {
                        req.node.densityData = req.densities;
                        if (req.meshData.triangleArray.Length > 0)
                            setMeshQueue.Enqueue(req);
                        else
                            req.node.setMesh(req.meshData);
                    }
                }
            }

            #region Save/Load threads
            Node node;
            while ((node = fileThread.getFinishedLoadRequest()) != null)
            { node.regenerateChunk(); nodesLoaded++; }
            #endregion
        }

        #region Node Saving/Loading
        /// <summary>
        /// Adds a node to the save queue.
        /// </summary>
        /// <param name="node"></param>
        public static void requestSave(Node node)
        {
            fileThread.enqueueSave(node);
        }

        /// <summary>
        /// Adds a node to the save queue.
        /// </summary>
        /// <param name="node"></param>
        public static void requestLoad(Node node)
        {
            fileThread.enqueueLoad(node);
        }
        #endregion

        #region Mesh Requests
        public static Queue<MeshRequest>[] requestArray = new Queue<MeshRequest>[NodeManager.maxLOD + 2];

        /// <summary>
        /// Adds a request to generate a mesh
        /// </summary>
        /// <param name="_node">The node the mesh is for</param>
        public static void requestMesh(Node _node)
        {
            MeshRequest req = new MeshRequest
            {
                node = _node,
                pos = _node.position,
                LOD = _node.LOD,
                isDone = false,
                hasDensities = (_node.densityData != null)
            };

            if (!req.hasDensities)
                lock (requestArray[req.LOD + 1])
                    requestArray[req.LOD + 1].Enqueue(req);
            else
                lock (requestArray[0])
                    requestArray[0].Enqueue(req);
        }

        /// <summary>
        /// Returns the next logical mesh to generate.
        /// </summary>
        /// <returns></returns>
        public static MeshRequest[] getNextRequests()
        {
            int size = Math.Min(10, getRequestCount());
            MeshRequest[] ret = new MeshRequest[size];

            lock (requestArray)
            {
                for (int i = 0; i < ret.Length; i++)
                {
                    ret[i] = getNextRequest();
                }
            }

            return ret;
        }

        /// <summary>
        /// Returns the next request.
        /// </summary>
        /// <returns></returns>
        public static MeshRequest getNextRequest()
        {
            for (int o = 0; o < requestArray.Length; o++)
            {
                if (requestArray[o].Count > 0)
                    lock (requestArray[o])
                        return requestArray[o].Dequeue();
            }

            return null;
        }

        /// <summary>
        /// Returns a count of all remaining mesh requests.
        /// </summary>
        /// <returns></returns>
        public static int getRequestCount()
        {
            int ret = 0;
            for (int i = 0; i < requestArray.Length; i++)
            {
                ret += requestArray[i].Count;
            }

            return ret;
        }

        public class MeshRequest
        {
            public int LOD;
            public Vector3 pos;
            public Node node;
            public bool isDone;
            public bool hasDensities;
            public MeshData meshData;
            public DensityData densities;
        }
        #endregion

        #region Threaded Generation
        /// <summary>
        /// Generates the mesh data
        /// </summary>
        public static void GenerateMeshData(MeshRequest request)
        {
            MeshData meshData = new MeshData();
            request.meshData = meshData;

            //Check if the node requesting a generation has been disposed
            if (request == null || request.node.disposed)
            {
                meshData.triangleArray = new Vector3[0];
                meshData.indexArray = new int[0][];
                meshData.uvArray = new Vector2[0];
                meshData.normalArray = new Vector3[0];
                request.isDone = true;
                return;
            }

            DensityData densityArray = request.densities;

            Vector3[, ,] densityNormals = new Vector3[17, 17, 17];
            List<Triangle> triangleList = new List<Triangle>();
            List<int> subMeshIDList = new List<int>();
            int[] subMeshTriCount = new int[QuixelEngine.materials.Length];
            Node node = request.node;
            request.meshData = meshData;

            //Unoptimized generation
            densityNormals = new Vector3[18, 18, 18];
            if (!request.hasDensities)
            {
                for (int x = -1; x < 18; x++)
                {
                    for (int y = -1; y < 18; y++)
                    {
                        for (int z = -1; z < 18; z++)
                        {
                            VoxelData data = calculateDensity(node, new Vector3I(x, y, z));
                            densityArray.set(x, y, z, data.density);
                            densityArray.setMaterial(x, y, z, data.material);
                        }
                    }
                }
            }

            for (int x = 0; x < 17; x++)
            {
                for (int y = 0; y < 17; y++)
                {
                    for (int z = 0; z < 17; z++)
                    {
                        densityNormals[x, y, z] = calculateDensityNormal(new Vector3I(x, y, z), densityArray, node.LOD);
                    }
                }
            }
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        generateTriangles(node, new Vector3I(x, y, z), triangleList, subMeshIDList, subMeshTriCount, densityArray, densityNormals);
                    }
                }
            }
            int ppos = 0;
            int li = 0;
            try
            {
                meshData.triangleArray = new Vector3[triangleList.Count * 3];
                meshData.indexArray = new int[QuixelEngine.materials.Length][];
                for (int i = 0; i < QuixelEngine.materials.Length; i++)
                    meshData.indexArray[i] = new int[(subMeshTriCount[i] * 3) * 3];
                meshData.uvArray = new Vector2[meshData.triangleArray.Length];
                meshData.normalArray = new Vector3[meshData.triangleArray.Length];

                int count = 0;
                int[] indCount = new int[QuixelEngine.materials.Length];
                for (int i = 0; i < triangleList.Count; i++)
                {
                    ppos = i;
                    Triangle triangle = triangleList[i];
                    meshData.triangleArray[count + 2] = triangle.pointOne;
                    meshData.triangleArray[count + 1] = triangle.pointTwo;
                    meshData.triangleArray[count + 0] = triangle.pointThree;

                    meshData.normalArray[count + 2] = triangle.nOne;
                    meshData.normalArray[count + 1] = triangle.nTwo;
                    meshData.normalArray[count + 0] = triangle.nThree;

                    int ind = subMeshIDList[i];
                    li = subMeshIDList[i];
                    meshData.indexArray[ind][indCount[ind] + 0] = count + 0;
                    meshData.indexArray[ind][indCount[ind] + 1] = count + 1;
                    meshData.indexArray[ind][indCount[ind] + 2] = count + 2;

                    meshData.uvArray[count + 0] = new Vector2(meshData.triangleArray[count + 0].x, meshData.triangleArray[count + 0].z);
                    meshData.uvArray[count + 1] = new Vector2(meshData.triangleArray[count + 1].x, meshData.triangleArray[count + 1].z);
                    meshData.uvArray[count + 2] = new Vector2(meshData.triangleArray[count + 2].x, meshData.triangleArray[count + 2].z);
                    count += 3;
                    indCount[subMeshIDList[i]] += 3;
                }
            }
            catch (Exception e)
            {
                StreamWriter sw = new StreamWriter("Error Log.txt");
                sw.WriteLine(e.Message + "\r\n" + e.StackTrace);
                for (int i = 0; i < QuixelEngine.materials.Length; i++)
                    sw.WriteLine(i + ": " + subMeshTriCount[i]);
                sw.WriteLine(ppos);
                sw.WriteLine(li);
                sw.Close();
            }
            request.isDone = true;
        }

        /// <summary>
        /// Calculates a density value given a location
        /// </summary>
        /// <param name="node"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        private static VoxelData calculateDensity(Node node, Vector3I pos)
        {
            int nodeWidth = NodeManager.LODSize[node.LOD];
            Vector3 ws = new Vector3(node.position.x + (nodeWidth * pos.x),
                node.position.y + (nodeWidth * pos.y),
                node.position.z + (nodeWidth * pos.z));

            return terrainGenerator.calculateDensity(ws);
        }

        #region Marching Cubes
        /// <summary>
        /// Generates the triangles for a specific voxel
        /// </summary>
        /// <param name="node">The node that contains the voxel</param>
        /// <param name="pos">The voxel position (Not real position) [16,16,16]</param>
        /// <param name="triangleList">The list used to contain triangles made so far</param>
        /// <param name="densities">The array that contains density information</param>
        /// <param name="densityNormals">The array that contains density normals</param>
        private static void generateTriangles(Node node, Vector3I pos, List<Triangle> triangleList, List<int> submeshIDList, int[] subMeshTriCount, DensityData densities, Vector3[, ,] densityNormals)
        {
            float size = NodeManager.LODSize[node.LOD];

            float[] denses = new float[8];
            denses[0] = densities.get(pos.x, pos.y, pos.z + 1);
            denses[1] = densities.get(pos.x + 1, pos.y, pos.z + 1);
            denses[2] = densities.get(pos.x + 1, pos.y, pos.z);
            denses[3] = densities.get(pos.x, pos.y, pos.z);
            denses[4] = densities.get(pos.x, pos.y + 1, pos.z + 1);
            denses[5] = densities.get(pos.x + 1, pos.y + 1, pos.z + 1);
            denses[6] = densities.get(pos.x + 1, pos.y + 1, pos.z);
            denses[7] = densities.get(pos.x, pos.y + 1, pos.z);

            byte cubeIndex = 0;

            if (denses[0] < isolevel)
                cubeIndex |= 1;
            if (denses[1] < isolevel)
                cubeIndex |= 2;
            if (denses[2] < isolevel)
                cubeIndex |= 4;
            if (denses[3] < isolevel)
                cubeIndex |= 8;
            if (denses[4] < isolevel)
                cubeIndex |= 16;
            if (denses[5] < isolevel)
                cubeIndex |= 32;
            if (denses[6] < isolevel)
                cubeIndex |= 64;
            if (denses[7] < isolevel)
                cubeIndex |= 128;

            if (cubeIndex == 0 || cubeIndex == 255)
                return;

            Vector3 origin = new Vector3((size * (pos.x))
                , (size * (pos.y))
                , (size * (pos.z)));

            Vector3[] positions = new Vector3[8];
            positions[0] = new Vector3(origin.x, origin.y, origin.z + size);
            positions[1] = new Vector3(origin.x + size, origin.y, origin.z + size);
            positions[2] = new Vector3(origin.x + size, origin.y, origin.z);
            positions[3] = new Vector3(origin.x, origin.y, origin.z);
            positions[4] = new Vector3(origin.x, origin.y + size, origin.z + size);
            positions[5] = new Vector3(origin.x + size, origin.y + size, origin.z + size);
            positions[6] = new Vector3(origin.x + size, origin.y + size, origin.z);
            positions[7] = new Vector3(origin.x, origin.y + size, origin.z);

            Vector3[][] vertlist = new Vector3[12][];
            if (IsBitSet(edgeTable[cubeIndex], 1))
                vertlist[0] = VertexInterp(isolevel, positions[0], positions[1], denses[0], denses[1], densityNormals[pos.x, pos.y, pos.z + 1], densityNormals[pos.x + 1, pos.y, pos.z + 1]);
            if (IsBitSet(edgeTable[cubeIndex], 2))
                vertlist[1] = VertexInterp(isolevel, positions[1], positions[2], denses[1], denses[2], densityNormals[pos.x + 1, pos.y, pos.z + 1], densityNormals[pos.x + 1, pos.y, pos.z]);
            if (IsBitSet(edgeTable[cubeIndex], 4))
                vertlist[2] = VertexInterp(isolevel, positions[2], positions[3], denses[2], denses[3], densityNormals[pos.x + 1, pos.y, pos.z], densityNormals[pos.x, pos.y, pos.z]);
            if (IsBitSet(edgeTable[cubeIndex], 8))
                vertlist[3] = VertexInterp(isolevel, positions[3], positions[0], denses[3], denses[0], densityNormals[pos.x, pos.y, pos.z], densityNormals[pos.x, pos.y, pos.z + 1]);
            if (IsBitSet(edgeTable[cubeIndex], 16))
                vertlist[4] = VertexInterp(isolevel, positions[4], positions[5], denses[4], denses[5], densityNormals[pos.x, pos.y + 1, pos.z + 1], densityNormals[pos.x + 1, pos.y + 1, pos.z + 1]);
            if (IsBitSet(edgeTable[cubeIndex], 32))
                vertlist[5] = VertexInterp(isolevel, positions[5], positions[6], denses[5], denses[6], densityNormals[pos.x + 1, pos.y + 1, pos.z + 1], densityNormals[pos.x + 1, pos.y + 1, pos.z]);
            if (IsBitSet(edgeTable[cubeIndex], 64))
                vertlist[6] = VertexInterp(isolevel, positions[6], positions[7], denses[6], denses[7], densityNormals[pos.x + 1, pos.y + 1, pos.z], densityNormals[pos.x, pos.y + 1, pos.z]);
            if (IsBitSet(edgeTable[cubeIndex], 128))
                vertlist[7] = VertexInterp(isolevel, positions[7], positions[4], denses[7], denses[4], densityNormals[pos.x, pos.y + 1, pos.z], densityNormals[pos.x, pos.y + 1, pos.z + 1]);
            if (IsBitSet(edgeTable[cubeIndex], 256))
                vertlist[8] = VertexInterp(isolevel, positions[0], positions[4], denses[0], denses[4], densityNormals[pos.x, pos.y, pos.z + 1], densityNormals[pos.x, pos.y + 1, pos.z + 1]);
            if (IsBitSet(edgeTable[cubeIndex], 512))
                vertlist[9] = VertexInterp(isolevel, positions[1], positions[5], denses[1], denses[5], densityNormals[pos.x + 1, pos.y, pos.z + 1], densityNormals[pos.x + 1, pos.y + 1, pos.z + 1]);
            if (IsBitSet(edgeTable[cubeIndex], 1024))
                vertlist[10] = VertexInterp(isolevel, positions[2], positions[6], denses[2], denses[6], densityNormals[pos.x + 1, pos.y, pos.z], densityNormals[pos.x + 1, pos.y + 1, pos.z]);
            if (IsBitSet(edgeTable[cubeIndex], 2048))
                vertlist[11] = VertexInterp(isolevel, positions[3], positions[7], denses[3], denses[7], densityNormals[pos.x, pos.y, pos.z], densityNormals[pos.x, pos.y + 1, pos.z]);

            int submesh = densities.getMaterial(pos.x, pos.y, pos.z);
            for (int i = 0; triTable[cubeIndex][i] != -1; i += 3)
            {
                submeshIDList.Add(submesh);
                subMeshTriCount[submesh] = subMeshTriCount[submesh] + 1;
                triangleList.Add(new Triangle(vertlist[triTable[cubeIndex][i]][0], vertlist[triTable[cubeIndex][i + 1]][0], vertlist[triTable[cubeIndex][i + 2]][0],
                    vertlist[triTable[cubeIndex][i]][1], vertlist[triTable[cubeIndex][i + 1]][1], vertlist[triTable[cubeIndex][i + 2]][1]));
            }
        }

        /// <summary>
        /// Interpolates the Vertex
        /// </summary>
        /// <param name="isolevel">The isolevel</param>
        /// <param name="p1">Position One</param>
        /// <param name="p2">Position Two</param>
        /// <param name="valp1">Value Position One</param>
        /// <param name="valp2">Value Position Two</param>
        /// <returns></returns>
        private static Vector3[] VertexInterp(float isolevel, Vector3 p1, Vector3 p2, float valp1, float valp2, Vector3 n1, Vector3 n2)
        {
            float mu;
            Vector3[] p = new Vector3[2];

            if (Mathf.Abs(isolevel - valp1) < 0.00001)
            {
                p[0] = p1;
                return p;
            }
            if (Mathf.Abs(isolevel - valp2) < 0.00001)
            {
                p[0] = p2;
                return p;
            }
            if (Mathf.Abs(valp1 - valp2) < 0.00001)
            {
                p[0] = p1;
                return p;
            }

            mu = (isolevel - valp1) / (valp2 - valp1);
            p[0].x = p1.x + mu * (p2.x - p1.x);
            p[0].y = p1.y + mu * (p2.y - p1.y);
            p[0].z = p1.z + mu * (p2.z - p1.z);

            float dist1 = Vector3.Distance(p1, p2);
            float dist2 = Vector3.Distance(p2, p[0]);
            p[1] = Vector3.Lerp(n2, n1, dist2 / dist1);
            p[1].Normalize();

            return (p);
        }

        /// <summary>
        /// Find out if a certain bit is set
        /// </summary>
        /// <param name="b">The bit to be checked</param>
        /// <param name="pos">The position of the bit to check</param>
        /// <returns></returns>
        static bool IsBitSet(int b, int pos)
        {
            return ((b & pos) == pos);
        }

        /// <summary>
        /// Calculates the normal.
        /// </summary>
        /// <param name="p"></param>
        private static Vector3 calculateDensityNormal(Vector3I p, DensityData densities, int lod)
        {
            Vector3 normal = new Vector3();
            normal.x = (densities.get(p.x + 1, p.y, p.z) - densities.get(p.x - 1, p.y, p.z)) / (NodeManager.LODSize[lod]);
            normal.y = (densities.get(p.x, p.y + 1, p.z) - densities.get(p.x, p.y - 1, p.z)) / (NodeManager.LODSize[lod]);
            normal.z = (densities.get(p.x, p.y, p.z + 1) - densities.get(p.x, p.y, p.z - 1)) / (NodeManager.LODSize[lod]);
            normal.Normalize();
            return normal;
        }
        #endregion

        #region Lookup Table A
        static int[] edgeTable = new int[256] {
			0x0  , 0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c,
			0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09, 0xf00,
			0x190, 0x99 , 0x393, 0x29a, 0x596, 0x49f, 0x795, 0x69c,
			0x99c, 0x895, 0xb9f, 0xa96, 0xd9a, 0xc93, 0xf99, 0xe90,
			0x230, 0x339, 0x33 , 0x13a, 0x636, 0x73f, 0x435, 0x53c,
			0xa3c, 0xb35, 0x83f, 0x936, 0xe3a, 0xf33, 0xc39, 0xd30,
			0x3a0, 0x2a9, 0x1a3, 0xaa , 0x7a6, 0x6af, 0x5a5, 0x4ac,
			0xbac, 0xaa5, 0x9af, 0x8a6, 0xfaa, 0xea3, 0xda9, 0xca0,
			0x460, 0x569, 0x663, 0x76a, 0x66 , 0x16f, 0x265, 0x36c,
			0xc6c, 0xd65, 0xe6f, 0xf66, 0x86a, 0x963, 0xa69, 0xb60,
			0x5f0, 0x4f9, 0x7f3, 0x6fa, 0x1f6, 0xff , 0x3f5, 0x2fc,
			0xdfc, 0xcf5, 0xfff, 0xef6, 0x9fa, 0x8f3, 0xbf9, 0xaf0,
			0x650, 0x759, 0x453, 0x55a, 0x256, 0x35f, 0x55 , 0x15c,
			0xe5c, 0xf55, 0xc5f, 0xd56, 0xa5a, 0xb53, 0x859, 0x950,
			0x7c0, 0x6c9, 0x5c3, 0x4ca, 0x3c6, 0x2cf, 0x1c5, 0xcc ,
			0xfcc, 0xec5, 0xdcf, 0xcc6, 0xbca, 0xac3, 0x9c9, 0x8c0,
			0x8c0, 0x9c9, 0xac3, 0xbca, 0xcc6, 0xdcf, 0xec5, 0xfcc,
			0xcc , 0x1c5, 0x2cf, 0x3c6, 0x4ca, 0x5c3, 0x6c9, 0x7c0,
			0x950, 0x859, 0xb53, 0xa5a, 0xd56, 0xc5f, 0xf55, 0xe5c,
			0x15c, 0x55 , 0x35f, 0x256, 0x55a, 0x453, 0x759, 0x650,
			0xaf0, 0xbf9, 0x8f3, 0x9fa, 0xef6, 0xfff, 0xcf5, 0xdfc,
			0x2fc, 0x3f5, 0xff , 0x1f6, 0x6fa, 0x7f3, 0x4f9, 0x5f0,
			0xb60, 0xa69, 0x963, 0x86a, 0xf66, 0xe6f, 0xd65, 0xc6c,
			0x36c, 0x265, 0x16f, 0x66 , 0x76a, 0x663, 0x569, 0x460,
			0xca0, 0xda9, 0xea3, 0xfaa, 0x8a6, 0x9af, 0xaa5, 0xbac,
			0x4ac, 0x5a5, 0x6af, 0x7a6, 0xaa , 0x1a3, 0x2a9, 0x3a0,
			0xd30, 0xc39, 0xf33, 0xe3a, 0x936, 0x83f, 0xb35, 0xa3c,
			0x53c, 0x435, 0x73f, 0x636, 0x13a, 0x33 , 0x339, 0x230,
			0xe90, 0xf99, 0xc93, 0xd9a, 0xa96, 0xb9f, 0x895, 0x99c,
			0x69c, 0x795, 0x49f, 0x596, 0x29a, 0x393, 0x99 , 0x190,
			0xf00, 0xe09, 0xd03, 0xc0a, 0xb06, 0xa0f, 0x905, 0x80c,
			0x70c, 0x605, 0x50f, 0x406, 0x30a, 0x203, 0x109, 0x0   };
        #endregion

        #region Lookup Table B

        static int[][] triTable = new int[256][] {
			new int[] {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
			new int[] {2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1},
			new int[] {8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1},
			new int[] {3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1},
			new int[] {4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1},
			new int[] {4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
			new int[] {5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1},
			new int[] {2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1},
			new int[] {9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
			new int[] {2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1},
			new int[] {10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1},
			new int[] {5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1},
			new int[] {5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1},
			new int[] {10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1},
			new int[] {8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1},
			new int[] {2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1},
			new int[] {7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1},
			new int[] {2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1},
			new int[] {11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1},
			new int[] {5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1},
			new int[] {11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1},
			new int[] {11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1},
			new int[] {5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1},
			new int[] {2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
			new int[] {5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1},
			new int[] {6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1},
			new int[] {3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1},
			new int[] {6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1},
			new int[] {5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
			new int[] {10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1},
			new int[] {6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1},
			new int[] {8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1},
			new int[] {7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1},
			new int[] {3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
			new int[] {5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1},
			new int[] {0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1},
			new int[] {9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1},
			new int[] {8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1},
			new int[] {5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1},
			new int[] {0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1},
			new int[] {6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1},
			new int[] {10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1},
			new int[] {10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1},
			new int[] {8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1},
			new int[] {1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1},
			new int[] {0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1},
			new int[] {10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1},
			new int[] {3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1},
			new int[] {6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1},
			new int[] {9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1},
			new int[] {8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1},
			new int[] {3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1},
			new int[] {6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1},
			new int[] {10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1},
			new int[] {10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1},
			new int[] {2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1},
			new int[] {7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1},
			new int[] {7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1},
			new int[] {2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1},
			new int[] {1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1},
			new int[] {11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1},
			new int[] {8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1},
			new int[] {0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1},
			new int[] {7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
			new int[] {10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
			new int[] {2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
			new int[] {6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1},
			new int[] {7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1},
			new int[] {2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1},
			new int[] {10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1},
			new int[] {10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1},
			new int[] {0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1},
			new int[] {7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1},
			new int[] {6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1},
			new int[] {8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1},
			new int[] {6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1},
			new int[] {4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1},
			new int[] {10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1},
			new int[] {8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1},
			new int[] {1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1},
			new int[] {8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1},
			new int[] {10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1},
			new int[] {10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
			new int[] {5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
			new int[] {11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1},
			new int[] {9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
			new int[] {6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1},
			new int[] {7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1},
			new int[] {3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1},
			new int[] {7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1},
			new int[] {3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1},
			new int[] {6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1},
			new int[] {9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1},
			new int[] {1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1},
			new int[] {4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1},
			new int[] {7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1},
			new int[] {6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1},
			new int[] {0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1},
			new int[] {6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1},
			new int[] {0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1},
			new int[] {11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1},
			new int[] {6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1},
			new int[] {5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1},
			new int[] {9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1},
			new int[] {1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1},
			new int[] {10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1},
			new int[] {0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1},
			new int[] {5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1},
			new int[] {10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1},
			new int[] {11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1},
			new int[] {9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1},
			new int[] {7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1},
			new int[] {2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1},
			new int[] {8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1},
			new int[] {9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1},
			new int[] {9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1},
			new int[] {1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1},
			new int[] {5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1},
			new int[] {0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1},
			new int[] {10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1},
			new int[] {2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1},
			new int[] {0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1},
			new int[] {0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1},
			new int[] {9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1},
			new int[] {5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1},
			new int[] {5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1},
			new int[] {8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1},
			new int[] {9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1},
			new int[] {1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1},
			new int[] {3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1},
			new int[] {4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1},
			new int[] {9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1},
			new int[] {11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1},
			new int[] {11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1},
			new int[] {2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1},
			new int[] {9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1},
			new int[] {3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1},
			new int[] {1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1},
			new int[] {4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1},
			new int[] {0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1},
			new int[] {9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1},
			new int[] {1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
			new int[] {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}};
        #endregion
        #endregion

        /// <summary>
        /// Used to draw information about mesh generation.
        /// </summary>
        public static void debugDraw()
        {
            GUILayout.Label("Requests: " + getRequestCount());
            //GUILayout.Label("Threads: " + threadList.Count);
            //GUILayout.Label("Active: " + activeRequests.Count);
            GUILayout.Label("Finished: " + chunksFinished);
            GUILayout.Label("Free Game Objects: " + ChunkPool.chunkList.Count);
            GUILayout.Label("Total Game Objects: " + ChunkPool.totalCreated);
            //GUILayout.Label("Saves Queued: " + saveQueue.Count);
            GUILayout.Label("Nodes Saved: " + nodesSaved);
            //GUILayout.Label("Loads Queued: " + loadQueue.Count);
            GUILayout.Label("Nodes Loaded: " + nodesLoaded);
        }
    }
}