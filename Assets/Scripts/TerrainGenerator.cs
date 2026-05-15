using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Procedurally builds the mission heightmap and bakes it into the Terrain's
// TerrainData asset. Deterministic: the same Seed + parameters always produce
// the same landscape, so spawns, landmarks and scripted beats stay reliable.
//
// The parameters + Seed are the real source of truth (version-controlled on
// this component); the baked heightmap in New Terrain.asset is the artifact,
// like the cooked plane model.
//
// Romanian "rolling hills throughout" composition:
//   continental base  -> broad plateau vs lowland (Transylvanian plateau feel)
// + rolling hills      -> the dominant character (Subcarpathian "dealuri")
// + Carpathian arc     -> a ridged band crossing the map (taller ridgelines)
// + domain warp        -> de-grids everything, cheap eroded look
// - river valley        -> a meandering low-flight corridor / landmark
// + flattened spawn pad -> keeps takeoff + the 200-500 m spawn shell clear
//
// Surface texturing is baked in the same pass: a splatmap blends grass ->
// rock -> snow by world altitude, with steep faces forced toward rock so
// cliffs never read as grass/snow. Missing layers are auto-generated as
// solid-colour placeholder assets (swap their diffuse for real art later).
[RequireComponent(typeof(Terrain))]
public class TerrainGenerator : MonoBehaviour
{
    public enum HeightmapRes { Res513 = 513, Res1025 = 1025, Res2049 = 2049, Res4097 = 4097 }
    public enum AlphamapRes { Res256 = 256, Res512 = 512, Res1024 = 1024, Res2048 = 2048 }

    [Header("Target")]
    [Tooltip("Terrain to bake into. Auto-filled from this GameObject.")]
    public Terrain TargetTerrain;

    [Header("Determinism")]
    public int Seed = 12345;

    [Header("Terrain Size (metres)")]
    public float SizeX = 10000f;
    public float SizeZ = 10000f;
    [Tooltip("World height range (TerrainData.size.y). Hills fill the lower band; this is just the ceiling.")]
    public float HeightMetres = 600f;
    [Tooltip("Heightmap grid resolution. 2049 ≈ 4.9 m/sample at 10 km — invisible from a fast plane, ~17 MB asset.")]
    public HeightmapRes Resolution = HeightmapRes.Res2049;

    [Header("Plains Floor")]
    [Tooltip("Base elevation of the lowlands (metres). Leaves headroom to carve the river below it.")]
    public float BaseElevation = 60f;

    [Header("Continental Base")]
    public float ContinentalRelief = 120f;
    public float ContinentalWavelength = 6000f;

    [Header("Rolling Hills (dominant)")]
    [Tooltip("Peak-to-trough hill relief in metres. 150-300 is the agreed 'rolling hills' band.")]
    public float HillRelief = 220f;
    public float HillWavelength = 900f;
    [Range(1, 6)] public int HillOctaves = 4;
    [Range(1.5f, 3f)] public float HillLacunarity = 2f;
    [Range(0.3f, 0.7f)] public float HillGain = 0.5f;

    [Header("Carpathian Arc")]
    public bool EnableCarpathians = true;
    [Tooltip("Extra height on the ridge crest (metres).")]
    public float RidgeRelief = 350f;
    public float RidgeWavelength = 1400f;
    [Tooltip("Half-width of the mountain band (metres). The band fades out beyond this.")]
    public float BandWidth = 2500f;
    [Tooltip("How far the arc bows across the map, as a fraction of SizeZ.")]
    [Range(0f, 0.5f)] public float ArcCurvature = 0.22f;

    [Header("Domain Warp")]
    [Tooltip("World-space jitter applied to sample coords so nothing is grid-aligned.")]
    public float WarpStrength = 140f;
    public float WarpWavelength = 1800f;

