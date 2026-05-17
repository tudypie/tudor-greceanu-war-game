using UnityEngine;
using UnityEngine.Events;

public class PlaneFlightModel : MonoBehaviour
{
    Transform _transform;
    Rigidbody _rigidbody;

    public PlaneFlightStats Stats;

    [Tooltip("Player plane only: start parked on the runway and require a " +
        "takeoff roll. Leave false for AI and any air-start plane.")]
    [SerializeField] bool StartGrounded = false;

    [Tooltip("Fired once, the moment the wheels leave the strip and the " +
        "plane is handed to the flight model. Never fires for air-start planes.")]
    public UnityEvent OnTakeoff;

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

    // Grounded takeoff/taxi model (player only — see StartGrounded). The
    // airborne code path below is entered exactly once, via a short speed
    // blend, and never exited back to the ground (there is no landing model).
    bool _grounded;
    float _groundSpeed;
    float _groundPitchDeg;
    bool _blendingToAir;
    bool _takingOff;
    float _takeoffTimer;
    float _blendStartSpeed;
    Terrain _terrain;
    float _terrainBaseY;

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
    /// Throttle position: airborne, 0 at <see cref="PlaneFlightStats.NormalThrust"/>
    /// and 1 at <see cref="PlaneFlightStats.MaxThrust"/>; while
    /// <see cref="IsGrounded"/> it is the absolute 0..1 ground lever (0 ==
    /// stationary). Ramps linearly toward the boost input rather than
    /// snapping. Useful for a throttle HUD.
    /// </summary>
    public float Throttle01 => _throttle01;

    /// <summary>
    /// True while the plane is still on the ground in the takeoff/taxi model
    /// (player only — see StartGrounded). Goes false for good at liftoff and
    /// never returns: there is no landing model. Lets the crash/HUD/audio
    /// treat a parked plane sensibly.
    /// </summary>
    public bool IsGrounded => _grounded;

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

        // Terrain handle, cached the layer-independent way the AI and crash
        // backstop do it (heightfield sampling, no colliders involved).
        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
        _terrainBaseY = _terrain != null ? _terrain.transform.position.y : 0f;

        // Player-only: start parked on the strip. Snap level (keeping the
        // authored heading), pin to the terrain at gear height, speed and
        // throttle at zero, and freeze the body so it can't drift before the
        // first physics step. AI never sets StartGrounded, so they run the
        // airborne model from frame one, byte-for-byte unchanged.
        if (StartGrounded && Stats != null)
        {
            _grounded = true;
            _groundSpeed = 0f;
            _throttle01 = 0f;
            CurrentSpeed = 0f;

            var flatForward = _transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                flatForward.Normalize();
                _transform.rotation =
                    Quaternion.LookRotation(flatForward, Vector3.up);
            }

