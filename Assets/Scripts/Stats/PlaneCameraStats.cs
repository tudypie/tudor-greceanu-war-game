using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Camera Stats", fileName = "PlaneCameraStats")]
public class PlaneCameraStats : ScriptableObject
{
    [Header("Follow")]
    public Vector3 FollowOffset = new Vector3(0f, 3f, -8f);

    [Header("Mouse Look")]
    public Vector2 MouseSensitivity = new Vector2(0.2f, 0.15f);
    public bool InvertY = false;
    public float MinPitch = -60f;
    public float MaxPitch = 75f;

    [Header("Smoothing")]
    [Range(0f, 1f)] public float CameraSpring = 0.92f;

    [Header("Cursor")]
    public bool LockCursor = true;
}
