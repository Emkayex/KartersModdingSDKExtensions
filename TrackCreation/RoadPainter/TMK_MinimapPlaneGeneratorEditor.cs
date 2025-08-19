using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
[CustomEditor(typeof(TMK_MinimapPlaneGenerator))]
public class TMK_MinimapPlaneGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var generator = (TMK_MinimapPlaneGenerator)target;

        if (GUILayout.Button("Generate Road Planes"))
        {
            generator.GeneratePlanes();
        }

        base.OnInspectorGUI();
    }
}
#endif
