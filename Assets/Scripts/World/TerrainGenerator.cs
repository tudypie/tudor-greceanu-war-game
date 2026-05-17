using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Procedurally bakes the mission heightmap + splatmap into the Terrain's
// TerrainData. Deterministic: same Seed + parameters -> same landscape, so
// spawns and scripted beats stay reliable. The params + Seed on this component
// are the source of truth; the baked asset is the artifact, like a cooked model.
[RequireComponent(typeof(Terrain))]
public class TerrainGenerator : MonoBehaviour
{
    public enum HeightmapRes { Res513 = 513, Res1025 = 1025, Res2049 = 2049, Res4097 = 4097 }
    public enum AlphamapRes { Res256 = 256, Res512 = 512, Res1024 = 1024, Res2048 = 2048 }

    [Header("Target")]
    [Tooltip("Terrain to bake into. Auto-filled from this GameObject.")]
    public Terrain TargetTerrain;
    [Tooltip("Dedicated TerrainData asset name (no extension) under " +
             "Assets/Content/Terrain. Empty = the active scene's name. " +
             "Generate & Save bakes into THIS asset only and repoints the " +
             "Terrain+Collider at it, so saving one scene can never overwrite " +
             "another's terrain — even if the scene was duplicated.")]
    public string AssetName = "";

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
    [Tooltip("Authored route the valley follows — the corridor the plane must " +
             "fly. Assign a RiverValleyPath (draw it with the Spline tool). " +
             "When set, the carve tracks this spline and the meander fields " +
             "below are ignored. Leave empty for the procedural sin() meander.")]
    public RiverValleyPath RiverPath;
    [Tooltip("Resolution of the distance field used to carve the authored " +
             "path. 513 over 10 km ≈ 19 m/texel, smooth under a 220 m valley. " +
             "Raise for a tighter route, lower for a faster bake. (No effect " +
             "on the procedural fallback.)")]
    [Range(129, 2049)] public int RiverPathFieldRes = 513;
    [Tooltip("Lateral meander amplitude (metres) and wavelength (metres along Z). " +
             "Used only by the procedural fallback (no RiverPath assigned).")]
    public float RiverMeander = 1200f;
    public float RiverMeanderWavelength = 4000f;

    // Coarse world-XZ distance-to-route field (metres, clamped to RiverWidth),
    // baked once per Generate when an authored RiverPath is in use. Bilinearly
    // sampled in the height loop so the carve costs ~4 taps, not a polyline
    // scan, per heightmap cell.
    float[,] _riverField;
    bool _riverFieldReady;

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
    [Tooltip("Low band, dominant. Auto-generated solid green if empty.")]
    public TerrainLayer GrassLayer;
    [Tooltip("Slightly yellower grass, mixed sparingly into the low band. " +
             "Auto-generated solid yellow-green if empty.")]
    public TerrainLayer DryGrassLayer;
    [Tooltip("Mid band + all steep faces. Auto-generated as solid grey if empty.")]
    public TerrainLayer RockLayer;
    [Tooltip("High band. Auto-generated as solid white if empty.")]
    public TerrainLayer SnowLayer;

    [Header("Lowland Tint Variation (subtle)")]
    [Tooltip("Wavelength (m) of the noise that varies dry-grass across the " +
             "field. Large = broad gentle areas, not patches.")]
    public float BiomeWavelength = 2600f;
    [Tooltip("Region-noise value (0..1) above which dry-grass starts mixing in.")]
    [Range(0f, 1f)] public float BiomeDryStart = 0.55f;
    [Tooltip("± soft range (0..1) over which dry-grass eases in. Wide = cleanly blended.")]
    public float BiomeBlend = 0.18f;
    [Tooltip("Max dry-grass fraction anywhere (0..1). Caps it so the field stays mostly green.")]
    [Range(0f, 1f)] public float DryGrassMax = 0.3f;

    [Header("Altitude Bands")]
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

    // Generation

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

        // Bridges world-space spline and terrain-local heightmap for the authored carve.
        Vector3 origin = TargetTerrain.transform.position;
        bool authoredRiver = EnableRiver && RiverPath != null && RiverPath.HasPath;
        if (authoredRiver) BuildRiverField(origin);

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

                // River valley: authored route if a RiverPath is set, else the sin() meander.
                if (authoredRiver)
                    h -= RiverCarveAuthored(wx, wz);
                else if (EnableRiver)
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

    // Surface texturing

    // Layer order is fixed and must match the maps[] channel indices below.
    const int LGrass = 0, LDryGrass = 1, LRock = 2, LSnow = 3;

