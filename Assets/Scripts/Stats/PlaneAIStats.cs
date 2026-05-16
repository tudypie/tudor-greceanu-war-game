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
    [Tooltip("Multiplier on the human player's effective distance during target selection. >1 makes the AI prefer other AI (allies) over the player; it only commits to the player when the player is the only hostile or vastly closer. 1 = no bias. Overridden while retaliating.")]
    public float PlayerTargetBias = 1f;

    [Header("Target Crowd Control (don't dogpile one plane)")]
    [Tooltip("Max enemy AIs that may pursue any one PLAYER-faction plane at once. An AI that would have picked the player while the cap is full instead falls through to another hostile (an ally) or keeps patrolling/wandering. It does NOT evict an AI already locked on. 0 = unlimited (old behaviour). The count is GLOBAL across every AI in the scene. Retaliation ignores this cap (whoever shoots it gets chased regardless).")]
    public int MaxAttackersOnPlayer = 4;
    [Tooltip("Same crowd cap, but applied to ANY single friendly target (player OR ally), so once the player is full the overflow doesn't just collapse onto one ally. 0 = unlimited.")]
    public int MaxAttackersPerTarget = 0;

    [Header("Distraction (fly around / act dumb instead of swarming)")]
    [Tooltip("Probability the AI actually commits the instant it could acquire a target. On a miss it stays on patrol and is 'distracted' (wandering, ignoring all targets) for a Distracted duration, then may roll again. 1 = always commits (old behaviour); lower = a more confused, scattered swarm.")]
    [Range(0f, 1f)] public float EngageChance = 0.65f;
    [Tooltip("Chance, evaluated once per TargetRefreshInterval while ALREADY chasing, that the AI loses interest and wanders off distracted instead of pressing the attack. Suppressed while retaliating. 0 = never self-disengages (old behaviour). Small values compound over time: ~0.015 at a 0.5s refresh ≈ a ~30s mean attention span before it breaks contact.")]
    [Range(0f, 1f)] public float DistractionChance = 0.015f;
    [Tooltip("While distracted the AI ignores every target and just patrols/wanders. Duration randomised between Min and Max seconds. Being shot (retaliation) still snaps it straight out of it.")]
    public float DistractedDurationMin = 5f;
    public float DistractedDurationMax = 12f;

    [Header("Retaliation (turn on whoever shoots it)")]
    [Tooltip("When hit by a hostile, immediately drop everything and pursue the attacker — even if it was patrolling, mid break-off, or the attacker is beyond AcquireRange.")]
    public bool RetaliateWhenShot = true;
    [Tooltip("Seconds after the last hit the AI stays locked onto its attacker: it won't switch to a closer hostile, won't lose it by range, and skips its deliberate break-off. Each new hit refreshes the timer.")]
    public float RetaliationDuration = 8f;

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
    [Tooltip("Gun spray (deg). ALWAYS applied — even point-blank dead on the tail (scaled by GunAimNoisePointBlankScale) — and grows with range / off-tail aspect. This is the main difficulty knob: lower = the AI lands shots and is deadly, higher = it sprays and misses. Kept small so when it has your tail it actually hits.")]
    public float GunAimNoiseDeg = 1.5f;
    [Tooltip("Frequency (Hz) of the Perlin jitter on the noised shot. High enough that a burst sprays between shots instead of walking on as one coherent block.")]
    public float GunAimNoiseFrequency = 9f;
    [Tooltip("Fraction of GunAimNoiseDeg applied at point-blank range; it ramps up to the full value at FireRange. 1 = noise constant with range.")]
    [Range(0f, 1f)] public float GunAimNoisePointBlankScale = 0.7f;
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
    [Tooltip("How often (s) the nearby-planes list used for avoidance is rebuilt. Lower = more responsive, slightly more cost.")]
    public float AvoidanceRefreshInterval = 0.5f;

    [Header("Terrain-Relative Altitude Floor")]
    [Tooltip("ABSOLUTE world-Y floor. The terrain floor is never allowed below this even over the sea / map edge.")]
    public float PatrolMinWorldY = 30f;
    [Tooltip("Desired clearance (world units) the AI keeps above the ground. The working floor = max(PatrolMinWorldY, groundHeight + this).")]
    public float TerrainClearance = 80f;
    [Tooltip("How far ahead (seconds of flight at current speed) the terrain is sampled so the AI climbs over a ridge BEFORE reaching it.")]
    public float TerrainLookAheadTime = 3.5f;
    [Tooltip("Floor under the speed used for the terrain look-ahead distance, so a near-stalled plane still anticipates ground ahead.")]
    public float TerrainLookAheadMinSpeed = 20f;
    [Tooltip("Hard floor-recovery hysteresis margin = max(TerrainClearance * this, FloorRecoverMarginMin). The AI must claw back this far above the floor before it stops the emergency climb-out.")]
    [Range(0f, 1f)] public float FloorRecoverMarginFraction = 0.4f;
    [Tooltip("Absolute minimum (world units) for the floor-recovery hysteresis margin.")]
    public float FloorRecoverMarginMin = 5f;
    [Tooltip("Number of ground samples taken between the plane and the look-ahead point. More = smoother anticipation, slightly more cost.")]
    [Range(2, 16)] public int TerrainProbeCount = 6;
    [Tooltip("Upward aim bias (world units) applied as the plane nears/drops toward the terrain floor. Keeps the AI from chasing into hills.")]
    public float AltitudeRecoverStrength = 800f;
    [Tooltip("DEPRECATED — superseded by the predictive Ground-Collision Avoidance block below; no longer read by PlaneAIController. Kept only so existing serialized .asset files don't break.")]
    public float TerrainAvoidLateralStrength = 500f;

    [Header("Service Ceiling (mirror of the floor, inverted)")]
    [Tooltip("How far below the flight model's ServiceCeiling the AI keeps. It clamps its aim point to (ServiceCeiling - this) and adds a soft DOWNWARD bias within this band (reusing AltitudeRecoverStrength), so it levels off instead of porpoising where the flight model would force its nose down anyway. Keep it comfortably larger than the flight CeilingRecoverMargin.")]
    public float CeilingClearance = 120f;

    [Header("Map Boundary (horizontal mirror of the ceiling)")]
    [Tooltip("How far inside the scene's MapBoundary box the AI keeps. It clamps its aim point (and patrol waypoints) to the box shrunk by this, and adds a soft INWARD bias over this band out to the hard edge (reusing AltitudeRecoverStrength), so it turns back on its own instead of grinding against the flight model's hard turn-back. Keep it comfortably larger than the MapBoundary RecoverMargin.")]
    public float BoundaryClearance = 250f;

    [Header("Predictive Ground-Collision Avoidance (GCAS)")]
    [Tooltip("Master switch. When off, the AI falls back to the legacy soft altitude bias (Layer 1 floor clamp + Layer 3 hard pull-up still protect it).")]
    public bool GcaEnabled = true;
    [Tooltip("When off (default), the terrain threat is estimated by casting the TRUE velocity forward (cheap, robust). When on, it is estimated by simulating the plane's best-effort pull-up recovery (more accurate over rising terrain, slightly costlier). Flip on only if field-testing shows late triggers on the steepest ridges.")]
    public bool GcaUsePredictiveSim = false;
    [Tooltip("Seconds the plane needs to arrest a dive and start climbing. Used as the time reference for the threat ramp: a predicted ground contact closer than this in time saturates the threat. Larger = the AI reacts earlier / more cautiously.")]
    public float GcaRecoverTime = 1.4f;
    [Tooltip("Seconds of flight (along the true velocity / recovery path) sampled ahead for terrain. Should comfortably exceed GcaRecoverTime.")]
    public float GcaProbeHorizonTime = 4f;
    [Tooltip("Terrain is sampled every Nth integration sub-step (sub-step = physics dt). Lower = denser sampling (catches narrow spikes), slightly more cost.")]
    [Range(1, 8)] public int GcaProbeStride = 3;
    [Tooltip("Half-angle (deg) of the lateral sensing fan added on top of the estimated turn, so a banking plane checks the curved ground track it will actually fly.")]
    public float GcaFanHalfAngleDeg = 8f;
    [Tooltip("Reference depth (world units) a predicted floor breach is normalised against for the depth component of the threat. Roughly TerrainClearance.")]
    public float GcaDepthRef = 80f;
    [Tooltip("Time constant (s) for the threat RISING. Small = arms quickly for safety.")]
    public float GcaThreatAttackTime = 0.10f;
    [Tooltip("Time constant (s) for the threat FALLING. Large = releases slowly to avoid chatter.")]
    public float GcaThreatReleaseTime = 0.60f;
    [Tooltip("At/above this threat the graduated overlay begins (gentle wings-level + aim toward the climb-out point).")]
    [Range(0f, 1f)] public float GcaSoftThreat = 0.25f;
    [Tooltip("While Pursuing, at/above this threat the AI abandons the chase and enters TerrainEvade (climb out, then re-engage). Must exceed GcaReengageThreat.")]
    [Range(0f, 1f)] public float GcaDisengageThreat = 0.55f;
    [Tooltip("TerrainEvade exits once the threat falls to/below this (Schmitt: lower than GcaDisengageThreat) AND GcaEvadeMinTime has elapsed.")]
    [Range(0f, 1f)] public float GcaReengageThreat = 0.30f;
    [Tooltip("Minimum seconds the AI stays in TerrainEvade before it is allowed to re-engage, so it doesn't flip straight back into the dive.")]
    public float GcaEvadeMinTime = 1f;
    [Tooltip("At/above this threat the AI commands a decisive full nose-up, wings-level pull (bypasses proportional PitchGain) — the predictive equivalent of the hard floor override, fired while still recoverable.")]
    [Range(0f, 1f)] public float GcaHardThreat = 0.80f;
    [Tooltip("At/above this threat the guns go cold (no point shooting while saving the airframe).")]
    [Range(0f, 1f)] public float GcaGunColdThreat = 0.50f;
    [Tooltip("World units the climb-out aim point is placed ABOVE the worst floor ahead, so steering toward it actually clears the ridge with margin.")]
    public float GcaClimbOutMargin = 80f;
    [Tooltip("Number of samples taken along a patrol/break-off route so the straight path to the point is lifted above any hill it would otherwise pass through (not just the endpoint).")]
    [Range(2, 16)] public int GcaRouteProbeCount = 6;
}
