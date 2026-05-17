using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Mission-1 enemy spawner: ordered waves of compass spawn groups. The first
// N planes of a group strike the airfield; the rest keep the prefab's EnemyAI.
public class WaveDirector : MonoBehaviour
{
    public enum Compass { North, South, East, West }

    [Serializable]
    public class SpawnGroup
    {
        public Compass Direction = Compass.North;
        [Min(0)] public int Count = 5;
        [Tooltip("First N of this group strike the airfield; the rest hunt the player.")]
        [Min(0)] public int AirfieldAttackers = 0;
    }

    [Serializable]
    public class Wave
    {
        public string Name = "Wave";
        [Tooltip("Delay after the previous wave ends before this one starts.")]
        public float StartDelay = 3f;
        [Tooltip("Force-advance after this long even if planes remain (0 = wait until cleared).")]
        public float MaxDuration = 0f;
        public List<SpawnGroup> Groups = new();
    }

    public PlaneAIController EnemyPrefab;
    public List<Wave> Waves = new();

    [Header("Victory")]
    [Tooltip("Scene loaded once the final wave is cleared with the airfield intact. Must be in Build Settings.")]
    public string WinScene = GameFlow.Video02;
    [Tooltip("Seconds the WIN banner holds before the next scene loads.")]
    public float WinDelay = 4f;

    [Header("Wave banner")]
    [Tooltip("Seconds the incoming-wave banner stays on screen before fading out.")]
    public float WaveBannerDuration = 3.5f;
    public int WaveBannerFontSize = 34;

    [Header("Spawn shell")]
    [Tooltip("Compass directions are measured from here (defaults to the Airfield, else this transform).")]
    public Transform Center;
    public float MinRadius = 1100f;
    public float MaxRadius = 1500f;
    public float MinAltitude = 220f;
    public float MaxAltitude = 460f;
    public float TerrainClearance = 40f;

    Terrain _terrain;
    float _terrainBaseY;
    readonly List<PlaneHealth> _alive = new();
    int _waveIndex = -1;
    bool _waveActive;
    float _nextWaveAt;
    float _waveDeadline;
    bool _won;
    float _winAt;
    bool _loading;
    string _bannerLabel;
    string _bannerName;
    float _bannerShownAt = -999f;

    void Start()
    {
        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
        _terrainBaseY = _terrain != null ? _terrain.transform.position.y : 0f;

        if (Center == null)
        {
            var af = Airfield.Instance;
            Center = af != null ? af.transform : transform;
        }
        if (Waves.Count > 0)
            _nextWaveAt = Time.time + Mathf.Max(0f, Waves[0].StartDelay);
    }

