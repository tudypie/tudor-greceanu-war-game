using System.Collections.Generic;
using UnityEngine;

public class PlaneMinimapHud : MonoBehaviour
{
    public PlaneMinimapStats Stats;

    const float RefreshInterval = 0.25f;

    PlaneLockOn _lockOn;
    PlaneHealth _ownHealth;
    Texture2D _ringTex;
    Texture2D _triangleTex;
    Texture2D _upArrowTex;
    Texture2D _downArrowTex;
    GUIStyle _cardinalStyle;
    readonly List<PlaneHealth> _hostiles = new List<PlaneHealth>();
    readonly List<PlaneHealth> _allies = new List<PlaneHealth>();
    float _nextRefresh;

    void Start()
    {
        _lockOn = GetComponent<PlaneLockOn>();
        _ownHealth = GetComponent<PlaneHealth>();
        _ringTex = BakeRing(128, 2, Color.white);
        _triangleTex = BakeTriangle(16, Color.white, true);
        _upArrowTex = BakeTriangle(16, Color.white, true);
        _downArrowTex = BakeTriangle(16, Color.white, false);
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneMinimapHud)} on {name} has no Stats assigned.", this);
        }
    }

    void Update()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _hostiles.Clear();
        _allies.Clear();
        var found = FindObjectsByType<PlaneHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            var ph = found[i];
            if (ph == null || ph == _ownHealth) continue;
            if (_ownHealth != null && _ownHealth.IsHostileTo(ph)) _hostiles.Add(ph);
            else _allies.Add(ph);
        }
        _nextRefresh = Time.unscaledTime + RefreshInterval;
    }

    void OnGUI()
    {
        if (!HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;
        if (Stats == null) return;

        float halfPx = Stats.MinimapSize * 0.5f;
        float cx = Screen.width - Stats.Margin - halfPx;
        float cy = Stats.Margin + halfPx;

        var boxRect = new Rect(cx - halfPx, cy - halfPx, Stats.MinimapSize, Stats.MinimapSize);
        GUI.Box(boxRect, GUIContent.none);

        float ringPx = Mathf.Min(Stats.MinimapSize, (Stats.FireRangeReference / Stats.MapRadius) * Stats.MinimapSize);
        var ringRect = new Rect(cx - ringPx * 0.5f, cy - ringPx * 0.5f, ringPx, ringPx);
        var prev = GUI.color;
        GUI.color = Stats.RingColor;
        GUI.DrawTexture(ringRect, _ringTex);
        GUI.color = prev;

        Vector3 f = transform.forward;
        float yaw = Mathf.Atan2(f.x, f.z);
        float cosY = Mathf.Cos(yaw);
        float sinY = Mathf.Sin(yaw);

        if (Stats.ShowCardinals)
        {
            if (_cardinalStyle == null)
                _cardinalStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            _cardinalStyle.fontSize = Stats.CardinalFontSize;
            _cardinalStyle.normal.textColor = Stats.CardinalColor;

            float cardRadius = halfPx - Stats.CardinalInset;
            prev = GUI.color;
            GUI.color = Color.white;
            // Cardinals rotated 90° CW: world +Z (old North) is now East so the
            // directional sun reads as sunrise in the east on the compass.
            DrawCardinal("E", 0f, 1f, cosY, sinY, cx, cy, cardRadius);
            DrawCardinal("S", 1f, 0f, cosY, sinY, cx, cy, cardRadius);
            DrawCardinal("W", 0f, -1f, cosY, sinY, cx, cy, cardRadius);
            DrawCardinal("N", -1f, 0f, cosY, sinY, cx, cy, cardRadius);
            GUI.color = prev;
        }

        Vector3 origin = transform.position;
        Transform locked = _lockOn != null ? _lockOn.LockedTarget : null;

        float twoPi = Mathf.PI * 2f;
        float sweepPeriod = Mathf.Max(0.01f, Stats.SweepPeriod);
        float fadeDuration = Mathf.Max(0.01f, Stats.PingFadeDuration);
        float sweepAngle = Mathf.Repeat(Time.unscaledTime / sweepPeriod, 1f) * twoPi;

        if (Stats.ShowSweepLine)
        {
            Matrix4x4 sweepBackup = GUI.matrix;
            GUIUtility.RotateAroundPivot(sweepAngle * Mathf.Rad2Deg - 90f, new Vector2(cx, cy));
            prev = GUI.color;
            GUI.color = Stats.SweepLineColor;
            GUI.DrawTexture(new Rect(cx, cy - Stats.SweepLineThickness * 0.5f, halfPx, Stats.SweepLineThickness), Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.matrix = sweepBackup;
        }

        for (int i = 0; i < _hostiles.Count; i++)
        {
            var hostile = _hostiles[i];
            if (hostile == null || hostile.IsDead) continue;
            if (hostile.transform == locked) continue;

            Vector3 epos = hostile.transform.position;
            float dx = epos.x - origin.x;
            float dz = epos.z - origin.z;
            float lx = dx * cosY - dz * sinY;
            float ly = dx * sinY + dz * cosY;
            float bearing = Mathf.Atan2(lx, ly);
            if (bearing < 0f) bearing += twoPi;
            float delta = Mathf.Repeat(sweepAngle - bearing, twoPi);
            float timeSincePing = delta / twoPi * sweepPeriod;
            float alpha = 1f - timeSincePing / fadeDuration;
            if (alpha <= 0f) continue;

            if (!ProjectToMinimap(epos, origin, cosY, sinY, cx, cy, halfPx, false, out Vector2 dot)) continue;

            var c = Stats.EnemyColor;
            c.a *= alpha;
            GUI.color = c;
            PickAltitudeIcon(epos.y - origin.y, Stats.EnemyDotSize, Stats.EnemyArrowSize, out Texture2D tex, out float size);
            DrawIcon(dot, size, tex);
        }

        for (int i = 0; i < _allies.Count; i++)
        {
            var ally = _allies[i];
            if (ally == null || ally.IsDead) continue;

            Vector3 apos = ally.transform.position;
            if (!ProjectToMinimap(apos, origin, cosY, sinY, cx, cy, halfPx, false, out Vector2 dot)) continue;

            GUI.color = Stats.AllyColor;
            PickAltitudeIcon(apos.y - origin.y, Stats.EnemyDotSize, Stats.EnemyArrowSize, out Texture2D tex, out float size);
            DrawIcon(dot, size, tex);
        }
        GUI.color = Color.white;

        if (locked != null && ProjectToMinimap(locked.position, origin, cosY, sinY, cx, cy, halfPx, true, out Vector2 lockedDot))
        {
            prev = GUI.color;
            GUI.color = Stats.LockedColor;
            PickAltitudeIcon(locked.position.y - origin.y, Stats.LockedDotSize, Stats.LockedArrowSize, out Texture2D lockedTex, out float lockedSize);
            DrawIcon(lockedDot, lockedSize, lockedTex);
            GUI.color = prev;
        }

        prev = GUI.color;
        GUI.color = Stats.PlayerColor;
        var pRect = new Rect(cx - Stats.PlayerMarkerSize * 0.5f, cy - Stats.PlayerMarkerSize * 0.5f, Stats.PlayerMarkerSize, Stats.PlayerMarkerSize);
        GUI.DrawTexture(pRect, _triangleTex);
        GUI.color = prev;
    }

    void DrawCardinal(string label, float wx, float wz, float cosY, float sinY,
                      float cx, float cy, float radius)
    {
        float lx = wx * cosY - wz * sinY;
        float ly = wx * sinY + wz * cosY;
        float sx = cx + lx * radius;
        float sy = cy - ly * radius;
        GUI.Label(new Rect(sx - 10f, sy - 10f, 20f, 20f), label, _cardinalStyle);
    }

    static void DrawIcon(Vector2 center, float size, Texture2D tex)
    {
        var r = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
        GUI.DrawTexture(r, tex);
    }

    void PickAltitudeIcon(float dy, float dotSize, float arrowSize, out Texture2D tex, out float size)
    {
        if (dy > Stats.AltitudeThreshold) { tex = _upArrowTex; size = arrowSize; }
        else if (dy < -Stats.AltitudeThreshold) { tex = _downArrowTex; size = arrowSize; }
        else { tex = Texture2D.whiteTexture; size = dotSize; }
    }

    bool ProjectToMinimap(Vector3 worldPos, Vector3 origin, float cosY, float sinY,
                          float cx, float cy, float halfPx, bool clampToEdge, out Vector2 guiPos)
    {
        float dx = worldPos.x - origin.x;
        float dz = worldPos.z - origin.z;
        float lx = dx * cosY - dz * sinY;
        float ly = dx * sinY + dz * cosY;

        float distSq = lx * lx + ly * ly;
        float rSq = Stats.MapRadius * Stats.MapRadius;

        if (distSq > rSq)
        {
            if (!clampToEdge) { guiPos = default; return false; }
            float dist = Mathf.Sqrt(distSq);
            lx = lx / dist * Stats.MapRadius;
            ly = ly / dist * Stats.MapRadius;
        }

        guiPos = new Vector2(cx + (lx / Stats.MapRadius) * halfPx, cy - (ly / Stats.MapRadius) * halfPx);
        return true;
    }

    static Texture2D BakeRing(int size, int thickness, Color c)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        var clear = new Color(0f, 0f, 0f, 0f);
        float center = (size - 1) * 0.5f;
        float outer = center;
        float inner = outer - thickness;
        float outerSq = outer * outer;
        float innerSq = inner * inner;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d2 = dx * dx + dy * dy;
                pixels[y * size + x] = (d2 <= outerSq && d2 >= innerSq) ? c : clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static Texture2D BakeTriangle(int size, Color c, bool pointUp)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        var clear = new Color(0f, 0f, 0f, 0f);
        int cxPix = size / 2;
        for (int y = 0; y < size; y++)
        {
            float t = pointUp ? (1f - (y / (float)(size - 1))) : (y / (float)(size - 1));
            int halfWidth = Mathf.RoundToInt(t * (size * 0.5f - 1f));
            for (int x = 0; x < size; x++)
            {
                bool inside = (x >= cxPix - halfWidth && x <= cxPix + halfWidth);
                pixels[y * size + x] = inside ? c : clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
