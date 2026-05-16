using UnityEngine;
using UnityEngine.InputSystem;

// Drop this on ANY scene object (an empty GameObject is fine) and press Play.
// It auto-finds the player plane and gives you a sandbox of cheats. Every
// weapon/flight tweak is applied to a RUNTIME CLONE of the stats asset, so the
// shared committed ScriptableObjects on disk are never touched and AI planes
// keep their normal stats. Toggle anything off and the originals come back.
//
//   (always) every round you fire detonates a mini-nuke where it lands,
//            erupting the ground into flying voxel chunks.
//   N : NUKE        - instakill every other plane in BlastRadius, with a
//                      mushroom fireball, white-out flash and bullet-time.
//   G : OP guns     - huge damage, machine-gun rate, no overheat, long range.
//   V : Warp speed  - flight thrust + agility cranked way up.
//   B : Chaos audio - pitch wobbles between chipmunk and demon.
//   H : hide/show the on-screen legend.
public class FunMode : MonoBehaviour
{
    [Header("OP guns (G)")]
    public float DamageMultiplier = 50f;
    public float FireIntervalScale = 0.15f;
    public float RangeMultiplier = 6f;

    [Header("Warp speed (V)")]
    public float SpeedMultiplier = 4f;
    public float AgilityMultiplier = 2f;

    [Header("Nuke (N)")]
    public float BlastRadius = 1200f;
    [Tooltip("World units ahead of the player the warhead detonates.")]
    public float BlastForwardOffset = 250f;
    [Tooltip("Time.timeScale during the bullet-time after a detonation.")]
    [Range(0.05f, 1f)] public float SlowMoScale = 0.25f;
    public float SlowMoDuration = 1.6f;

    [Header("Shot mini-nukes")]
    [Tooltip("Each round you fire detonates a small blast where it lands.")]
    public float MiniNukeRadius = 120f;
    [Tooltip("Min seconds between shot blasts so rapid fire doesn't spawn hundreds.")]
    public float MiniNukeInterval = 0.22f;

    [Header("Voxel ground debris")]
    [Tooltip("Edge length of each erupted terrain cube (world units).")]
    public float VoxelSize = 14f;
    [Tooltip("Max half-extent of the voxel patch, in cubes. Grid scales with " +
             "blast radius up to this.")]
    public int VoxelGridHalf = 8;
    [Tooltip("Outward launch speed of the chunks (m/s, mass-independent).")]
    public float VoxelForce = 38f;
    public float VoxelLifetime = 2.6f;
    [Tooltip("Global cap on live chunks so sustained fire can't melt the CPU.")]
    public int MaxLiveVoxels = 600;
    [Tooltip("Skip ground debris when the blast is higher than radius * this " +
             "above the terrain (an air burst, nothing to erupt).")]
    public float GroundContactFactor = 1.4f;

    [Header("Chaos audio (B)")]
    public float PitchMin = 0.55f;
    public float PitchMax = 1.85f;

    GameObject _player;
    PlaneHealth _playerHealth;
    PlaneShooter _shooter;
    PlaneFlightModel _flight;
    Terrain _terrain;

    PlaneWeaponStats _weaponOrig, _weaponClone;
    PlaneFlightStats _flightOrig, _flightClone;
    PlaneAudioStats _audioStats;

    bool _opGuns, _warp, _chaosAudio, _showHelp = true;

    float _slowMoUntil;
    float _flashUntil;
    float _nextMiniNuke;
    GUIStyle _legendStyle;

    static Material _voxelMat;
    static MaterialPropertyBlock _voxelMpb;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    // Unity has no global pitch knob (AudioListener has no .pitch), so chaos
    // audio drives .pitch on every live AudioSource directly. The set is
    // re-scanned periodically because one-shots/explosions spawn transient
    // sources that wouldn't otherwise get caught.
    AudioSource[] _audioSources;
    float _nextAudioScan;
    bool _audioDriven;

