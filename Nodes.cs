using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System;
using System.IO;

namespace Quixel
{
    /// <summary>
    /// Controls the 27 top-level nodes. Handles node searching.
    /// </summary>
    internal static class NodeManager
    {
        #region Fields
        public static string worldName;
        public static Vector3I curBottomNode = new Vector3I(-111, 0, 0);
        public static Vector3I curTopNode = new Vector3I(0, 0, 0);

        public static Vector3I[] viewChunkPos;

        //public static Node[,,] topNodes = new Node[3,3,3];
        public static Node[, ,] topNodes = new Node[3, 3, 3];

        /// <summary> Mask used for positioning subnodes</summary>
        public static Vector3[] mask = new Vector3[8] {
        new Vector3(0, 0, 0),
        new Vector3(1, 0, 0),
        new Vector3(0, 1, 0),
        new Vector3(0, 0, 1),
        new Vector3(1, 1, 0),
        new Vector3(1, 0, 1),
        new Vector3(0, 1, 1),
        new Vector3(1, 1, 1)
    };

        /// <summary>Refers to the tri size of each vertex</summary>
        public static int[] LODSize = new int[11] {
        1,
        2, 
        4, 
        8, 16, 32, 64, 128, 256, 512, 1024
    };

        //DEPRECATED
        /// <summary>Radius for each LOD value</summary>
        public static int[] LODRange = new int[11] {
        0, 
        7, 
        12, 
        26, 50, 100, 210, 400, 700, 1200, 2400
    };

        public static int[] nodeCount = new int[11];

        /// <summary>Density array size</summary>
        public static int nodeSize = 16;

        /// <summary>The maximum allowed LOD. The top level nodes will be this LOD.</summary>
        public static int maxLOD = 10;
        #endregion

        /// <summary>
        /// Initializes the node manager.
        /// </summary>
        public static void init(string worldName)
        {
            NodeManager.worldName = worldName;
            float nSize = LODSize[maxLOD] * nodeSize;
            for (int x = -1; x < 2; x++)
            {
                for (int y = -1; y < 2; y++)
                {
                    for (int z = -1; z < 2; z++)
                    {
                        topNodes[x + 1, y + 1, z + 1] = new Node(null,
                            new Vector3(x * nSize,
                                y * nSize,
                                z * nSize),
                            0, maxLOD, Node.RenderType.FRONT);
                    }
                }
            }

            viewChunkPos = new Vector3I[maxLOD + 1];
            for (int i = 0; i <= maxLOD; i++)
                viewChunkPos[i] = new Vector3I();
        }

        /// <summary>
        /// Sets the view position, and checks if chunks need to be updated
        /// </summary>
        /// <param name="pos"></param>
        public static void setViewPosition(Vector3 pos)
        {
            for (int i = 0; i <= maxLOD; i++)
            {
                float nWidth = LODSize[i] * nodeSize;
                viewChunkPos[i].x = (int)(pos.x / nWidth);
                viewChunkPos[i].y = (int)(pos.y / nWidth);
                viewChunkPos[i].z = (int)(pos.z / nWidth);
            }

            float sWidth = LODSize[0] * nodeSize * 0.5f;
            Vector3I newPos = new Vector3I((int)(pos.x / sWidth), (int)(pos.y / sWidth), (int)(pos.z / sWidth));

            if (!curTopNode.Equals(getTopNode(pos)))
            {
                float nodeWidth = LODSize[maxLOD] * nodeSize;
                Vector3I diff = getTopNode(pos).Subtract(curTopNode);
                curTopNode = getTopNode(pos);
                while (diff.x > 0)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        for (int z = 0; z < 3; z++)
                        {
                            topNodes[0, y, z].dispose();
                            topNodes[0, y, z] = topNodes[1, y, z];
                            topNodes[1, y, z] = topNodes[2, y, z];
                            topNodes[2, y, z] = new Node(null,
                                                new Vector3((curTopNode.x * nodeWidth) + nodeWidth,
                                                    (curTopNode.y * nodeWidth) + ((y - 1) * nodeWidth),
                                                    (curTopNode.z * nodeWidth) + ((z - 1) * nodeWidth)),
                                                0, maxLOD, Node.RenderType.FRONT);
                        }
                    }
                    diff.x--;
                }

