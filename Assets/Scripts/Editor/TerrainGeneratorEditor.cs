using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var gen = (TerrainGenerator)target;
        EditorGUILayout.Space();

        if (GUILayout.Button("Generate (preview, no save)", GUILayout.Height(28)))
        {
            gen.Generate();
        }

        if (GUILayout.Button("Generate & Save Asset", GUILayout.Height(32)))
        {
            gen.GenerateAndSave();
        }

        EditorGUILayout.HelpBox(
            "Preview rebuilds the heightmap in memory so you can iterate on " +
            "Seed/amplitude live. Save bakes it into New Terrain.asset (commit that).",
            MessageType.Info);
    }
}
