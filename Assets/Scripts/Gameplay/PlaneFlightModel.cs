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

    public bool IsStalling => _stalling;

    // SSOT for the hard ceiling: the MapBoundary box top if present, else
    // ServiceCeiling. Read by the forced nose-down and the AI's soft cap.
    public float EffectiveServiceCeiling
    {
        get
        {
            var b = MapBoundary.Instance;
            if (b != null) return b.TopY;
            return Stats != null ? Stats.ServiceCeiling : float.MaxValue;
        }
    }

    public bool OverCeiling => _overCeiling;

    // 0 below the warn band, ramps to 1 at the ceiling. Drives the HUD.
    public float CeilingProximity { get; private set; }

    public bool OverBoundary => _overBoundary;

    // 0 inside the warn band, ramps to 1 at the map edge. Drives the HUD.
    public float BoundaryProximity { get; private set; }

    public float CurrentSpeed { get; private set; }

    // Airborne: 0 at NormalThrust, 1 at MaxThrust. Grounded: absolute 0..1 ground lever.
    public float Throttle01 => _throttle01;

    // False for good at liftoff and never returns (no landing model).
    public bool IsGrounded => _grounded;

    void Start()
    {
        _transform = transform;
        _rigidbody = GetComponent<Rigidbody>();
        // Lift and weight are modelled manually, so built-in gravity stays off.
        if (_rigidbody != null) _rigidbody.useGravity = false;
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneFlightModel)} on {name} has no Stats assigned.", this);
        }

        // Layer-independent terrain handle (heightfield sampling, no colliders).
        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
        _terrainBaseY = _terrain != null ? _terrain.transform.position.y : 0f;

        // Player-only: start parked on the strip, level, pinned at gear height,
        // speed/throttle zero, body frozen. AI never sets StartGrounded.
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

        // On the wheels: a separate taxi/takeoff model, handed off once at liftoff.
        if (_grounded) { GroundedFixedUpdate(dt); return; }

        // Takeoff window (player only; takeoffAuth stays 1 for AI). Over
        // TakeoffSpeedBlendTime airspeed blends to cruise and control authority
        // eases in, so the plane flies off smoothly instead of rearing up.
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

        // Boost spools the throttle toward MaxThrust; releasing bleeds it back.
        var throttleTarget = Boost ? 1f : 0f;
        var throttleRate = Boost ? Stats.ThrottleAccelRate : Stats.ThrottleDecelRate;
        _throttle01 = Mathf.MoveTowards(_throttle01, throttleTarget, throttleRate * dt);

        // Agility scales with throttle so handling firms up as it spools.
        var agility = Mathf.Lerp(1f, Stats.ThrustAgilityMultiplier, _throttle01);
        // Mute the agility spike at the instant of liftoff (no-op when takeoffAuth == 1).
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

        // Hard altitude ceiling (hysteresis like the stall): above it the nose
        // is forced down until the plane sinks CeilingRecoverMargin below it.
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

        // Map boundary (XZ edges): horizontal turn-back limit, same ramp +
        // hysteresis as the ceiling. No MapBoundary -> inert.
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
            // Lost the wing: force the nose down, ignoring pilot input.
            targetPitchRate = Stats.StallNoseDownRate * agility;
        else if (_overCeiling)
        {
            // Mush over toward thicker air, but only down to CeilingMaxDiveAngle
            // so it settles into a descent, not a vertical tuck.
            var diveLimit = -Mathf.Sin(Stats.CeilingMaxDiveAngle * Mathf.Deg2Rad);
            targetPitchRate = climbFactor <= diveLimit
                ? 0f
                : Stats.CeilingNoseDownRate * agility;
        }
        else
            // Pitch authority eases in over takeoff; stall/ceiling overrides keep full authority.
            targetPitchRate = (Stats.InvertPitch ? -PitchInput : PitchInput)
                * Stats.PitchIncreaseSpeed * agility * takeoffAuth;
        // Roll/yaw ease in the same way; the _overBoundary block below
        // reassigns these at full strength so recovery isn't weakened.
        var targetRollRate = (Mathf.Approximately(RollInput, 0f)
            ? -bank * Stats.RollAutoLevelSpeed * agility
            : -RollInput * Stats.RollIncreaseSpeed * agility) * takeoffAuth;
        var targetYawRate =
            (YawInput * Stats.YawSpeed - bank * Stats.BankTurnSpeed)
            * agility * takeoffAuth;

        // Outside the map: override roll & yaw and bank back toward centre
        // (pitch left to stall/ceiling/terrain). Bearing to home is horizontal-
        // only, then a held proportional bank lets bank-to-turn carry it round.
        if (_overBoundary && boundary != null)
        {
            var toCenter = boundary.Center - _transform.position;
            toCenter.y = 0f;
            var heading = _transform.forward;
            heading.y = 0f;
            if (toCenter.sqrMagnitude > 0.0001f && heading.sqrMagnitude > 0.0001f)
            {
                // Signed horizontal bearing to home (+ve == home to the right);
                // target bank is -sign(bearing) to match the bank-turn sign.
                var bearing = Vector3.SignedAngle(
                    heading.normalized, toCenter.normalized, Vector3.up);
                // 0 with the nose on home, ramping to full by 90 deg off.
                var need = Mathf.Clamp01(Mathf.Abs(bearing) / 90f);
                const float maxBankRightY = 0.85f; // ~58 deg of bank held
                var targetBank = -Mathf.Sign(bearing) * maxBankRightY * need;
                // Proportional roll toward the held bank, clamped like a stick.
                var rollCmd = Mathf.Clamp(
                    (targetBank - bank) * boundary.TurnGain, -1f, 1f);
                targetRollRate = rollCmd * Stats.RollIncreaseSpeed * agility;
                // Coordinated rudder; the held bank carries the rest of the turn.
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

        // Lift: airspeed fully counters weight at cruise; below that it sinks slowly.
        var liftSupport = Mathf.Clamp01(
            speed / Mathf.Max(Stats.NormalThrust, 0.0001f) * Stats.LiftMultiplier);

        var velocity = _transform.forward * speed;
        velocity.y -= _stalling
            ? Stats.StallSinkSpeed
            : Stats.MaxSinkSpeed * (1f - liftSupport);

        // Direct velocity assignment: thrust maps straight to m/s.
        _rigidbody.linearVelocity = velocity;

        // Just after liftoff, rescale the assigned speed up from rotation speed
        // to cruise (direction untouched) so the air model doesn't snap.
        if (_blendingToAir)
        {
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

    // Taxi + flown takeoff on the wheels, separate from flight. No auto-liftoff:
    // at/above Vr held nose-up climbs off, handed to the flight model only once
    // MinFlyAltitude up; release and it settles back onto the runway.
    void GroundedFixedUpdate(float dt)
    {
        var pos = _transform.position;

        // Throttle on its own slow taxi spool, separate from the air boost spool.
        var throttleTarget = Boost ? 1f : 0f;
        var throttleRate = Boost
            ? Stats.TaxiThrottleAccelRate
            : Stats.TaxiThrottleDecelRate;
        _throttle01 = Mathf.MoveTowards(_throttle01, throttleTarget, throttleRate * dt);

        // Ground speed chases a throttle-proportional target at the taxi rates.
        var targetSpeed = _throttle01 * Stats.MaxGroundSpeed;
        var speedRate = _groundSpeed < targetSpeed
            ? Stats.GroundAccel
            : Stats.GroundBrakeDecel;
        _groundSpeed = Mathf.MoveTowards(_groundSpeed, targetSpeed, speedRate * dt);
        CurrentSpeed = _groundSpeed;

        // Nosewheel steering from yaw input: zero parked, ramping to full by
        // GroundSteerSpeedRampUp and staying full. RollInput is inert here.
        if (_groundSpeed > 0.05f && !Mathf.Approximately(YawInput, 0f))
        {
            var steerScale = Mathf.Clamp01(
                _groundSpeed / Mathf.Max(Stats.GroundSteerSpeedRampUp, 0.0001f));
            _transform.Rotate(
                Vector3.up,
                YawInput * Stats.GroundSteerRateDeg * steerScale * dt,
                Space.World);
        }

        // Only at/above Vr does nose-up input have authority. Held -> the nose
        // keeps rising at GroundPitchRate (only a near-vertical guard);
        // released -> back to level.
        const float pitchGuardDeg = 85f;
        var climbSign = Stats.InvertPitch ? 1f : -1f;
        var climbCmd = Mathf.Clamp01(PitchInput * climbSign);
        var pitchAuthority = _groundSpeed >= Stats.RotationSpeed;
        var targetPitch = pitchAuthority ? climbCmd * pitchGuardDeg : 0f;
        _groundPitchDeg = Mathf.MoveTowards(
            _groundPitchDeg, targetPitch, Stats.GroundPitchRate * dt);

        // Attitude: heading from the flat forward, wings level, nose up by
        // _groundPitchDeg (negative Euler-X is nose-up here).
        var flatForward = _transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        flatForward.Normalize();
        _transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up)
            * Quaternion.Euler(-_groundPitchDeg, 0f, 0f);
        var climbForward = _transform.forward;

        // Wheels-on-strip height; no terrain (test scene) treats current Y as ground.
        var haveTerrain = _terrain != null;
        var restY = haveTerrain
            ? _terrainBaseY + _terrain.SampleHeight(pos) + Stats.GroundGearHeight
            : pos.y;

        // Follow the nose at ground speed. Nose down -> taxi, Y glued to the
        // strip (clamped settle); nose up -> climb but never sink through it.
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

        // Flying gate: only once lifted MinFlyAltitude above the strip (no
        // terrain: a held rotation above Vr). One-way handoff to the air model.
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
