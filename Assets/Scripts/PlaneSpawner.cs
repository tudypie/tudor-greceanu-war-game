using System.Collections.Generic;
using UnityEngine;

public class PlaneSpawner : MonoBehaviour
{
    public PlaneAIController EnemyPrefab;

    [Header("Population")]
    public int TargetCount = 3;
    public float RespawnDelay = 2f;

    [Header("Spawn Shell")]
    public float MinRadius = 250f;
    public float MaxRadius = 500f;
    public float MinAltitudeAbovePlayer = -60f;
    public float MaxAltitudeAbovePlayer = 120f;
    public float MinWorldY = 0f;

    [Header("Out-Of-Sight")]
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

        if (_player == null || _camera == null || EnemyPrefab == null) return;
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
        var dir = Random.onUnitSphere;
        var dist = Random.Range(MinRadius, MaxRadius);
        var candidate = _player.position + dir * dist;

        var dy = candidate.y - _player.position.y;
        if (dy < MinAltitudeAbovePlayer || dy > MaxAltitudeAbovePlayer)
        {
            var clampedDy = Mathf.Clamp(dy, MinAltitudeAbovePlayer, MaxAltitudeAbovePlayer);
            candidate.y = _player.position.y + clampedDy;
        }
        if (candidate.y < MinWorldY) candidate.y = MinWorldY;

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
        var toPlayer = _player.position - pos;
        var rot = toPlayer.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toPlayer.normalized, Vector3.up)
            : Quaternion.identity;

        var enemy = Instantiate(EnemyPrefab, pos, rot);
        _alive.Add(enemy);

        var health = enemy.GetComponent<PlaneHealth>();
        if (health != null) health.Died += OnEnemyDied;
    }

    void OnEnemyDied()
    {
        _nextSpawnAt = Time.time + RespawnDelay;
    }
}
