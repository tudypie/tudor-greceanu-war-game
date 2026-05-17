using System;
using UnityEngine;

// Mission-1 protect target: an objective facade over a sibling PlaneHealth.
[RequireComponent(typeof(PlaneHealth))]
public class Airfield : MonoBehaviour
{
    public static Airfield Instance { get; private set; }

    [Tooltip("Radius strikers aim their runs at; also the selected gizmo size.")]
    public float StrikeRadius = 150f;
    public bool DebugReadout = true;

    public event Action Destroyed;

    PlaneHealth _health;
    bool _failed;

    public float Integrity01 => _health != null ? _health.HealthNormalized : 0f;
    public bool IsDestroyed => _health != null && _health.IsDead;

    void Awake()
    {
        if (Instance == null) Instance = this;
        _health = GetComponent<PlaneHealth>();
        _health.DestroyOnDeath = false; // keep the objective alive to show failure
        _health.Faction = PlaneFaction.Ally;
        _health.Died += OnDied;
    }

    void OnDestroy()
    {
        if (_health != null) _health.Died -= OnDied;
        if (Instance == this) Instance = null;
    }

    public void ApplyDamage(float amount, PlaneHealth attacker = null)
    {
        if (_health != null) _health.TakeDamage(amount, attacker);
    }

    void OnDied()
    {
        if (_failed) return;
        _failed = true;
        Debug.Log("Airfield destroyed - MISSION FAILED");
        Destroyed?.Invoke();
    }

    void OnGUI()
    {
        if (!DebugReadout || !HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;

        var prevAlign = GUI.skin.label.alignment;
        var prevSize = GUI.skin.label.fontSize;
        var prevColor = GUI.color;

        if (_failed)
        {
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = 28;
            GUI.color = new Color(1f, 0.3f, 0.25f, 1f);
            GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 40f),
                "MISSION FAILED — AIRFIELD DESTROYED");
        }
        else
        {
            var pct = Mathf.CeilToInt(Integrity01 * 100f);
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.skin.label.fontSize = 18;
            GUI.color = Color.Lerp(new Color(1f, 0.3f, 0.25f, 1f),
                new Color(0.4f, 1f, 0.5f, 1f), Integrity01);
            GUI.Label(new Rect(0f, 40f, Screen.width, 26f), $"AIRFIELD {pct}%");
        }

        GUI.color = prevColor;
        GUI.skin.label.alignment = prevAlign;
        GUI.skin.label.fontSize = prevSize;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, StrikeRadius);
    }
}
