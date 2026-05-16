using UnityEngine;

public class PlaneFlightModel : MonoBehaviour
{
    Transform _transform;
    Rigidbody _rigidbody;

    public PlaneFlightStats Stats;

    [HideInInspector] public float PitchInput;
    [HideInInspector] public float RollInput;
    [HideInInspector] public float YawInput;
    [HideInInspector] public bool Boost;

    float _pitchRate;
    float _rollRate;
    float _yawRate;
    float _throttle01;
    bool _stalling;
    bool _overCeiling;
    bool _overBoundary;

    public Transform CachedTransform => _transform;
    public Rigidbody Body => _rigidbody;
    public bool InvertPitch => Stats != null && Stats.InvertPitch;

    /// <summary>
    /// True while the wing is stalled: airspeed has fallen below
    /// <see cref="PlaneFlightStats.StallSpeed"/> with the nose pitched too
    /// high. While stalling the plane loses lift, the nose is forced down,
    /// and it sinks until it dives back to flying speed.
    /// </summary>
    public bool IsStalling => _stalling;

    /// <summary>
    /// True while above <see cref="PlaneFlightStats.ServiceCeiling"/>: the air
    /// is too thin to climb, so pilot/AI pitch input is overridden and the nose
    /// is forced down until the plane sinks back below the ceiling. Cleared
    /// with hysteresis (<see cref="PlaneFlightStats.CeilingRecoverMargin"/>).
    /// </summary>
    public bool OverCeiling => _overCeiling;

    /// <summary>
    /// 0 below the warning band, ramps to 1 at the service ceiling, and stays
    /// 1 while above it. Drives the player altitude-warning HUD.
    /// </summary>
    public float CeilingProximity { get; private set; }

    /// <summary>
    /// True while outside the scene's <see cref="MapBoundary"/> box: pilot/AI
    /// roll &amp; yaw are overridden and the plane banks back toward the centre
    /// until it is back inside. Cleared with hysteresis
    /// (<see cref="MapBoundary.RecoverMargin"/>).
    /// </summary>
    public bool OverBoundary => _overBoundary;

    /// <summary>
    /// 0 inside the warning band, ramps to 1 at the map boundary edge, and
    /// stays 1 while outside it. Drives the player boundary-warning HUD.
    /// </summary>
    public float BoundaryProximity { get; private set; }

    /// <summary>
    /// Current airspeed in m/s along the flight path, after climb/dive drag
    /// is applied. Useful for HUD readouts and audio.
    /// </summary>
    public float CurrentSpeed { get; private set; }

    /// <summary>
    /// Throttle position: 0 at <see cref="PlaneFlightStats.NormalThrust"/>,
    /// 1 at <see cref="PlaneFlightStats.MaxThrust"/>. Ramps linearly toward
    /// the boost input rather than snapping. Useful for a throttle HUD.
    /// </summary>
    public float Throttle01 => _throttle01;

