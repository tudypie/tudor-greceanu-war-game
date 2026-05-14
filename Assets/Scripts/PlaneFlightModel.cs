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

    public Transform CachedTransform => _transform;
    public Rigidbody Body => _rigidbody;
    public bool InvertPitch => Stats != null && Stats.InvertPitch;

    void Start()
    {
        _transform = transform;
        _rigidbody = GetComponent<Rigidbody>();
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

        var targetPitchRate = (Stats.InvertPitch ? -PitchInput : PitchInput) * Stats.PitchIncreaseSpeed * agility;
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

        var thrust = Boost ? Stats.MaxThrust : Stats.NormalThrust;
        _rigidbody.linearVelocity = _transform.forward * (thrust * Time.fixedDeltaTime);
    }
}
