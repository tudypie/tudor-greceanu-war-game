using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Minimap Stats", fileName = "PlaneMinimapStats")]
public class PlaneMinimapStats : ScriptableObject
{
    [Header("Map")]
    public float MapRadius = 500f;
    public float MinimapSize = 180f;
    public float Margin = 20f;
    public float FireRangeReference = 350f;

    [Header("Markers")]
    public float EnemyDotSize = 4f;
    public float LockedDotSize = 6f;
    public float PlayerMarkerSize = 14f;
    public float EnemyArrowSize = 9f;
    public float LockedArrowSize = 12f;
    [Tooltip("Vertical separation (world units) at which an enemy is treated as 'above' or 'below' rather than 'level'.")]
    public float AltitudeThreshold = 15f;

    [Header("Colors")]
    public Color EnemyColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color AllyColor = new Color(0.3f, 0.85f, 1f, 1f);
    public Color LockedColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color PlayerColor = new Color(0.3f, 1f, 0.4f, 1f);
    public Color RingColor = new Color(1f, 0.3f, 0.3f, 0.25f);

    [Header("Cardinal Directions")]
    public bool ShowCardinals = true;
    public Color CardinalColor = new Color(1f, 1f, 1f, 0.7f);
    public int CardinalFontSize = 11;
    [Tooltip("Pixels the N/E/S/W labels sit inside the minimap edge.")]
    public float CardinalInset = 9f;

    [Header("Radar Sweep")]
    [Tooltip("Seconds for the sweep to complete one full rotation.")]
    public float SweepPeriod = 3f;
    [Tooltip("Seconds an enemy stays visible after being pinged before fading out fully.")]
    public float PingFadeDuration = 2f;
    public bool ShowSweepLine = true;
    public Color SweepLineColor = new Color(1f, 0.3f, 0.3f, 0.35f);
    public float SweepLineThickness = 1.5f;
}
