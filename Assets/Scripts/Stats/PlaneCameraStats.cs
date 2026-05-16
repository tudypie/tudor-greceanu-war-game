using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Camera Stats", fileName = "PlaneCameraStats")]
public class PlaneCameraStats : ScriptableObject
{
    [Header("Follow")]
    public Vector3 FollowOffset = new Vector3(0f, 3f, -8f);

    [Header("First Person")]
    // Cockpit point in the plane's local space (right, up, forward).
    public Vector3 FirstPersonOffset = new Vector3(0f, 0.8f, 0.6f);
    public bool StartInFirstPerson = false;

    [Header("Mouse Look")]
    public Vector2 MouseSensitivity = new Vector2(0.2f, 0.15f);
    public bool InvertY = false;
    public float MinPitch = -60f;
    public float MaxPitch = 75f;

    [Header("Cursor")]
    public bool LockCursor = true;
}