    void Start()
    {
        var input = FindFirstObjectByType<PlanePlayerInput>();
        if (input == null)
        {
            Debug.LogWarning("FunMode: no player plane (PlanePlayerInput) found in scene.", this);
            enabled = false;
            return;
        }

        _player = input.gameObject;
        _playerHealth = _player.GetComponent<PlaneHealth>();
        _shooter = _player.GetComponent<PlaneShooter>();
        _flight = _player.GetComponent<PlaneFlightModel>();
        _terrain = FindFirstObjectByType<Terrain>();

        if (_shooter != null)
        {
            _weaponOrig = _shooter.Stats;
            _shooter.Shot += OnShot;
        }
        if (_flight != null) _flightOrig = _flight.Stats;

        var pa = FindFirstObjectByType<PlayerPlaneAudio>();
        if (pa != null) _audioStats = pa.Stats;
    }

    void OnDestroy()
    {
        if (_shooter != null) _shooter.Shot -= OnShot;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.gKey.wasPressedThisFrame) SetOpGuns(!_opGuns);
        if (kb.vKey.wasPressedThisFrame) SetWarp(!_warp);
        if (kb.bKey.wasPressedThisFrame) _chaosAudio = !_chaosAudio;
        if (kb.hKey.wasPressedThisFrame) _showHelp = !_showHelp;
        if (kb.nKey.wasPressedThisFrame) DetonateNuke();

        DriveChaosAudio();

