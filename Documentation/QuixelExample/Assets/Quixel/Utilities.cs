using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System;
using System.IO;

namespace Quixel
{
    internal class DensityData
    {
        #region Fields
        /// <summary> Reference to another density data that contains the change in densities used for paging. </summary>
        private DensityData changeData;

        /// <summary> Density values </summary>
        public float[, ,] Values;

        /// <summary> Array of bytes that dictates which material to render </summary>
        public byte[, ,] Materials;
        #endregion

        public DensityData()
        {
            Values = new float[19, 19, 19];
            Materials = new byte[19, 19, 19];
            for (int x = 0; x < 19; x++)
                for (int y = 0; y < 19; y++)
                    for (int z = 0; z < 19; z++)
                    {
                        Values[x, y, z] = -100000f;
                        Materials[x, y, z] = 0;
                    }
        }

        /// <summary>
        /// Returns the density value at the given coordinates.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public float get(int x, int y, int z)
        {
            if (changeData != null)
                if (changeData.get(x, y, z) > -99999f)
                    return changeData.get(x, y, z);

            return Values[x + 1, y + 1, z + 1];
        }

        /// <summary>
        /// Returns the density value at the given coordinates.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public float get(Vector3I pos)
        {
            if (changeData != null)
                if (changeData.get(pos) > -99999f)
                    return changeData.get(pos);

            return get(pos.x, pos.y, pos.z);
        }

        /// <summary>
        /// Returns the material index of a particular voxel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public byte getMaterial(int x, int y, int z)
        {
            if (changeData != null)
                if (changeData.getMaterial(x, y, z) != 0)
                    return changeData.getMaterial(x, y, z);
            return Materials[x + 1, y + 1, z + 1];
        }

        /// <summary>
        /// Sets the density value at the given coordinates.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public void set(int x, int y, int z, float val)
        {
            Values[x + 1, y + 1, z + 1] = val;
        }

        /// <summary>
        /// Sets the density value at the given coordinates.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public void set(Vector3I pos, float val)
        {
            set(pos.x, pos.y, pos.z, val);
        }

        /// <summary>
        /// Sets the material of a particular voxel.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="val"></param>
        public void setMaterial(int x, int y, int z, byte val)
        {
            Materials[x + 1, y + 1, z + 1] = val;
        }

        /// <summary>
        /// Applies changes (additive) using another Density Data
        /// </summary>
        /// <param name="other"></param>
        public void applyChanges(DensityData other)
        {
            for (int x = 0; x < 19; x++)
                for (int y = 0; y < 19; y++)
                    for (int z = 0; z < 19; z++)
                        if (other.Values[x, y, z] > -99999f)
                            Values[x, y, z] = other.Values[x, y, z];
        }

        /// <summary>
        /// Compresses (RLE) the density data.
        /// </summary>
        /// <returns></returns>
        public float[] compressDensityData()
        {
            List<float> data = new List<float>();
            float last = Values[0, 0, 0];
            data.Add(0f);
            data.Add(last);

            int count = 0;
            for (int x = 0; x < 19; x++)
                for (int y = 0; y < 19; y++)
                    for (int z = 0; z < 19; z++)
                    {
                        count++;
                        if (Values[x, y, z] == last)
                        {
                            data[data.Count - 2] = count;
                        }
                        else
                        {
                            data.Add(1f);
                            data.Add(Values[x, y, z]);
                            count = 1;
                        }

                        last = Values[x, y, z];
                    }

            float[] ret = new float[data.Count];
            for (int i = 0; i < data.Count; i++)
                ret[i] = data[i];

            return ret;
        }

        /// <summary>
        /// Compress the materials using run-length encoding.
        /// </summary>
        /// <returns></returns>
        public int[] compressMaterialData()
        {
            List<int> data = new List<int>();
            int last = Materials[0, 0, 0];
            data.Add(0);
            data.Add(last);

            int count = 0;
            for (int x = 0; x < 19; x++)
                for (int y = 0; y < 19; y++)
                    for (int z = 0; z < 19; z++)
                    {
                        count++;
                        if (Materials[x, y, z] == last)
                        {
                            data[data.Count - 2] = count;
                        }
                        else
                        {
                            data.Add(1);
                            data.Add(Materials[x, y, z]);
                            count = 1;
                        }

                        last = Materials[x, y, z];
                    }

            int[] ret = new int[data.Count];
            for (int i = 0; i < data.Count; i++)
                ret[i] = data[i];

            return ret;
        }

