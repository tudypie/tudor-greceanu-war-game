using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlaneFlightModel))]
public class PlaneAIController : MonoBehaviour
{
    enum AIState { Patrolling, Chasing }

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

    [Header("Patrol")]
    public float PatrolRadius = 350f;
    public float PatrolVerticalRange = 60f;
    public float PatrolMinWorldY = 30f;
    public float PatrolWaypointReachDistance = 70f;
    public float PatrolWaypointTimeout = 12f;

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

    Vector3 _anchor;
    Vector3 _patrolWaypoint;
    float _patrolWaypointDeadline;
    AIState _state = AIState.Patrolling;

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
        _anchor = _transform.position;
        PickNewPatrolWaypoint();
        _committedAimPoint = _patrolWaypoint;
    }

    void FixedUpdate()
    {
        if (_model == null) return;

        UpdateState();

        if (Time.fixedTime >= _commitUntil)
        {
            _committedAimPoint = _state == AIState.Chasing && Target != null
                ? Target.position
                : _patrolWaypoint;
            _commitUntil = Time.fixedTime + Random.Range(CommitMin, CommitMax);
        }

        var toAim = _committedAimPoint - _transform.position;
        var aimDistance = toAim.magnitude;
        if (aimDistance < 0.0001f) return;

        var dirWorld = toAim / aimDistance;
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
            var wantsFire = false;
            if (_state == AIState.Chasing && Target != null)
            {
                var liveDistance = Vector3.Distance(Target.position, _transform.position);
                var coneCos = Mathf.Cos(FireConeDeg * Mathf.Deg2Rad);
                var aligned = dirLocal.z > coneCos;
                var inRange = liveDistance < FireRange && liveDistance > FireMinDistance;
                wantsFire = aligned && inRange;
            }
            _shooter.Trigger = ResolveBurstTrigger(wantsFire);
        }
    }

    void UpdateState()
    {
        if (_state == AIState.Chasing) return;

        if (Target != null)
        {
            var distSq = (Target.position - _anchor).sqrMagnitude;
            if (distSq <= PatrolRadius * PatrolRadius)
            {
                _state = AIState.Chasing;
                _commitUntil = 0f;
                return;
            }
        }

        var reachSq = PatrolWaypointReachDistance * PatrolWaypointReachDistance;
        if ((_patrolWaypoint - _transform.position).sqrMagnitude <= reachSq ||
            Time.fixedTime >= _patrolWaypointDeadline)
        {
            PickNewPatrolWaypoint();
            _commitUntil = 0f;
        }
    }

    void PickNewPatrolWaypoint()
    {
        var horiz = Random.insideUnitCircle * PatrolRadius;
        var dy = Random.Range(-PatrolVerticalRange, PatrolVerticalRange);
        _patrolWaypoint = _anchor + new Vector3(horiz.x, dy, horiz.y);
        if (_patrolWaypoint.y < PatrolMinWorldY) _patrolWaypoint.y = PatrolMinWorldY;
        _patrolWaypointDeadline = Time.fixedTime + PatrolWaypointTimeout;
    }

    void OnDrawGizmos()
    {
        var chasing = Application.isPlaying && _state == AIState.Chasing;
        Gizmos.color = chasing ? Color.red : Color.green;

        var anchor = Application.isPlaying ? _anchor : transform.position;
        DrawHorizontalCircle(anchor, PatrolRadius, 48);

        DrawArrow(transform.position, transform.forward, 50f);
    }

    static void DrawHorizontalCircle(Vector3 center, float radius, int segments)
    {
        var step = Mathf.PI * 2f / segments;
        var prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            var t = i * step;
            var next = center + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    static void DrawArrow(Vector3 origin, Vector3 dir, float length)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();
        var tip = origin + dir * length;
        Gizmos.DrawLine(origin, tip);

        var up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
        var side = Vector3.Cross(dir, up).normalized;
        var back = tip - dir * (length * 0.2f);
        Gizmos.DrawLine(tip, back + side * (length * 0.12f));
        Gizmos.DrawLine(tip, back - side * (length * 0.12f));
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
