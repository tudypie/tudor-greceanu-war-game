using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

// Authored centreline of the river valley — "drumul celor puțini", the low
// corridor the plane must fly down. This is the single source of truth for
// that route: TerrainGenerator carves the valley along it, and gameplay
// (flight-corridor objective, AI nav, checkpoints) reads the same curve.
//
// The route is a Unity Spline you draw with the native Spline tool in the
// Scene View (select this object, pick the Spline tool, click to lay knots).
// We don't re-author anything here; we just consume the SplineContainer:
//   * RebuildCache()  bakes the spline into a flat world-XZ polyline so
//                      distance/closest-point queries are cheap and
//                      deterministic (the bake calls this once per Generate).
//   * DistanceXZ()     horizontal distance from a world point to the route —
//                      this is what shapes the carved V.
//   * ClosestPoint() / EvaluatePosition01() / Length — for gameplay.
//
// Queries are purely horizontal (XZ); the spline's Y is ignored, exactly like
// the old procedural meander it replaces. Place/scale this object freely —
// the spline is evaluated in world space, so only the knots matter.
[RequireComponent(typeof(SplineContainer))]
public class RiverValleyPath : MonoBehaviour
{
    [Tooltip("Spacing (metres) of the cached polyline sampled off the spline. " +
             "Smaller = truer meanders but a heavier terrain bake. 40 m is " +
             "invisible under a 220 m-wide valley seen from a fast plane.")]
    public float PolylineSpacing = 40f;

    [Tooltip("Hard cap on cached polyline points (bounds the bake cost on a " +
             "very long route).")]
    public int MaxPolylinePoints = 2000;

    [Header("Gizmo")]
    public bool DrawGizmo = true;
    public Color GizmoColor = new(0.3f, 0.7f, 1f, 0.9f);

    SplineContainer _container;
    readonly List<Vector2> _ptsXZ = new();   // world XZ, sampled centreline
    readonly List<float> _cum = new();        // cumulative length to each pt
    float _length;

    public SplineContainer Container =>
        _container != null ? _container : _container = GetComponent<SplineContainer>();

    // Usable only once the spline has an actual line (>= 2 knots).
    public bool HasPath
    {
        get
        {
            var c = Container;
            return c != null && c.Splines.Count > 0 && c.Spline != null
                   && c.Spline.Count >= 2;
        }
    }

    // World length of the route (metres). Cheap; reads the spline directly.
    public float Length => HasPath ? Container.CalculateLength() : 0f;

    // Cached polyline (world XZ). Read-only; rebuilt by RebuildCache().
    public IReadOnlyList<Vector2> PolylineXZ => _ptsXZ;

    // --- Cache --------------------------------------------------------------

    // Bake the spline into a world-XZ polyline. The terrain bake calls this
    // once up front so per-sample distance queries don't re-evaluate the
    // spline 4 M times.
    public void RebuildCache()
    {
        _ptsXZ.Clear();
        _cum.Clear();
        _length = 0f;
        if (!HasPath) return;

        float len = Mathf.Max(Container.CalculateLength(), 1f);
        int segs = Mathf.Clamp(
            Mathf.CeilToInt(len / Mathf.Max(PolylineSpacing, 1f)),
            8, Mathf.Max(8, MaxPolylinePoints - 1));

        Vector3 prevW = Container.EvaluatePosition(0f);
        _ptsXZ.Add(new Vector2(prevW.x, prevW.z));
        _cum.Add(0f);
        for (int i = 1; i <= segs; i++)
        {
            Vector3 w = Container.EvaluatePosition(i / (float)segs);
            var p = new Vector2(w.x, w.z);
            _length += Vector2.Distance(_ptsXZ[^1], p);
            _ptsXZ.Add(p);
            _cum.Add(_length);
        }
    }

    void EnsureCache()
    {
        if (_ptsXZ.Count < 2) RebuildCache();
    }

    // --- Queries ------------------------------------------------------------

    // Horizontal distance (metres) from a world point to the route. Returns
    // +inf when there is no path so callers can fall back cleanly.
    public float DistanceXZ(float worldX, float worldZ)
    {
        EnsureCache();
        if (_ptsXZ.Count < 2) return float.PositiveInfinity;

        var q = new Vector2(worldX, worldZ);
        float best = float.PositiveInfinity;
        for (int i = 1; i < _ptsXZ.Count; i++)
        {
            float d = PointSegmentSqr(q, _ptsXZ[i - 1], _ptsXZ[i]);
            if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }

    // Nearest point on the route to a world position, plus its normalised
    // arc-length t (0..1). For gameplay: "how far along the corridor am I,
    // and where is the centreline I should be hugging." Y comes from the
    // spline so the returned point sits on the authored route.
    public Vector3 ClosestPoint(Vector3 world, out float t01)
    {
        EnsureCache();
        t01 = 0f;
        if (_ptsXZ.Count < 2) return world;

        var q = new Vector2(world.x, world.z);
        float best = float.PositiveInfinity, bestArc = 0f;
        for (int i = 1; i < _ptsXZ.Count; i++)
        {
            Vector2 a = _ptsXZ[i - 1], b = _ptsXZ[i];
            Vector2 ab = b - a;
            float L2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            float u = Mathf.Clamp01(Vector2.Dot(q - a, ab) / L2);
            Vector2 proj = a + ab * u;
            float d = (q - proj).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestArc = Mathf.Lerp(_cum[i - 1], _cum[i], u);
            }
        }
        t01 = _length > 1e-3f ? bestArc / _length : 0f;
        return EvaluatePosition01(t01);
    }

    // World position on the spline at normalised t (0..1). Y included.
    public Vector3 EvaluatePosition01(float t01) =>
        HasPath ? (Vector3)Container.EvaluatePosition(Mathf.Clamp01(t01)) : transform.position;

    static float PointSegmentSqr(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float L2 = ab.sqrMagnitude;
        if (L2 < 1e-6f) return (p - a).sqrMagnitude;
        float u = Mathf.Clamp01(Vector2.Dot(p - a, ab) / L2);
        return (p - (a + ab * u)).sqrMagnitude;
    }

    // --- Gizmo --------------------------------------------------------------

    void OnDrawGizmos()
    {
        if (!DrawGizmo || !HasPath) return;
        if (_ptsXZ.Count < 2) RebuildCache();

        // Lift the line to the spline's Y so it reads against the terrain
        // (the queries themselves ignore Y).
        float y = ((Vector3)Container.EvaluatePosition(0f)).y;
        Gizmos.color = GizmoColor;
        for (int i = 1; i < _ptsXZ.Count; i++)
        {
            Vector2 a = _ptsXZ[i - 1], b = _ptsXZ[i];
            Gizmos.DrawLine(new Vector3(a.x, y, a.y), new Vector3(b.x, y, b.y));
        }
    }
}
