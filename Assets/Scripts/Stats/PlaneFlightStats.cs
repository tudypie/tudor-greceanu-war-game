using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Flight Stats", fileName = "PlaneFlightStats")]
public class PlaneFlightStats : ScriptableObject
{
    [Header("Thrust")]
    // The world runs at 1/3 real scale (lengths and linear velocities scaled
    // by s = 1/3 for float precision and physics stability — angular rates,
    // times, and ratios are left at real scale). Thrust is interpreted
    // directly as cruise speed in scaled m/s:
    //   56  scaled m/s == 168 real m/s ~= 605 km/h (P-39 cruise)
    //   66.7 scaled m/s == 200 real m/s ~= 720 km/h (boost / WEP)
    public float NormalThrust = 56f;
    public float MaxThrust = 66.7f;
    public float ThrustAgilityMultiplier = 1.8f;
    // Holding the boost input spools the throttle linearly from NormalThrust
    // up to MaxThrust; releasing it bleeds back down. Rates are in
    // throttle-fraction per second, so 0.5 == 2 s to swing the full range.
    // Spool-up is deliberately slower than spool-down so speed is a dial the
    // pilot can hold partway, not an on/off switch.
    public float ThrottleAccelRate = 0.5f;
    public float ThrottleDecelRate = 0.7f;

    [Header("World Scale")]
    // Multiply a scaled m/s figure (CurrentSpeed, thrust) by this to recover
    // real-world m/s. The world runs at s = 1/3 scale for speeds, so the
    // inverse is 3. This is the single source of truth for the ratio: HUD and
    // audio read it instead of hardcoding a magic number, so re-scaling the
    // whole sim is a one-field change. Pure display/feel — no physics reads it.
    [Tooltip("Scaled m/s -> real m/s. World speed scale is 1/3, so 3.")]
    public float RealSpeedScale = 3f;

    [Header("Pitch")]
    public float PitchIncreaseSpeed = 300f;
    public bool InvertPitch = true;

    [Header("Roll")]
    public float RollIncreaseSpeed = 420f;
    public float RollAutoLevelSpeed = 120f;

    [Header("Yaw")]
    public float YawSpeed = 30f;
    public float BankTurnSpeed = 15f;

    [Header("Response")]
    public float RollResponseTime = 0.3f;
    public float PitchResponseTime = 0.3f;
    public float YawResponseTime = 0.3f;

    [Header("Aerodynamics")]
    // Below this airspeed (scaled m/s) with a high nose attitude the wing
    // stalls (13.3 == 40 real m/s).
    public float StallSpeed = 13.3f;
    // Scales how much speed is converted into lift. 1 = full power keeps the
    // plane level; lower values make it sink even at cruise speed.
    public float LiftMultiplier = 1f;
    // How strongly climbing bleeds airspeed (and diving regains it).
    // speedMultiplier = 1 - climbFactor * DragMultiplier.
    public float DragMultiplier = 0.35f;
    // forward.y above this counts as "nose too high" for the stall check
    // (~0.35 == ~20 degrees nose up).
    public float StallPitchThreshold = 0.35f;
    // Maximum gentle descent rate (scaled m/s) when lift is fully lost but
    // not stalled, e.g. throttled back too far (8.3 == 25 real m/s).
    public float MaxSinkSpeed = 8.3f;
    // Descent rate (scaled m/s) once a full stall takes over and gravity
    // wins (20 == 60 real m/s).
    public float StallSinkSpeed = 20f;
    // Forced nose-down pitch rate (deg/s) applied while stalling so the plane
    // automatically recovers airspeed in a dive.
    public float StallNoseDownRate = 90f;
    // Stall clears once airspeed climbs back above StallSpeed * this factor.
    public float StallRecoverFactor = 1.2f;

    [Header("Service Ceiling")]
    // Absolute world-Y the airframe can't climb past: the air is too thin and
    // the engine runs out of breath. Above it the plane "can't resist" — pilot
    // (or AI) pitch input is ignored and the nose is forced down until it
    // sinks back below the ceiling. Tallest terrain is ~600, so this sits well
    // above the landscape; it only bites a sustained zoom-climb.
    public float ServiceCeiling = 900f;
    // Band (world units) below the ceiling where the player warning arms, so
    // the pilot gets a heads-up before control is taken.
    public float CeilingWarnBand = 200f;
    // Forced nose-down pitch rate (deg/s) once above the ceiling. Mirrors
    // StallNoseDownRate but a touch gentler — it's a high-altitude mush, not a
    // violent low-speed stall.
    public float CeilingNoseDownRate = 55f;
    // Hysteresis: control only returns once back this far below the ceiling,
    // so it doesn't flip-flop on the line.
    public float CeilingRecoverMargin = 70f;
    // The forced ceiling nose-down stops once the plane has pitched this far
    // (degrees) below horizontal, so it mushes over into a descent instead of
    // tucking into a near-vertical dive. 60 == nose 60 deg down, not 90.
    public float CeilingMaxDiveAngle = 60f;

    // The horizontal limit (the map boundary, the ceiling's twin) is NOT here:
    // it is a scene-placed box you size in the editor — see MapBoundary.
}
