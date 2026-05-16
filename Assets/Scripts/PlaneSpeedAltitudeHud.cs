using UnityEngine;

// Player-only airspeed + altitude instrument panel (top-left corner).
//
// Speed comes straight from the flight model's CurrentSpeed, which is in
// scaled m/s (the world runs at 1/3 real scale for *speeds* only). Multiplying
// by SpeedScale (3) recovers real m/s, then 3.6 gives the km/h a pilot expects
// to read: cruise 56 scaled -> 605 km/h, matching PlaneFlightStats.
//
// Altitude is plain world-Y: scene distances stay full real scale, so Y is
// already metres above sea level, and it's the same frame of reference the
// service-ceiling system in PlaneFlightModel uses. When a Terrain is present
// the height above ground (AGL) is also shown — the number that actually
// matters when hugging the hills.
//
// Colour cues reuse existing flight-model state instead of re-deriving it, so
// they always agree with the stall logic and the ceiling-warning HUD:
//   speed   red while stalling, amber as it nears StallSpeed
//   altitude lerps amber->red with CeilingProximity, red once OverCeiling
[RequireComponent(typeof(PlaneFlightModel))]
public class PlaneSpeedAltitudeHud : MonoBehaviour
{
    public float TopMargin = 16f;
    public float LeftMargin = 16f;
    public float PanelWidth = 188f;
    public int FontSize = 20;

    [Tooltip("Also show height above the terrain surface when a Terrain exists.")]
    public bool ShowAltitudeAboveGround = true;
    [Tooltip("Airspeed within this multiple of StallSpeed shows an amber caution.")]
    public float StallWarnFactor = 1.18f;

    PlaneFlightModel _model;
    PlanePlayerInput _player;
    PlaneHealth _health;
    Terrain _terrain;
    float _terrainBaseY;

    static readonly Color Normal = new Color(0.85f, 0.95f, 1f, 0.95f);
    static readonly Color Caution = new Color(1f, 0.8f, 0.2f, 1f);
    static readonly Color Danger = new Color(1f, 0.3f, 0.22f, 1f);

    void Start()
    {
        _model = GetComponent<PlaneFlightModel>();
        _player = GetComponent<PlanePlayerInput>();
        _health = GetComponent<PlaneHealth>();

        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
        _terrainBaseY = _terrain != null ? _terrain.transform.position.y : 0f;
    }

    void OnGUI()
    {
        if (!HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;
        // Player plane only, and not while dead/crashed.
        if (_model == null || _player == null) return;
        if (_health != null && _health.IsDead) return;

        var stats = _model.Stats;
        // Ratio lives on the flight stats SO (single source of truth); fall
        // back to 1 (raw scaled units) only if the plane is misconfigured.
        var speedScale = stats != null ? stats.RealSpeedScale : 1f;
        var speedKmh = _model.CurrentSpeed * speedScale * 3.6f;
        var altitude = transform.position.y;

        var speedColor = Normal;
        if (stats != null)
        {
            if (_model.IsStalling) speedColor = Danger;
            else if (_model.CurrentSpeed < stats.StallSpeed * StallWarnFactor)
                speedColor = Caution;
        }

        var altColor = _model.OverCeiling
            ? Danger
            : Color.Lerp(Normal, Danger, _model.CeilingProximity);

        var lines = ShowAltitudeAboveGround && _terrain != null ? 3 : 2;
        var rowH = FontSize + 6f;
        var panel = new Rect(LeftMargin, TopMargin, PanelWidth, rowH * lines + 12f);
        GUI.Box(panel, GUIContent.none);

        var labelX = panel.x + 12f;
        var labelW = 54f;
        var valueX = panel.x + 12f;
        var valueW = PanelWidth - 24f;
        var y = panel.y + 6f;

        DrawRow(labelX, valueX, labelW, valueW, y, rowH,
                "SPD", $"{speedKmh:0} km/h", speedColor);
        y += rowH;
        DrawRow(labelX, valueX, labelW, valueW, y, rowH,
                "ALT", $"{altitude:0} m", altColor);

        if (lines == 3)
        {
            var groundY = _terrainBaseY + _terrain.SampleHeight(transform.position);
            var agl = Mathf.Max(0f, altitude - groundY);
            y += rowH;
            DrawRow(labelX, valueX, labelW, valueW, y, rowH,
                    "AGL", $"{agl:0} m",
                    agl < 60f ? Caution : Normal);
        }
    }

    static void DrawRow(float labelX, float valueX, float labelW, float valueW,
                        float y, float h, string label, string value, Color valueColor)
    {
        var skin = GUI.skin.label;
        var prevSize = skin.fontSize;
        var prevAlign = skin.alignment;
        var prevColor = GUI.color;

        skin.fontSize = Mathf.RoundToInt(h - 6f);

        // Dim fixed caption on the left.
        skin.alignment = TextAnchor.MiddleLeft;
        GUI.color = new Color(0.65f, 0.7f, 0.78f, 0.9f);
        GUI.Label(new Rect(labelX, y, labelW, h), label);

        // Right-aligned value with a cheap drop shadow so it stays legible
        // over both bright sky and dark terrain.
        var valueRect = new Rect(valueX, y, valueW, h);
        skin.alignment = TextAnchor.MiddleRight;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.Label(new Rect(valueRect.x + 1f, valueRect.y + 1f, valueRect.width, valueRect.height), value);
        GUI.color = valueColor;
        GUI.Label(valueRect, value);

        skin.fontSize = prevSize;
        skin.alignment = prevAlign;
        GUI.color = prevColor;
    }
}