    [Header("River Valley")]
    public bool EnableRiver = true;
    public float RiverDepth = 50f;
    public float RiverWidth = 220f;
    [Tooltip("Lateral meander amplitude (metres) and wavelength (metres along Z).")]
    public float RiverMeander = 1200f;
    public float RiverMeanderWavelength = 4000f;

    [Header("Spawn Pad (kept flat for takeoff + spawn shell)")]
    [Tooltip("World XZ centre of the flat pad (terrain-local; (0,0) = SW corner). Default = map centre.")]
    public Vector2 FlattenCentre = new(5000f, 5000f);
    public float FlattenRadius = 700f;
    [Tooltip("Smooth blend ring outside the flat radius (metres).")]
    public float FlattenBlend = 600f;

    [Header("Surface Texturing (altitude + slope)")]
    public bool ApplyTexturing = true;
    [Tooltip("Splatmap grid resolution. 512 is plenty for band/slope blends seen from a plane.")]
    public AlphamapRes Resolution_Alphamap = AlphamapRes.Res512;
    [Tooltip("Low band. Auto-generated as a solid green placeholder if left empty.")]
    public TerrainLayer GrassLayer;
    [Tooltip("Mid band + all steep faces. Auto-generated as solid grey if empty.")]
    public TerrainLayer RockLayer;
    [Tooltip("High band. Auto-generated as solid white if empty.")]
    public TerrainLayer SnowLayer;
    [Tooltip("World altitude (m) of the grass->rock crossover; ± blend each side.")]
    public float GrassRockAltitude = 170f;
    public float GrassRockBlend = 60f;
    [Tooltip("World altitude (m) of the rock->snow crossover; ± blend each side.")]
    public float RockSnowAltitude = 330f;
    public float RockSnowBlend = 70f;
    [Tooltip("Slopes steeper than this (degrees) blend toward rock regardless of altitude.")]
    [Range(0f, 90f)] public float CliffAngle = 34f;
    [Tooltip("Soft range (degrees) over which the cliff->rock blend ramps in.")]
    public float CliffBlend = 12f;

    // --- Generation ---------------------------------------------------------

    public void Generate()
    {
        if (TargetTerrain == null) TargetTerrain = GetComponent<Terrain>();
        if (TargetTerrain == null || TargetTerrain.terrainData == null)
        {
            Debug.LogError("[TerrainGenerator] No Terrain / TerrainData to bake into.", this);
            return;
        }

        var data = TargetTerrain.terrainData;
        int res = (int)Resolution;

        // heightmapResolution must be set before size; it also resets heights.
        data.heightmapResolution = res;
        data.size = new Vector3(SizeX, HeightMetres, SizeZ);

        // Seed -> stable, well-separated noise offsets (PerlinNoise has no seed).
        var rng = new System.Random(Seed);
        Vector2 oCont = RandOffset(rng), oHill = RandOffset(rng);
        Vector2 oWarpX = RandOffset(rng), oWarpZ = RandOffset(rng);
        Vector2 oRidge = RandOffset(rng), oArc = RandOffset(rng);

        // heights[z, x], normalised 0..1 over HeightMetres.
        var heights = new float[res, res];
        float invH = 1f / Mathf.Max(1f, HeightMetres);

        for (int zi = 0; zi < res; zi++)
        {
            // World metres at this grid line (terrain-local: 0..Size).
            float wz = zi / (float)(res - 1) * SizeZ;

            for (int xi = 0; xi < res; xi++)
            {
                float wx = xi / (float)(res - 1) * SizeX;

                // Domain warp: perturb the sample point.
                float warpX = (Fbm(wx, wz, 1f / WarpWavelength, 2, 2f, 0.5f, oWarpX) - 0.5f) * 2f;
                float warpZ = (Fbm(wx, wz, 1f / WarpWavelength, 2, 2f, 0.5f, oWarpZ) - 0.5f) * 2f;
                float sx = wx + warpX * WarpStrength;
                float sz = wz + warpZ * WarpStrength;

                // Metres of elevation, accumulated.
                float h = BaseElevation;

                // Continental base: broad plateau vs lowland.
                h += (Fbm(sx, sz, 1f / ContinentalWavelength, 2, 2f, 0.5f, oCont) - 0.5f)
                     * 2f * ContinentalRelief;

                // Rolling hills: the dominant terrain character.
                h += (Fbm(sx, sz, 1f / HillWavelength, HillOctaves, HillLacunarity, HillGain, oHill)
                      - 0.5f) * 2f * HillRelief;

                // Carpathian arc: ridged noise gated by a curved band mask.
                if (EnableCarpathians)
                    h += CarpathianBand(sx, sz, oRidge, oArc) * RidgeRelief;

                // River valley: carve a meandering channel below the floor.
                if (EnableRiver)
                    h -= RiverCarve(wx, wz);

                // Flatten the spawn pad so takeoff + the spawn shell stay clear.
                h = ApplyFlattenPad(wx, wz, h);

                heights[zi, xi] = Mathf.Clamp01(h * invH);
            }
        }

        data.SetHeights(0, 0, heights);
        Debug.Log($"[TerrainGenerator] Baked {res}x{res} heightmap " +
                  $"({SizeX}x{SizeZ} m, seed {Seed}).", this);

        if (ApplyTexturing) BakeSplatmap(data);
    }

