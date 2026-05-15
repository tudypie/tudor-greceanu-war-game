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
}
