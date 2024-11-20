using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
[CustomEditor(typeof(TMK_TerrainGenerator))]
public class TMK_TerrainGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var terrainGenerator = (TMK_TerrainGenerator)target;

        if (GUILayout.Button("Generate Terrain"))
        {
            terrainGenerator.GenerateTerrain();
        }

        base.OnInspectorGUI();
    }
}
#endif
