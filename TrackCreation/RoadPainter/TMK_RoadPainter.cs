using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[ExecuteInEditMode]
public class TMK_RoadPainter : MonoBehaviour
{
    public List<int> RoadTextureIndexes = new List<int> { 1 };
    public float TextureThreshold = 0.5f;
    public float MaxDistanceFromRoadToPreservePoints = 1f;

    [HideInInspector]
    private readonly List<Vector3> RoadPoints = new List<Vector3>();

    [HideInInspector]
    private Terrain TerrainParent;

#if UNITY_EDITOR
    public void ResetHoleMap()
    {
        // Create a hole map and erase all the holes on the parent terrain
        TerrainParent = gameObject.GetComponent<Terrain>();
        var holeRes = TerrainParent.terrainData.holesResolution;
        var holeMap = new bool[holeRes, holeRes];
        for (var x = 0; x < holeRes; x++)
        {
            for (var y = 0; y < holeRes; y++)
            {
                holeMap[x, y] = true;
            }
        }

        TerrainParent.terrainData.SetHoles(0, 0, holeMap);
    }

    public void GenerateRoadMesh()
    {
        TerrainParent = gameObject.GetComponent<Terrain>();

        var terrainData = TerrainParent.terrainData;
        Undo.RecordObject(terrainData, "Automatically generated road mesh");

        // Get terrain positions and sizes used in converting terrain points to world points
        var terrainPosX = TerrainParent.transform.position.x;
        var terrainPosZ = TerrainParent.transform.position.z;
        var terrainSizeX = TerrainParent.terrainData.size.x;
        var terrainSizeZ = TerrainParent.terrainData.size.z;

        // Go through the terrain's texture data, and if the amount of the road texture exceeds a threshold, get the world point and record it as a road point
        var splatmapData = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
        var xRange = splatmapData.GetLength(0);
        var yRange = splatmapData.GetLength(1);
        RoadPoints.Clear();
        for (var x = 0; x < xRange; x++)
        {
            for (var y = 0; y < yRange; y++)
            {
                foreach (var roadTextureIndex in RoadTextureIndexes)
                {
                    if (splatmapData[x, y, roadTextureIndex] > TextureThreshold)
                    {
                        var roadPt = new Vector3(
                            terrainPosX + ((float)x / xRange * terrainSizeX),
                            terrainData.GetInterpolatedHeight((float)x / xRange, (float)y / yRange),
                            terrainPosZ + ((float)y / yRange * terrainSizeZ)
                        );
                        RoadPoints.Add(roadPt);
                        break;
                    }
                }
            }
        }

        // Create a new terrain that is the child of the existing terrain
        // This terrain will consist only of the road path, and the rest of it will be holes
        var newTerrain = CloneTerrain(TerrainParent);
        newTerrain.name = "RoadTerrain";
        newTerrain.gameObject.transform.parent = TerrainParent.gameObject.transform;

        // Go through all the points in the hole map of the new terrain, get world points, and see if they are within range of any of the road points
        // Those points will be preserved, but all other points will be holes
        var holeRes = newTerrain.terrainData.holesResolution;
        var holeMap = new bool[holeRes, holeRes];
        var roadPtsNoHeight = RoadPoints.Select(pt => new Vector2(pt.x, pt.z)).ToArray(); // Use Vector2 objects and ignore the height
        Enumerable.Range(0, holeRes).AsParallel().ForAll(x =>
        {
            for (var y = 0; y < holeRes; y++)
            {
                // Get a Vector2 world position of the hole being checked and ignore the height since that's not important for this calculation
                var xHole = terrainPosX + ((float)x / holeRes * terrainSizeX);
                var zHole = terrainPosZ + ((float)y / holeRes * terrainSizeZ);
                var holePt = new Vector2(xHole, zHole);

                holeMap[x, y] = false;
                foreach (var roadPt in roadPtsNoHeight)
                {
                    if (Vector2.Distance(holePt, roadPt) < MaxDistanceFromRoadToPreservePoints)
                    {
                        holeMap[x, y] = true;
                        break;
                    }
                }
            }
        });
        newTerrain.terrainData.SetHoles(0, 0, holeMap);

        // After applying the hole map to the new terrain, apply the inverted hole map to the original terrain
        // This ensures each can be assigned to a different layer (ground, offroad, etc.) and not interfere with each other
        for (var x = 0; x < holeRes; x++)
        {
            for (var y = 0; y < holeRes; y++)
            {
                holeMap[x, y] = !holeMap[x, y];
            }
        }
        terrainData.SetHoles(0, 0, holeMap);
    }

    private Terrain CloneTerrain(Terrain terrain)
    {
        // Create a new Terrain and copy the original size, resolution, heightmap, etc.
        var terrainData = terrain.terrainData;
        var heightmapRes = terrainData.heightmapResolution;
        var newTerrainData = new TerrainData
        {
            heightmapResolution = heightmapRes,
            size = terrainData.size,
            baseMapResolution = terrainData.baseMapResolution
        };
        newTerrainData.SetHeights(0, 0, terrainData.GetHeights(0, 0, heightmapRes, heightmapRes));

        var newTerrainGameObj = Terrain.CreateTerrainGameObject(newTerrainData);
        var newTerrain = newTerrainGameObj.GetComponent<Terrain>();

        // Position it in the same place as the original terrain
        newTerrain.transform.position = terrain.transform.position;
        // newTerrain.drawHeightmap = false;
        // newTerrain.materialTemplate = null;

        // Copy the texture/splatmap information to the new terrain to ensure every appears correctly rather than being the default gray
        var origAlphaMaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
        newTerrainData.terrainLayers = terrainData.terrainLayers;
        newTerrainData.alphamapResolution = terrainData.alphamapResolution;
        newTerrainData.SetAlphamaps(0, 0, origAlphaMaps);

        return newTerrain;
    }
#endif
}
