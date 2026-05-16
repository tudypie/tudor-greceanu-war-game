using UnityEngine;

// MISSION 1 ONLY (Makievska). Light airfield AA. It defends the strip: it
// engages ENEMY-faction planes (the IL-2 / LaGG packets) that come in low
// near the field, throwing tracer up and occasionally drawing blood. Its real
// job is atmosphere + the low-altitude threat gradient — the sky over the
// field is no longer empty and quiet.
//
// Self-contained: builds its own tracer LineRenderer, finds targets via
// PlaneHealth, deals damage straight through the shared PlaneHealth.TakeDamage
// API (no shared script changed). Ground piece — Y is snapped to terrain.
[DefaultExecutionOrder(20)]
public class FlakBattery : MonoBehaviour
{
    [Header("Engagement")]
    [Tooltip("Only ENEMY-faction planes within this slant range are engaged.")]
    public float Range = 1600f;
    [Tooltip("Don't bother with anything higher than this above the battery (keeps it a LOW-altitude threat).")]
    public float EngageCeiling = 700f;
    [Tooltip("Seconds between bursts.")]
    public float ReloadTime = 1.4f;
    public int RoundsPerBurst = 4;
    public float RoundInterval = 0.08f;

    [Header("Lethality (deliberately light — atmosphere first)")]
    [Tooltip("Probability a given round in a burst actually scores, scaled down with range.")]
    [Range(0f, 1f)] public float HitChance = 0.16f;
    public float DamagePerRound = 6f;
    [Tooltip("Aim scatter (deg) of the tracer so it reads as flak, not a sniper.")]
    public float TracerSpreadDeg = 3.5f;

    [Header("Tracer")]
    public Color TracerColor = new(1f, 0.7f, 0.25f, 1f);
    public float TracerWidth = 1.6f;
    public float TracerDuration = 0.09f;
    [Tooltip("Local offset of the muzzle (e.g. up to a gun barrel tip).")]
    public Vector3 MuzzleOffset = new(0f, 6f, 0f);

    Terrain _terrain;
    PlaneHealth[] _planes;
    float _nextScan;
    float _nextBurstAt;
    int _roundsLeft;
    float _nextRoundAt;
    PlaneHealth _target;
    LineRenderer _tracer;
    float _tracerHideAt;

    void Start()
    {
        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
        var p = transform.position;
        if (_terrain != null)
        {
            p.y = _terrain.transform.position.y + _terrain.SampleHeight(p);
            transform.position = p;
        }
        BuildTracer();
    }

    Vector3 Muzzle() => transform.position + MuzzleOffset;

    void Update()
    {
        if (Time.time >= _nextScan)
        {
            _planes = Object.FindObjectsByType<PlaneHealth>(FindObjectsSortMode.None);
            _nextScan = Time.time + 0.5f;
            _target = PickTarget();
        }

        if (_tracer != null && _tracer.enabled && Time.time >= _tracerHideAt)
            _tracer.enabled = false;

        if (_target == null || _target.IsDead) return;

        var muzzle = Muzzle();
        var toT = _target.transform.position - muzzle;
        float dist = toT.magnitude;
        if (dist > Range) return;

        if (_roundsLeft <= 0)
        {
            if (Time.time < _nextBurstAt) return;
            _roundsLeft = Mathf.Max(1, RoundsPerBurst);
            _nextRoundAt = Time.time;
        }

        if (Time.time < _nextRoundAt) return;
        FireRound(muzzle, dist);
        _roundsLeft--;
        _nextRoundAt = Time.time + RoundInterval;
        if (_roundsLeft <= 0) _nextBurstAt = Time.time + ReloadTime;
    }

    PlaneHealth PickTarget()
    {
        if (_planes == null) return null;
        var m = Muzzle();
        PlaneHealth best = null;
        float bestSq = Range * Range;
        foreach (var ph in _planes)
        {
            if (ph == null || ph.IsDead || ph.Faction != PlaneFaction.Enemy) continue;
            var d = ph.transform.position - m;
            if (d.y > EngageCeiling) continue;
            float sq = d.sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = ph; }
        }
        return best;
    }

    void FireRound(Vector3 muzzle, float dist)
    {
        var aimAt = _target.transform.position;
        var dir = (aimAt - muzzle).normalized;
        // Flak scatter.
        dir = Quaternion.AngleAxis(Random.Range(-TracerSpreadDeg, TracerSpreadDeg), Vector3.up)
              * Quaternion.AngleAxis(Random.Range(-TracerSpreadDeg, TracerSpreadDeg), transform.right)
              * dir;

        ShowTracer(muzzle, muzzle + dir * dist);

        // Probabilistic hit, thinning with range — light by design.
        float rangeFrac = 1f - Mathf.Clamp01(dist / Mathf.Max(Range, 1f));
        if (Random.value < HitChance * Mathf.Lerp(0.4f, 1f, rangeFrac))
            _target.TakeDamage(DamagePerRound);
    }

    void BuildTracer()
    {
        var host = new GameObject("FlakTracer");
        host.transform.SetParent(transform, false);
        _tracer = host.AddComponent<LineRenderer>();
        _tracer.positionCount = 2;
        _tracer.useWorldSpace = true;
        _tracer.startWidth = TracerWidth;
        _tracer.endWidth = TracerWidth * 0.4f;
        _tracer.material = new Material(Shader.Find("Sprites/Default"));
        _tracer.startColor = TracerColor;
        _tracer.endColor = new Color(TracerColor.r, TracerColor.g, TracerColor.b, 0f);
        _tracer.enabled = false;
    }

    void ShowTracer(Vector3 a, Vector3 b)
    {
        if (_tracer == null) return;
        _tracer.enabled = true;
        _tracer.SetPosition(0, a);
        _tracer.SetPosition(1, b);
        _tracerHideAt = Time.time + TracerDuration;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.7f, 0.25f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, Range);
        Gizmos.color = new Color(1f, 0.7f, 0.25f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, EngageCeiling);
    }
}