        if (Time.unscaledTime >= _slowMoUntil && Time.timeScale != 1f)
            Time.timeScale = 1f;
    }

    void DriveChaosAudio()
    {
        if (_chaosAudio)
        {
            if (_audioSources == null || Time.unscaledTime >= _nextAudioScan)
            {
                _audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                _nextAudioScan = Time.unscaledTime + 0.4f;
            }

            var p = Mathf.Lerp(PitchMin, PitchMax,
                Mathf.PerlinNoise(Time.unscaledTime * 1.7f, 0f) * 0.7f
                + (Mathf.Sin(Time.unscaledTime * 11f) * 0.5f + 0.5f) * 0.3f);

            foreach (var s in _audioSources)
                if (s != null) s.pitch = p;
            _audioDriven = true;
        }
        else if (_audioDriven)
        {
            ResetAudioPitch();
            _audioDriven = false;
        }
    }

    void ResetAudioPitch()
    {
        foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (s != null) s.pitch = 1f;
        _audioSources = null;
    }

    void SetOpGuns(bool on)
    {
        _opGuns = on;
        if (_shooter == null || _weaponOrig == null) return;

        if (on)
        {
            _weaponClone = Instantiate(_weaponOrig);
            _weaponClone.Damage = _weaponOrig.Damage * DamageMultiplier;
            _weaponClone.FireInterval = _weaponOrig.FireInterval * FireIntervalScale;
            _weaponClone.Range = _weaponOrig.Range * RangeMultiplier;
            _weaponClone.HeatPerShot = 0f;
            _shooter.Stats = _weaponClone;
        }
        else
        {
            _shooter.Stats = _weaponOrig;
            if (_weaponClone != null) Destroy(_weaponClone);
        }
    }

    void SetWarp(bool on)
    {
        _warp = on;
        if (_flight == null || _flightOrig == null) return;

        if (on)
        {
            _flightClone = Instantiate(_flightOrig);
            _flightClone.NormalThrust = _flightOrig.NormalThrust * SpeedMultiplier;
            _flightClone.MaxThrust = _flightOrig.MaxThrust * SpeedMultiplier;
            _flightClone.ThrustAgilityMultiplier =
                _flightOrig.ThrustAgilityMultiplier * AgilityMultiplier;
            _flight.Stats = _flightClone;
        }
        else
        {
            _flight.Stats = _flightOrig;
            if (_flightClone != null) Destroy(_flightClone);
        }
    }

    // Each round the gun fires lands a mini-nuke at the bullet's impact point.
    // The raycast mirrors PlaneShooter.Fire exactly (same muzzle offset, aim
    // direction, range and hit mask) so the blast goes where the bullets go.
    // Rate-limited so machine-gun / OP fire can't spawn hundreds of rigs.
    void OnShot()
    {
        if (_shooter == null || _shooter.Stats == null) return;
        if (Time.time < _nextMiniNuke) return;
        _nextMiniNuke = Time.time + Mathf.Max(0.02f, MiniNukeInterval);

        var t = _shooter.transform;
        var st = _shooter.Stats;
        var origin = t.position + t.forward * _shooter.MuzzleOffsetZ;
        var dir = _shooter.UseAimDirection ? _shooter.AimDirection.normalized : t.forward;
        var point = Physics.Raycast(origin, dir, out var hit, st.Range,
            st.HitMask, QueryTriggerInteraction.Ignore)
            ? hit.point
            : origin + dir * st.Range;

        Detonate(point, MiniNukeRadius, false);
    }

    void DetonateNuke()
    {
        if (_player == null) return;
        var t = _player.transform;
        Detonate(t.position + t.forward * BlastForwardOffset, BlastRadius, true);
    }

    void Detonate(Vector3 center, float radius, bool cinematic)
    {
        var planes = FindObjectsByType<PlaneHealth>(FindObjectsSortMode.None);
        foreach (var p in planes)
        {
            if (p == null || p == _playerHealth || p.IsDead) continue;
            if ((p.transform.position - center).sqrMagnitude <= radius * radius)
                p.TakeDamage(1e9f, _playerHealth);
        }

        SpawnFireball(center, radius, cinematic ? 2.5f : 0.8f);
        EruptVoxels(center, radius);
        PlayBlastSound(center);

        if (!cinematic) return;
        _flashUntil = Time.unscaledTime + 0.45f;
        _slowMoUntil = Time.unscaledTime + SlowMoDuration;
        Time.timeScale = SlowMoScale;
    }

    // Samples the real terrain surface in a circular patch under the blast and
    // erupts it into rigidbody cubes flung outward. Pure cosmetic spawn — the
    // Unity Terrain itself is never modified, so AI / spline / splatmap are
    // untouched and there's nothing to restore.
    void EruptVoxels(Vector3 center, float radius)
    {
        if (_terrain == null || Voxel.Live >= MaxLiveVoxels) return;

        var groundY = _terrain.transform.position.y + _terrain.SampleHeight(center);
        if (center.y - groundY > radius * GroundContactFactor) return;

        var half = Mathf.Clamp(
            Mathf.RoundToInt(radius / Mathf.Max(VoxelSize, 0.1f) / 4f),
            2, VoxelGridHalf);
        var patch = half * VoxelSize;

        if (_voxelMat == null)
            _voxelMat = new Material(Shader.Find("Sprites/Default"));
        _voxelMpb ??= new MaterialPropertyBlock();

        var dirt = new Color(0.34f, 0.27f, 0.20f);
        var grass = new Color(0.27f, 0.38f, 0.17f);

        for (int gx = -half; gx <= half; gx++)
        for (int gz = -half; gz <= half; gz++)
        {
            if (Voxel.Live >= MaxLiveVoxels) return;

            var ox = gx * VoxelSize;
            var oz = gz * VoxelSize;
            if (ox * ox + oz * oz > patch * patch) continue;

            var pos = new Vector3(center.x + ox, 0f, center.z + oz);
            pos.y = _terrain.transform.position.y + _terrain.SampleHeight(pos);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = pos;
            cube.transform.rotation = Random.rotation;
            cube.transform.localScale = Vector3.one * VoxelSize;

            var tint = Color.Lerp(dirt, grass, Random.value)
                       * Random.Range(0.8f, 1.15f);
            tint.a = 1f;
            var rend = cube.GetComponent<MeshRenderer>();
            rend.sharedMaterial = _voxelMat;
            _voxelMpb.SetColor(ColorId, tint);
            rend.SetPropertyBlock(_voxelMpb);

            var rb = cube.AddComponent<Rigidbody>();
            rb.AddExplosionForce(VoxelForce, center, patch * 1.5f,
                VoxelSize * 0.6f, ForceMode.VelocityChange);
            rb.angularVelocity = Random.insideUnitSphere * 12f;

            cube.AddComponent<Voxel>().Life = VoxelLifetime;
        }
    }

    void SpawnFireball(Vector3 center, float radius, float life)
    {
        var rig = new GameObject("NukeFX");
        rig.transform.position = center;

        var mat = new Material(Shader.Find("Sprites/Default"));

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(ball.GetComponent<Collider>());
        ball.transform.SetParent(rig.transform, false);
        ball.GetComponent<MeshRenderer>().material = mat;

        var shock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(shock.GetComponent<Collider>());
        shock.transform.SetParent(rig.transform, false);
        shock.GetComponent<MeshRenderer>().material = new Material(mat);

        var light = rig.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.75f, 0.4f);
        light.range = radius * 1.5f;
        light.intensity = 12f;

        rig.AddComponent<NukeFx>().Init(ball.transform, shock.transform,
            light, mat, radius, life);
    }

    void PlayBlastSound(Vector3 center)
    {
        if (_audioStats == null || _audioStats.Explosion == null
            || _audioStats.Explosion.Length == 0) return;
        var clip = _audioStats.Explosion[Random.Range(0, _audioStats.Explosion.Length)];
        if (clip != null) AudioSource.PlayClipAtPoint(clip, center, 1f);
    }

    void OnGUI()
    {
        if (Time.unscaledTime < _flashUntil)
        {
            var a = Mathf.InverseLerp(_flashUntil, _flashUntil - 0.45f,
                Time.unscaledTime);
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = prev;
        }

        if (!_showHelp) return;
        _legendStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(14, 12, 660, 180),
            $"FUN MODE\n" +
            $"shooting drops mini-nukes that erupt the ground into voxels\n" +
            $"[N] NUKE   " +
            $"[G] OP guns: {(_opGuns ? "ON" : "off")}   " +
            $"[V] Warp: {(_warp ? "ON" : "off")}   " +
            $"[B] Chaos audio: {(_chaosAudio ? "ON" : "off")}\n" +
            $"[H] hide this", _legendStyle);
    }

    void OnDisable()
    {
        if (_shooter != null && _weaponOrig != null) _shooter.Stats = _weaponOrig;
        if (_flight != null && _flightOrig != null) _flight.Stats = _flightOrig;
        ResetAudioPitch();
        Time.timeScale = 1f;
    }

    // Animates the detonation: fireball blooms and fades, shockwave races out,
    // flash light decays, then the whole rig deletes itself.
    class NukeFx : MonoBehaviour
    {
        Transform _ball, _shock;
        Light _light;
        Material _mat;
        float _radius, _life, _age;

        public void Init(Transform ball, Transform shock, Light light,
            Material mat, float radius, float life)
        {
            _ball = ball; _shock = shock; _light = light;
            _mat = mat; _radius = radius; _life = Mathf.Max(0.1f, life);
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            var k = _age / _life;
            if (k >= 1f) { Destroy(gameObject); return; }

            var bloom = Mathf.Sqrt(Mathf.Clamp01(_age / (_life * 0.25f)));
            var ballSize = _radius * 0.9f * bloom;
            _ball.localScale = Vector3.one * ballSize;
            _ball.localPosition = Vector3.up * ballSize * 0.35f;

            var shockSize = Mathf.Lerp(0f, _radius * 2f, k);
            _shock.localScale = Vector3.one * shockSize;

            var fade = 1f - k;
            _mat.color = new Color(1f, Mathf.Lerp(0.2f, 0.85f, fade),
                0.15f, fade);
            _light.intensity = 12f * fade * fade;
            _light.range = _radius * (1.5f + k);
        }
    }

    // One erupted terrain cube. Counts itself against the global cap and
    // shrinks away over its final half-second so the patch doesn't pop out.
    class Voxel : MonoBehaviour
    {
        public static int Live;
        public float Life = 2.6f;

        float _age;
        Vector3 _scale;

        void Awake() { Live++; }
        void OnDestroy() { Live--; }

        void Start() { _scale = transform.localScale; }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Life) { Destroy(gameObject); return; }

            var fadeFrom = Life - 0.5f;
            if (_age > fadeFrom)
                transform.localScale = _scale *
                    Mathf.Clamp01((Life - _age) / 0.5f);
        }
    }
}
