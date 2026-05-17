using UnityEngine;

[RequireComponent(typeof(PlaneShooter))]
public class PlaneShooterHud : MonoBehaviour
{
    PlaneShooter _shooter;

    public float BarWidth = 240f;
    public float BarHeight = 14f;
    public float BottomMargin = 40f;

    public float KillsTopMargin = 16f;
    public int KillsFontSize = 22;

    void Start()
    {
        _shooter = GetComponent<PlaneShooter>();
    }

    void OnGUI()
    {
        if (!HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;
        if (_shooter == null) return;

        var x = (Screen.width - BarWidth) * 0.5f;
        var y = Screen.height - BottomMargin;

        var bg = new Rect(x, y, BarWidth, BarHeight);
        GUI.Box(bg, GUIContent.none);

        var frac = Mathf.Clamp01(_shooter.HeatNormalized);
        var fill = new Rect(x + 2f, y + 2f, (BarWidth - 4f) * frac, BarHeight - 4f);

        var prev = GUI.color;
        GUI.color = _shooter.Overheated
            ? new Color(1f, 0.3f, 0.3f, 0.9f)
            : Color.Lerp(new Color(0.3f, 1f, 0.4f, 0.9f), new Color(1f, 0.85f, 0.2f, 0.9f), frac);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = prev;

        var label = _shooter.Overheated ? "OVERHEATED" : $"HEAT {(int)(frac * 100f)}%";
        var labelRect = new Rect(x, y - 18f, BarWidth, 18f);
        var prevAlign = GUI.skin.label.alignment;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.Label(labelRect, label);
        GUI.skin.label.alignment = prevAlign;

        var killsRect = new Rect(0f, KillsTopMargin, Screen.width, KillsFontSize + 6f);
        var prevSize = GUI.skin.label.fontSize;
        var prevStyleAlign = GUI.skin.label.alignment;
        GUI.skin.label.fontSize = KillsFontSize;
        GUI.skin.label.alignment = TextAnchor.UpperCenter;
        GUI.Label(killsRect, $"KILLS {_shooter.Kills}");
        GUI.skin.label.fontSize = prevSize;
        GUI.skin.label.alignment = prevStyleAlign;
    }
}
