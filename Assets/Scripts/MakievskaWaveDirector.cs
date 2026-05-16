using System.Collections.Generic;
using UnityEngine;

// MISSION 1 ONLY (Makievska). Scripted enemy choreography for the airfield-
// defence beat: the Soviet packets come in FROM THE EAST ("dinspre răsărit,
// niște muște dezordonate"), hit, then there is a lull Greceanu exploits,
// then a second packet. Two roles fly it:
//   * IL-2 Sturmovik  — the ground-attack body: comes in LOW and slow, the
//     thing the player must stop before it works the runway.
//   * LaGG-3          — the high escort screen above the Shturmoviks.
//
// This is a self-contained additive director. It does NOT modify the shared
// PlaneSpawner / PlaneAIController; it just Instantiates the SAME enemy
// PlaneAIController prefab the scene already uses and, per role, swaps in
// optional Mission-1 stat ScriptableObjects (AI behaviour + flight model)
// BEFORE the clone's Start runs. Leave the role SO slots empty and every
// enemy simply flies the prefab defaults — the waves/direction/altitude
// split still work. Disable the scene's existing enemy PlaneSpawner so the
// two don't both populate; keep the ally PlaneSpawner as-is.
//
// Mission-1 isolation: this script is referenced only by the Makievska scene.
// Nothing here changes behaviour in any other scene.
public class MakievskaWaveDirector : MonoBehaviour
{
    public enum Role { Il2, Lagg }

    [System.Serializable]
    public struct Wave
    {
        [Tooltip("Seconds after mission start this wave is allowed to begin.")]
        public float StartDelay;
        public int Il2Count;
        public int LaggCount;
        [Tooltip("Seconds between successive spawns within the wave (a trickle, not a pop).")]
        public float SpawnInterval;
        [Tooltip("If the living enemy count drops to/below this, the NEXT wave may start early (the 'they retreated — go!' beat). <0 disables the early trigger.")]
        public int EarlyNextWhenAliveAtMost;
    }

    [Header("Enemy prefab (reuse the scene's existing enemy PlaneAIController)")]
    public PlaneAIController EnemyPrefab;

    [Header("Per-role stat overrides (optional — Mission-1 SOs; empty = prefab defaults)")]
    public PlaneAIStats Il2AIStats;
    public PlaneFlightStats Il2FlightStats;
    public PlaneAIStats LaggAIStats;
    public PlaneFlightStats LaggFlightStats;

    [Header("Approach geometry (enemies come from the EAST)")]
    [Tooltip("Compass-ish bearing the packet flies IN from, degrees CW from world +X. 0 = due east. The arc is centred here.")]
    public float IngressBearingDeg = 0f;
    [Tooltip("Half-angle (deg) of the spawn arc around the ingress bearing.")]
    public float ArcHalfAngleDeg = 28f;
    public float MinRadius = 1400f;
    public float MaxRadius = 2000f;
    [Tooltip("Field centre. Empty -> MapBoundary.Instance.Center, else this object's position.")]
    public Transform FieldCentre;

    [Header("Role altitude bands (metres above sampled terrain)")]
    public float Il2MinAlt = 60f;
    public float Il2MaxAlt = 160f;
    public float LaggMinAlt = 280f;
    public float LaggMaxAlt = 440f;
    [Tooltip("Hard floor: spawns are always at least this far above the sampled ground.")]
    public float TerrainClearance = 45f;

    [Header("Waves")]
    public List<Wave> Waves = new();

    static readonly Wave[] DefaultWaves =
    {
        // ~40 total across two packets with a lull, IL-2 heavy + a thin escort.
        new() { StartDelay = 3f,  Il2Count = 14, LaggCount = 6, SpawnInterval = 0.5f, EarlyNextWhenAliveAtMost = 6 },
        new() { StartDelay = 75f, Il2Count = 14, LaggCount = 6, SpawnInterval = 0.5f, EarlyNextWhenAliveAtMost = -1 },
    };

    Terrain _terrain;
    float _terrainBaseY;
    Transform _player;
    readonly List<PlaneHealth> _alive = new();

    Wave[] _plan;
    int _waveIndex = -1;
    bool _waveActive;
    int _il2Left, _laggLeft;
    float _nextSpawnAt;
    float _missionStart;

    void Start()
    {
        _missionStart = Time.time;
        _plan = (Waves != null && Waves.Count > 0) ? Waves.ToArray() : DefaultWaves;

        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
        _terrainBaseY = _terrain != null ? _terrain.transform.position.y : 0f;

        var p = FindFirstObjectByType<PlanePlayerInput>();
        if (p != null) _player = p.transform;

        if (EnemyPrefab == null)
            Debug.LogError("[MakievskaWaveDirector] No EnemyPrefab assigned — no enemies will spawn.", this);
    }

    Vector3 Centre()
    {
        if (FieldCentre != null) return FieldCentre.position;
        var b = MapBoundary.Instance;
        return b != null ? b.Center : transform.position;
    }

