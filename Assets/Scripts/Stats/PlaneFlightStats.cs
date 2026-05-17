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

    [Header("Takeoff / Ground")]
    // Only used by a plane flagged StartGrounded on its PlaneFlightModel
    // (player only). Taxi is a SEPARATE model from flight — none of these
    // touch the airborne thrust/throttle values above. All speeds are scaled
    // m/s (world runs at 1/3 scale). The plane only becomes "flying" when the
    // pilot actively rotates and lifts it MinFlyAltitude off the strip
    // (holding the nose-up input); there is no automatic liftoff.

    // Wheel height below the body pivot; keeps the box collider just clear of
    // the TerrainCollider so the parked plane rests on the strip.
    public float GroundGearHeight = 0.6f;

    // Throttle lever spool on the ground, kept separate from the airborne
    // boost spool so taxi power is its own slow, deliberate control.
    public float TaxiThrottleAccelRate = 0.4f;
    public float TaxiThrottleDecelRate = 0.6f;
    // Taxi / takeoff-roll acceleration. Deliberately slow — a heavy fighter
    // gathering speed, NOT the snappy airborne response (0 -> MaxGroundSpeed
    // takes ~7 s at the defaults).
    public float GroundAccel = 3f;
    // Roll-out / braking when the throttle is backed off (a touch quicker
    // than it accelerates so chopping power settles the taxi).
    public float GroundBrakeDecel = 5f;
    // Top speed on the wheels.
    public float MaxGroundSpeed = 22f;

    // Vr — rotation speed. Below it the elevator has no authority and the
    // plane just rolls; at/above it, holding the nose-up input pitches the
    // nose up and the plane starts to climb off the strip. ~Just above
    // StallSpeed (13.3) so a rotation makes real lift, not a stall.
    public float RotationSpeed = 16f;
    // How fast the nose rises/falls while rotating (deg/s) as long as the
    // nose-up input is held — the rotation is NOT capped at a fixed angle
    // (only a near-vertical safety guard in the model). Deliberately
    // unhurried so rotation feels like flying a heavy plane off, not
    // flicking it.
    public float GroundPitchRate = 18f;
    // The "is it flying yet" gate: the plane is handed to the flight model
    // only once the pilot has actually lifted it this far (world units) above
    // the strip. No speed alone ever triggers it — it must be flown off.
    public float MinFlyAltitude = 8f;
    // Max rate (m/s) the Y pin corrects at while taxiing, so dropping back
    // onto the strip is a firm settle rather than an instant snap.
    public float GroundSettleSpeed = 10f;
    // The takeoff transition window. Over this many seconds after the wheels
    // leave the strip BOTH the airspeed blends up to cruise AND pilot control
    // authority / engine agility ramp in from TakeoffControlStartAuthority to
    // full — one knob for "how long the takeoff takes" (Vr ~16 -> cruise 56).
    public float TakeoffSpeedBlendTime = 3.5f;
    // How much pilot control authority + engine agility is available the
    // instant the wheels leave the strip (0..1), smoothstepping to full over
    // TakeoffSpeedBlendTime. Low so the controls are soft at liftoff and firm
    // up as airspeed builds — like a real takeoff — instead of the air model
    // snapping to full 300 deg/s pitch authority and rearing the nose up.
    public float TakeoffControlStartAuthority = 0.15f;

    // Nosewheel/rudder steering authority while taxiing (deg/s at full
    // deflection). Like a car: it has to be rolling to steer.
    public float GroundSteerRateDeg = 45f;
    // Steering ramps from none at a standstill up to full by this ground
    // speed and STAYS full above it: you can't pivot parked, but once it's
    // rolling it always steers — it does NOT wash out at takeoff-roll speed.
    public float GroundSteerSpeedRampUp = 4f;
}
