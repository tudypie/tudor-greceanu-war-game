using System;
using UnityEngine;

public class PlaneShooter : MonoBehaviour
{
    Transform _transform;
    LineRenderer _tracer;

    public event Action<float> Hit;
    public event Action Killed;

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
    public float MuzzleOffsetZ => Stats != null ? Stats.MuzzleOffsetZ : 0f;

    void Awake()
    {
        _tracer = GetComponent<LineRenderer>();
        if (_tracer == null) _tracer = gameObject.AddComponent<LineRenderer>();
        _ownHealth = GetComponent<PlaneHealth>();
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneShooter)} on {name} has no Stats assigned.", this);
            return;
        }
        ConfigureTracer();
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

        if (_tracer.enabled && Time.time >= _tracerHideTime)
        {
            _tracer.enabled = false;
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
                victim.TakeDamage(Stats.Damage);
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

        _heat += Stats.HeatPerShot;
        if (_heat >= Stats.MaxHeat)
        {
            _heat = Stats.MaxHeat;
            _overheated = true;
        }
    }

    bool IsHostile(PlaneHealth victim)
    {
        if (_ownHealth == null) return true;
        return _ownHealth.IsHostileTo(victim);
    }

    void ShowTracer(Vector3 from, Vector3 to)
    {
        _tracer.enabled = true;
        _tracer.SetPosition(0, from);
        _tracer.SetPosition(1, to);
        _tracerHideTime = Time.time + Stats.TracerDuration;
    }

    void ConfigureTracer()
    {
        _tracer.positionCount = 2;
        _tracer.useWorldSpace = true;
        _tracer.startWidth = Stats.TracerWidth;
        _tracer.endWidth = Stats.TracerWidth;
        _tracer.enabled = false;
        if (_tracer.sharedMaterial == null)
        {
            _tracer.material = new Material(Shader.Find("Sprites/Default"));
        }
        _tracer.startColor = Stats.TracerColor;
        _tracer.endColor = new Color(Stats.TracerColor.r, Stats.TracerColor.g, Stats.TracerColor.b, 0f);
    }
}
