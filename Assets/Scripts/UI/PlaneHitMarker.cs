using UnityEngine;

[RequireComponent(typeof(PlaneShooter))]
public class PlaneHitMarker : MonoBehaviour
{
    PlaneShooter _shooter;
    PlaneLockOn _lockOn;

    public float Duration = 0.18f;
    public float ArmLength = 10f;
    public float Gap = 5f;
    public float Thickness = 2.5f;
    public Color HitColor = new Color(1f, 1f, 1f, 0.95f);
    public Color LockedHitColor = new Color(1f, 0.35f, 0.25f, 1f);
    public float ReferenceHeight = 1080f;

    float _showUntil;
    bool _lastHitWasLocked;

    float UiScale => Screen.height / Mathf.Max(1f, ReferenceHeight);

    void Awake()
    {
        _shooter = GetComponent<PlaneShooter>();
        _lockOn = GetComponent<PlaneLockOn>();
    }

    void OnEnable()
    {
        if (_shooter != null) _shooter.Hit += OnHit;
    }

    void OnDisable()
    {
        if (_shooter != null) _shooter.Hit -= OnHit;
    }

    void OnHit(float damage)
    {
        _showUntil = Time.time + Duration;
        _lastHitWasLocked = _lockOn != null && _lockOn.HasLock;
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (Time.time > _showUntil) return;

        Vector2 screen;
        if (_lockOn != null && _lockOn.CrosshairVisible)
            screen = _lockOn.CrosshairScreen;
        else
            screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        var pivot = new Vector2(screen.x, Screen.height - screen.y);

        var s = UiScale;
        var arm = ArmLength * s;
        var gap = Gap * s;
        var th = Mathf.Max(1f, Thickness * s);

        var remaining = Mathf.Clamp01((_showUntil - Time.time) / Mathf.Max(0.0001f, Duration));
        var col = _lastHitWasLocked ? LockedHitColor : HitColor;
        col.a *= remaining;

        var prevColor = GUI.color;
        var prevMatrix = GUI.matrix;
        GUI.color = col;
        GUIUtility.RotateAroundPivot(45f, pivot);
        var tex = Texture2D.whiteTexture;

        GUI.DrawTexture(new Rect(pivot.x - th * 0.5f, pivot.y - gap - arm, th, arm), tex);
        GUI.DrawTexture(new Rect(pivot.x - th * 0.5f, pivot.y + gap, th, arm), tex);
        GUI.DrawTexture(new Rect(pivot.x - gap - arm, pivot.y - th * 0.5f, arm, th), tex);
        GUI.DrawTexture(new Rect(pivot.x + gap, pivot.y - th * 0.5f, arm, th), tex);

        GUI.matrix = prevMatrix;
        GUI.color = prevColor;
    }
}
