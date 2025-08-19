using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[ExecuteInEditMode]
public class TMK_MinimapPlaneGenerator : MonoBehaviour
{
    private class DataForThreadExecution
    {
        public Vector3 TerrainSize { get; set; }
        public int HoleRes { get; set; }
        public bool[,] HoleMap { get; set; }
    }

    private class ThreadReturnData
    {
        public Vector3[] Vertices { get; set; }
        public int[] Triangles { get; set; }
    }

    private class WrappedValue<T>
    {
        public T Value { get; set; }

        public WrappedValue(T value)
        {
            Value = value;
        }
    }

    public List<Terrain> Terrains;

#if UNITY_EDITOR
    public void GeneratePlanes()
    {
        var datas = Terrains.Select(t => (t, new DataForThreadExecution
        {
            TerrainSize = t.terrainData.size,
            HoleRes = t.terrainData.holesResolution,
            HoleMap = t.terrainData.GetHoles(0, 0, t.terrainData.holesResolution, t.terrainData.holesResolution)
        })).ToArray();

        var results = datas.AsParallel().Select(tpl => (tpl.t, GetDataForPlane(tpl.Item2))).ToArray();
        foreach (var (terrain, result) in results)
        {
            var mesh = new Mesh
            {
                vertices = result.Vertices,
                triangles = result.Triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mapMesh = new GameObject("TerrainMap", typeof(MeshFilter), typeof(MeshRenderer));
            mapMesh.transform.parent = terrain.gameObject.transform;
            mapMesh.transform.position = terrain.gameObject.transform.position;
            mapMesh.GetComponent<MeshFilter>().mesh = mesh;
            mapMesh.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
        }
    }

    private ThreadReturnData GetDataForPlane(DataForThreadExecution data)
    {
        // Get the hole map and then start generating a plane with no vertices where there are holes in the terrain
        var terrainSize = data.TerrainSize;
        var holeRes = data.HoleRes;
        var holeMap = data.HoleMap;

        // Create arrays for the plane data
        var planeVerts = new Vector3[holeRes, holeRes];
        var planeUvs = new Vector2[holeRes, holeRes];

        // Go through each point in the hole map data and create vertices in world space coordinates
        for (var x = 0; x < holeRes; x++)
        {
            for (var y = 0; y < holeRes; y++)
            {
                // Calculate the world space position of this hole map point by using a percent of the terrain size
                // The plane will be flat, so the height will always be 0
                var zWorldPos = terrainSize.x * (1.0f * x / (holeRes - 1));
                var xWorldPos = terrainSize.z * (1.0f * y / (holeRes - 1));
                planeVerts[x, y] = new Vector3(xWorldPos, 0, zWorldPos);
                // planeUvs[x, y] = new Vector2(1.0f * x / holeRes, 1.0f * y / holeRes);
                planeUvs[x, y] = new Vector2(1.0f * x / (holeRes - 1), 1.0f * y / (holeRes - 1));
            }
        }

        // Go through each vertex and associated hole, and if any corner of a triangle is a hole, do not add a triangle
        var tris = new List<int>();
        for (var x = 0; x < (holeRes - 1); x++)
        {
            for (var y = 0; y < (holeRes - 1); y++)
            {
                if (!holeMap[x, y] || !holeMap[x + 1, y] || !holeMap[x, y + 1] || !holeMap[x + 1, y + 1])
                {
                    continue;
                }

                var i00 = x * holeRes + y;
                var i01 = (x + 1) * holeRes + y;
                var i10 = x * holeRes + y + 1;
                var i11 = (x + 1) * holeRes + y + 1;

                tris.Add(i00);
                tris.Add(i11);
                tris.Add(i10);

                tris.Add(i00);
                tris.Add(i01);
                tris.Add(i11);
            }
        }

        var flattenedVerts = FlattenArray(planeVerts).ToList();

        var optDict = tris.Distinct().ToDictionary(t => t, _ => new WrappedValue<int>(0));
        for (var i = flattenedVerts.Count - 1; i >= 0; i--)
        {
            if (!optDict.ContainsKey(i))
            {
                flattenedVerts.RemoveAt(i);
                foreach (var key in optDict.Keys.Where(key => key > i))
                {
                    optDict[key].Value += 1;
                }
            }
        }

        for (var i = 0; i < tris.Count; i++)
        {
            tris[i] -= optDict[tris[i]].Value;
        }

        return new ThreadReturnData
        {
            Vertices = flattenedVerts.ToArray(),
            Triangles = tris.ToArray()
        };
    }

    private static T[] FlattenArray<T>(T[,] array)
    {
        var xDim = array.GetLength(0);
        var yDim = array.GetLength(1);
        var newArray = new T[xDim * yDim];
        for (var x = 0; x < xDim; x++)
        {
            for (var y = 0; y < yDim; y++)
            {
                newArray[(x * yDim) + y] = array[x, y];
            }
        }

        return newArray;
    }
#endif
}
