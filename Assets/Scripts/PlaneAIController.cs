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
    public float BehindRollAssist = 1f;
    public float BehindPitchAssist = 0.5f;

    [Header("Firing")]
    public float FireConeDeg = 8f;
    public float FireRange = 350f;
    public float FireMinDistance = 12f;

    [Header("Feel")]
    public float ReactionTime = 0.15f;

    float _smoothPitch, _smoothRoll, _smoothYaw;

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
    }

    void FixedUpdate()
    {
        if (Target == null || _model == null) return;

        var toTarget = Target.position - _transform.position;
        var distance = toTarget.magnitude;
        if (distance < 0.0001f) return;

        var dirWorld = toTarget / distance;
        var dirLocal = _transform.InverseTransformDirection(dirWorld);

        var pitchSign = _model.InvertPitch ? +1f : -1f;
        var targetPitch = Mathf.Clamp(dirLocal.y * PitchGain * pitchSign, -1f, 1f);
        var targetRoll = Mathf.Clamp(dirLocal.x * RollGain, -1f, 1f);
        var targetYaw = Mathf.Clamp(dirLocal.x * YawGain, -1f, 1f);

        if (dirLocal.z < 0f)
        {
            var side = dirLocal.x >= 0f ? 1f : -1f;
            targetRoll = Mathf.Clamp(targetRoll + side * BehindRollAssist, -1f, 1f);
            targetPitch = Mathf.Clamp(targetPitch + BehindPitchAssist * pitchSign, -1f, 1f);
        }

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
            var inRange = distance < FireRange && distance > FireMinDistance;
            _shooter.Trigger = aligned && inRange;
        }
    }
}
