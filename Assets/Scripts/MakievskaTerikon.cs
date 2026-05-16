using System.Collections.Generic;
using UnityEngine;

// MISSION 1 ONLY (Makievska). Donbas "terikoane" — the conical coal-mine
// spoil tips that are the single most recognisable man-made landmark of the
// Makiivka/Donetsk steppe. They do three jobs on the flat playing field:
//   * navigation references on an otherwise featureless map,
//   * hard cover: each cone has a MeshCollider, so PlaneShooter hitscan and
//     the AI's HasShotLineOfSight raycast are both genuinely masked by it —
//     diving behind a terikon actually breaks a gun solution,
//   * a low-altitude hazard that rewards reading the terrain.
//
// Self-contained and additive: this script is referenced ONLY by the Makievska
// scene, builds its own cone meshes + a dark spoil material at runtime, and
// snaps each cone onto the active Terrain. Nothing in any shared script or
// other scene is touched. If the Cones list is left empty it falls back to a
// baked default cluster around the airfield + the eastern ingress lane.
//
// NOTE: the fighter AI senses ground via Terrain.SampleHeight only, so it does
// NOT avoid these mesh cones — keep them as field landmarks/cover, not tall
// obstacles dropped into the middle of the furball, and a careless AI clipping
// one is acceptable flavour.
[ExecuteAlways]
public class MakievskaTerikon : MonoBehaviour
{
    [System.Serializable]
    public struct Cone
    {
        [Tooltip("Terrain-local XZ (metres, 0..TerrainSize). The base is snapped onto the terrain surface here.")]
        public Vector2 PosXZ;
        [Tooltip("Cone height in metres above the sampled ground.")]
        public float Height;
        [Tooltip("Base radius in metres. ~Height gives the steep ~42° spoil-tip angle of repose.")]
        public float BaseRadius;
    }

    [Tooltip("Leave empty to use the baked default cluster (airfield + eastern ingress).")]
    public List<Cone> Cones = new();

    [Header("Build")]
    [Range(8, 48)] public int RadialSegments = 20;
    [Range(1, 6)] public int HeightRings = 3;
    [Tooltip("Per-vertex radial jitter (fraction of radius) so cones read as eroded spoil heaps, not perfect geometry. Seeded — deterministic.")]
    [Range(0f, 0.25f)] public float Roughness = 0.09f;
    [Tooltip("Metres the base skirt is pushed below the sampled ground so the cone never floats on a slope.")]
    public float BaseSink = 8f;
    public int Seed = 84832;
    [Tooltip("Dark desaturated spoil-heap colour (weathered shale/coal waste).")]
    public Color SpoilColor = new(0.17f, 0.15f, 0.13f);

    Terrain _terrain;
    readonly List<GameObject> _built = new();

    static readonly Cone[] DefaultCluster =
    {
        // x,z are terrain-local; cluster hugs the airfield (player spawn ~399,396)
        // and steps east along the enemy ingress lane.
        new() { PosXZ = new Vector2(720f,  680f), Height = 58f, BaseRadius = 56f },
        new() { PosXZ = new Vector2(1180f, 520f), Height = 76f, BaseRadius = 70f },
        new() { PosXZ = new Vector2(520f,  1080f), Height = 50f, BaseRadius = 50f },
        new() { PosXZ = new Vector2(1520f, 920f), Height = 88f, BaseRadius = 80f },
        new() { PosXZ = new Vector2(980f,  1480f), Height = 54f, BaseRadius = 54f },
    };

    void OnEnable() => Rebuild();

    [ContextMenu("Rebuild Terikoane")]
    public void Rebuild()
    {
        Clear();
        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();

        var mat = MakeSpoilMaterial();
        var src = (Cones != null && Cones.Count > 0) ? Cones.ToArray() : DefaultCluster;

        for (int i = 0; i < src.Length; i++)
            _built.Add(BuildOne(src[i], i, mat));
    }

    void Clear()
    {
        foreach (var go in _built)
            if (go != null) DestroyImmediateSafe(go);
        _built.Clear();

        // Also sweep any leftover children from a previous build (editor reloads).
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (c != null && c.name.StartsWith("Terikon_"))
                DestroyImmediateSafe(c.gameObject);
        }
    }

    static void DestroyImmediateSafe(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    GameObject BuildOne(Cone c, int index, Material mat)
    {
        var go = new GameObject($"Terikon_{index}");
        go.transform.SetParent(transform, false);

        float groundY = 0f;
        var local = new Vector3(c.PosXZ.x, 0f, c.PosXZ.y);
        if (_terrain != null)
        {
            var world = _terrain.transform.position + local;
            groundY = _terrain.transform.position.y + _terrain.SampleHeight(world);
            go.transform.position = new Vector3(world.x, groundY - BaseSink, world.z);
        }
        else
        {
            go.transform.localPosition = local - Vector3.up * BaseSink;
        }

        var mesh = BuildConeMesh(
            Mathf.Max(c.Height, 1f) + BaseSink,
            Mathf.Max(c.BaseRadius, 1f),
            index);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    // Radially-segmented cone, apex up, open base (the terrain hides it).
    // Per-ring deterministic jitter gives an eroded spoil-heap silhouette.
    Mesh BuildConeMesh(float height, float radius, int salt)
    {
        int seg = Mathf.Clamp(RadialSegments, 8, 48);
        int rings = Mathf.Clamp(HeightRings, 1, 6);
        var rng = new System.Random(Seed + salt * 7919);

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        // Ring 0 = base, ring `rings` = apex.
        for (int r = 0; r <= rings; r++)
        {
            float t = r / (float)rings;            // 0 base -> 1 apex
            float ringR = radius * (1f - t);
            float ringY = height * t;
            for (int s = 0; s < seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2f;
                float jitter = 1f + ((float)rng.NextDouble() - 0.5f) * 2f * Roughness * (1f - t);
                float rr = ringR * jitter;
                verts.Add(new Vector3(Mathf.Cos(a) * rr, ringY, Mathf.Sin(a) * rr));
                norms.Add(new Vector3(Mathf.Cos(a), 0.35f, Mathf.Sin(a)).normalized);
            }
        }

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < seg; s++)
            {
                int s1 = (s + 1) % seg;
                int a = r * seg + s;
                int b = r * seg + s1;
                int cc = (r + 1) * seg + s;
                int d = (r + 1) * seg + s1;
                tris.Add(a); tris.Add(cc); tris.Add(b);
                tris.Add(b); tris.Add(cc); tris.Add(d);
            }
        }

        var mesh = new Mesh { name = "TerikonCone" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    Material MakeSpoilMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Sprites/Default");
        var m = new Material(shader) { name = "TerikonSpoil" };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", SpoilColor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", SpoilColor);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
        return m;
    }

    void OnDrawGizmosSelected()
    {
        var src = (Cones != null && Cones.Count > 0) ? Cones.ToArray() : DefaultCluster;
        var t = Terrain.activeTerrain;
        Gizmos.color = new Color(0.6f, 0.45f, 0.3f, 0.8f);
        foreach (var c in src)
        {
            var local = new Vector3(c.PosXZ.x, 0f, c.PosXZ.y);
            Vector3 baseP = t != null
                ? t.transform.position + new Vector3(local.x, t.SampleHeight(t.transform.position + local), local.z)
                : local;
            Gizmos.DrawLine(baseP, baseP + Vector3.up * c.Height);
            Gizmos.DrawWireSphere(baseP + Vector3.up * c.Height, c.BaseRadius * 0.12f);
        }
    }
}
