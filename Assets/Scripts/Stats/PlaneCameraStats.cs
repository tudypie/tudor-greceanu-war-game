using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Camera Stats", fileName = "PlaneCameraStats")]
public class PlaneCameraStats : ScriptableObject
{
    [Header("Follow")]
    public Vector3 FollowOffset = new Vector3(0f, 3f, -8f);
    [Tooltip("How fast the chase cam swings around to sit behind the plane's " +
             "heading. Higher = snappier, lower = lazier trailing.")]
    public float FollowYawSmoothing = 6f;
    [Tooltip("How fast the chase cam matches the plane's climb/dive angle.")]
    public float FollowPitchSmoothing = 5f;
    [Tooltip("Fraction of the plane's climb angle the camera adopts. Lower " +
             "keeps the horizon steadier through loops.")]
    [Range(0f, 1f)] public float FollowPitchFactor = 0.5f;

    [Header("Aim Lean")]
    [Tooltip("Degrees the camera leans horizontally toward the reticle when " +
             "it's pushed to the edge of its travel.")]
    public float AimLeanYaw = 9f;
    [Tooltip("Degrees the camera leans vertically toward the reticle.")]
    public float AimLeanPitch = 6f;

    [Header("Free Look (hold RMB)")]
    [Tooltip("Degrees the camera pans per unit of mouse delta while RMB is held.")]
    public Vector2 FreeLookSensitivity = new Vector2(0.18f, 0.14f);
    public bool InvertFreeLookY = false;
    [Tooltip("How fast the camera eases back to the auto-trail after RMB is released.")]
    public float FreeLookReturnSmoothing = 8f;

    [Header("First Person")]
    // Cockpit point in the plane's local space (right, up, forward).
    public Vector3 FirstPersonOffset = new Vector3(0f, 0.8f, 0.6f);
    public bool StartInFirstPerson = false;

    [Header("Pitch Clamp")]
    public float MinPitch = -60f;
    public float MaxPitch = 75f;

    [Header("Cursor")]
    public bool LockCursor = true;
}
