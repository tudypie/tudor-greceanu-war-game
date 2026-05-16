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
    /// The hard altitude ceiling actually in force: the TOP of the scene's
    /// <see cref="MapBoundary"/> box when one is present, otherwise
    /// <see cref="PlaneFlightStats.ServiceCeiling"/>. Single source of truth —
    /// the flight model's forced nose-down and the AI's soft cap both read it.
    /// </summary>
    public float EffectiveServiceCeiling
    {
        get
        {
            var b = MapBoundary.Instance;
            if (b != null) return b.TopY;
            return Stats != null ? Stats.ServiceCeiling : float.MaxValue;
        }
    }

    /// <summary>
    /// True while above <see cref="EffectiveServiceCeiling"/>: the air is too
    /// thin to climb, so pilot/AI pitch input is overridden and the nose is
    /// forced down until the plane sinks back below the ceiling. Cleared with
    /// hysteresis (<see cref="PlaneFlightStats.CeilingRecoverMargin"/>).
    /// </summary>
    public bool OverCeiling => _overCeiling;

    /// <summary>
    /// 0 below the warning band, ramps to 1 at <see cref="EffectiveServiceCeiling"/>,
    /// and stays 1 while above it. Drives the player altitude-warning HUD.
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

        // Hard altitude ceiling, with the same hysteresis pattern as the
        // stall: above it the air is too thin to climb, so the nose is forced
        // down until the plane sinks back CeilingRecoverMargin below it. The
        // ceiling is the TOP of the scene's MapBoundary box when one is
        // present, otherwise the flight-stats ServiceCeiling. (The box's XZ
        // edges are a separate, horizontal turn-back limit handled below; its
        // top is an ALTITUDE limit — forced nose-down, ceiling HUD — not a
        // "leaving combat area" one.)
        var boundary = MapBoundary.Instance;
        var serviceCeiling = boundary != null ? boundary.TopY : Stats.ServiceCeiling;
        var altitude = _transform.position.y;
        var warnBand = Mathf.Max(Stats.CeilingWarnBand, 0.0001f);
        CeilingProximity = Mathf.Clamp01(
            (altitude - (serviceCeiling - warnBand)) / warnBand);
        if (!_overCeiling)
        {
            if (altitude > serviceCeiling) _overCeiling = true;
        }
        else if (altitude < serviceCeiling - Stats.CeilingRecoverMargin)
        {
            _overCeiling = false;
        }

        // Map boundary (XZ edges only): the horizontal turn-back limit. Same
        // warn-band ramp + hysteresis as the ceiling, measured as the signed
        // distance to the box edge. No MapBoundary in the scene -> inert.
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
        // The bearing to home is taken in the HORIZONTAL plane only, so a hard
        // bank doesn't spin the "which way is home" signal around the rolling
        // body (the old body-frame version just barrel-rolled the plane in
        // place instead of coming about). We roll TO and HOLD a bank angle
        // proportional to how far the nose is off home and let the model's own
        // bank-to-turn coupling carry it round — a coordinated turn exactly
        // like everywhere else — rolling out level as the nose swings back in.
        if (_overBoundary && boundary != null)
        {
            var toCenter = boundary.Center - _transform.position;
            toCenter.y = 0f;
            var heading = _transform.forward;
            heading.y = 0f;
            if (toCenter.sqrMagnitude > 0.0001f && heading.sqrMagnitude > 0.0001f)
            {
                // Signed horizontal bearing to home: +ve == home is to the
                // right. A right turn needs right.y < 0 (the model's existing
                // bank-turn sign), so the target bank is -sign(bearing).
                var bearing = Vector3.SignedAngle(
                    heading.normalized, toCenter.normalized, Vector3.up);
                // 0 with the nose on home, ramping to full by 90 deg off.
                var need = Mathf.Clamp01(Mathf.Abs(bearing) / 90f);
                const float maxBankRightY = 0.85f; // ~58 deg of bank held
                var targetBank = -Mathf.Sign(bearing) * maxBankRightY * need;
                // Proportional roll toward that held bank (mirrors the -bank
                // auto-level term, just toward a non-zero target), normalised
                // and clamped like a stick deflection then scaled to a rate.
                var rollCmd = Mathf.Clamp(
                    (targetBank - bank) * boundary.TurnGain, -1f, 1f);
                targetRollRate = rollCmd * Stats.RollIncreaseSpeed * agility;
                // Rudder coordinated with the turn; the held bank does the
                // rest through the standard -bank * BankTurnSpeed term.
                var rudder = Mathf.Sign(bearing) * need;
                targetYawRate =
                    (rudder * Stats.YawSpeed - bank * Stats.BankTurnSpeed) * agility;
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