    // --- Surface texturing --------------------------------------------------

    // Blend grass -> rock -> snow by altitude, then pull steep faces toward
    // rock. Reads the *baked* terrain back via GetInterpolatedHeight /
    // GetSteepness so the splat tracks the real surface (incl. warp, river,
    // flatten pad) at the alphamap's own resolution.
    void BakeSplatmap(TerrainData data)
    {
        var layers = ResolveLayers();
        if (layers == null) return;          // missing layers, warning already logged
        data.terrainLayers = layers;

        int ar = (int)Resolution_Alphamap;
        data.alphamapResolution = ar;
        var maps = new float[ar, ar, 3];

        for (int y = 0; y < ar; y++)
        {
            float v = y / (float)(ar - 1);   // normalised Z (terrain length)
            for (int x = 0; x < ar; x++)
            {
                float u = x / (float)(ar - 1);          // normalised X (width)
                float alt = data.GetInterpolatedHeight(u, v);   // metres
                float steep = data.GetSteepness(u, v);          // degrees

                // Altitude bands with smooth crossfades.
                float s1 = SStep(GrassRockAltitude - GrassRockBlend,
                                 GrassRockAltitude + GrassRockBlend, alt);
                float s2 = SStep(RockSnowAltitude - RockSnowBlend,
                                 RockSnowAltitude + RockSnowBlend, alt);
                float wGrass = 1f - s1;
                float wRock = Mathf.Clamp01(s1 - s2);
                float wSnow = s2;

                // Steep faces -> rock, fading in across CliffBlend degrees.
                float cf = SStep(CliffAngle, CliffAngle + CliffBlend, steep);
                wGrass = Mathf.Lerp(wGrass, 0f, cf);
                wRock = Mathf.Lerp(wRock, 1f, cf);
                wSnow = Mathf.Lerp(wSnow, 0f, cf);

                float sum = wGrass + wRock + wSnow;
                if (sum < 1e-5f) { wRock = 1f; sum = 1f; }
                maps[y, x, 0] = wGrass / sum;
                maps[y, x, 1] = wRock / sum;
                maps[y, x, 2] = wSnow / sum;
            }
        }

        data.SetAlphamaps(0, 0, maps);
        Debug.Log($"[TerrainGenerator] Baked {ar}x{ar} splatmap " +
                  $"(grass<{GrassRockAltitude}m, snow>{RockSnowAltitude}m, " +
                  $"cliff>{CliffAngle}°).", this);
    }

    // Smoothstep over [a, b] -> 0..1 (handles a == b without dividing by zero).
    static float SStep(float a, float b, float x) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

