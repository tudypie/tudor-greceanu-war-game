using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Flight Stats", fileName = "PlaneFlightStats")]
public class PlaneFlightStats : ScriptableObject
{
    [Header("Thrust")]
    // Speeds are scaled m/s (world runs at 1/3 scale); thrust == cruise speed.
    public float NormalThrust = 56f;
    public float MaxThrust = 66.7f;
    public float ThrustAgilityMultiplier = 1.8f;
    // Throttle-fraction per second. Spool-up slower than spool-down so speed is a holdable dial.
    public float ThrottleAccelRate = 0.5f;
    public float ThrottleDecelRate = 0.7f;

    [Header("World Scale")]
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
    public float StallSpeed = 13.3f;
    // 1 = full power keeps the plane level; lower sinks even at cruise.
    public float LiftMultiplier = 1f;
    // speedMultiplier = 1 - climbFactor * DragMultiplier.
    public float DragMultiplier = 0.35f;
    // forward.y above this counts as nose-too-high for the stall check (~20 deg).
    public float StallPitchThreshold = 0.35f;
    // Gentle descent rate when lift is lost but not stalled.
    public float MaxSinkSpeed = 8.3f;
    // Descent rate once a full stall takes over.
    public float StallSinkSpeed = 20f;
    // Forced nose-down rate (deg/s) while stalling, to recover airspeed in a dive.
    public float StallNoseDownRate = 90f;
    // Stall clears above StallSpeed * this factor.
    public float StallRecoverFactor = 1.2f;

    [Header("Service Ceiling")]
    // Absolute world-Y the airframe can't climb past; pitch input is ignored above it.
    public float ServiceCeiling = 900f;
    // Band below the ceiling where the player warning arms.
    public float CeilingWarnBand = 200f;
    // Forced nose-down rate (deg/s) above the ceiling; gentler than the stall.
    public float CeilingNoseDownRate = 55f;
    // Hysteresis: control returns only this far back below the ceiling.
    public float CeilingRecoverMargin = 70f;
    // Forced ceiling nose-down stops at this dive angle so it mushes, not tucks.
    public float CeilingMaxDiveAngle = 60f;

    // Horizontal limit (map boundary) is a scene-placed box, see MapBoundary.

    [Header("Takeoff / Ground")]
    // Only used when StartGrounded (player). Taxi is a separate model from flight;
    // becomes flying only when lifted MinFlyAltitude off the strip, no auto liftoff.

    // Keeps the box collider just clear of the TerrainCollider when parked.
    public float GroundGearHeight = 0.6f;

    // Ground throttle spool, kept separate from the airborne boost spool.
    public float TaxiThrottleAccelRate = 0.4f;
    public float TaxiThrottleDecelRate = 0.6f;
    // Deliberately slow takeoff-roll accel (0 -> MaxGroundSpeed ~7 s).
    public float GroundAccel = 3f;
    public float GroundBrakeDecel = 5f;
    public float MaxGroundSpeed = 22f;

    // Vr: below it the elevator has no authority; just above StallSpeed.
    public float RotationSpeed = 16f;
    // Nose pitch rate (deg/s) while rotating; not capped at a fixed angle.
    public float GroundPitchRate = 18f;
    // Flight model takes over only once lifted this far above the strip.
    public float MinFlyAltitude = 8f;
    // Max rate the Y pin corrects at while taxiing, so it settles not snaps.
    public float GroundSettleSpeed = 10f;
    // Takeoff window: airspeed blends to cruise and control authority ramps to full over this.
    public float TakeoffSpeedBlendTime = 3.5f;
    // Control authority at the instant of liftoff (0..1), smoothstepping to full.
    public float TakeoffControlStartAuthority = 0.15f;

    // Nosewheel steering authority (deg/s); has to be rolling to steer.
    public float GroundSteerRateDeg = 45f;
    // Steering ramps to full by this speed and stays full above it.
    public float GroundSteerSpeedRampUp = 4f;
}