    void Update()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] == null || _alive[i].IsDead) _alive.RemoveAt(i);

        if (EnemyPrefab == null || _plan == null) return;

        if (!_waveActive)
        {
            int next = _waveIndex + 1;
            if (next < _plan.Length)
            {
                var w = _plan[next];
                bool timeReady = Time.time - _missionStart >= w.StartDelay;
                bool prevEarly =
                    _waveIndex >= 0 &&
                    _plan[_waveIndex].EarlyNextWhenAliveAtMost >= 0 &&
                    _alive.Count <= _plan[_waveIndex].EarlyNextWhenAliveAtMost &&
                    Time.time - _missionStart >= Mathf.Min(w.StartDelay, 12f);
                if (timeReady || prevEarly) BeginWave(next);
            }
            return;
        }

        if (_il2Left <= 0 && _laggLeft <= 0) { _waveActive = false; return; }
        if (Time.time < _nextSpawnAt) return;

        // Alternate roles so the escort is interleaved with the Shturmoviks.
        if (_il2Left > 0 && (_laggLeft <= 0 || _il2Left >= _laggLeft))
        {
            SpawnOne(Role.Il2);
            _il2Left--;
        }
        else if (_laggLeft > 0)
        {
            SpawnOne(Role.Lagg);
            _laggLeft--;
        }
        _nextSpawnAt = Time.time + Mathf.Max(_plan[_waveIndex].SpawnInterval, 0.05f);
    }

    void BeginWave(int idx)
    {
        _waveIndex = idx;
        _waveActive = true;
        _il2Left = Mathf.Max(0, _plan[idx].Il2Count);
        _laggLeft = Mathf.Max(0, _plan[idx].LaggCount);
        _nextSpawnAt = Time.time;
        Debug.Log($"[MakievskaWaveDirector] Wave {idx + 1}: {_il2Left} IL-2 + {_laggLeft} LaGG-3 from the east.", this);
    }

    void SpawnOne(Role role)
    {
        var centre = Centre();

        // Random point on the eastern ingress arc.
        float bearing = (IngressBearingDeg + Random.Range(-ArcHalfAngleDeg, ArcHalfAngleDeg)) * Mathf.Deg2Rad;
        var dir = new Vector3(Mathf.Cos(bearing), 0f, Mathf.Sin(bearing));
        float dist = Random.Range(MinRadius, MaxRadius);
        var pos = centre + dir * dist;

        float lo = role == Role.Il2 ? Il2MinAlt : LaggMinAlt;
        float hi = role == Role.Il2 ? Il2MaxAlt : LaggMaxAlt;
        float groundY = _terrain != null
            ? _terrainBaseY + _terrain.SampleHeight(pos)
            : centre.y;
        pos.y = groundY + Mathf.Max(Random.Range(lo, hi), TerrainClearance);

        // Face the field (so they ingress toward the airfield/player).
        var look = (_player != null ? _player.position : centre) - pos;
        var rot = look.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(look.normalized, Vector3.up)
            : Quaternion.identity;

        var enemy = Instantiate(EnemyPrefab, pos, rot);

        // Role stat swap — applied immediately, before the clone's Start runs.
        var ai = enemy.GetComponent<PlaneAIController>();
        var flight = enemy.GetComponent<PlaneFlightModel>();
        if (role == Role.Il2)
        {
            if (Il2AIStats != null && ai != null) ai.Stats = Il2AIStats;
            if (Il2FlightStats != null && flight != null) flight.Stats = Il2FlightStats;
        }
        else
        {
            if (LaggAIStats != null && ai != null) ai.Stats = LaggAIStats;
            if (LaggFlightStats != null && flight != null) flight.Stats = LaggFlightStats;
        }

        var health = enemy.GetComponent<PlaneHealth>();
        if (health != null)
        {
            health.Faction = PlaneFaction.Enemy;
            _alive.Add(health);
        }

        // Tag IL-2s for the airfield ground-attack behaviour (Airfield task).
        if (role == Role.Il2)
        {
            var run = enemy.GetComponent<Il2GroundAttackRun>();
            if (run == null) run = enemy.gameObject.AddComponent<Il2GroundAttackRun>();
            run.Director = this;
        }
    }

    // The airfield the IL-2s run on; resolved lazily so script order doesn't
    // matter. Returns null until/unless an Airfield exists in the scene.
    Airfield _airfield;
    bool _airfieldResolved;
    public Airfield FindAirfield()
    {
        if (!_airfieldResolved)
        {
            _airfield = FindFirstObjectByType<Airfield>();
            _airfieldResolved = true;
        }
        return _airfield;
    }

    void OnDrawGizmosSelected()
    {
        var centre = Application.isPlaying ? Centre()
            : (FieldCentre != null ? FieldCentre.position : transform.position);
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.9f);
        for (int i = -1; i <= 1; i++)
        {
            float b = (IngressBearingDeg + i * ArcHalfAngleDeg) * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b));
            Gizmos.DrawLine(centre + d * MinRadius, centre + d * MaxRadius);
        }
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.35f);
        Gizmos.DrawWireSphere(centre, MinRadius);
        Gizmos.DrawWireSphere(centre, MaxRadius);
    }
}
