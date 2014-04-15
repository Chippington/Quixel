using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System;
using System.IO;

namespace Quixel
{
    public static class NodeEditor
    {
        #region Fields
        /// <summary> The type of brush to edit with. </summary>
        public enum BrushType
        {
            BOX, SPHERE
        }

        public static float DENSITY_SOLIDHARD = MeshFactory.isolevel - 0.1f;
        public static float DENSITY_SOLIDSOFT = MeshFactory.isolevel - 5f;
        public static float DENSITY_EMPTY = MeshFactory.isolevel + 1f;

        private static byte materialID = 0;
        #endregion

        /// <summary>
        /// Sets the material id to paint/brush with.
        /// The material ID is the material's location in the material array.
        /// </summary>
        /// <param name="matID"></param>
        public static void setMaterial(byte matID)
        {
            materialID = matID;
        }

        /// <summary>
        /// Applies a brush to the terrain.
        /// </summary>
        /// <param name="type">The type/shape of the brush.</param>
        /// <param name="size">The size (radius) of the brush.</param>
        /// <param name="pos">The (real world) position to apply the brush.</param>
        /// <param name="val">Density to apply.</param>
        public static void applyBrush(BrushType type, int size, Vector3 pos, float val)
        {
            List<Node> changedNodes = new List<Node>();
            float nodeWidth = NodeManager.LODSize[0];
            Vector3 realPos = new Vector3();
            Vector3I point = new Vector3I((int)Math.Round(pos.x / nodeWidth),
                (int)Math.Round(pos.y / nodeWidth),
                (int)Math.Round(pos.z / nodeWidth));

            List<Vector3> points = getPoints(type, size, pos);
            for (int o = 0; o < points.Count; o++)
            {
                realPos = points[o];
                Node[] editNodes = NodeManager.searchNodeContainingDensity(realPos, 0);

                for (int i = 0; i < editNodes.Length; i++)
                {
                    Node editNode = editNodes[i];
                    if (editNode != null && editNode.LOD == 0)
                    {
                        if (!changedNodes.Contains(editNode))
                            changedNodes.Add(editNode);


                        if (type == BrushType.SPHERE)
                        {
                            float v = val;
                            float dist = Vector3.Distance(pos, realPos);

                            if (val > MeshFactory.isolevel)
                                v = 10f - ((dist / (float)size) * 5f);
                            if (val < MeshFactory.isolevel)
                                v = 10f + ((dist / (float)size) * 5f);

                        }

                        editNode.setDensityFromWorldPos(realPos, val);
                        editNode.setMaterialFromWorldPos(realPos, materialID);

                    }
                }
            }

            applyPaint(type, size + 1, pos, false);
            for (int i = 0; i < changedNodes.Count; i++)
                changedNodes[i].regenerateChunk();
        }

        /// <summary>
        /// 'Heals' the terrain around a given point, essentially returning the terrain to it's original state.
        /// </summary>
        /// <param name="type">The type/shape of the brush.</param>
        /// <param name="size">The size (radius) of the brush.</param>
        /// <param name="pos">The (real world) position to apply the brush.</param>
        public static void applyHeal(BrushType type, int size, Vector3 pos)
        {
            byte mID = materialID;
            materialID = 0;
            applyBrush(type, size, pos, -100000f);
            materialID = mID;
        }

        /// <summary>
        /// Applies 'paint' to the area around the brush.
        /// </summary>
        /// <param name="type">The type/shape of the brush.</param>
        /// <param name="size">The size (radius) of the brush.</param>
        /// <param name="pos">The (real world) position to apply the brush.</param>
        /// <param name="regen">Set to true to have the engine regen the chunk after painting.</param>
        public static void applyPaint(BrushType type, int size, Vector3 pos, bool regen)
        {
            List<Node> changedNodes = new List<Node>();
            float nodeWidth = NodeManager.LODSize[0];
            Vector3 realPos = new Vector3();
            Vector3I point = new Vector3I((int)Math.Round(pos.x / nodeWidth),
                (int)Math.Round(pos.y / nodeWidth),
                (int)Math.Round(pos.z / nodeWidth));

            List<Vector3> points = getPoints(type, size, pos);
            for (int o = 0; o < points.Count; o++)
            {
                realPos = points[o];
                Node[] editNodes = NodeManager.searchNodeContainingDensity(realPos, 0);

                for (int i = 0; i < editNodes.Length; i++)
                {
                    Node editNode = editNodes[i];
                    if (editNode != null && editNode.LOD == 0)
                    {
                        if (!changedNodes.Contains(editNode))
                            changedNodes.Add(editNode);

                        editNode.setMaterialFromWorldPos(realPos, materialID);

                    }
                }
            }

            if (regen)
                for (int i = 0; i < changedNodes.Count; i++)
                    changedNodes[i].regenerateChunk();
        }

        /// <summary>
        /// Returns a list of points inside of a given brush type and size.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="size"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        private static List<Vector3> getPoints(BrushType type, int size, Vector3 pos)
        {
            float nodeWidth = NodeManager.LODSize[0];
            Vector3I point = new Vector3I((int)Math.Round(pos.x / nodeWidth),
                (int)Math.Round(pos.y / nodeWidth),
                (int)Math.Round(pos.z / nodeWidth));

            List<Vector3> ret = new List<Vector3>();
            switch (type)
            {
                case BrushType.BOX:
                    for (int x = 0; x <= size; x++)
                    {
                        for (int y = 0; y <= size; y++)
                        {
                            for (int z = 0; z <= size; z++)
                            {
                                Vector3 realPos = new Vector3();
                                realPos.x = ((point.x + x) * nodeWidth);
                                realPos.y = ((point.y + y) * nodeWidth);
                                realPos.z = ((point.z + z) * nodeWidth);
                                ret.Add(realPos);
                            }
                        }
                    }
                    break;

                case BrushType.SPHERE:
                    for (int x = -size; x < size; x++)
                    {
                        for (int y = -size * 2; y < size; y++)
                        {
                            for (int z = -size; z < size; z++)
                            {
                                Vector3 realPos = new Vector3();
                                realPos.x = ((point.x + x) * nodeWidth);
                                realPos.y = ((point.y + y) * nodeWidth);
                                realPos.z = ((point.z + z) * nodeWidth);

                                if (Vector3.Distance(realPos, pos) < size * nodeWidth)
                                {
                                    ret.Add(realPos);
                                }
                            }
                        }
                    }
                    break;
            }

            return ret;
        }
    }
}