    void Update()
    {
        if (_won)
        {
            if (!_loading && Time.time >= _winAt)
            {
                _loading = true;
                SceneManager.LoadScene(WinScene);
            }
            return;
        }

        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] == null || _alive[i].IsDead) _alive.RemoveAt(i);

        if (_waveActive)
        {
            var cleared = _alive.Count == 0;
            var timedOut = _waveDeadline > 0f && Time.time >= _waveDeadline;
            if (cleared || timedOut)
            {
                _waveActive = false;
                var next = _waveIndex + 1;
                if (next < Waves.Count)
                    _nextWaveAt = Time.time + Mathf.Max(0f, Waves[next].StartDelay);
                else
                    CompleteMission();
            }
            return;
        }

        if (_waveIndex + 1 >= Waves.Count) return;
        if (Time.time < _nextWaveAt) return;
        StartWave(_waveIndex + 1);
    }

    // A destroyed airfield is a loss, not a win (owned by Airfield's state).
    void CompleteMission()
    {
        if (_won) return;
        var af = Airfield.Instance;
        if (af != null && af.IsDestroyed) return;
        _won = true;
        _winAt = Time.time + Mathf.Max(0f, WinDelay);
        Debug.Log("All waves cleared - MISSION 1 COMPLETE");
    }

    void StartWave(int index)
    {
        _waveIndex = index;
        var wave = Waves[index];
        _bannerLabel = $"WAVE {index + 1} / {Waves.Count}";
        _bannerName = !string.IsNullOrWhiteSpace(wave.Name) && wave.Name != "Wave"
            ? wave.Name : null;
        _bannerShownAt = Time.time;
        foreach (var g in wave.Groups)
            for (int i = 0; i < g.Count; i++)
                SpawnOne(g.Direction, i < g.AirfieldAttackers);

        _waveActive = true;
        _waveDeadline = wave.MaxDuration > 0f ? Time.time + wave.MaxDuration : 0f;
    }

    void SpawnOne(Compass dir, bool striker)
    {
        if (EnemyPrefab == null) return;

        var center = Center != null ? Center.position : transform.position;
        var d = Direction(dir);
        var lateral = new Vector3(-d.z, 0f, d.x); // spread across the bearing

        var pos = center
            + d * UnityEngine.Random.Range(MinRadius, MaxRadius)
            + lateral * UnityEngine.Random.Range(-MaxRadius * 0.25f, MaxRadius * 0.25f);
        pos.y = UnityEngine.Random.Range(MinAltitude, MaxAltitude);

        if (_terrain != null)
        {
            var floorY = _terrainBaseY + _terrain.SampleHeight(pos) + TerrainClearance;
            if (pos.y < floorY) pos.y = floorY;
        }

        var facing = center - pos;
        var rot = facing.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(facing.normalized, Vector3.up)
            : Quaternion.identity;

        var plane = Instantiate(EnemyPrefab, pos, rot);
        var health = plane.GetComponent<PlaneHealth>();
        if (health != null)
        {
            health.Faction = PlaneFaction.Enemy;
            _alive.Add(health);
        }
        if (striker) plane.gameObject.AddComponent<AirfieldStrikeRun>();
    }

    static Vector3 Direction(Compass c) => c switch
    {
        Compass.North => Vector3.forward,
        Compass.South => Vector3.back,
        Compass.East => Vector3.right,
        _ => Vector3.left,
    };

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        var skin = GUI.skin.label;
        var prevAlign = skin.alignment;
        var prevSize = skin.fontSize;
        var prevColor = GUI.color;
        skin.alignment = TextAnchor.MiddleCenter;

        if (_won)
        {
            skin.fontSize = 28;
            GUI.color = new Color(0.4f, 1f, 0.5f, 1f);
            GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 40f),
                "MISSION COMPLETE");
        }
        else if (HudToggle.Visible && _bannerLabel != null)
        {
            var age = Time.time - _bannerShownAt;
            if (age >= 0f && age < WaveBannerDuration)
            {
                // Hold solid, then fade over the final second.
                var fade = WaveBannerDuration - age;
                var alpha = fade < 1f ? Mathf.Clamp01(fade) : 1f;
                DrawBannerLine(_bannerLabel, Screen.height * 0.26f,
                    WaveBannerFontSize, new Color(1f, 0.85f, 0.3f, alpha));
                if (_bannerName != null)
                    DrawBannerLine(_bannerName, Screen.height * 0.26f + WaveBannerFontSize + 10f,
                        Mathf.RoundToInt(WaveBannerFontSize * 0.6f),
                        new Color(1f, 1f, 1f, alpha * 0.9f));
            }
        }

        GUI.color = prevColor;
        skin.alignment = prevAlign;
        skin.fontSize = prevSize;
    }

    // Centered line with a drop shadow so it stays legible over sky/terrain.
    void DrawBannerLine(string text, float y, int fontSize, Color color)
    {
        GUI.skin.label.fontSize = fontSize;
        var h = fontSize + 10f;
        GUI.color = new Color(0f, 0f, 0f, color.a * 0.6f);
        GUI.Label(new Rect(2f, y + 2f, Screen.width, h), text);
        GUI.color = color;
        GUI.Label(new Rect(0f, y, Screen.width, h), text);
    }

    void OnDrawGizmosSelected()
    {
        var center = Center != null ? Center.position
            : (Airfield.Instance != null ? Airfield.Instance.transform.position
                                         : transform.position);
        var colors = new[]
        {
            new Color(0.4f, 0.7f, 1f, 1f), new Color(1f, 0.6f, 0.3f, 1f),
            new Color(0.5f, 1f, 0.5f, 1f), new Color(1f, 0.9f, 0.4f, 1f),
        };
        var dirs = new[] { Compass.North, Compass.South, Compass.East, Compass.West };
        for (int i = 0; i < 4; i++)
        {
            Gizmos.color = colors[i];
            var d = Direction(dirs[i]);
            var mid = (MinRadius + MaxRadius) * 0.5f;
            var midAlt = (MinAltitude + MaxAltitude) * 0.5f;
            Gizmos.DrawLine(center + d * MinRadius + Vector3.up * MinAltitude,
                            center + d * MaxRadius + Vector3.up * MaxAltitude);
            Gizmos.DrawWireSphere(center + d * mid + Vector3.up * midAlt, 60f);
        }
    }
}
