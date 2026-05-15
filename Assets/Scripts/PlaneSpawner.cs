using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PlaneSpawner : MonoBehaviour
{
    public PlaneAIController EnemyPrefab;
    public PlaneFaction SpawnFaction = PlaneFaction.Enemy;

    [Header("Population")]
    public int TargetCount = 3;
    public float RespawnDelay = 2f;
    [Tooltip("If true, the spawner fills TargetCount once and never respawns after deaths. Use for a finite ally squadron.")]
    public bool OneShot = false;

    int _totalSpawned;

    [Header("Spawn Shell")]
    [Tooltip("Shell is centered on this GameObject's position.")]
    public float MinRadius = 250f;
    public float MaxRadius = 500f;
    [FormerlySerializedAs("MinAltitudeAbovePlayer")]
    [Tooltip("Minimum vertical offset relative to the spawner's Y.")]
    public float MinAltitudeOffset = -60f;
    [FormerlySerializedAs("MaxAltitudeAbovePlayer")]
    [Tooltip("Maximum vertical offset relative to the spawner's Y.")]
    public float MaxAltitudeOffset = 120f;
    public float MinWorldY = 0f;

    [Header("Out-Of-Sight")]
    public bool spawnOutOfSight = false;
    public int MaxPlacementAttempts = 16;
    public float OffscreenMargin = 80f;

    Transform _player;
    Camera _camera;
    readonly List<PlaneAIController> _alive = new();
    float _nextSpawnAt;

    void Start()
    {
        var player = FindFirstObjectByType<PlanePlayerInput>();
        if (player != null)
        {
            _player = player.transform;
            var follow = player.GetComponent<PlaneCameraFollow>();
            if (follow != null) _camera = follow.Camera;
        }
        if (_camera == null) _camera = Camera.main;
    }

    void Update()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] == null) _alive.RemoveAt(i);

        if (EnemyPrefab == null) return;
        if (OneShot && _totalSpawned >= TargetCount) return;
        if (_alive.Count >= TargetCount) return;
        if (Time.time < _nextSpawnAt) return;

        if (!TrySpawnOne()) _nextSpawnAt = Time.time + 0.25f;
    }

    bool TrySpawnOne()
    {
        for (int i = 0; i < MaxPlacementAttempts; i++)
        {
            if (TryPickSpawnPosition(out var pos))
            {
                Spawn(pos);
                return true;
            }
        }
        return false;
    }

    bool TryPickSpawnPosition(out Vector3 pos)
    {
        var origin = transform.position;
        var dir = Random.onUnitSphere;
        var dist = Random.Range(MinRadius, MaxRadius);
        var candidate = origin + dir * dist;

        var dy = candidate.y - origin.y;
        if (dy < MinAltitudeOffset || dy > MaxAltitudeOffset)
        {
            var clampedDy = Mathf.Clamp(dy, MinAltitudeOffset, MaxAltitudeOffset);
            candidate.y = origin.y + clampedDy;
        }
        if (candidate.y < MinWorldY) candidate.y = MinWorldY;

        if (!spawnOutOfSight || _camera == null)
        {
            pos = candidate;
            return true;
        }

        var sp = _camera.WorldToScreenPoint(candidate);
        if (sp.z <= 0f)
        {
            pos = candidate;
            return true;
        }

        if (sp.x < -OffscreenMargin || sp.x > Screen.width + OffscreenMargin ||
            sp.y < -OffscreenMargin || sp.y > Screen.height + OffscreenMargin)
        {
            pos = candidate;
            return true;
        }

        pos = default;
        return false;
    }

    void Spawn(Vector3 pos)
    {
        Vector3 facing;
        if (_player != null) facing = _player.position - pos;
        else facing = transform.forward;
        var rot = facing.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(facing.normalized, Vector3.up)
            : Quaternion.identity;

        var enemy = Instantiate(EnemyPrefab, pos, rot);
        _alive.Add(enemy);
        _totalSpawned++;

        var health = enemy.GetComponent<PlaneHealth>();
        if (health != null)
        {
            health.Faction = SpawnFaction;
            health.Died += OnSpawnDied;
        }
    }

    void OnSpawnDied()
    {
        if (OneShot) return;
        _nextSpawnAt = Time.time + RespawnDelay;
    }

    void OnDrawGizmos()
    {
        var origin = transform.position;

        var color = FactionColor(SpawnFaction);
        var faint = color; faint.a = 0.15f;
        var medium = color; medium.a = 0.45f;

        Gizmos.color = medium;
        DrawHorizontalCircle(origin + Vector3.up * MinAltitudeOffset, MinRadius, 48);
        DrawHorizontalCircle(origin + Vector3.up * MaxAltitudeOffset, MinRadius, 48);
        DrawHorizontalCircle(origin + Vector3.up * MinAltitudeOffset, MaxRadius, 48);
        DrawHorizontalCircle(origin + Vector3.up * MaxAltitudeOffset, MaxRadius, 48);

        Gizmos.color = faint;
        DrawVerticalSeams(origin, MinRadius, MinAltitudeOffset, MaxAltitudeOffset, 8);
        DrawVerticalSeams(origin, MaxRadius, MinAltitudeOffset, MaxAltitudeOffset, 8);

        Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.35f);
        DrawHorizontalCircle(new Vector3(origin.x, MinWorldY, origin.z), MaxRadius, 48);

        Gizmos.color = color;
        Gizmos.DrawWireSphere(origin, 3f);
    }

    static Color FactionColor(PlaneFaction f)
    {
        switch (f)
        {
            case PlaneFaction.Enemy: return new Color(1f, 0.25f, 0.25f, 1f);
            case PlaneFaction.Ally: return new Color(0.3f, 0.85f, 1f, 1f);
            default: return new Color(0.3f, 1f, 0.4f, 1f);
        }
    }

    static void DrawHorizontalCircle(Vector3 center, float radius, int segments)
    {
        var step = Mathf.PI * 2f / segments;
        var prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            var t = i * step;
            var next = center + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    static void DrawVerticalSeams(Vector3 reference, float radius, float minDy, float maxDy, int seams)
    {
        var step = Mathf.PI * 2f / seams;
        for (int i = 0; i < seams; i++)
        {
            var t = i * step;
            var offset = new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            var bottom = reference + offset + Vector3.up * minDy;
            var top = reference + offset + Vector3.up * maxDy;
            Gizmos.DrawLine(bottom, top);
        }
    }
}
