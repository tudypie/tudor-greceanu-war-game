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
    bool _stalling;

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
    /// Current airspeed in m/s along the flight path, after climb/dive drag
    /// is applied. Useful for HUD readouts and audio.
    /// </summary>
    public float CurrentSpeed { get; private set; }

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
        var agility = Boost ? Stats.ThrustAgilityMultiplier : 1f;
        var dt = Time.fixedDeltaTime;

        var bank = _transform.right.y;

        // climbFactor: +1 when pointing straight up, -1 straight down.
        var climbFactor = Vector3.Dot(_transform.forward, Vector3.up);

        // Air drag: climbing bleeds airspeed, diving builds it back up.
        var speedMultiplier = 1f - climbFactor * Stats.DragMultiplier;
        var baseThrust = Boost ? Stats.MaxThrust : Stats.NormalThrust;
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

        var targetPitchRate = _stalling
            // Lost the wing: force the nose down to trade altitude for speed,
            // ignoring pilot pitch input until recovered.
            ? Stats.StallNoseDownRate * agility
            : (Stats.InvertPitch ? -PitchInput : PitchInput) * Stats.PitchIncreaseSpeed * agility;
        var targetRollRate = Mathf.Approximately(RollInput, 0f)
            ? -bank * Stats.RollAutoLevelSpeed * agility
            : -RollInput * Stats.RollIncreaseSpeed * agility;
        var targetYawRate = (YawInput * Stats.YawSpeed - bank * Stats.BankTurnSpeed) * agility;

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
