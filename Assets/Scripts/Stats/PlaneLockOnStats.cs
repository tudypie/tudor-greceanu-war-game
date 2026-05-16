using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Lock On Stats", fileName = "PlaneLockOnStats")]
public class PlaneLockOnStats : ScriptableObject
{
    [Header("Reticle Box (px @ 1080p)")]
    public float BoxWidth = 360f;
    public float BoxHeight = 280f;

    [Header("Free Aim Reticle")]
    [Tooltip("Screen pixels the reticle moves per unit of mouse delta (x, y).")]
    public Vector2 ReticleSensitivity = new Vector2(1f, 1f);
    [Tooltip("Invert vertical reticle movement.")]
    public bool InvertReticleY = false;
    [Tooltip("How far the reticle can travel from screen center, as a fraction " +
             "of half the screen width/height. 1 = all the way to the edge.")]
    [Range(0.05f, 1f)] public float ReticleRangeX = 0.42f;
    [Range(0.05f, 1f)] public float ReticleRangeY = 0.36f;
    [Tooltip("Fraction of the way back to screen center the reticle eases per " +
             "second when the mouse is still. 0 = stays where you left it.")]
    public float ReticleRecenterPerSecond = 0f;
    [Tooltip("Distance ahead the aim point sits when the camera ray hits nothing " +
             "— the gun-convergence range for free aim.")]
    public float AimConvergeDistance = 350f;

    [Header("Crosshair")]
    [Tooltip("Crosshair arm length in pixels at 1080p — scales with resolution.")]
    public float CrosshairSize = 18f;
    [Tooltip("Line thickness in pixels at 1080p — scales with resolution.")]
    public float LineThickness = 2f;
    [Tooltip("Reference screen height the px values were authored against.")]
    public float ReferenceHeight = 1080f;

    [Header("Lock-On (soft aim assist)")]
    public float MaxLockDistance = 800f;
    [Tooltip("Time the target must stay inside the box for a lock to complete.")]
    public float AcquireTime = 0.25f;
    [Tooltip("Time the target can stay outside the box before the lock drops.")]
    public float LoseGrace = 0.35f;

    [Header("Colors")]
    [Tooltip("Outline of the rectangle the reticle is free to move within " +
             "(ReticleRangeX/Y). Set alpha to 0 to hide it.")]
    public Color ReticleBoundsColor = new Color(0.5f, 0.8f, 1f, 0.25f);
    public Color BoxColor = new Color(1f, 1f, 1f, 0.35f);
    public Color CrosshairColor = new Color(1f, 1f, 1f, 0.9f);
    public Color LockColor = new Color(1f, 0.25f, 0.25f, 1f);
}
