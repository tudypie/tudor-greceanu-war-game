using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlaneFlightModel))]
public class PlaneAIController : MonoBehaviour
{
    PlaneFlightModel _model;
    PlaneShooter _shooter;
    Transform _transform;

    public Transform Target;
    public bool AutoFindPlayer = true;

    [Header("Steering")]
    public float PitchGain = 2.5f;
    public float YawGain = 1.5f;
    public float RollGain = 3.5f;

    [Header("Commit Window")]
    public float CommitMin = 0.8f;
    public float CommitMax = 1.5f;

    [Header("Firing")]
    public float FireConeDeg = 8f;
    public float FireRange = 350f;
    public float FireMinDistance = 12f;

    [Header("Burst Fire")]
    public float BurstMin = 0.4f;
    public float BurstMax = 0.8f;
    public float CooldownMin = 0.6f;
    public float CooldownMax = 1.2f;

    [Header("Feel")]
    public float ReactionTime = 0.15f;

    float _smoothPitch, _smoothRoll, _smoothYaw;
    Vector3 _committedAimPoint;
    float _commitUntil;
    float _burstUntil;
    float _cooldownUntil;

    void Start()
    {
        _transform = transform;
        _model = GetComponent<PlaneFlightModel>();
        _shooter = GetComponent<PlaneShooter>();
        if (Target == null && AutoFindPlayer)
        {
            var player = FindFirstObjectByType<PlanePlayerInput>();
            if (player != null) Target = player.transform;
        }
        if (Target != null) _committedAimPoint = Target.position;
    }

    void FixedUpdate()
    {
        if (Target == null || _model == null) return;

        if (Time.fixedTime >= _commitUntil)
        {
            _committedAimPoint = Target.position;
            _commitUntil = Time.fixedTime + Random.Range(CommitMin, CommitMax);
        }

        var liveDistance = Vector3.Distance(Target.position, _transform.position);

        var toTarget = _committedAimPoint - _transform.position;
        var distance = toTarget.magnitude;
        if (distance < 0.0001f) return;

        var dirWorld = toTarget / distance;
        var dirLocal = _transform.InverseTransformDirection(dirWorld);

        var pitchSign = _model.InvertPitch ? +1f : -1f;
        var targetPitch = Mathf.Clamp(dirLocal.y * PitchGain * pitchSign, -1f, 1f);
        var targetRoll = Mathf.Clamp(dirLocal.x * RollGain, -1f, 1f);
        var targetYaw = Mathf.Clamp(dirLocal.x * YawGain, -1f, 1f);

        var alpha = ReactionTime > 0f
            ? 1f - Mathf.Exp(-Time.fixedDeltaTime / ReactionTime)
            : 1f;
        _smoothPitch = Mathf.Lerp(_smoothPitch, targetPitch, alpha);
        _smoothRoll = Mathf.Lerp(_smoothRoll, targetRoll, alpha);
        _smoothYaw = Mathf.Lerp(_smoothYaw, targetYaw, alpha);

        _model.PitchInput = _smoothPitch;
        _model.RollInput = _smoothRoll;
        _model.YawInput = _smoothYaw;

        _model.Boost = false;

        if (_shooter != null)
        {
            var coneCos = Mathf.Cos(FireConeDeg * Mathf.Deg2Rad);
            var aligned = dirLocal.z > coneCos;
            var inRange = liveDistance < FireRange && liveDistance > FireMinDistance;
            _shooter.Trigger = ResolveBurstTrigger(aligned && inRange);
        }
    }

    bool ResolveBurstTrigger(bool wantsFire)
    {
        var now = Time.time;

        if (now < _cooldownUntil) return false;

        if (now < _burstUntil) return wantsFire;

        if (_burstUntil > 0f && now >= _burstUntil && _cooldownUntil < _burstUntil)
        {
            _cooldownUntil = now + Random.Range(CooldownMin, CooldownMax);
            return false;
        }

        if (wantsFire)
        {
            _burstUntil = now + Random.Range(BurstMin, BurstMax);
            return true;
        }

        return false;
    }
}
