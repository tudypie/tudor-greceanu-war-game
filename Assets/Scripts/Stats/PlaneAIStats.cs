using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane AI Stats", fileName = "PlaneAIStats")]
public class PlaneAIStats : ScriptableObject
{
    [Header("Steering")]
    public float PitchGain = 2.5f;
    public float YawGain = 1.5f;
    public float RollGain = 3.5f;
    [Tooltip("Exponential reaction smoothing on control inputs (seconds). Lower = snappier, more dangerous.")]
    public float ReactionTime = 0.15f;

    [Header("Targeting")]
    [Tooltip("While patrolling (no target), a hostile within this range of the AI's CURRENT position is acquired. Measured from the plane, NOT its spawn point.")]
    public float AcquireRange = 900f;
    [Tooltip("Once chasing, the target is only dropped after it stays beyond this range for LoseTargetTime seconds. Make it comfortably larger than AcquireRange so a brief gap doesn't end the fight.")]
    public float LoseRange = 1500f;
    [Tooltip("Seconds the target must stay beyond LoseRange before the AI gives up and returns to patrol.")]
    public float LoseTargetTime = 6f;
    [Tooltip("How often (s) the AI re-evaluates / switches to the closest hostile.")]
    public float TargetRefreshInterval = 0.5f;
    [Tooltip("A new candidate must be this fraction of the current target's distance (or closer) to steal the lock. <1 adds hysteresis.")]
    public float TargetSwitchHysteresis = 0.75f;

    [Header("Pursuit")]
    [Tooltip("Seconds of positional lag baked into the STEERING aim point. Small = the nose tracks tightly. 0 disables lag.")]
    public float LagSeconds = 0.08f;
    [Tooltip("Seconds of target-velocity lead the AI steers toward (lead pursuit / cutting the corner). Higher = more aggressive intercept.")]
    public float SteerLeadTime = 0.5f;

    [Header("Patrol (only when it has NO target)")]
    public float PatrolRadius = 350f;
    public float PatrolVerticalRange = 70f;
    public float PatrolWaypointReachDistance = 60f;
    public float PatrolWaypointTimeout = 12f;

    [Header("Deliberate Break-Off (give the player a window)")]
    [Tooltip("After chasing continuously for this long (randomised Min..Max seconds) the AI deliberately breaks off so the player can reposition, then re-engages.")]
    public float EngageDurationMin = 9f;
    public float EngageDurationMax = 16f;
    [Tooltip("How long (randomised Min..Max seconds) a break-off lasts before the AI turns back in.")]
    public float BreakOffDurationMin = 2.5f;
    public float BreakOffDurationMax = 4f;
    [Tooltip("After a break-off ends, the AI must chase at least this long before another scheduled break-off can start.")]
    public float BreakOffCooldown = 6f;
    [Tooltip("World-units the break-off aim point is thrown ahead/away so the AI extends out before turning back.")]
    public float ExtendDistance = 300f;
    [Tooltip("Emergency break-off: if the target is closer than this AND badly behind the nose (dot < BadAspectDot), break to avoid an overshoot/merge. Subject to BreakOffCooldown.")]
    public float MergeBreakDistance = 60f;
    [Tooltip("dot(forward, dirToTarget) below this (while inside MergeBreakDistance) triggers an emergency break.")]
    public float BadAspectDot = -0.3f;

    [Header("Firing — genuinely dangerous when on your tail")]
    public float FireConeDeg = 10f;
    public float FireRange = 350f;
    public float FireMinDistance = 15f;
    [Tooltip("Seconds the target must stay within FireConeDeg before the gun aim-assist engages.")]
    public float GunLockAcquireTime = 0.15f;
    [Tooltip("Max degrees the assisted gun solution may bend off the nose. Hard jinkers that pull more than this still beat it.")]
    public float GunLockMaxCorrectionDeg = 16f;
    [Tooltip("Gun spray (deg). ALWAYS applied — even point-blank dead on the tail (~0.7x) — and grows with range / off-tail aspect. Tuned so the AI is a poor shot: most rounds miss, only the occasional one chips you. Raise = misses more, lower = deadlier. This is the main difficulty knob.")]
    public float GunAimNoiseDeg = 5f;
    [Tooltip("The gun is HITSCAN (instant raycast) so it does NOT lead — keep this tiny. It only compensates the sub-step between the AI's solve and the shot. 0.15+ will throw shots ahead of crossing targets.")]
    public float GunLeadTime = 0.02f;

    [Header("Burst Fire")]
    public float BurstMin = 0.5f;
    public float BurstMax = 1.0f;
    public float CooldownMin = 0.7f;
    public float CooldownMax = 1.4f;

    [Header("Collision Avoidance (other planes)")]
    public float AvoidanceRadius = 80f;
    [Tooltip("Cos of forward cone for avoidance. 0 = 90 deg cone, 0.5 = 60 deg cone.")]
    public float AvoidanceAheadDot = 0.2f;
    [Tooltip("World units of sideways bias applied to the aim point when a plane is adjacent.")]
    public float AvoidanceStrength = 200f;

    [Header("Terrain-Relative Altitude Floor")]
    [Tooltip("ABSOLUTE world-Y floor. The terrain floor is never allowed below this even over the sea / map edge.")]
    public float PatrolMinWorldY = 30f;
    [Tooltip("Desired clearance (world units) the AI keeps above the ground. The working floor = max(PatrolMinWorldY, groundHeight + this).")]
    public float TerrainClearance = 80f;
    [Tooltip("How far ahead (seconds of flight at current speed) the terrain is sampled so the AI climbs over a ridge BEFORE reaching it.")]
    public float TerrainLookAheadTime = 3.5f;
    [Tooltip("Number of ground samples taken between the plane and the look-ahead point. More = smoother anticipation, slightly more cost.")]
    [Range(2, 16)] public int TerrainProbeCount = 6;
    [Tooltip("Upward aim bias (world units) applied as the plane nears/drops toward the terrain floor. Keeps the AI from chasing into hills.")]
    public float AltitudeRecoverStrength = 800f;
    [Tooltip("Sideways bias toward the lower flank when a tall ridge is dead ahead, so the AI goes AROUND a mountain instead of stalling up its face.")]
    public float TerrainAvoidLateralStrength = 500f;

    [Header("Service Ceiling (mirror of the floor, inverted)")]
    [Tooltip("How far below the flight model's ServiceCeiling the AI keeps. It clamps its aim point to (ServiceCeiling - this) and adds a soft DOWNWARD bias within this band (reusing AltitudeRecoverStrength), so it levels off instead of porpoising where the flight model would force its nose down anyway. Keep it comfortably larger than the flight CeilingRecoverMargin.")]
    public float CeilingClearance = 120f;
}