                while (diff.x < 0)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        for (int z = 0; z < 3; z++)
                        {
                            topNodes[2, y, z].dispose();
                            topNodes[2, y, z] = topNodes[1, y, z];
                            topNodes[1, y, z] = topNodes[0, y, z];
                            topNodes[0, y, z] = new Node(null,
                                                new Vector3((curTopNode.x * nodeWidth) - nodeWidth,
                                                    (curTopNode.y * nodeWidth) + ((y - 1) * nodeWidth),
                                                    (curTopNode.z * nodeWidth) + ((z - 1) * nodeWidth)),
                                                0, maxLOD, Node.RenderType.FRONT);
                        }
                    }
                    diff.x++;
                }

                while (diff.y > 0)
                {
                    for (int x = 0; x < 3; x++)
                    {
                        for (int z = 0; z < 3; z++)
                        {
                            topNodes[x, 0, z].dispose();
                            topNodes[x, 0, z] = topNodes[x, 1, z];
                            topNodes[x, 1, z] = topNodes[x, 2, z];
                            topNodes[x, 2, z] = new Node(null,
                                                new Vector3((curTopNode.x * nodeWidth) + ((x - 1) * nodeWidth),
                                                    (curTopNode.y * nodeWidth) + nodeWidth,
                                                    (curTopNode.z * nodeWidth) + ((z - 1) * nodeWidth)),
                                                0, maxLOD, Node.RenderType.FRONT);
                        }
                    }
                    diff.y--;
                }

                while (diff.y < 0)
                {
                    for (int x = 0; x < 3; x++)
                    {
                        for (int z = 0; z < 3; z++)
                        {
                            topNodes[x, 2, z].dispose();
                            topNodes[x, 2, z] = topNodes[x, 1, z];
                            topNodes[x, 1, z] = topNodes[x, 0, z];
                            topNodes[x, 0, z] = new Node(null,
                                                new Vector3((curTopNode.x * nodeWidth) + ((x - 1) * nodeWidth),
                                                    (curTopNode.y * nodeWidth) - nodeWidth,
                                                    (curTopNode.z * nodeWidth) + ((z - 1) * nodeWidth)),
                                                0, maxLOD, Node.RenderType.FRONT);
                        }
                    }

                    diff.y++;
                }

                while (diff.z > 0)
                {
                    for (int x = 0; x < 3; x++)
                    {
                        for (int y = 0; y < 3; y++)
                        {
                            topNodes[x, y, 0].dispose();
                            topNodes[x, y, 0] = topNodes[x, y, 1];
                            topNodes[x, y, 1] = topNodes[x, y, 2];
                            topNodes[x, y, 2] = new Node(null,
                                                new Vector3((curTopNode.x * nodeWidth) + ((x - 1) * nodeWidth),
                                                    (curTopNode.y * nodeWidth) + ((y - 1) * nodeWidth),
                                                    (curTopNode.z * nodeWidth) + nodeWidth),
                                                0, maxLOD, Node.RenderType.FRONT);
                        }
                    }

                    diff.z--;
                }

                while (diff.z < 0)
                {
                    for (int x = 0; x < 3; x++)
                    {
                        for (int y = 0; y < 3; y++)
                        {
                            topNodes[x, y, 2].dispose();
                            topNodes[x, y, 2] = topNodes[x, y, 1];
                            topNodes[x, y, 1] = topNodes[x, y, 0];
                            topNodes[x, y, 0] = new Node(null,
                                                new Vector3((curTopNode.x * nodeWidth) + ((x - 1) * nodeWidth),
                                                    (curTopNode.y * nodeWidth) + ((y - 1) * nodeWidth),
                                                    (curTopNode.z * nodeWidth) - nodeWidth),
                                                0, maxLOD, Node.RenderType.FRONT);
                        }
                    }
                    diff.z++;
                }
            }

            if (curBottomNode.x != newPos.x || curBottomNode.y != newPos.y || curBottomNode.z != newPos.z)
            {
                Vector3 setPos = new Vector3(newPos.x * sWidth + (sWidth / 1f), newPos.y * sWidth + (sWidth / 1f), newPos.z * sWidth + (sWidth / 1f));
                for (int x = 0; x < 3; x++)
                    for (int y = 0; y < 3; y++)
                        for (int z = 0; z < 3; z++)
                        {
                            topNodes[x, y, z].viewPosChanged(setPos);
                        }

                curBottomNode = newPos;
            }
        }

        /// <summary>
        /// Returns a node containing the point as close as possible to the requested LOD.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="searchLOD"></param>
        /// <returns>Null if no such node exists.</returns>
        public static Node[] searchNodeContainingDensity(Vector3 pos, int searchLOD)
        {
            Node[] ret = new Node[8];
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {
                        if (topNodes[x, y, z] != null)
                            topNodes[x, y, z].searchNodeCreate(pos, searchLOD, ref ret);
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Returns a node containing the point as close as possible to the requested LOD.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="searchLOD"></param>
        /// <returns>Null if no such node exists.</returns>
        public static Node searchNode(Vector3 pos, int searchLOD)
        {
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {
                        if (topNodes[x, y, z] != null)
                            if (topNodes[x, y, z].containsPoint(pos))
                                return topNodes[x, y, z].searchNode(pos, searchLOD);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Calculates the offset position given a parent's node and the subnode ID.
        /// </summary>
        /// <param name="parentNode">Parent that contains t</param>
        /// <param name="subNodeID">Index of the node in the subnode array</param>
        /// <returns></returns>
        public static Vector3 getOffsetPosition(Node parentNode, int subNodeID)
        {
            //Vector3 ret = new Vector3(parentNode.position.x, parentNode.position.y, parentNode.position.z);
            int parentWidth = nodeSize * LODSize[parentNode.LOD];
            return new Vector3
            {
                x = parentNode.position.x + ((parentWidth / 2) * mask[subNodeID].x),
                y = parentNode.position.y + ((parentWidth / 2) * mask[subNodeID].y),
                z = parentNode.position.z + ((parentWidth / 2) * mask[subNodeID].z)
            };
        }

        /// <summary>
        /// Returns a 3d integer vector position of the "top" (highest LOD) node that contains the given position.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public static Vector3I getTopNode(Vector3 pos)
        {
            Vector3I ret = new Vector3I();
            ret.x = (int)Mathf.Floor(pos.x / (NodeManager.LODSize[maxLOD] * nodeSize));
            ret.y = (int)Mathf.Floor(pos.y / (NodeManager.LODSize[maxLOD] * nodeSize));
            ret.z = (int)Mathf.Floor(pos.z / (NodeManager.LODSize[maxLOD] * nodeSize));

            return ret;
        }

        /// <summary>
        /// Used to draw the chunk wirecubes in scene view
        /// </summary>
        /// <returns></returns>
        public static int debugDraw()
        {
            int ct = 0;
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {
                        if (topNodes[x, y, z] != null)
                            ct += topNodes[x, y, z].debugDraw();
                    }
                }
            }
            return ct;
        }
    }

    /// <summary>
    /// Octree node (chunk)
    /// </summary>
    internal class Node
    {
        #region Fields
        /// <summary> When we are already generating a new mesh and need a new regeneration. </summary>
        public bool regenFlag = false;
        private bool regenReq = false;
        public bool permanent = false;
        public bool hasDensityChangeData = false;

        /// <summary> Density array that contains the mesh data. </summary>
        public DensityData densityData;
        public DensityData densityChangeData;

        /// <summary>Index of this node in the parent node's subnode array</summary>
        public int subNodeID;

        /// <summary>Level of Detail value</summary>
        public int LOD;

        /// <summary> The integer position of the chunk (Not real) </summary>
        public Vector3I chunkPos;

        /// <summary>Real position</summary>
        public Vector3 position;

        /// <summary>Subnodes under this parent node</summary>
        public Node[] subNodes = new Node[8];

        /// <summary>
        /// Neighbor nodes of the same LOD
        /// Null means the neighbor node either doesn't exist or hasn't been allocated yet.
        /// </summary>
        public Node[] neighborNodes = new Node[6];

        /// <summary>The parent that owns this node. Null if top-level</summary>
        public Node parent;

        /// <summary>Gameobject that contains the mesh</summary>
        private GameObject chunk;

        /// <summary> Center of the chunk in real pos </summary>
        public Vector3 center;

        public bool disposed = false;
        public bool hasMesh = false;
        public bool collides = false;
        public bool empty = true;

        public RenderType renderType;
        public enum RenderType
        {
            FRONT, BACK
        }

        /// <summary> Mask used for positioning subnodes</summary>
        public static Vector3[] neighborMask = new Vector3[6] {
        new Vector3(-1, 0, 0),
        new Vector3(0, -1, 0),
        new Vector3(0, 0, -1),
        new Vector3(1, 0, 0),
        new Vector3(0, 1, 0),
        new Vector3(0, 0, 1),
    };

        public static int[] oppositeNeighbor = new int[6] {
        3, 4, 5, 0, 1, 2
    };
        #endregion

        public Node(Node parent, Vector3 position, int subNodeID, int LOD, RenderType renderType)
        {
            densityChangeData = new DensityData();
            this.parent = parent;
            this.position = position;
            this.subNodeID = subNodeID;
            this.LOD = LOD;

            float chunkWidth = (NodeManager.LODSize[LOD] * NodeManager.nodeSize) / 2f;
            center = new Vector3(position.x + chunkWidth,
                position.y + chunkWidth,
                position.z + chunkWidth);

            setRenderType(renderType);

            if (parent != null && parent.permanent)
                permanent = true;

            NodeManager.nodeCount[LOD]++;

            float nWidth = NodeManager.LODSize[LOD] * NodeManager.nodeSize;
            chunkPos.x = (int)(center.x / nWidth);
            chunkPos.y = (int)(center.y / nWidth);
            chunkPos.z = (int)(center.z / nWidth);

            if (LOD == 0)
            {
                string dir = getDirectory();
                if (Directory.Exists(dir) && File.Exists(dir + "\\densities.txt"))
                    MeshFactory.requestLoad(this);
            }

            regenReq = true;
            MeshFactory.requestMesh(this);
        }

        /// <summary>
        /// Called when the viewpoint has changed.
        /// </summary>
        /// <param name="pos"></param>
        public void viewPosChanged(Vector3 pos)
        {
            //float sep = 10f;

            //float distance = ((center.x - pos.x) * (center.x - pos.x) +
            //(center.y - pos.y) * (center.y - pos.y) +
            //(center.z - pos.z) * (center.z - pos.z));

            //if (distance < (((float)NodeManager.LODRange[LOD]) * sep) * (((float)NodeManager.LODRange[LOD]) * sep))
            Vector3I viewPos = NodeManager.viewChunkPos[LOD];
            int size = 1;
            if ((viewPos.x >= chunkPos.x - size && viewPos.x <= chunkPos.x + size)
                && (viewPos.y >= chunkPos.y - size && viewPos.y <= chunkPos.y + size)
                && (viewPos.z >= chunkPos.z - size && viewPos.z <= chunkPos.z + size))
            {
                if (isBottomLevel())
                    createSubNodes(RenderType.FRONT);

                for (int i = 0; i < 8; i++)
                {
                    if (subNodes[i] != null)
                        subNodes[i].viewPosChanged(pos);
                }
            }
            //else if (!permanent)
            else
            {
                size += 2;
                if (LOD < 3 && (viewPos.x < chunkPos.x - size || viewPos.x > chunkPos.x + size)
                    || (viewPos.y < chunkPos.y - size || viewPos.y > chunkPos.y + size)
                    || (viewPos.z < chunkPos.z - size || viewPos.z > chunkPos.z + size))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (subNodes[i] != null)
                        {
                            subNodes[i].dispose();
                            subNodes[i] = null;
                        }
                    }
                }
                else if (LOD >= 3)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (subNodes[i] != null)
                        {
                            subNodes[i].dispose();
                            subNodes[i] = null;
                        }
                    }
                }
            }

            if (LOD == 0)
            {
                float nodeSize = (float)NodeManager.LODSize[0] * (float)NodeManager.nodeSize;
                Vector3I viewChunk = new Vector3I((int)(pos.x / nodeSize),
                    (int)(pos.y / nodeSize),
                    (int)(pos.z / nodeSize));

                Vector3I curChunk = new Vector3I((int)(position.x / nodeSize),
                    (int)(position.y / nodeSize),
                    (int)(position.z / nodeSize));

                if (curChunk.x >= viewChunk.x - 3 && curChunk.x <= viewChunk.x + 3 &&
                    curChunk.y >= viewChunk.y - 3 && curChunk.y <= viewChunk.y + 3 &&
                    curChunk.z >= viewChunk.z - 3 && curChunk.z <= viewChunk.z + 3)
                {
                    collides = true;
                    if (chunk != null)
                    {
                        chunk.GetComponent<MeshCollider>().sharedMesh = chunk.GetComponent<MeshFilter>().sharedMesh;
                    }
                }
            }

            renderCheck();
        }

        /// <summary>
        /// Searches for a node containing the given point and LOD, creating it if none is found.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public void searchNodeCreate(Vector3 pos, int searchLOD, ref Node[] list)
        {
            if (containsDensityPoint(pos))
            {
                if (LOD == searchLOD)
                {
                    for (int i = 0; i < list.Length; i++)
                        if (list[i] == null)
                        {
                            list[i] = this;
                            return;
                        }
                    Debug.Log("A");
                }
                else
                {
                    if (isBottomLevel())
                        createSubNodes(RenderType.FRONT);

                    for (int i = 0; i < 8; i++)
                    {
                        subNodes[i].searchNodeCreate(pos, searchLOD, ref list);
                    }
                }
            }
        }

        /// <summary>
        /// Searches for a node containing the given point and LOD.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public Node searchNode(Vector3 pos, int searchLOD)
        {
            if (containsDensityPoint(pos))
            {
                if (searchLOD == LOD)
                    return this;

                if (!isBottomLevel())
                    for (int i = 0; i < 8; i++)
                        if (subNodes[i] != null)
                        {
                            if (subNodes[i].containsPoint(pos))
                                return subNodes[i].searchNode(pos, searchLOD);
                        }

                return this;
            }

            if (parent != null)
                return parent.searchNode(pos, searchLOD);

            return NodeManager.searchNode(pos, searchLOD);
        }

        /// <summary>
        /// Returns whether or not the point is within the chunk.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool containsPoint(Vector3 pos)
        {
            float chunkWidth = NodeManager.LODSize[LOD] * NodeManager.nodeSize;
            if ((pos.x >= position.x && pos.y >= position.y && pos.z >= position.z)
                && (pos.x <= position.x + chunkWidth && pos.y <= position.y + chunkWidth && pos.z <= position.z + chunkWidth))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns whether or not the point is within the chunk.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool containsDensityPoint(Vector3 pos)
        {
            float chunkWidth = NodeManager.LODSize[LOD] * NodeManager.nodeSize;
			Vector3 corner1 = new Vector3(position.x - NodeManager.LODSize[LOD],
										position.y - NodeManager.LODSize[LOD],
										position.z - NodeManager.LODSize[LOD]);
										
			Vector3 corner2 = new Vector3(position.x + chunkWidth + NodeManager.LODSize[LOD],
										position.y + chunkWidth + NodeManager.LODSize[LOD],
										position.z + chunkWidth + NodeManager.LODSize[LOD]);
										
			if((pos.x >= corner1.x && pos.y >= corner1.y && pos.z >= corner1.z) &&
				(pos.x <= corner2.x && pos.y <= corner2.y && pos.z <= corner2.z)) {
				return true;
			}
			
            return false;
        }

        /// <summary>
        /// Checks whether this chunk should render and where.
        /// </summary>
        public void renderCheck()
        {
            if (disposed)
            {
                if (chunk != null)
                    chunk.GetComponent<MeshFilter>().renderer.enabled = false;
                return;
            }

            if (chunk != null)
                if (isBottomLevel())
                {
                    chunk.GetComponent<MeshFilter>().renderer.enabled = true;
                    setRenderType(RenderType.FRONT);
                }
                else
                {
                    bool render = false;
                    for (int i = 0; i < 8; i++)
                        if (subNodes[i] != null && !subNodes[i].hasMesh)
                        {
                            render = true;
                            setRenderType(RenderType.FRONT);
                        }
                    chunk.GetComponent<MeshFilter>().renderer.enabled = render;
                }
        }

        /// <summary>
        /// Called when a mesh has been generated
        /// </summary>
        /// <param name="mesh">Mesh data</param>
        public void setMesh(MeshData meshData)
        {
            densityData.setChangeData(densityChangeData);

            regenReq = false;
            if (regenFlag)
            {
                regenFlag = false;
                regenReq = true;
                MeshFactory.requestMesh(this);
            }

            hasMesh = true;
            if (meshData.indexArray.Length == 0)
                return;

            if (chunk == null)
            {
                chunk = ChunkPool.getChunk();
                if (LOD > 2)
                    chunk.transform.position = position - new Vector3(0f, (NodeManager.LODSize[LOD] / 2f), 0f);
                else
                    chunk.transform.position = position;

                //chunk.GetComponent<MeshFilter>().mesh.subMeshCount = QuixelEngine.materials.Length;
                chunk.GetComponent<MeshRenderer>().materials = QuixelEngine.materials;
            }

            empty = false;
            Mesh mesh = new Mesh();
            mesh.subMeshCount = QuixelEngine.materials.Length;
            mesh.vertices = meshData.triangleArray;

            for (int i = 0; i < QuixelEngine.materials.Length; i++)
            {
                if (meshData.indexArray[i].Length > 0)
                    mesh.SetTriangles(meshData.indexArray[i], i);
            }
            //mesh.triangles = meshData.indexArray;
            mesh.uv = meshData.uvArray;

            mesh.normals = meshData.normalArray;
            //mesh.RecalculateBounds();
            mesh.Optimize();

            chunk.GetComponent<MeshFilter>().mesh = mesh;

            if (LOD == 0 && collides)
                chunk.GetComponent<MeshCollider>().sharedMesh = mesh;
            meshData.dispose();

            renderCheck();
            switch (renderType)
            {
                case RenderType.BACK:
                    if (chunk != null)
                        chunk.layer = 9;
                    break;

                case RenderType.FRONT:
                    if (chunk != null)
                        chunk.layer = 8;
                    break;
            }
            if (parent != null)
                parent.renderCheck();
        }

        /// <summary>
        /// Sets the render type of the chunk.
        /// Front will be rendered last, on top of Back.
        /// </summary>
        /// <param name="r"></param>
        public void setRenderType(RenderType r)
        {
            switch (r)
            {
                case RenderType.BACK:
                    if (chunk != null)
                        chunk.layer = 9;
                    break;

                case RenderType.FRONT:
                    if (chunk != null)
                        chunk.layer = 8;
                    break;
            }

            renderType = r;
        }

        /// <summary>
        /// Returns whether or not this is the bottom level of the tree.
        /// </summary>
        /// <returns></returns>
        public bool isBottomLevel()
        {
            for (int i = 0; i < 8; i++)
            {
                if (subNodes[i] == null)
                    return true;
                else if (subNodes[i].disposed)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if this is a transitional node.
        /// True if an adjacent node of the same LOD has subnodes.
        /// </summary>
        /// <returns></returns>
        public bool isTransitional()
        {
            if (LOD == 1 || LOD == 0)
                return false;

            for (int i = 0; i < 6; i++)
            {
                if (neighborNodes[i] != null)
                    if (neighborNodes[i].LOD == this.LOD && neighborNodes[i].isBottomLevel() && !isBottomLevel())
                        return true;
            }

            return false;
        }

        /// <summary>
        /// Populates the subnode array
        /// </summary>
        public void createSubNodes(RenderType type)
        {
            if (LOD == 0)
                return;

            if (subNodes[0] != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (subNodes[i].renderType != type)
                        subNodes[i].setRenderType(type);

                    subNodes[i].disposed = false;
                }
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                if (subNodes[i] == null)
                    subNodes[i] = new Node(this, NodeManager.getOffsetPosition(this, i), i, LOD - 1, type);
            }
        }
        /// <summary>
        /// Sets the density of a point, given a world pos.
        /// </summary>
        /// <param name="worldPos"></param>
        public void setDensityFromWorldPos(Vector3 worldPos, float val)
        {
            worldPos = worldPos - position;
            Vector3I arrayPos = new Vector3I((int)Math.Round(worldPos.x) / NodeManager.LODSize[LOD],
                                            (int)Math.Round(worldPos.y) / NodeManager.LODSize[LOD],
                                            (int)Math.Round(worldPos.z) / NodeManager.LODSize[LOD]);

            if (arrayPos.x < -1 || arrayPos.x > 17 ||
                arrayPos.y < -1 || arrayPos.y > 17 ||
                arrayPos.z < -1 || arrayPos.z > 17)
            {
                Debug.Log("Wrong node. " + arrayPos + ":" + worldPos + ":" + containsDensityPoint(worldPos).ToString());
                return;
            }

            densityChangeData.set(arrayPos.x, arrayPos.y, arrayPos.z, val);
            setPermanence(true);

            hasDensityChangeData = true;
            MeshFactory.requestSave(this);
        }

        /// <summary>
        /// Sets the material of the voxel at the given world position.
        /// </summary>
        /// <param name="worldPos"></param>
        /// <param name="val"></param>
        public void setMaterialFromWorldPos(Vector3 worldPos, byte val)
        {
            worldPos = worldPos - position;
            Vector3I arrayPos = new Vector3I((int)Math.Round(worldPos.x) / NodeManager.LODSize[LOD],
                                            (int)Math.Round(worldPos.y) / NodeManager.LODSize[LOD],
                                            (int)Math.Round(worldPos.z) / NodeManager.LODSize[LOD]);

            if (arrayPos.x < -1 || arrayPos.x > 17 ||
                arrayPos.y < -1 || arrayPos.y > 17 ||
                arrayPos.z < -1 || arrayPos.z > 17)
            {
                Debug.Log("Wrong node. " + arrayPos);
                return;
            }

            bool change = (densityChangeData.getMaterial(arrayPos.x, arrayPos.y, arrayPos.z) != val);
            densityChangeData.setMaterial(arrayPos.x, arrayPos.y, arrayPos.z, val);

            if (change)
            {
                setPermanence(true);
                hasDensityChangeData = true;
                MeshFactory.requestSave(this);
            }
        }

        /// <summary>
        /// Regenerates the chunk without threading.
        /// </summary>
        public void regenerateChunk()
        {
            if (regenReq)
            {
                regenFlag = true;
            }
            else
            {
                MeshFactory.requestMesh(this);
                regenReq = true;
            }
        }

        /// <summary>
        /// Saves changes, if any.
        /// </summary>
        public void saveChanges()
        {
            if (!permanent)
                return;

            string dir = getDirectory();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            int[] matData = densityChangeData.compressMaterialData();
            float[] data = densityChangeData.compressDensityData();

            StreamWriter writer = new StreamWriter(dir + "\\materials.txt");
            for (int i = 0; i < matData.Length; i += 2)
                writer.WriteLine(matData[i] + "," + matData[i + 1]);
            writer.Close();

            writer = new StreamWriter(dir + "\\densities.txt");
            for (int i = 0; i < data.Length; i += 2)
                writer.WriteLine(data[i] + "," + data[i + 1]);
            writer.Close();
        }

        /// <summary>
        /// Attempts to load density changes.
        /// </summary>
        public bool loadChanges()
        {
            string dir = getDirectory();
            if (!Directory.Exists(dir))
                return false;

            if (!File.Exists(dir + "\\densities.txt"))
                return false;

            List<string> fileData = new List<string>();
            StreamReader reader = new StreamReader(dir + "\\densities.txt");
            while (!reader.EndOfStream)
                fileData.Add(reader.ReadLine());

            float[] data = new float[fileData.Count * 2];
            string[] split = new string[2];
            for (int i = 0; i < fileData.Count; i++)
            {
                split = fileData[i].Split(',');
                data[(i * 2)] = float.Parse(split[0]);
                data[(i * 2) + 1] = float.Parse(split[1]);
            }

            reader.Close();
            densityChangeData.decompressDensityData(data);

            fileData = new List<string>();
            reader = new StreamReader(dir + "\\materials.txt");
            while (!reader.EndOfStream)
                fileData.Add(reader.ReadLine());

            int[] mdata = new int[fileData.Count * 2];
            split = new string[2];
            for (int i = 0; i < fileData.Count; i++)
            {
                split = fileData[i].Split(',');
                mdata[(i * 2)] = int.Parse(split[0]);
                mdata[(i * 2) + 1] = int.Parse(split[1]);
            }

            reader.Close();
            densityChangeData.decompressMaterialData(mdata);

            hasDensityChangeData = true;
            return true;
        }

        /// <summary>
        /// Disposes of subnodes
        /// </summary>
        public void dispose()
        {
            //If already disposed, exit.
            if (disposed)
                return;

            disposed = true;

            for (int i = 0; i < 8; i++)
            {
                if (subNodes[i] != null)
                {
                    subNodes[i].dispose();
                    subNodes[i] = null;
                }
            }

            if (permanent)
            {
                if (chunk != null)
                    chunk.GetComponent<MeshFilter>().renderer.enabled = false;
                return;
            }

            NodeManager.nodeCount[LOD]--;

            hasMesh = false;
            if (parent != null)
                parent.renderCheck();

            //Remove this node from the neighbor of existing nodes.
            for (int i = 0; i < 6; i++)
            {
                if (neighborNodes[i] != null)
                {
                    neighborNodes[i].neighborNodes[oppositeNeighbor[i]] = null;
                }
            }

            if (chunk != null)
            {
                UnityEngine.Object.Destroy(chunk.GetComponent<MeshFilter>().sharedMesh);
                Mesh mesh = chunk.GetComponent<MeshCollider>().sharedMesh;
                if (mesh != null)
                    UnityEngine.Object.Destroy(mesh);

                ChunkPool.recycleChunk(chunk);
            }

            if (densityData != null)
                DensityPool.recycleDensityData(densityData);
            if (densityChangeData != null)
                DensityPool.recycleDensityData(densityChangeData);
        }

        /// <summary>
        /// Attempts to find any neighbors that we don't have a reference to.
        /// </summary>
        private void findNeighbors()
        {
            float nodeWidth = NodeManager.LODSize[LOD] * NodeManager.nodeSize;
            Vector3 searchPos = new Vector3();
            for (int i = 0; i < 6; i++)
            {
                if (neighborNodes[i] != null)
                {
                    if (neighborNodes[i].LOD == LOD)
                        continue;
                }
                searchPos.x = center.x + (neighborMask[i].x * nodeWidth);
                searchPos.y = center.y + (neighborMask[i].y * nodeWidth);
                searchPos.z = center.z + (neighborMask[i].z * nodeWidth);
                neighborNodes[i] = NodeManager.searchNode(searchPos, LOD);

                if (neighborNodes[i] != null && neighborNodes[i].LOD == LOD)
                    neighborNodes[i].neighborNodes[oppositeNeighbor[i]] = this;
            }
        }

        /// <summary>
        /// Sets the permanence of this node. If true, it will not be disposed of when out of range.
        /// </summary>
        private void setPermanence(bool perm)
        {
            if (perm == true)
                if (parent != null)
                    parent.setPermanence(true);

            if (perm == false)
                for (int i = 0; i < 8; i++)
                    if (subNodes[i] != null)
                        subNodes[i].setPermanence(false);

            permanent = perm;
        }

        /// <summary>
        /// Checks if the node intersects with a boundary box of the given size around the player.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="width"></param>
        /// <returns></returns>
        private bool checkRangeIntersection(Vector3 pos, float width)
        {
            float nodeWidth = NodeManager.nodeSize * NodeManager.LODSize[LOD];
            Vector3[] corners = new Vector3[8] {
            new Vector3(position.x, position.y, position.z),
            new Vector3(position.x + nodeWidth, position.y, position.z),
            new Vector3(position.x, position.y + nodeWidth, position.z),
            new Vector3(position.x, position.y, position.z + nodeWidth),
            new Vector3(position.x + nodeWidth, position.y + nodeWidth, position.z),
            new Vector3(position.x + nodeWidth, position.y, position.z + nodeWidth),
            new Vector3(position.x, position.y + nodeWidth, position.z + nodeWidth),
            new Vector3(position.x + nodeWidth, position.y + nodeWidth, position.z + nodeWidth),
        };

            for (int i = 0; i < 8; i++)
            {
                //float distance = (float)Math.Sqrt((corners[i].x - pos.x) * (corners[i].x - pos.x) +
                //(corners[i].y - pos.y) * (corners[i].y - pos.y) +
                //(corners[i].z - pos.z) * (corners[i].z - pos.z));
                float distance = Vector3.Distance(pos, corners[i]);
                if (distance < width)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the directory of where this node should be saved.
        /// </summary>
        /// <returns></returns>
        private string getDirectory()
        {
            if (parent == null) return NodeManager.worldName + "\\" + subNodeID.ToString();
            return parent.getDirectory() + "\\" + subNodeID;
        }

        public override string ToString()
        {
            return "LOD: " + LOD + ", Pos: " + position.ToString();
        }

        /// <summary>
        /// Draws chunk outlines (Very buggy and laggy)
        /// </summary>
        /// <returns></returns>
        public int debugDraw()
        {
            return 0;
			//Debug drawing, draws the outlines of chunks, very laggy use at own risk.
			/*
            if (true)
            {
                int nodeWidth = NodeManager.nodeSize * NodeManager.LODSize[LOD];
                if (LOD == 0) Gizmos.color = Color.red;
                if (LOD == 1) Gizmos.color = Color.yellow;
                if (LOD == 2) Gizmos.color = Color.white;
                if (LOD == 3) Gizmos.color = Color.green;
                if (LOD == 4) Gizmos.color = Color.blue;

                //if(isTransitional())
                //Gizmos.color = Color.magenta;

                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.5f);
                Vector3 drawPos = new Vector3(position.x + (nodeWidth / 2), position.y + (nodeWidth / 2), position.z + (nodeWidth / 2));

                //if (!isTransitional())
                //{
                if (LOD == 0)// && !empty)
                {
                    Gizmos.DrawWireCube(drawPos, new Vector3(nodeWidth, nodeWidth, nodeWidth));
                    //}
                    //else if(LOD == 3)
                    //{
                    //    Gizmos.DrawCube(drawPos, new Vector3(nodeWidth, nodeWidth, nodeWidth));
                    //}
                    Gizmos.DrawCube(center, new Vector3(10, 10, 10));
                }
            }

            int ct = 1;
            for (int i = 0; i < 8; i++)
                if (subNodes[i] != null)
                    ct += subNodes[i].debugDraw();
            return ct;
			*/
        }
    }
}