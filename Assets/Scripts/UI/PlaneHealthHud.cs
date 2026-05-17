using UnityEngine;

[RequireComponent(typeof(PlaneHealth))]
public class PlaneHealthHud : MonoBehaviour
{
    PlaneHealth _health;

    public float BarWidth = 240f;
    public float BarHeight = 14f;
    public float BottomMargin = 70f;

    void Start()
    {
        _health = GetComponent<PlaneHealth>();
    }

    void OnGUI()
    {
        if (!HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;
        if (_health == null) return;

        var x = (Screen.width - BarWidth) * 0.5f;
        var y = Screen.height - BottomMargin;

        var bg = new Rect(x, y, BarWidth, BarHeight);
        GUI.Box(bg, GUIContent.none);

        var frac = Mathf.Clamp01(_health.HealthNormalized);
        var fill = new Rect(x + 2f, y + 2f, (BarWidth - 4f) * frac, BarHeight - 4f);

        var prev = GUI.color;
        GUI.color = Color.Lerp(new Color(1f, 0.2f, 0.2f, 0.9f), new Color(0.3f, 1f, 0.4f, 0.9f), frac);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = prev;

        var label = $"HP {(int)_health.Health}/{(int)_health.MaxHealth}";
        var labelRect = new Rect(x, y - 18f, BarWidth, 18f);
        var prevAlign = GUI.skin.label.alignment;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.Label(labelRect, label);
        GUI.skin.label.alignment = prevAlign;
    }
}
