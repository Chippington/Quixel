using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System;
using System.IO;

namespace Quixel
{
    /// <summary>
    /// Thread used for generating chunks.
    /// </summary>
    internal class GeneratorThread
    {
        private Queue<MeshFactory.MeshRequest> genQueue;
        private Queue<MeshFactory.MeshRequest> finishedQueue;
        private Thread thread;

        public GeneratorThread()
        {
            Debug.Log("Generation Thread Started");
            genQueue = new Queue<MeshFactory.MeshRequest>();
            finishedQueue = new Queue<MeshFactory.MeshRequest>();

            try
            {
                thread = new Thread((System.Object obj) =>
                {
                    generateLoop();
                });

                //new ThreadStart(generateLoop));
                thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                thread.IsBackground = true;
                thread.Start();
            }
            catch (Exception e)
            {
                Debug.Log(e.Message + "\r\n" + e.StackTrace);
            }
        }

        /// <summary>
        /// Queue a mesh for generation.
        /// </summary>
        /// <param name="req"></param>
        public void queueGenerateMesh(MeshFactory.MeshRequest req)
        {
            lock (genQueue)
                genQueue.Enqueue(req);
        }

        /// <summary>
        /// Returns a finished mesh request if there is one.
        /// </summary>
        /// <returns></returns>
        public MeshFactory.MeshRequest getFinishedMesh()
        {
            if (finishedQueue.Count > 0)
                lock (finishedQueue)
                    return finishedQueue.Dequeue();

            return null;
        }

        /// <summary>
        /// Gets the count of queued mesh requests.
        /// </summary>
        /// <returns></returns>
        public int getCount()
        {
            return genQueue.Count;
        }

        /// <summary>
        /// Continuously check if we have chunks to generate.
        /// </summary>
        private void generateLoop()
        {
            bool sleep = true;
            while (QuixelEngine.isActive())
            {
                sleep = false;

                MeshFactory.MeshRequest req = MeshFactory.getNextRequest();
                if (req == null)
                    sleep = true;
                else
                {
                    if (req.densities == null)
                        if (!req.hasDensities)
                            req.densities = DensityPool.getDensityData();
                        else
                            req.densities = req.node.densityData;

                    MeshFactory.GenerateMeshData(req);

                    lock (finishedQueue)
                        finishedQueue.Enqueue(req);
                }

                if (sleep)
                    Thread.Sleep(30);
                else
                    Thread.Sleep(4);
            }
        }
    }

    /// <summary>
    /// Thread used for generating densities.
    /// Currently unused due to bugs
    /// </summary>
    internal class DensityThread
    {
        private Queue<DensityData> genQueue;
        private Queue<DensityData> finishedQueue;
        private Thread thread;

        public DensityThread()
        {
            genQueue = new Queue<DensityData>();
            finishedQueue = new Queue<DensityData>();

            thread = new Thread(new ThreadStart(recycleLoop));
            thread.Priority = System.Threading.ThreadPriority.BelowNormal;
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Queue a mesh for generation.
        /// </summary>
        /// <param name="req"></param>
        public void queueRecycleDensity(DensityData req)
        {
            lock (genQueue)
                genQueue.Enqueue(req);
        }

        /// <summary>
        /// Returns a finished mesh request if there is one.
        /// </summary>
        /// <returns></returns>
        public DensityData getFinishedDensity()
        {
            if (finishedQueue.Count > 0)
                lock (finishedQueue)
                    return finishedQueue.Dequeue();

            return null;
        }

        /// <summary>
        /// Continuously check if we have chunks to generate.
        /// </summary>
        private void recycleLoop()
        {
            while (QuixelEngine.isActive())
            {
                Thread.Sleep(1);
                if (genQueue.Count > 0)
                {
                    DensityData d = null;
                    lock (genQueue)
                        d = genQueue.Dequeue();

                    d.dispose();

                    lock (finishedQueue)
                        finishedQueue.Enqueue(d);
                }
                else
                {
                    Thread.Sleep(30);
                }
            }
        }
    }

    /// <summary>
    /// Thread used for saving/loading chunks.
    /// </summary>
    internal class FileThread
    {
        private Queue<Node> finishedLoadQueue;
        private Queue<Node> loadQueue;
        private Queue<Node> saveQueue;
        private Thread thread;

        public FileThread()
        {
            finishedLoadQueue = new Queue<Node>();
            loadQueue = new Queue<Node>();
            saveQueue = new Queue<Node>();

            try
            {
                thread = new Thread(new ThreadStart(commitLoop));
                thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                thread.IsBackground = true;
                thread.Start();
            }
            catch (Exception e)
            {
                Debug.Log(e.Message + "\r\n" + e.StackTrace);
            }
        }

        /// <summary>
        /// Queues the node for saving.
        /// </summary>
        /// <param name="node"></param>
        public void enqueueSave(Node node)
        {
            lock (saveQueue)
                if (!saveQueue.Contains(node))
                    saveQueue.Enqueue(node);
        }

        /// <summary>
        /// Queues the node for loading.
        /// </summary>
        /// <param name="node"></param>
        public void enqueueLoad(Node node)
        {
            lock (loadQueue)
                loadQueue.Enqueue(node);
        }

        /// <summary>
        /// Returns a finished node request if there is one.
        /// </summary>
        /// <returns></returns>
        public Node getFinishedLoadRequest()
        {
            if (finishedLoadQueue.Count > 0)
                lock (finishedLoadQueue)
                    return finishedLoadQueue.Dequeue();

            return null;
        }

        /// <summary>
        /// Loop for committing saves and loads.
        /// </summary>
        private void commitLoop()
        {
            while (QuixelEngine.isActive())
            {
                try
                {
                    Thread.Sleep(3);
                    if (saveQueue.Count > 0)
                    {
                        MeshFactory.nodesSaved++;
                        lock (saveQueue)
                            saveQueue.Dequeue().saveChanges();
                    }

                    if (loadQueue.Count > 0)
                    {
                        Node node = null;
                        lock (loadQueue)
                            node = loadQueue.Dequeue();
                        if (node.loadChanges())
                        {
                            MeshFactory.nodesLoaded++;
                            lock (finishedLoadQueue)
                                finishedLoadQueue.Enqueue(node);
                        }
                    }
                }
                catch (Exception e)
                {
                    StreamWriter writer = new StreamWriter("Error Log.txt");
                    writer.WriteLine(e.Message + "\r\n" + e.StackTrace);
                    writer.Close();
                }
            }
        }
    }


}