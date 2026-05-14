using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Lock On Stats", fileName = "PlaneLockOnStats")]
public class PlaneLockOnStats : ScriptableObject
{
    [Header("Reticle Box (px @ 1080p)")]
    public float BoxWidth = 360f;
    public float BoxHeight = 280f;

    [Header("Crosshair")]
    [Tooltip("Distance ahead of the plane used to project the free crosshair position.")]
    public float CrosshairRange = 300f;
    [Tooltip("Higher = the crosshair catches up to its target screen position faster.")]
    public float CrosshairSmoothing = 12f;
    [Tooltip("Crosshair arm length in pixels at 1080p — scales with resolution.")]
    public float CrosshairSize = 18f;
    [Tooltip("Line thickness in pixels at 1080p — scales with resolution.")]
    public float LineThickness = 2f;
    [Tooltip("Reference screen height the px values were authored against.")]
    public float ReferenceHeight = 1080f;

    [Header("Lock-On")]
    public float MaxLockDistance = 800f;
    [Tooltip("Time the target must stay inside the box for a lock to complete.")]
    public float AcquireTime = 0.25f;
    [Tooltip("Time the target can stay outside the box before the lock drops.")]
    public float LoseGrace = 0.35f;

    [Header("Colors")]
    public Color BoxColor = new Color(1f, 1f, 1f, 0.35f);
    public Color CrosshairColor = new Color(1f, 1f, 1f, 0.9f);
    public Color LockColor = new Color(1f, 0.25f, 0.25f, 1f);
}
