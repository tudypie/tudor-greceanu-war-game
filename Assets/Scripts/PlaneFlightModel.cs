using UnityEngine;

public class PlaneFlightModel : MonoBehaviour
{
    Transform _transform;
    Rigidbody _rigidbody;

    public float NormalThrust = 600f;
    public float MaxThrust = 1200f;
    public float ThrustAgilityMultiplier = 1.8f;

    public float PitchIncreaseSpeed = 300f;
    public bool InvertPitch = true;

    public float RollIncreaseSpeed = 420f;
    public float RollAutoLevelSpeed = 120f;

    public float YawSpeed = 30f;
    public float BankTurnSpeed = 15f;

    public float RollResponseTime = 0.3f;
    public float PitchResponseTime = 0.3f;
    public float YawResponseTime = 0.3f;

    [HideInInspector] public float PitchInput;
    [HideInInspector] public float RollInput;
    [HideInInspector] public float YawInput;
    [HideInInspector] public bool Boost;

    float _pitchRate;
    float _rollRate;
    float _yawRate;

    public Transform CachedTransform => _transform;
    public Rigidbody Body => _rigidbody;

    void Start()
    {
        _transform = transform;
        _rigidbody = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        var agility = Boost ? ThrustAgilityMultiplier : 1f;
        var dt = Time.fixedDeltaTime;

        var bank = _transform.right.y;

        var targetPitchRate = (InvertPitch ? -PitchInput : PitchInput) * PitchIncreaseSpeed * agility;
        var targetRollRate = Mathf.Approximately(RollInput, 0f)
            ? -bank * RollAutoLevelSpeed * agility
            : -RollInput * RollIncreaseSpeed * agility;
        var targetYawRate = (YawInput * YawSpeed - bank * BankTurnSpeed) * agility;

        var alphaPitch = 1f - Mathf.Exp(-dt / Mathf.Max(PitchResponseTime, 0.0001f));
        var alphaRoll = 1f - Mathf.Exp(-dt / Mathf.Max(RollResponseTime, 0.0001f));
        var alphaYaw = 1f - Mathf.Exp(-dt / Mathf.Max(YawResponseTime, 0.0001f));

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

        var thrust = Boost ? MaxThrust : NormalThrust;
        _rigidbody.linearVelocity = _transform.forward * (thrust * Time.fixedDeltaTime);
    }
}
