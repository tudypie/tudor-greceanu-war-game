using System;
using UnityEngine;

public class PlaneShooter : MonoBehaviour
{
    Transform _transform;

    [Tooltip("Transforms the tracer LineRenderers spawn from (e.g. left/right wing guns). " +
             "If empty, a single tracer is used at MuzzleOffsetZ along forward.")]
    public Transform[] MuzzlePoints;

    LineRenderer[] _tracers;

    public event Action<float> Hit;
    public event Action Killed;
    // Raised once per round actually fired (after the heat/overheat gate).
    // Audio drives a single reused looping source off this instead of
    // instantiating an AudioSource per shot.
    public event Action Shot;
    // Raised once on the heat-limit (false -> true) edge. Fire() is gated by
    // !_overheated, so this can't re-enter until heat recovers.
    public event Action OverheatStarted;

    public int Kills { get; private set; }

    [HideInInspector] public bool Trigger;

    public PlaneWeaponStats Stats;

    PlaneHealth _ownHealth;

    [HideInInspector] public bool UseAimDirection;
    [HideInInspector] public Vector3 AimDirection;

    float _heat;
    bool _overheated;
    float _nextFireTime;
    float _tracerHideTime;

    public float Heat => _heat;
    public float HeatNormalized => Stats != null && Stats.MaxHeat > 0f ? _heat / Stats.MaxHeat : 0f;
    public bool Overheated => _overheated;
    public bool IsFiring => Trigger && !_overheated;
    public float MuzzleOffsetZ => Stats != null ? Stats.MuzzleOffsetZ : 0f;

    void Awake()
    {
        _ownHealth = GetComponent<PlaneHealth>();
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneShooter)} on {name} has no Stats assigned.", this);
            return;
        }
        BuildTracers();
    }

    void Start()
    {
        _transform = transform;
    }

    void Update()
    {
        if (Stats == null) return;
        var dt = Time.deltaTime;

        if (Trigger && !_overheated && Time.time >= _nextFireTime)
        {
            Fire();
            _nextFireTime = Time.time + Stats.FireInterval;
        }

        if (!Trigger || _overheated)
        {
            var cool = _overheated ? Stats.OverheatedCoolPerSecond : Stats.CoolPerSecond;
            _heat = Mathf.Max(0f, _heat - cool * dt);
        }

        if (_overheated && _heat <= Stats.MaxHeat * Stats.ResumeHeatFraction)
        {
            _overheated = false;
        }

        if (Time.time >= _tracerHideTime)
        {
            for (int i = 0; i < _tracers.Length; i++)
            {
                if (_tracers[i].enabled) _tracers[i].enabled = false;
            }
        }
    }

    void Fire()
    {
        var origin = _transform.position + _transform.forward * Stats.MuzzleOffsetZ;
        var direction = UseAimDirection ? AimDirection.normalized : _transform.forward;

        Vector3 endPoint;
        if (Physics.Raycast(origin, direction, out var hit, Stats.Range, Stats.HitMask, QueryTriggerInteraction.Ignore))
        {
            endPoint = hit.point;
            var victim = hit.collider.GetComponentInParent<PlaneHealth>();
            if (victim != null && victim != _ownHealth && IsHostile(victim))
            {
                var wasDead = victim.IsDead;
                victim.TakeDamage(Stats.Damage, _ownHealth);
                Hit?.Invoke(Stats.Damage);
                if (!wasDead && victim.IsDead)
                {
                    Kills++;
                    Killed?.Invoke();
                }
            }
        }
        else
        {
            endPoint = origin + direction * Stats.Range;
        }

        ShowTracer(origin, endPoint);
        Shot?.Invoke();

        _heat += Stats.HeatPerShot;
        if (_heat >= Stats.MaxHeat)
        {
            _heat = Stats.MaxHeat;
            _overheated = true;
            OverheatStarted?.Invoke();
        }
    }

    bool IsHostile(PlaneHealth victim)
    {
        if (_ownHealth == null) return true;
        return _ownHealth.IsHostileTo(victim);
    }

    void ShowTracer(Vector3 fallbackFrom, Vector3 to)
    {
        var hasMuzzles = MuzzlePoints != null && MuzzlePoints.Length > 0;
        for (int i = 0; i < _tracers.Length; i++)
        {
            var from = hasMuzzles && MuzzlePoints[i] != null
                ? MuzzlePoints[i].position
                : fallbackFrom;
            _tracers[i].enabled = true;
            _tracers[i].SetPosition(0, from);
            _tracers[i].SetPosition(1, to);
        }
        _tracerHideTime = Time.time + Stats.TracerDuration;
    }

    void BuildTracers()
    {
        var count = MuzzlePoints != null && MuzzlePoints.Length > 0 ? MuzzlePoints.Length : 1;
        _tracers = new LineRenderer[count];

        Material shared = null;
        for (int i = 0; i < count; i++)
        {
            var host = new GameObject($"Tracer_{i}");
            host.transform.SetParent(transform, false);
            var lr = host.AddComponent<LineRenderer>();

            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = Stats.TracerWidth;
            lr.endWidth = Stats.TracerWidth;
            lr.enabled = false;
            if (shared == null) shared = new Material(Shader.Find("Sprites/Default"));
            lr.material = shared;
            lr.startColor = Stats.TracerColor;
            lr.endColor = new Color(Stats.TracerColor.r, Stats.TracerColor.g, Stats.TracerColor.b, 0f);

            _tracers[i] = lr;
        }
    }
}
