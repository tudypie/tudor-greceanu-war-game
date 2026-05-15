using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane AI Stats", fileName = "PlaneAIStats")]
public class PlaneAIStats : ScriptableObject
{
    [Header("Steering")]
    public float PitchGain = 1.8f;
    public float YawGain = 1.0f;
    public float RollGain = 2.5f;

    [Header("Commit Window")]
    public float CommitMin = 0.8f;
    public float CommitMax = 1.5f;

    [Header("Targeting")]
    public float EngagementRadius = 350f;
    public float TargetSwitchHysteresis = 0.75f;
    public float TargetRefreshInterval = 0.5f;

    [Header("Pursuit")]
    [Tooltip("Seconds of lag baked into the aim point. Higher = aim trails target more, easier to dodge.")]
    public float LagSeconds = 0.35f;

    [Header("Patrol")]
    public float PatrolRadius = 350f;
    public float PatrolVerticalRange = 60f;
    [Tooltip("Hard minimum world Y for ALL AI aim points — patrol, extend, AND chasing. The altitude floor.")]
    public float PatrolMinWorldY = 30f;
    public float PatrolWaypointReachDistance = 70f;
    public float PatrolWaypointTimeout = 12f;

    [Header("Extend (Break-Off)")]
    public float ExtendMin = 2.0f;
    public float ExtendMax = 4.0f;
    public float ExtendDistance = 200f;
    [Tooltip("If dot(forward, dirToTarget) drops below this while chasing, break off.")]
    public float BadAspectDot = -0.2f;
    [Tooltip("After (re)entering Chasing, the AI must commit to turning toward the target for at least this long before another Extend is allowed. Prevents perpetual extend/chase ping-pong straight-line flight.")]
    public float RepositionDuration = 2.5f;

    [Header("Firing")]
    public float FireConeDeg = 5f;
    public float FireRange = 280f;
    public float FireMinDistance = 12f;

    [Header("Gunnery (AI Lock — kept weaker than the player)")]
    [Tooltip("Seconds the target must stay within FireConeDeg before the gun aim-assist engages.")]
    public float GunLockAcquireTime = 0.8f;
    [Tooltip("Max degrees the assisted gun solution may bend off the nose. Smaller = AI must point closer; hard jinkers beat it.")]
    public float GunLockMaxCorrectionDeg = 5f;
    [Tooltip("Base aim error in degrees; scaled up with range and off-tail aspect so only close, saddled shots land.")]
    public float GunAimNoiseDeg = 4f;
    [Tooltip("Seconds of target-velocity lead baked into the gun solution.")]
    public float GunLeadTime = 0.15f;

    [Header("Burst Fire")]
    public float BurstMin = 0.3f;
    public float BurstMax = 0.5f;
    public float CooldownMin = 1.2f;
    public float CooldownMax = 2.0f;

    [Header("Feel")]
    public float ReactionTime = 0.35f;

    [Header("Collision Avoidance")]
    public float AvoidanceRadius = 80f;
    [Tooltip("Cos of forward cone for avoidance. 0 = 90 deg cone, 0.5 = 60 deg cone.")]
    public float AvoidanceAheadDot = 0.2f;
    [Tooltip("World units of sideways bias applied to aim point when a plane is adjacent.")]
    public float AvoidanceStrength = 200f;

    [Header("Terrain Avoidance")]
    [Tooltip("Forward spherecast distance for terrain/obstacles.")]
    public float TerrainLookAhead = 180f;
    [Tooltip("Sphere radius used for the forward cast — should comfortably wrap the plane.")]
    public float TerrainSafetyRadius = 14f;
    [Tooltip("World units of bias applied when an obstacle is right in front.")]
    public float TerrainStrength = 600f;

    [Header("Altitude Floor")]
    [Tooltip("Upward aim bias (world units) applied as the plane nears/drops below PatrolMinWorldY. Keeps AI from chasing into the ground; 0 disables.")]
    public float AltitudeRecoverStrength = 800f;
}
