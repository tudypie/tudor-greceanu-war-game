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
[RequireComponent(typeof(Terrain))]
public class TerrainGenerator : MonoBehaviour
{
    public enum HeightmapRes { Res513 = 513, Res1025 = 1025, Res2049 = 2049, Res4097 = 4097 }

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
    }

#if UNITY_EDITOR
    // Generate, then persist the modified TerrainData asset to disk so the
    // bake is committed (deterministic mission, no runtime hitch).
    public void GenerateAndSave()
    {
        Generate();
        if (TargetTerrain == null || TargetTerrain.terrainData == null) return;
        EditorUtility.SetDirty(TargetTerrain.terrainData);
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
