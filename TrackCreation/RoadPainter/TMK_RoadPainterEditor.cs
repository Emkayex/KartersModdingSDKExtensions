using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
[CustomEditor(typeof(TMK_RoadPainter))]
public class TMK_RoadPainterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var roadMeshGenerator = (TMK_RoadPainter)target;

        if (GUILayout.Button("Generate Road Mesh"))
        {
            roadMeshGenerator.GenerateRoadMesh();
        }
        if (GUILayout.Button("Reset Holes"))
        {
            roadMeshGenerator.ResetHoleMap();
        }

        base.OnInspectorGUI();
    }
}
#endif