            if (_terrain != null)
            {
                var pos = _transform.position;
                pos.y = _terrainBaseY + _terrain.SampleHeight(pos)
                    + Stats.GroundGearHeight;
                _transform.position = pos;
            }

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }
    }

    void FixedUpdate()
    {
        if (Stats == null) return;
        var dt = Time.fixedDeltaTime;

        // On the wheels: a separate, self-contained takeoff/taxi model. It
        // hands off to the airborne code below exactly once (at liftoff) and
        // is never re-entered, so everything past here is the original model.
        if (_grounded) { GroundedFixedUpdate(dt); return; }

        // Takeoff transition window (player only; armed by the grounded
        // handoff, never by AI — so for AI takeoffAuth stays 1 and everything
        // below is the original model). Over TakeoffSpeedBlendTime the
        // airspeed blends up to cruise (bottom of this method) AND pilot
        // control authority + engine agility ease in from
        // TakeoffControlStartAuthority to full, so the plane flies off the
        // strip smoothly instead of the air model snapping to full authority
        // and rearing the nose up.
        var takeoffAuth = 1f;
        if (_takingOff || _blendingToAir)
        {
            _takeoffTimer += dt;
            var tk = Stats.TakeoffSpeedBlendTime > 0f
                ? Mathf.Clamp01(_takeoffTimer / Stats.TakeoffSpeedBlendTime)
                : 1f;
            if (_takingOff)
            {
                var ease = tk * tk * (3f - 2f * tk); // smoothstep
                takeoffAuth =
                    Mathf.Lerp(Stats.TakeoffControlStartAuthority, 1f, ease);
                if (tk >= 1f) _takingOff = false;
            }
        }

        // Hold the boost input to spool the throttle up toward MaxThrust;
        // release it and it bleeds back down to NormalThrust. The linear
        // ramp makes airspeed a dial the pilot can hold anywhere in between.
        var throttleTarget = Boost ? 1f : 0f;
        var throttleRate = Boost ? Stats.ThrottleAccelRate : Stats.ThrottleDecelRate;
        _throttle01 = Mathf.MoveTowards(_throttle01, throttleTarget, throttleRate * dt);

        // Boost agility scales with the throttle so handling firms up
        // smoothly as it spools instead of snapping when the key goes down.
        var agility = Mathf.Lerp(1f, Stats.ThrustAgilityMultiplier, _throttle01);
        // Takeoff: mute the throttle-at-1.0 -> max-WEP agility spike at the
        // instant of liftoff; it firms up with the rest of the controls.
        // takeoffAuth == 1 in normal flight and for AI, so this is a no-op.
        agility = Mathf.Lerp(1f, agility, takeoffAuth);

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
            // Pilot pitch authority eases in over the takeoff (takeoffAuth);
            // the stall / ceiling overrides above keep FULL authority so a
            // post-liftoff stall still recovers normally.
            targetPitchRate = (Stats.InvertPitch ? -PitchInput : PitchInput)
                * Stats.PitchIncreaseSpeed * agility * takeoffAuth;
        // Pilot roll/yaw authority eases in the same way. The _overBoundary
        // block below REASSIGNS these at full strength, so boundary recovery
        // is never weakened by the takeoff ramp.
        var targetRollRate = (Mathf.Approximately(RollInput, 0f)
            ? -bank * Stats.RollAutoLevelSpeed * agility
            : -RollInput * Stats.RollIncreaseSpeed * agility) * takeoffAuth;
        var targetYawRate =
            (YawInput * Stats.YawSpeed - bank * Stats.BankTurnSpeed)
            * agility * takeoffAuth;

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

        // Just left the ground: the air model jumps straight to cruise speed,
        // so for a moment after liftoff rescale the assigned velocity from the
        // rotation speed up to it (direction untouched). This is a pure post-
        // process on the output above — the airborne logic itself is intact.
        if (_blendingToAir)
        {
            // Shares the single _takeoffTimer (advanced once at the top of
            // this method) with the control-authority ramp.
            var blendT = Stats.TakeoffSpeedBlendTime > 0f
                ? Mathf.Clamp01(_takeoffTimer / Stats.TakeoffSpeedBlendTime)
                : 1f;
            var assigned = _rigidbody.linearVelocity;
            var mag = assigned.magnitude;
            if (mag > 0.0001f)
            {
                var blendedSpeed = Mathf.Lerp(_blendStartSpeed, mag, blendT);
                _rigidbody.linearVelocity = assigned * (blendedSpeed / mag);
            }
            if (blendT >= 1f) _blendingToAir = false;
        }
    }

    // Free taxi + a flown takeoff on the wheels — a model entirely separate
    // from flight. Throttle is a slow, real 0->1 lever (idle 0 == stationary).
    // There is NO automatic liftoff: below Vr the plane only rolls; at/above
    // Vr the held nose-up input rotates the nose and the plane climbs off the
    // strip, and only once it is actually MinFlyAltitude up is it handed to
    // the flight model. Let go and it settles back onto the runway.
    void GroundedFixedUpdate(float dt)
    {
        var pos = _transform.position;

        // Throttle on its own slow taxi spool, separate from the airborne
        // boost spool. Hold Boost (Space) to spool up, release to spool down.
        var throttleTarget = Boost ? 1f : 0f;
        var throttleRate = Boost
            ? Stats.TaxiThrottleAccelRate
            : Stats.TaxiThrottleDecelRate;
        _throttle01 = Mathf.MoveTowards(_throttle01, throttleTarget, throttleRate * dt);

        // Ground speed chases a throttle-proportional target with the slow
        // taxi accel / brake rates (deliberately nothing like the air model).
        var targetSpeed = _throttle01 * Stats.MaxGroundSpeed;
        var speedRate = _groundSpeed < targetSpeed
            ? Stats.GroundAccel
            : Stats.GroundBrakeDecel;
        _groundSpeed = Mathf.MoveTowards(_groundSpeed, targetSpeed, speedRate * dt);
        CurrentSpeed = _groundSpeed;

        // Nosewheel/rudder steering from yaw input (Q/E), car-like: no
        // authority parked, ramping up to full by GroundSteerSpeedRampUp and
        // staying full while it has speed (it does NOT wash out at takeoff-
        // roll speed). RollInput (Move.x) is inert here.
        if (_groundSpeed > 0.05f && !Mathf.Approximately(YawInput, 0f))
        {
            var steerScale = Mathf.Clamp01(
                _groundSpeed / Mathf.Max(Stats.GroundSteerSpeedRampUp, 0.0001f));
            _transform.Rotate(
                Vector3.up,
                YawInput * Stats.GroundSteerRateDeg * steerScale * dt,
                Space.World);
        }

        // Rotation: only at/above Vr does the nose-up input have authority.
        // The nose-up sign matches the airframe's climb convention exactly
        // (cf. PlaneAIController.ClimbInputSign). Held -> the nose keeps
        // rising at GroundPitchRate (NOT capped at a fixed angle — only a
        // near-vertical guard so the heading math below stays well defined,
        // and the plane hands off to the flight model long before then);
        // released -> falls back to level.
        const float pitchGuardDeg = 85f;
        var climbSign = Stats.InvertPitch ? 1f : -1f;
        var climbCmd = Mathf.Clamp01(PitchInput * climbSign);
        var pitchAuthority = _groundSpeed >= Stats.RotationSpeed;
        var targetPitch = pitchAuthority ? climbCmd * pitchGuardDeg : 0f;
        _groundPitchDeg = Mathf.MoveTowards(
            _groundPitchDeg, targetPitch, Stats.GroundPitchRate * dt);

        // Attitude: heading from the (post-steer) flat forward, wings always
        // level, nose pitched up by _groundPitchDeg. Negative Euler-X is
        // nose-up for a +Z-forward body, matching the air model's sign.
        var flatForward = _transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        flatForward.Normalize();
        _transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up)
            * Quaternion.Euler(-_groundPitchDeg, 0f, 0f);
        var climbForward = _transform.forward;

        // Wheels-on-the-strip height. Layer-independent heightfield sample;
        // with no terrain (test scene) treat the current Y as ground.
        var haveTerrain = _terrain != null;
        var restY = haveTerrain
            ? _terrainBaseY + _terrain.SampleHeight(pos) + Stats.GroundGearHeight
            : pos.y;

        // Velocity in the model's direct-assignment style: follow the nose at
        // ground speed. Nose down -> a pure taxi: glue Y to the strip (tracking
        // terrain up and down, clamped so dropping back is a firm settle, not
        // a snap). Nose up -> follow the climb but never sink through the strip.
        var velocity = climbForward * _groundSpeed;
        if (_groundPitchDeg <= 0.1f)
        {
            velocity.y = Mathf.Clamp(
                (restY - pos.y) / dt,
                -Stats.GroundSettleSpeed, Stats.GroundSettleSpeed);
        }
        else
        {
            var minVy = (restY - pos.y) / dt;
            if (velocity.y < minVy) velocity.y = minVy;
        }
        _rigidbody.linearVelocity = velocity;
        _rigidbody.angularVelocity = Vector3.zero;

        // "Is it flying yet" gate: only once the pilot has actually lifted it
        // MinFlyAltitude above the strip. No speed-only auto-liftoff — the
        // plane stays in the taxi model until it is flown off. (No terrain:
        // fall back to a held rotation above Vr.) Handoff is one-way: clear
        // _grounded and arm the speed blend; the air model owns it from here.
        var flyingNow = haveTerrain
            ? pos.y - restY >= Stats.MinFlyAltitude
            : _groundPitchDeg > 0.1f && _groundSpeed >= Stats.RotationSpeed;
        if (flyingNow)
        {
            _grounded = false;
            _blendingToAir = true;
            _takingOff = true;
            _takeoffTimer = 0f;
            _blendStartSpeed = _groundSpeed;
            OnTakeoff?.Invoke();
        }
    }
}