    // Returns the 3 layers in [grass, rock, snow] order. In the editor any
    // unassigned slot is created as a committed solid-colour placeholder
    // asset; at runtime it falls back to a transient in-memory layer.
    TerrainLayer[] ResolveLayers()
    {
#if UNITY_EDITOR
        const string root = "Assets/Data";
        const string dir = root + "/Terrain";
        if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(root))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Data");
            UnityEditor.AssetDatabase.CreateFolder(root, "Terrain");
        }
        if (GrassLayer == null) GrassLayer = MakeLayerAsset(dir, "Terrain.Grass", new Color(0.36f, 0.52f, 0.22f));
        if (RockLayer == null) RockLayer = MakeLayerAsset(dir, "Terrain.Rock", new Color(0.45f, 0.42f, 0.38f));
        if (SnowLayer == null) SnowLayer = MakeLayerAsset(dir, "Terrain.Snow", new Color(0.92f, 0.93f, 0.96f));
#else
        if (GrassLayer == null) GrassLayer = MakeLayerTransient(new Color(0.36f, 0.52f, 0.22f));
        if (RockLayer == null) RockLayer = MakeLayerTransient(new Color(0.45f, 0.42f, 0.38f));
        if (SnowLayer == null) SnowLayer = MakeLayerTransient(new Color(0.92f, 0.93f, 0.96f));