    // Reads the baked terrain back via GetInterpolatedHeight/GetSteepness so
    // the splat tracks the real surface (warp, river, flatten pad included).
    void BakeSplatmap(TerrainData data)
    {
        var layers = ResolveLayers();
        if (layers == null) return;          // missing layers, warning already logged
        data.terrainLayers = layers;

        int ar = (int)Resolution_Alphamap;
        data.alphamapResolution = ar;
        var maps = new float[ar, ar, layers.Length];

        // Region-noise offset: deterministic from Seed but independent of the
        // height pass's RNG stream so re-baking is stable.
        Vector2 oBiome = RandOffset(new System.Random(Seed * 397 + 17));
        float biomeFreq = 1f / Mathf.Max(1f, BiomeWavelength);

        for (int y = 0; y < ar; y++)
        {
            float v = y / (float)(ar - 1);   // normalised Z (terrain length)
            float wz = v * SizeZ;
            for (int x = 0; x < ar; x++)
            {
                float u = x / (float)(ar - 1);          // normalised X (width)
                float wx = u * SizeX;
                float alt = data.GetInterpolatedHeight(u, v);   // metres
                float steep = data.GetSteepness(u, v);          // degrees

                // Altitude bands with smooth crossfades.
                float s1 = SStep(GrassRockAltitude - GrassRockBlend,
                                 GrassRockAltitude + GrassRockBlend, alt);
                float s2 = SStep(RockSnowAltitude - RockSnowBlend,
                                 RockSnowAltitude + RockSnowBlend, alt);
                float wLow = 1f - s1;                        // total low-band weight
                float wRock = Mathf.Clamp01(s1 - s2);
                float wSnow = s2;

                // Ease a capped amount of dry-grass into the low band so the field stays mostly green.
                float b = Fbm(wx, wz, biomeFreq, 2, 2f, 0.5f, oBiome);
                float dry = SStep(BiomeDryStart - BiomeBlend,
                                  BiomeDryStart + BiomeBlend, b);
                float yellowFrac = dry * Mathf.Clamp01(DryGrassMax);
                float wGreen = wLow * (1f - yellowFrac);
                float wYellow = wLow * yellowFrac;

                // Steep faces -> rock, but eased out above the snow line (s2)
                // so crests stay snow-capped instead of bare rock.
                float cf = SStep(CliffAngle, CliffAngle + CliffBlend, steep) * (1f - s2);
                wGreen = Mathf.Lerp(wGreen, 0f, cf);
                wYellow = Mathf.Lerp(wYellow, 0f, cf);
                wRock = Mathf.Lerp(wRock, 1f, cf);
                wSnow = Mathf.Lerp(wSnow, 0f, cf);

                float sum = wGreen + wYellow + wRock + wSnow;
                if (sum < 1e-5f) { wRock = 1f; sum = 1f; }
                maps[y, x, LGrass] = wGreen / sum;
                maps[y, x, LDryGrass] = wYellow / sum;
                maps[y, x, LRock] = wRock / sum;
                maps[y, x, LSnow] = wSnow / sum;
            }
        }

        data.SetAlphamaps(0, 0, maps);
        Debug.Log($"[TerrainGenerator] Baked {ar}x{ar} splatmap " +
                  $"(mostly grass, ≤{DryGrassMax:P0} dry-grass @ {BiomeWavelength}m, " +
                  $"snow>{RockSnowAltitude}m, cliff>{CliffAngle}°).", this);
    }

    // Smoothstep over [a, b] -> 0..1 (handles a == b without dividing by zero).
    static float SStep(float a, float b, float x) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

    // Placeholder tints (also used by the runtime transient fallback).
    static readonly Color CGrass = new(0.36f, 0.52f, 0.22f);
    static readonly Color CDryGrass = new(0.50f, 0.55f, 0.26f);
    static readonly Color CRock = new(0.45f, 0.42f, 0.38f);
    static readonly Color CSnow = new(0.92f, 0.93f, 0.96f);

