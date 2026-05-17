using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

[CustomEditor(typeof(RiverValleyPath))]
public class RiverValleyPathEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var path = (RiverValleyPath)target;
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Draw the route with the native Spline tool: keep this object " +
            "selected, pick the Spline tool in the toolbar, then click in the " +
            "Scene View to lay knots down the valley. TerrainGenerator carves " +
            "along this curve and gameplay reads the same route.",
            MessageType.Info);

        if (GUILayout.Button("Lay Starter Route (gentle S down the map)",
                              GUILayout.Height(28)))
            LayStarterRoute(path);

        using (new EditorGUI.DisabledScope(!path.HasPath))
            if (GUILayout.Button("Rebuild Cache & Preview"))
            {
                path.RebuildCache();
                SceneView.RepaintAll();
            }

        if (path.HasPath)
            EditorGUILayout.LabelField("Route length",
                $"{path.Length:N0} m  ({path.PolylineXZ.Count} cached pts)");
    }

    // Seeds the spline with a few knots down +Z, spanning the terrain footprint if present.
    static void LayStarterRoute(RiverValleyPath path)
    {
        var container = path.Container;
        if (container == null) return;

        var gen = FindFirstObjectByType<TerrainGenerator>();
        Vector3 origin = gen != null && gen.TargetTerrain != null
            ? gen.TargetTerrain.transform.position : Vector3.zero;
        float sizeX = gen != null ? gen.SizeX : 4000f;
        float sizeZ = gen != null ? gen.SizeZ : 4000f;
        float y = origin.y + (gen != null ? gen.BaseElevation : 60f) + 5f;

        // Five knots, gentle lateral wander around ~0.4 * SizeX.
        float[] zN = { 0.06f, 0.30f, 0.50f, 0.72f, 0.94f };
        float[] xN = { 0.38f, 0.30f, 0.46f, 0.34f, 0.42f };

        Undo.RecordObject(container, "Lay Starter River Route");
        var spline = container.Spline;
        spline.Clear();
        for (int i = 0; i < zN.Length; i++)
        {
            Vector3 world = new(origin.x + xN[i] * sizeX, y,
                               origin.z + zN[i] * sizeZ);
            spline.Add(container.transform.InverseTransformPoint(world),
                       TangentMode.AutoSmooth);
        }
        path.RebuildCache();
        EditorUtility.SetDirty(container);
        SceneView.RepaintAll();
    }
}