#endif
        if (GrassLayer == null || RockLayer == null || SnowLayer == null)
        {
            Debug.LogWarning("[TerrainGenerator] Texturing skipped: assign Grass/Rock/Snow layers.", this);
            return null;
        }
        return new[] { GrassLayer, RockLayer, SnowLayer };
    }

    static Texture2D SolidTex(Color c)
    {
        var t = new Texture2D(8, 8, TextureFormat.RGB24, false);
        var px = new Color[64];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    TerrainLayer MakeLayerTransient(Color c) =>
        new() { diffuseTexture = SolidTex(c), tileSize = new Vector2(60f, 60f) };

#if UNITY_EDITOR
    // Creates (or reuses) a committed .terrainlayer + solid .png so the bake
    // is self-contained and version-controlled, like the cooked heightmap.
    TerrainLayer MakeLayerAsset(string dir, string name, Color c)
    {
        string layerPath = $"{dir}/{name}.terrainlayer";
        var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (existing != null) return existing;

        string texPath = $"{dir}/{name}.png";
        var tex = SolidTex(c);
        System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
        DestroyImmediate(tex);
        UnityEditor.AssetDatabase.ImportAsset(texPath);
        var diffuse = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        var layer = new TerrainLayer { diffuseTexture = diffuse, tileSize = new Vector2(60f, 60f) };
        UnityEditor.AssetDatabase.CreateAsset(layer, layerPath);
        return layer;
    }
#endif

#if UNITY_EDITOR
    // Generate, then persist the modified TerrainData asset to disk so the
    // bake is committed (deterministic mission, no runtime hitch).
    public void GenerateAndSave()
    {
        Generate();
        if (TargetTerrain == null || TargetTerrain.terrainData == null) return;
        EditorUtility.SetDirty(TargetTerrain.terrainData);
        EditorUtility.SetDirty(this);   // keep auto-assigned layer refs
        AssetDatabase.SaveAssets();
        Debug.Log("[TerrainGenerator] TerrainData asset saved.", this);
    }

    [ContextMenu("Generate (preview, no save)")]
    void CtxGenerate() => Generate();

    [ContextMenu("Generate & Save Asset")]
    void CtxGenerateAndSave() => GenerateAndSave();
#endif

    // --- Noise --------------------------------------------------------------

    Vector2 RandOffset(System.Random rng) =>
        new((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);

    // Fractal Brownian motion in ~0..1 (octaves of Perlin, offset by seed).
    static float Fbm(float x, float z, float freq, int octaves,
                     float lacunarity, float gain, Vector2 offset)
    {
        float sum = 0f, amp = 1f, max = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += Mathf.PerlinNoise(offset.x + x * freq, offset.y + z * freq) * amp;
            max += amp;
            amp *= gain;
            freq *= lacunarity;
        }
        return sum / max;
    }

    // Ridged multifractal in 0..1 (sharp crests), gated by a curved band so
    // the mountains read as a single Carpathian arc rather than blanket peaks.
    float CarpathianBand(float x, float z, Vector2 oRidge, Vector2 oArc)
    {
        // The arc's centreline bows across X as a function of Z.
        float t = z / Mathf.Max(1f, SizeZ);
        float arcWobble = (Fbm(0f, z, 1f / (SizeZ * 0.5f), 2, 2f, 0.5f, oArc) - 0.5f) * 2f;
        float centreX = SizeX * 0.5f
                        + Mathf.Sin(t * Mathf.PI) * SizeZ * ArcCurvature
                        + arcWobble * BandWidth * 0.6f;

        float d = Mathf.Abs(x - centreX);
        if (d >= BandWidth) return 0f;
        // 1 at the spine, smooth to 0 at the band edge.
        float mask = 1f - Mathf.SmoothStep(0f, 1f, d / BandWidth);

        float n = Fbm(x, z, 1f / RidgeWavelength, 4, 2f, 0.5f, oRidge);
        float ridged = 1f - Mathf.Abs(n * 2f - 1f);
        ridged *= ridged; // sharpen crests
        return ridged * mask;
    }

    // Metres to subtract for the river: a smooth V centred on a meandering
    // path that snakes along +Z.
    float RiverCarve(float x, float z)
    {
        float pathX = SizeX * 0.32f
                      + Mathf.Sin(z / Mathf.Max(1f, RiverMeanderWavelength) * Mathf.PI * 2f)
                        * RiverMeander;
        float d = Mathf.Abs(x - pathX);
        if (d >= RiverWidth) return 0f;
        return (1f - Mathf.SmoothStep(0f, 1f, d / RiverWidth)) * RiverDepth;
    }

    // Blend the terrain toward BaseElevation inside FlattenRadius, easing out
    // across FlattenBlend so the spawn pad is flat without a hard rim.
    float ApplyFlattenPad(float x, float z, float h)
    {
        float d = Vector2.Distance(new Vector2(x, z), FlattenCentre);
        if (d <= FlattenRadius) return BaseElevation;
        if (d >= FlattenRadius + FlattenBlend) return h;
        float k = Mathf.SmoothStep(0f, 1f, (d - FlattenRadius) / FlattenBlend);
        return Mathf.Lerp(BaseElevation, h, k);
    }

    // --- Editor viz ---------------------------------------------------------

    void OnDrawGizmosSelected()
    {
        var t = TargetTerrain != null ? TargetTerrain : GetComponent<Terrain>();
        if (t == null) return;
        var o = t.transform.position;

        // Terrain footprint.
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
        Vector3 a = o, b = o + new Vector3(SizeX, 0, 0);
        Vector3 c = o + new Vector3(SizeX, 0, SizeZ), e = o + new Vector3(0, 0, SizeZ);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, e); Gizmos.DrawLine(e, a);

        // Spawn pad (flat radius + blend ring).
        var padCentre = o + new Vector3(FlattenCentre.x, BaseElevation, FlattenCentre.y);
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.7f);
        DrawCircle(padCentre, FlattenRadius);
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.25f);
        DrawCircle(padCentre, FlattenRadius + FlattenBlend);
    }

    static void DrawCircle(Vector3 centre, float radius, int seg = 64)
    {
        var step = Mathf.PI * 2f / seg;
        var prev = centre + new Vector3(radius, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            var next = centre + new Vector3(Mathf.Cos(i * step) * radius, 0,
                                            Mathf.Sin(i * step) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