    // Layers in [grass, dry-grass, rock, snow] order. Unassigned slots become a
    // committed placeholder asset in the editor, a transient layer at runtime.
    TerrainLayer[] ResolveLayers()
    {
#if UNITY_EDITOR
        EnsureTerrainFolder();
        const string dir = TerrainDir;
        if (GrassLayer == null) GrassLayer = MakeLayerAsset(dir, "Terrain.Grass", CGrass);
        if (DryGrassLayer == null) DryGrassLayer = MakeLayerAsset(dir, "Terrain.DryGrass", CDryGrass);
        if (RockLayer == null) RockLayer = MakeLayerAsset(dir, "Terrain.Rock", CRock);
        if (SnowLayer == null) SnowLayer = MakeLayerAsset(dir, "Terrain.Snow", CSnow);
#else
        if (GrassLayer == null) GrassLayer = MakeLayerTransient(CGrass);
        if (DryGrassLayer == null) DryGrassLayer = MakeLayerTransient(CDryGrass);
        if (RockLayer == null) RockLayer = MakeLayerTransient(CRock);
        if (SnowLayer == null) SnowLayer = MakeLayerTransient(CSnow);
#endif
        if (GrassLayer == null || DryGrassLayer == null ||
            RockLayer == null || SnowLayer == null)
        {
            Debug.LogWarning("[TerrainGenerator] Texturing skipped: assign all 4 terrain layers.", this);
            return null;
        }
        return new[] { GrassLayer, DryGrassLayer, RockLayer, SnowLayer };
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
    // Creates or reuses a committed .terrainlayer + solid .png so the bake is
    // self-contained and version-controlled.
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
    const string TerrainDir = "Assets/Content/Terrain";

    static void EnsureTerrainFolder()
    {
        if (AssetDatabase.IsValidFolder(TerrainDir)) return;
        if (!AssetDatabase.IsValidFolder("Assets/Content"))
            AssetDatabase.CreateFolder("Assets", "Content");
        AssetDatabase.CreateFolder("Assets/Content", "Terrain");
    }

    // The owned TerrainData path; <name> defaults to the scene so each scene bakes its own file.
    string OwnedAssetPath()
    {
        string name = string.IsNullOrWhiteSpace(AssetName)
            ? gameObject.scene.name : AssetName.Trim();
        if (string.IsNullOrEmpty(name)) name = gameObject.name;
        return $"{TerrainDir}/{name}.asset";
    }

    // Points the Terrain + its collider at this scene's own TerrainData,
    // cloning a shared/duplicated asset to the owned path first. Prevents the
    // "saving one scene overwrites another's terrain" bug.
    void EnsureOwnedTerrainData()
    {
        EnsureTerrainFolder();
        string path = OwnedAssetPath();

        var current = TargetTerrain.terrainData;
        string currentPath = current != null ? AssetDatabase.GetAssetPath(current) : "";
        if (currentPath.Replace('\\', '/') == path) return;   // already owned

        var owned = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if (owned == null)
        {
            owned = current != null ? Instantiate(current) : new TerrainData();
            AssetDatabase.CreateAsset(owned, path);
        }

        TargetTerrain.terrainData = owned;
        var col = GetComponent<TerrainCollider>();
        if (col != null) { col.terrainData = owned; EditorUtility.SetDirty(col); }
        EditorUtility.SetDirty(TargetTerrain);
        Debug.Log($"[TerrainGenerator] Bake target repointed to {path} " +
                  $"(was '{(string.IsNullOrEmpty(currentPath) ? "none" : currentPath)}').", this);
    }

    // Generate, then persist the TerrainData asset so the bake is committed.
    public void GenerateAndSave()
    {
        if (TargetTerrain == null) TargetTerrain = GetComponent<Terrain>();
        if (TargetTerrain == null)
        {
            Debug.LogError("[TerrainGenerator] No Terrain to bake into.", this);
            return;
        }
        EnsureOwnedTerrainData();
        Generate();
        if (TargetTerrain.terrainData == null) return;
        EditorUtility.SetDirty(TargetTerrain.terrainData);
        EditorUtility.SetDirty(this);   // keep auto-assigned layer refs
        AssetDatabase.SaveAssets();
        Debug.Log($"[TerrainGenerator] Saved " +
                  $"{AssetDatabase.GetAssetPath(TargetTerrain.terrainData)}.", this);
    }

    [ContextMenu("Generate (preview, no save)")]
    void CtxGenerate() => Generate();

    [ContextMenu("Generate & Save Asset")]
    void CtxGenerateAndSave() => GenerateAndSave();
#endif

    // Noise

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

    // Ridged noise gated by a curved band so the peaks read as one Carpathian arc.
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

    // Metres to subtract for the river: a smooth V on a sin() meander along +Z.
    float RiverCarve(float x, float z)
    {
        float pathX = SizeX * 0.32f
                      + Mathf.Sin(z / Mathf.Max(1f, RiverMeanderWavelength) * Mathf.PI * 2f)
                        * RiverMeander;
        float d = Mathf.Abs(x - pathX);
        if (d >= RiverWidth) return 0f;
        return (1f - Mathf.SmoothStep(0f, 1f, d / RiverWidth)) * RiverDepth;
    }

    // Same V profile as RiverCarve, but distance comes from the precomputed
    // spline field (bilinear, ~4 taps per cell) instead of a sin() path.
    float RiverCarveAuthored(float wx, float wz)
    {
        if (!_riverFieldReady) return 0f;
        float d = SampleRiverField(wx, wz);
        if (d >= RiverWidth) return 0f;
        return (1f - Mathf.SmoothStep(0f, 1f, d / RiverWidth)) * RiverDepth;
    }

    // Bake distance-to-route (clamped to RiverWidth, so it stays sharp only
    // near the channel) over the terrain footprint, once per Generate.
    void BuildRiverField(Vector3 origin)
    {
        _riverFieldReady = false;
        RiverPath.RebuildCache();
        if (!RiverPath.HasPath) return;

        int f = Mathf.Clamp(RiverPathFieldRes, 129, 2049);
        _riverField = new float[f, f];
        for (int zi = 0; zi < f; zi++)
        {
            float wz = origin.z + zi / (float)(f - 1) * SizeZ;
            for (int xi = 0; xi < f; xi++)
            {
                float wx = origin.x + xi / (float)(f - 1) * SizeX;
                _riverField[zi, xi] =
                    Mathf.Min(RiverPath.DistanceXZ(wx, wz), RiverWidth);
            }
        }
        _riverFieldReady = true;
    }

    // Bilinear lookup into _riverField; (wx, wz) are terrain-local metres.
    float SampleRiverField(float wx, float wz)
    {
        int f = _riverField.GetLength(0);
        float fx = Mathf.Clamp(wx / SizeX, 0f, 1f) * (f - 1);
        float fz = Mathf.Clamp(wz / SizeZ, 0f, 1f) * (f - 1);
        int x0 = Mathf.FloorToInt(fx), z0 = Mathf.FloorToInt(fz);
        int x1 = Mathf.Min(x0 + 1, f - 1), z1 = Mathf.Min(z0 + 1, f - 1);
        float tx = fx - x0, tz = fz - z0;

        float a = Mathf.Lerp(_riverField[z0, x0], _riverField[z0, x1], tx);
        float b = Mathf.Lerp(_riverField[z1, x0], _riverField[z1, x1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    // Flatten to BaseElevation inside FlattenRadius, easing out over FlattenBlend (no hard rim).
    float ApplyFlattenPad(float x, float z, float h)
    {
        float d = Vector2.Distance(new Vector2(x, z), FlattenCentre);
        if (d <= FlattenRadius) return BaseElevation;
        if (d >= FlattenRadius + FlattenBlend) return h;
        float k = Mathf.SmoothStep(0f, 1f, (d - FlattenRadius) / FlattenBlend);
        return Mathf.Lerp(BaseElevation, h, k);
    }

    // Editor viz

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

        // Authored corridor: centreline + the ±RiverWidth banks, lifted onto the baked ground.
        if (EnableRiver && RiverPath != null && RiverPath.HasPath)
        {
            var p = RiverPath.PolylineXZ;
            if (p.Count < 2) RiverPath.RebuildCache();
            for (int i = 1; i < p.Count; i++)
            {
                Vector2 pa = p[i - 1], pb = p[i];
                Vector2 dir = (pb - pa);
                if (dir.sqrMagnitude < 1e-6f) continue;
                dir.Normalize();
                Vector2 nrm = new(dir.y, -dir.x);       // XZ perpendicular

                Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
                Gizmos.DrawLine(GroundPt(t, pa), GroundPt(t, pb));

                Vector2 la = pa + nrm * RiverWidth, lb = pb + nrm * RiverWidth;
                Vector2 ra = pa - nrm * RiverWidth, rb = pb - nrm * RiverWidth;
                Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.35f);
                Gizmos.DrawLine(GroundPt(t, la), GroundPt(t, lb));
                Gizmos.DrawLine(GroundPt(t, ra), GroundPt(t, rb));
            }
        }
    }

    // World point at terrain-XZ p, lifted onto the terrain surface.
    static Vector3 GroundPt(Terrain t, Vector2 p)
    {
        var w = new Vector3(p.x, 0f, p.y);
        w.y = t.transform.position.y + t.SampleHeight(w);
        return w;
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
