internal class MountainTerrain : IGenerator
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
		d += noise * 800f;

		//Uncomment for some silly looking caves/hills
		//if (yy < -150f)
			//d -= (SimplexNoise.Noise.Generate(xx / 680f, yy / 600f, zz / 680f) * 190f) * ((yy + 150f) / 300f);

		return new VoxelData()
		{
			density = d,
			material = 0
		};
	}
}