    void Start()
    {
        _transform = transform;
        _rigidbody = GetComponent<Rigidbody>();
        // Lift and weight are modelled manually below, so Unity's built-in
        // gravity must stay off (it would also be overwritten by the direct
        // velocity assignment anyway).
        if (_rigidbody != null) _rigidbody.useGravity = false;
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneFlightModel)} on {name} has no Stats assigned.", this);
        }
    }

    void FixedUpdate()
    {
        if (Stats == null) return;
        var dt = Time.fixedDeltaTime;

        // Hold the boost input to spool the throttle up toward MaxThrust;
        // release it and it bleeds back down to NormalThrust. The linear
        // ramp makes airspeed a dial the pilot can hold anywhere in between.
        var throttleTarget = Boost ? 1f : 0f;
        var throttleRate = Boost ? Stats.ThrottleAccelRate : Stats.ThrottleDecelRate;
        _throttle01 = Mathf.MoveTowards(_throttle01, throttleTarget, throttleRate * dt);

        // Boost agility scales with the throttle so handling firms up
        // smoothly as it spools instead of snapping when the key goes down.
        var agility = Mathf.Lerp(1f, Stats.ThrustAgilityMultiplier, _throttle01);

        var bank = _transform.right.y;

        // climbFactor: +1 when pointing straight up, -1 straight down.
        var climbFactor = Vector3.Dot(_transform.forward, Vector3.up);

        // Air drag: climbing bleeds airspeed, diving builds it back up.
        var speedMultiplier = 1f - climbFactor * Stats.DragMultiplier;
        var baseThrust = Mathf.Lerp(Stats.NormalThrust, Stats.MaxThrust, _throttle01);
        var speed = baseThrust * speedMultiplier;
        CurrentSpeed = speed;

        // Stall state with hysteresis so it doesn't flicker on the threshold.
        var noseTooHigh = climbFactor > Stats.StallPitchThreshold;
        if (!_stalling)
        {
            if (speed < Stats.StallSpeed && noseTooHigh) _stalling = true;
        }
        else if (speed > Stats.StallSpeed * Stats.StallRecoverFactor)
        {
            _stalling = false;
        }

        // Service ceiling with the same hysteresis pattern as the stall: above
        // it the air is too thin to climb, so the nose is forced down until
        // the plane sinks back CeilingRecoverMargin below the ceiling.
        var altitude = _transform.position.y;
        var warnBand = Mathf.Max(Stats.CeilingWarnBand, 0.0001f);
        CeilingProximity = Mathf.Clamp01(
            (altitude - (Stats.ServiceCeiling - warnBand)) / warnBand);
        if (!_overCeiling)
        {
            if (altitude > Stats.ServiceCeiling) _overCeiling = true;
        }
        else if (altitude < Stats.ServiceCeiling - Stats.CeilingRecoverMargin)
        {
            _overCeiling = false;
        }

        // Map boundary: the horizontal mirror of the ceiling, but the limit is
        // the scene's MapBoundary box rather than an altitude. Same warn-band
        // ramp + hysteresis, measured as the signed distance to the box edge.
        // No MapBoundary in the scene -> the whole feature is inert.
        var boundary = MapBoundary.Instance;
        if (boundary == null)
        {
            BoundaryProximity = 0f;
            _overBoundary = false;
        }
        else
        {
            var edge = boundary.SignedEdgeDistanceXZ(_transform.position);
            var boundaryWarn = Mathf.Max(boundary.WarnBand, 0.0001f);
            BoundaryProximity = Mathf.Clamp01(1f + edge / boundaryWarn);
            if (!_overBoundary)
            {
                if (edge > 0f) _overBoundary = true;
            }
            else if (edge < -boundary.RecoverMargin)
            {
                _overBoundary = false;
            }
        }

        float targetPitchRate;
        if (_stalling)
            // Lost the wing: force the nose down to trade altitude for speed,
            // ignoring pilot pitch input until recovered.
            targetPitchRate = Stats.StallNoseDownRate * agility;
        else if (_overCeiling)
        {
            // Too high to sustain: the plane mushes over and the nose drops
            // back toward thicker air, overriding pilot/AI pitch input — but
            // only until it reaches CeilingMaxDiveAngle below horizontal, so
            // it settles into a descent rather than tucking into a vertical
            // dive. climbFactor == -sin(angle) at that nose-down attitude.
            var diveLimit = -Mathf.Sin(Stats.CeilingMaxDiveAngle * Mathf.Deg2Rad);
            targetPitchRate = climbFactor <= diveLimit
                ? 0f
                : Stats.CeilingNoseDownRate * agility;
        }
        else
            targetPitchRate = (Stats.InvertPitch ? -PitchInput : PitchInput)
                * Stats.PitchIncreaseSpeed * agility;
        var targetRollRate = Mathf.Approximately(RollInput, 0f)
            ? -bank * Stats.RollAutoLevelSpeed * agility
            : -RollInput * Stats.RollIncreaseSpeed * agility;
        var targetYawRate = (YawInput * Stats.YawSpeed - bank * Stats.BankTurnSpeed) * agility;

        // Outside the map: override pilot/AI roll & yaw and bank back toward
        // the field centre (pitch is left to the stall/ceiling/terrain logic).
        // Same input->rate mapping as a real stick deflection, with the
        // command synthesised from the bearing error so it whips around while
        // the nose points outward and rolls out level as it comes back in.
        if (_overBoundary && boundary != null)
        {
            var toCenter = boundary.Center - _transform.position;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.0001f)
            {
                var dirLocal = _transform.InverseTransformDirection(toCenter.normalized);
                // dirLocal.x > 0: centre is off our right; dirLocal.z > 0: it
                // is ahead (the AI's own steering sign convention).
                var turnDir = dirLocal.x >= 0f ? 1f : -1f;
                var need = 1f - Mathf.Clamp01(dirLocal.z);
                var turn = Mathf.Clamp(turnDir * need * boundary.TurnGain, -1f, 1f);
                targetRollRate = -turn * Stats.RollIncreaseSpeed * agility;
                targetYawRate = (turn * Stats.YawSpeed - bank * Stats.BankTurnSpeed) * agility;
            }
        }

        var alphaPitch = 1f - Mathf.Exp(-dt / Mathf.Max(Stats.PitchResponseTime, 0.0001f));
        var alphaRoll = 1f - Mathf.Exp(-dt / Mathf.Max(Stats.RollResponseTime, 0.0001f));
        var alphaYaw = 1f - Mathf.Exp(-dt / Mathf.Max(Stats.YawResponseTime, 0.0001f));

        _pitchRate = Mathf.Lerp(_pitchRate, targetPitchRate, alphaPitch);
        _rollRate = Mathf.Lerp(_rollRate, targetRollRate, alphaRoll);
        _yawRate = Mathf.Lerp(_yawRate, targetYawRate, alphaYaw);

        var deltaPitch = _pitchRate * dt;
        var deltaRoll = _rollRate * dt;
        var deltaYaw = _yawRate * dt;

        var localRotation = _transform.localRotation;
        localRotation *= Quaternion.Euler(0f, 0f, deltaRoll);
        localRotation *= Quaternion.Euler(deltaPitch, 0f, 0f);
        _transform.localRotation = localRotation;

        _transform.Rotate(Vector3.up, deltaYaw, Space.World);

        // Lift: airspeed (scaled by LiftMultiplier) supports the plane. At
        // cruise it fully counters weight and the plane holds altitude purely
        // from its attitude; below that lift falls short and it sinks slowly,
        // which is why the plane loses altitude when thrust is too low.
        var liftSupport = Mathf.Clamp01(
            speed / Mathf.Max(Stats.NormalThrust, 0.0001f) * Stats.LiftMultiplier);

        var velocity = _transform.forward * speed;
        velocity.y -= _stalling
            ? Stats.StallSinkSpeed
            : Stats.MaxSinkSpeed * (1f - liftSupport);

        // Direct velocity assignment: thrust maps straight to m/s, no
        // Time.fixedDeltaTime applied to the velocity itself.
        _rigidbody.linearVelocity = velocity;
    }
}
