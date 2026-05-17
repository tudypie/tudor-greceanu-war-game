using UnityEngine;

// Scene-placed axis-aligned box that bounds the playable area. X/Z edges are
// the horizontal turn-back limit (PlaneFlightModel banks planes back inside);
// the box top replaces PlaneFlightStats.ServiceCeiling as the hard altitude
// ceiling. One per scene, found via Instance; rotation is ignored. With no
// MapBoundary in the scene the feature is inert.
public class MapBoundary : MonoBehaviour
{
    public static MapBoundary Instance { get; private set; }

    [Tooltip("Box size in world units, centred on this object's position. X and Z are the horizontal turn-back limit; the box TOP (position.y + Size.y/2) is the hard altitude ceiling (forced nose-down). The bottom is cosmetic.")]
    public Vector3 Size = new Vector3(2800f, 600f, 2800f);

    [Tooltip("Band (world units) inside the edge where the player warning arms and the AI starts easing back in.")]
    public float WarnBand = 300f;

    [Tooltip("Hysteresis: once the flight model has taken over, control only returns after the plane is back this far inside the edge.")]
    public float RecoverMargin = 200f;

    [Tooltip("Authority of the forced turn-back: a stick-deflection gain on the bearing error toward the centre (mirrors the AI RollGain feel). Eased to zero as the nose swings back inside so it rolls out level.")]
    public float TurnGain = 2f;

    void OnEnable()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning(
                $"[MapBoundary] More than one in the scene; '{name}' replaces '{Instance.name}'.",
                this);
        Instance = this;
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    public Vector3 Center => transform.position;

    // Hard altitude ceiling; replaces PlaneFlightStats.ServiceCeiling.
    public float TopY => transform.position.y + Mathf.Max(Size.y, 0.02f) * 0.5f;

    float HalfX => Mathf.Max(Size.x, 0.02f) * 0.5f;
    float HalfZ => Mathf.Max(Size.z, 0.02f) * 0.5f;

    // Signed distance to the box edge on XZ: negative inside, positive
    // outside. Drives the warn-band ramp and the hysteresis.
    public float SignedEdgeDistanceXZ(Vector3 worldPos)
    {
        var c = transform.position;
        var dx = Mathf.Abs(worldPos.x - c.x) - HalfX;
        var dz = Mathf.Abs(worldPos.z - c.z) - HalfZ;
        return Mathf.Max(dx, dz);
    }

    // Nearest point inside the box shrunk by inset (Y kept). AI uses this to
    // keep its aim point and patrol waypoints inside.
    public Vector3 ClampInsideXZ(Vector3 worldPos, float inset)
    {
        var c = transform.position;
        var hx = Mathf.Max(HalfX - inset, 0.01f);
        var hz = Mathf.Max(HalfZ - inset, 0.01f);
        worldPos.x = Mathf.Clamp(worldPos.x, c.x - hx, c.x + hx);
        worldPos.z = Mathf.Clamp(worldPos.z, c.z - hz, c.z + hz);
        return worldPos;
    }

    // Horizontal distance to the shrunk box (0 inside); the AI's soft
    // inward-bias ramp.
    public float OutsideDistanceXZ(Vector3 worldPos, float inset)
    {
        var c = transform.position;
        var ox = Mathf.Max(Mathf.Abs(worldPos.x - c.x) - Mathf.Max(HalfX - inset, 0.01f), 0f);
        var oz = Mathf.Max(Mathf.Abs(worldPos.z - c.z) - Mathf.Max(HalfZ - inset, 0.01f), 0f);
        return Mathf.Sqrt(ox * ox + oz * oz);
    }

    void OnDrawGizmos()
    {
        var c = transform.position;
        var size = new Vector3(Mathf.Max(Size.x, 0.02f),
                               Mathf.Max(Size.y, 0.02f),
                               Mathf.Max(Size.z, 0.02f));

        // Hard edge: where the flight model takes the stick.
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(c, size);

        // Warning band: where the player caution arms / the AI eases in.
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.55f);
        Gizmos.DrawWireCube(c, new Vector3(
            Mathf.Max(size.x - WarnBand * 2f, 0.02f), size.y,
            Mathf.Max(size.z - WarnBand * 2f, 0.02f)));

        // Recover line: where the forced turn-back releases (hysteresis).
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireCube(c, new Vector3(
            Mathf.Max(size.x - RecoverMargin * 2f, 0.02f), size.y,
            Mathf.Max(size.z - RecoverMargin * 2f, 0.02f)));

        // Faint floor fill so the footprint reads from above.
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.06f);
        Gizmos.DrawCube(new Vector3(c.x, c.y - size.y * 0.5f, c.z),
                        new Vector3(size.x, 0.02f, size.z));
    }
}
