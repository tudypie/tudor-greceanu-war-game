using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(PlaneHealth))]
public class PlaneCameraShake : MonoBehaviour
{
    public Camera Camera;

    [Tooltip("Peak angular shake in degrees at full intensity.")]
    public float Magnitude = 1.6f;

    [Tooltip("How long a shake takes to fully decay.")]
    public float Duration = 0.35f;

    [Tooltip("Damage amount that produces a full-magnitude shake. Smaller hits scale down linearly.")]
    public float DamageReference = 25f;

    [Tooltip("Noise sample rate — higher = more jittery, lower = more swaying.")]
    public float Frequency = 28f;

    PlaneHealth _health;
    float _shakeStart = -999f;
    float _shakeAmount;
    float _seed;

    void Awake()
    {
        _health = GetComponent<PlaneHealth>();
        _seed = Random.value * 1000f;
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
        var intensity = Mathf.Clamp01(amount / Mathf.Max(0.0001f, DamageReference));
        var remaining = _shakeAmount * RemainingFraction();
        if (intensity > remaining)
        {
            _shakeAmount = intensity;
            _shakeStart = Time.time;
        }
    }

    float RemainingFraction()
    {
        if (Duration <= 0f) return 0f;
        var t = (Time.time - _shakeStart) / Duration;
        return Mathf.Clamp01(1f - t);
    }

    void LateUpdate()
    {
        if (Camera == null) return;
        var remaining = RemainingFraction();
        if (remaining <= 0f || _shakeAmount <= 0f) return;

        var amp = _shakeAmount * remaining * Magnitude;
        var t = (Time.time - _shakeStart) * Frequency;
        var pitch = (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f * amp;
        var yaw = (Mathf.PerlinNoise(_seed + 13f, t) - 0.5f) * 2f * amp;
        var roll = (Mathf.PerlinNoise(_seed + 29f, t) - 0.5f) * amp;

        Camera.transform.rotation *= Quaternion.Euler(pitch, yaw, roll);
    }
}
