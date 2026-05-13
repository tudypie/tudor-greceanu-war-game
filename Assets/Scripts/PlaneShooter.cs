using UnityEngine;

public class PlaneShooter : MonoBehaviour
{
    Transform _transform;
    LineRenderer _tracer;

    [HideInInspector] public bool Trigger;

    public float Range = 400f;
    public float FireInterval = 0.08f;
    public float MuzzleOffsetZ = 4f;
    public LayerMask HitMask = ~0;

    public float HeatPerShot = 6f;
    public float MaxHeat = 100f;
    public float CoolPerSecond = 35f;
    public float OverheatedCoolPerSecond = 55f;
    [Range(0f, 1f)] public float ResumeHeatFraction = 0.4f;

    public float TracerDuration = 0.04f;
    public Color TracerColor = new Color(1f, 0.85f, 0.3f, 1f);
    public float TracerWidth = 0.12f;

    float _heat;
    bool _overheated;
    float _nextFireTime;
    float _tracerHideTime;

    public float Heat => _heat;
    public float HeatNormalized => _heat / MaxHeat;
    public bool Overheated => _overheated;

    void Awake()
    {
        _tracer = GetComponent<LineRenderer>();
        if (_tracer == null) _tracer = gameObject.AddComponent<LineRenderer>();
        ConfigureTracer();
    }

    void Start()
    {
        _transform = transform;
    }

    void Update()
    {
        var dt = Time.deltaTime;

        if (Trigger && !_overheated && Time.time >= _nextFireTime)
        {
            Fire();
            _nextFireTime = Time.time + FireInterval;
        }

        if (!Trigger || _overheated)
        {
            var cool = _overheated ? OverheatedCoolPerSecond : CoolPerSecond;
            _heat = Mathf.Max(0f, _heat - cool * dt);
        }

        if (_overheated && _heat <= MaxHeat * ResumeHeatFraction)
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
        var origin = _transform.position + _transform.forward * MuzzleOffsetZ;
        var direction = _transform.forward;

        Vector3 endPoint;
        if (Physics.Raycast(origin, direction, out var hit, Range, HitMask, QueryTriggerInteraction.Ignore))
        {
            endPoint = hit.point;
        }
        else
        {
            endPoint = origin + direction * Range;
        }

        ShowTracer(origin, endPoint);

        _heat += HeatPerShot;
        if (_heat >= MaxHeat)
        {
            _heat = MaxHeat;
            _overheated = true;
        }
    }

    void ShowTracer(Vector3 from, Vector3 to)
    {
        _tracer.enabled = true;
        _tracer.SetPosition(0, from);
        _tracer.SetPosition(1, to);
        _tracerHideTime = Time.time + TracerDuration;
    }

    void ConfigureTracer()
    {
        _tracer.positionCount = 2;
        _tracer.useWorldSpace = true;
        _tracer.startWidth = TracerWidth;
        _tracer.endWidth = TracerWidth;
        _tracer.enabled = false;
        if (_tracer.sharedMaterial == null)
        {
            _tracer.material = new Material(Shader.Find("Sprites/Default"));
        }
        _tracer.startColor = TracerColor;
        _tracer.endColor = new Color(TracerColor.r, TracerColor.g, TracerColor.b, 0f);
    }
}