        /// <summary>
        /// Decompresses a sparse array of densities.
        /// </summary>
        /// <param name="data"></param>
        public void decompressDensityData(float[] data)
        {
            int total = 0;
            int index = 0;
            int count = 0;
            for (int x = 0; x < 19; x++)
                for (int y = 0; y < 19; y++)
                    for (int z = 0; z < 19; z++)
                    {
                        total++;

                        if (count >= data[index])
                        {
                            count = 0;
                            index += 2;
                        }

                        count++;

                        Values[x, y, z] = data[index + 1];
                    }
        }

        /// <summary>
        /// Decompresses a compressed array of materials.
        /// </summary>
        /// <param name="data"></param>
        public void decompressMaterialData(int[] data)
        {
            int total = 0;
            int index = 0;
            int count = 0;
            for (int x = 0; x < 19; x++)
                for (int y = 0; y < 19; y++)
                    for (int z = 0; z < 19; z++)
                    {
                        total++;

                        if (count >= data[index])
                        {
                            count = 0;
                            index += 2;
                        }

                        count++;

                        Materials[x, y, z] = (byte)data[index + 1];
                    }
        }

        /// <summary>
        /// Sets the change data for this density array.
        /// Values are pulled from here if available.
        /// </summary>
        public void setChangeData(DensityData data)
        {
            changeData = data;
        }

        /// <summary>
        /// Disposes of the density array.
        /// </summary>
        public void dispose()
        {
            for (int x = 0; x < 19; x++)
                for (int y = 0; y < 19; y++)
                    for (int z = 0; z < 19; z++)
                        Values[x, y, z] = -100000f;
        }
    }

    internal struct Triangle
    {
        public Vector3 pointOne, pointTwo, pointThree;
        public Vector3 nOne, nTwo, nThree;

        /// <summary>
        /// Creates a triangle consisting of 6 vectors. 3 for points, 3 for normals.
        /// </summary>
        /// <param name="PointOne"></param>
        /// <param name="PointTwo"></param>
        /// <param name="PointThree"></param>
        /// <param name="nOne"></param>
        /// <param name="nTwo"></param>
        /// <param name="nThree"></param>
        public Triangle(Vector3 PointOne, Vector3 PointTwo, Vector3 PointThree, Vector3 nOne, Vector3 nTwo, Vector3 nThree)
        {
            this.pointOne = PointOne;
            this.pointTwo = PointTwo;
            this.pointThree = PointThree;

            this.nOne = nOne;
            this.nTwo = nTwo;
            this.nThree = nThree;
        }
    }

    internal struct Vector3I
    {
        public int x;
        public int y;
        public int z;

        public Vector3I(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// Checks equality with another Vector3I object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            Vector3I o = (Vector3I)obj;
            return (x == o.x && y == o.y && z == o.z);
        }

        /// <summary>
        /// Only because Unity complained if I didn't
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Adds another vector's values to this
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public Vector3I Add(Vector3I other)
        {
            return new Vector3I(x + other.x, y + other.y, z + other.z);
        }

        /// <summary>
        /// Subtracts another vectory's values from this
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public Vector3I Subtract(Vector3I other)
        {
            return new Vector3I(x - other.x, y - other.y, z - other.z);
        }

        /// <summary>
        /// Convert to string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("({0},{1},{2})", x, y, z);
        }
    }

    internal class MeshData
    {
        public Vector3[] triangleArray;
        public Vector2[] uvArray;
        public int[][] indexArray;
        public Vector3[] normalArray;

        /// <summary>
        /// Disposes the meshdata.
        /// </summary>
        public void dispose()
        {
            triangleArray = null;
            uvArray = null;
            indexArray = null;
            normalArray = null;
        }
    }
}