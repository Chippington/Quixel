using UnityEngine;
using System.Collections;
using Quixel;

public class TerrainController : MonoBehaviour {
	public Material[] materialArray;
	public GameObject player;

	// Use this for initialization
	void Start () {
		//Set the smallest voxel size to 1 unit, and the largest LOD to 8.
		//Must be called before QuixelEngine.init(...);
		QuixelEngine.setVoxelSize(2, 8);
		
		//Set the 'camera' object to the player. This is what the engine uses
		//To determine with nodes to load.
		QuixelEngine.setCameraObj(player);
		
		//Initialize Quixel with the material array and use this game object as
		//the 'parent' terrain object. All nodes for the terrain are created under
		//the parent.
		QuixelEngine.init(materialArray, this.gameObject, "testWorld");
	}
	
	// Update is called once per frame
	void Update () {
		//Updates all components of the Quixel Engine.
		QuixelEngine.update();
	}
	
	void OnApplicationQuit() {
		//Terminate Quixel. This must be used or the threads would have no quit flag.
        QuixelEngine.terminate();
    }
}
