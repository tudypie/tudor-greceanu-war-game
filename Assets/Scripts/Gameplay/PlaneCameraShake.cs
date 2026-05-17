using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(PlaneHealth))]
public class PlaneCameraShake : MonoBehaviour
{
    public PlaneCameraShakeStats Stats;
    public Camera Camera;

    PlaneHealth _health;
    float _shakeStart = -999f;
    float _shakeAmount;
    float _seed;

    void Awake()
    {
        _health = GetComponent<PlaneHealth>();
        _seed = Random.value * 1000f;
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneCameraShake)} on {name} has no Stats assigned.", this);
        }
    }

    void Start()
    {
        if (Camera == null)
        {
            var follow = GetComponent<PlaneCameraFollow>();
            if (follow != null) Camera = follow.Camera;
        }
        if (Camera == null) Camera = UnityEngine.Camera.main;
    }

    void OnEnable()
    {
        if (_health != null) _health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (_health != null) _health.Damaged -= OnDamaged;
    }

    void OnDamaged(float amount)
    {
        if (Stats == null) return;
        var intensity = Mathf.Clamp01(amount / Mathf.Max(0.0001f, Stats.DamageReference));
        var remaining = _shakeAmount * RemainingFraction();
        if (intensity > remaining)
        {
            _shakeAmount = intensity;
            _shakeStart = Time.time;
        }
    }

    float RemainingFraction()
    {
        if (Stats == null || Stats.Duration <= 0f) return 0f;
        var t = (Time.time - _shakeStart) / Stats.Duration;
        return Mathf.Clamp01(1f - t);
    }

    void LateUpdate()
    {
        if (Camera == null || Stats == null) return;
        var remaining = RemainingFraction();
        if (remaining <= 0f || _shakeAmount <= 0f) return;

        var amp = _shakeAmount * remaining * Stats.Magnitude;
        var t = (Time.time - _shakeStart) * Stats.Frequency;
        var pitch = (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f * amp;
        var yaw = (Mathf.PerlinNoise(_seed + 13f, t) - 0.5f) * 2f * amp;
        var roll = (Mathf.PerlinNoise(_seed + 29f, t) - 0.5f) * amp;

        Camera.transform.rotation *= Quaternion.Euler(pitch, yaw, roll);
    }
}
