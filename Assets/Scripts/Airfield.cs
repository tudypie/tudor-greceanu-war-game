using System;
using UnityEngine;

// MISSION 1 ONLY (Makievska). The airfield as a defendable objective: a single
// integrity value the IL-2s degrade by completing attack runs over the strip.
// This is the win/lose spine of "Apără aerodromul" — it does not depend on any
// shared script and exposes plain C# events a HUD/mission manager can later
// subscribe to. Self-contained: a debug OnGUI readout is included so the beat
// is testable before a real HUD exists.
//
// Place the host GameObject over the runway (the flattened spawn pad). Y is
// snapped to the terrain at Start; TargetPoint is what the IL-2 run aims at.
public class Airfield : MonoBehaviour
{
    [Tooltip("Abstract integrity 'hit points'. Each completed IL-2 pass removes Il2GroundAttackRun.DamagePerPass.")]
    public float MaxIntegrity = 100f;

    [Tooltip("Mission is FAILED when integrity falls to/below this fraction of max.")]
    [Range(0f, 1f)] public float FailFraction = 0.5f;

    [Tooltip("Radius (m) of the strike zone an IL-2 must reach to deliver a pass.")]
    public float TargetRadius = 220f;

    [Tooltip("Draw a debug integrity readout (Mission-1 stand-in until a real HUD subscribes).")]
    public bool DebugReadout = true;

    public float Integrity { get; private set; }
    public float IntegrityNormalized => Mathf.Clamp01(Integrity / Mathf.Max(MaxIntegrity, 0.0001f));
    public bool IsLost { get; private set; }

    // (normalizedIntegrity) on every change; (—) when the field is lost.
    public event Action<float> IntegrityChanged;
    public event Action Lost;

    Terrain _terrain;

    // Ground point at the runway centre the IL-2s run on.
    public Vector3 TargetPoint
    {
        get
        {
            var p = transform.position;
            if (_terrain == null) _terrain = Terrain.activeTerrain;
            if (_terrain != null) p.y = _terrain.transform.position.y + _terrain.SampleHeight(p);
            return p;
        }
    }

    void Awake()
    {
        Integrity = MaxIntegrity;
        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
    }

    void Start()
    {
        // Sit the marker on the strip.
        var p = transform.position;
        if (_terrain != null)
        {
            p.y = _terrain.transform.position.y + _terrain.SampleHeight(p);
            transform.position = p;
        }
        IntegrityChanged?.Invoke(IntegrityNormalized);
    }

    // Called by Il2GroundAttackRun when a Shturmovik completes a pass over the
    // strip. Returns true if this pass pushed the field into the lost state.
    public bool ApplyAttackDamage(float amount)
    {
        if (IsLost || amount <= 0f) return false;
        Integrity = Mathf.Max(0f, Integrity - amount);
        IntegrityChanged?.Invoke(IntegrityNormalized);
        Debug.Log($"[Airfield] Hit for {amount:0}. Integrity {Integrity:0}/{MaxIntegrity:0} " +
                  $"({IntegrityNormalized:P0}).", this);

        if (!IsLost && IntegrityNormalized <= FailFraction)
        {
            IsLost = true;
            Lost?.Invoke();
            Debug.LogWarning("[Airfield] AERODROMUL COMPROMIS — mission failed.", this);
        }
        return IsLost;
    }

    void OnGUI()
    {
        if (!DebugReadout) return;
        var pct = Mathf.RoundToInt(IntegrityNormalized * 100f);
        var label = IsLost ? $"AERODROM: PIERDUT ({pct}%)" : $"AERODROM: {pct}%";
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = IsLost ? Color.red
                       : Color.Lerp(Color.red, Color.green, Mathf.InverseLerp(FailFraction, 1f, IntegrityNormalized)) }
        };
        GUI.Label(new Rect(20f, 20f, 420f, 32f), label, style);
    }

    void OnDrawGizmos()
    {
        var c = transform.position;
        var t = Terrain.activeTerrain;
        if (t != null) c.y = t.transform.position.y + t.SampleHeight(c);
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
        const int seg = 48;
        var prev = c + new Vector3(TargetRadius, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            var n = c + new Vector3(Mathf.Cos(a) * TargetRadius, 0f, Mathf.Sin(a) * TargetRadius);
            Gizmos.DrawLine(prev, n);
            prev = n;
        }
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.35f);
        Gizmos.DrawLine(c, c + Vector3.up * 120f);
    }
}
