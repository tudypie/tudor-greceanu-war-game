using UnityEngine;

[RequireComponent(typeof(PlaneHealth))]
public class PlaneHealthBar : MonoBehaviour
{
    PlaneHealth _health;
    Transform _transform;

    public Camera Camera;
    public Vector3 WorldOffset = new Vector3(0f, 3f, 0f);
    public float BarWidth = 80f;
    public float BarHeight = 8f;
    public bool HideWhenFull = false;
    public float MaxVisibleDistance = 300f;
    public float FadeDistance = 60f;

    void Start()
    {
        _transform = transform;
        _health = GetComponent<PlaneHealth>();
        if (Camera == null) Camera = UnityEngine.Camera.main;
    }

    void OnGUI()
    {
        if (!HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;
        if (_health == null || _health.IsDead) return;
        if (Camera == null) Camera = UnityEngine.Camera.main;
        if (Camera == null) return;

        var frac = Mathf.Clamp01(_health.HealthNormalized);
        if (HideWhenFull && frac >= 0.999f) return;

        var worldPos = _transform.position + WorldOffset;
        var dist = Vector3.Distance(Camera.transform.position, worldPos);
        if (MaxVisibleDistance > 0f && dist > MaxVisibleDistance) return;

        var alpha = 1f;
        if (FadeDistance > 0f && MaxVisibleDistance > 0f)
        {
            var fadeStart = Mathf.Max(0f, MaxVisibleDistance - FadeDistance);
            alpha = Mathf.Clamp01(1f - (dist - fadeStart) / FadeDistance);
            if (alpha <= 0f) return;
        }

        var sp = Camera.WorldToScreenPoint(worldPos);
        if (sp.z <= 0f) return;

        var x = sp.x - BarWidth * 0.5f;
        var y = Screen.height - sp.y - BarHeight * 0.5f;

        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        var bg = new Rect(x, y, BarWidth, BarHeight);
        GUI.Box(bg, GUIContent.none);

        var fill = new Rect(x + 2f, y + 2f, (BarWidth - 4f) * frac, BarHeight - 4f);
        var fillColor = Color.Lerp(new Color(1f, 0.2f, 0.2f, 0.9f), new Color(0.3f, 1f, 0.4f, 0.9f), frac);
        fillColor.a *= alpha;
        GUI.color = fillColor;
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = prev;
    }
}
