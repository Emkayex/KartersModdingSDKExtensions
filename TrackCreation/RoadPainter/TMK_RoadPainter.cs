using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[ExecuteInEditMode]
public class TMK_RoadPainter : MonoBehaviour
{
    [HideInInspector]
    private readonly List<Vector3> RoadPoints = new List<Vector3>();

    [HideInInspector]
    private Terrain TerrainParent;

#if UNITY_EDITOR
    public void ResetHoleMap()
    {
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
        var heightmapDimension = terrainData.heightmapResolution;
        Undo.RecordObject(terrainData, "Automatically generated road mesh");

        var terrainPosX = TerrainParent.transform.position.x;
        var terrainPosZ = TerrainParent.transform.position.z;
        var terrainSizeX = TerrainParent.terrainData.size.x;
        var terrainSizeZ = TerrainParent.terrainData.size.z;

        var splatmapData = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
        var xRange = splatmapData.GetLength(0);
        var yRange = splatmapData.GetLength(1);
        RoadPoints.Clear();
        for (var x = 0; x < xRange; x++)
        {
            for (var y = 0; y < yRange; y++)
            {
                if (splatmapData[x, y, 1] > 0.5f)
                {
                    var roadPt = new Vector3(
                        terrainPosX + ((float)x / xRange * terrainSizeX),
                        terrainData.GetInterpolatedHeight((float)x / xRange, (float)y / yRange),
                        terrainPosZ + ((float)y / yRange * terrainSizeZ)
                    );
                    RoadPoints.Add(roadPt);
                }
            }
        }

        var newTerrain = CloneTerrain(TerrainParent);
        newTerrain.name = "RoadTerrain";
        newTerrain.gameObject.transform.parent = TerrainParent.gameObject.transform;
        var holeRes = newTerrain.terrainData.holesResolution;
        var holeMap = new bool[holeRes, holeRes];

        var holeRadius = 1.0f;
        var roadPtsNoHeight = RoadPoints.Select(pt => new Vector2(pt.x, pt.z)).ToArray();
        Enumerable.Range(0, holeRes).AsParallel().ForAll(x =>
        {
            for (var y = 0; y < holeRes; y++)
            {
                var xHole = terrainPosX + ((float)x / holeRes * terrainSizeX);
                var zHole = terrainPosZ + ((float)y / holeRes * terrainSizeZ);
                var holePt = new Vector2(xHole, zHole);

                holeMap[x, y] = false;
                foreach (var roadPt in roadPtsNoHeight)
                {
                    if (Vector2.Distance(holePt, roadPt) < holeRadius)
                    {
                        holeMap[x, y] = true;
                        break;
                    }
                }
            }
        });

        newTerrain.terrainData.SetHoles(0, 0, holeMap);

        for (var x = 0; x < holeRes; x++)
        {
            for (var y = 0; y < holeRes; y++)
            {
                holeMap[x, y] = !holeMap[x, y];
            }
        }
        terrainData.SetHoles(0, 0, holeMap);
    }

    // private void OnDrawGizmos()
    // {
    //     Gizmos.color = Color.red;
    //     var radius = 0.3f;

    //     for (var i = 0; i < RoadPoints.Count; i++)
    //     {
    //         Gizmos.DrawSphere(RoadPoints[i], radius);

    //         if (i < RoadPoints.Count - 1)
    //         {
    //             Gizmos.color = Color.yellow;
    //             Gizmos.DrawLine(RoadPoints[i], RoadPoints[i + 1]);
    //             Gizmos.color = Color.red;
    //         }
    //     }
    // }

    private Terrain CloneTerrain(Terrain terrain)
    {
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

        newTerrain.transform.position = terrain.transform.position;
        // newTerrain.drawHeightmap = false;
        // newTerrain.materialTemplate = null;

        var origAlphaMaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
        newTerrainData.terrainLayers = terrainData.terrainLayers;
        newTerrainData.alphamapResolution = terrainData.alphamapResolution;
        newTerrainData.SetAlphamaps(0, 0, origAlphaMaps);

        return newTerrain;
    }
#endif
}
