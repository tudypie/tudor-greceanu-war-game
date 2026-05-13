using System.Collections.Generic;
using UnityEngine;

public class PlaneMinimapHud : MonoBehaviour
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
    public Color LockedColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color PlayerColor = new Color(0.3f, 1f, 0.4f, 1f);
    public Color RingColor = new Color(1f, 0.3f, 0.3f, 0.25f);

    const float RefreshInterval = 0.25f;

    PlaneLockOn _lockOn;
    Texture2D _ringTex;
    Texture2D _triangleTex;
    Texture2D _upArrowTex;
    Texture2D _downArrowTex;
    readonly List<PlaneAIController> _enemies = new List<PlaneAIController>();
    float _nextRefresh;

    void Start()
    {
        _lockOn = GetComponent<PlaneLockOn>();
        _ringTex = BakeRing(128, 2, Color.white);
        _triangleTex = BakeTriangle(16, Color.white, true);
        _upArrowTex = BakeTriangle(16, Color.white, true);
        _downArrowTex = BakeTriangle(16, Color.white, false);
    }

    void Update()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _enemies.Clear();
        var found = FindObjectsByType<PlaneAIController>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++) _enemies.Add(found[i]);
        _nextRefresh = Time.unscaledTime + RefreshInterval;
    }

    void OnGUI()
    {
        if (!HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;
        float halfPx = MinimapSize * 0.5f;
        float cx = Screen.width - Margin - halfPx;
        float cy = Margin + halfPx;

        var boxRect = new Rect(cx - halfPx, cy - halfPx, MinimapSize, MinimapSize);
        GUI.Box(boxRect, GUIContent.none);

        float ringPx = Mathf.Min(MinimapSize, (FireRangeReference / MapRadius) * MinimapSize);
        var ringRect = new Rect(cx - ringPx * 0.5f, cy - ringPx * 0.5f, ringPx, ringPx);
        var prev = GUI.color;
        GUI.color = RingColor;
        GUI.DrawTexture(ringRect, _ringTex);
        GUI.color = prev;

        Vector3 f = transform.forward;
        float yaw = Mathf.Atan2(f.x, f.z);
        float cosY = Mathf.Cos(yaw);
        float sinY = Mathf.Sin(yaw);

        Vector3 origin = transform.position;
        Transform locked = _lockOn != null ? _lockOn.LockedTarget : null;

        prev = GUI.color;
        GUI.color = EnemyColor;
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy == null) continue;
            if (enemy.transform == locked) continue;
            if (!ProjectToMinimap(enemy.transform.position, origin, cosY, sinY, cx, cy, halfPx, false, out Vector2 dot)) continue;
            PickAltitudeIcon(enemy.transform.position.y - origin.y, EnemyDotSize, EnemyArrowSize, out Texture2D tex, out float size);
            DrawIcon(dot, size, tex);
        }
        GUI.color = prev;

        if (locked != null && ProjectToMinimap(locked.position, origin, cosY, sinY, cx, cy, halfPx, true, out Vector2 lockedDot))
        {
            prev = GUI.color;
            GUI.color = LockedColor;
            PickAltitudeIcon(locked.position.y - origin.y, LockedDotSize, LockedArrowSize, out Texture2D lockedTex, out float lockedSize);
            DrawIcon(lockedDot, lockedSize, lockedTex);
            GUI.color = prev;
        }

        prev = GUI.color;
        GUI.color = PlayerColor;
        var pRect = new Rect(cx - PlayerMarkerSize * 0.5f, cy - PlayerMarkerSize * 0.5f, PlayerMarkerSize, PlayerMarkerSize);
        GUI.DrawTexture(pRect, _triangleTex);
        GUI.color = prev;
    }

    static void DrawIcon(Vector2 center, float size, Texture2D tex)
    {
        var r = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
        GUI.DrawTexture(r, tex);
    }

    void PickAltitudeIcon(float dy, float dotSize, float arrowSize, out Texture2D tex, out float size)
    {
        if (dy > AltitudeThreshold) { tex = _upArrowTex; size = arrowSize; }
        else if (dy < -AltitudeThreshold) { tex = _downArrowTex; size = arrowSize; }
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
        float rSq = MapRadius * MapRadius;

        if (distSq > rSq)
        {
            if (!clampToEdge) { guiPos = default; return false; }
            float dist = Mathf.Sqrt(distSq);
            lx = lx / dist * MapRadius;
            ly = ly / dist * MapRadius;
        }

        guiPos = new Vector2(cx + (lx / MapRadius) * halfPx, cy - (ly / MapRadius) * halfPx);
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
