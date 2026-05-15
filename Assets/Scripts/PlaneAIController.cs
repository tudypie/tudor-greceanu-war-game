using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlaneFlightModel))]
[RequireComponent(typeof(PlaneHealth))]
public class PlaneAIController : MonoBehaviour
{
    enum AIState { Patrolling, Chasing, Extending }

    PlaneFlightModel _model;
    PlaneShooter _shooter;
    PlaneHealth _health;
    Transform _transform;

    public PlaneAIStats Stats;

    float _smoothPitch, _smoothRoll, _smoothYaw;
    Vector3 _committedAimPoint;
    float _commitUntil;
    float _burstUntil;
    float _cooldownUntil;

    Vector3 _anchor;
    Vector3 _patrolWaypoint;
    float _patrolWaypointDeadline;
    AIState _state = AIState.Patrolling;

    PlaneHealth _target;
    float _nextTargetRefresh;

    Vector3 _extendAimPoint;
    float _extendUntil;
    float _nextExtendAllowed;

    float _gunLockTime;
    float _aimNoiseSeed;
    const float GunNoiseFrequency = 1.7f;

    bool _floorRecovering;

    const int LagBufferSize = 32;
    readonly Vector3[] _lagPositions = new Vector3[LagBufferSize];
    readonly float[] _lagTimes = new float[LagBufferSize];
    int _lagHead;
    int _lagCount;
    PlaneHealth _lagSampledTarget;

    void Start()
    {
        _transform = transform;
        _model = GetComponent<PlaneFlightModel>();
        _shooter = GetComponent<PlaneShooter>();
        _health = GetComponent<PlaneHealth>();
        _anchor = _transform.position;
        _aimNoiseSeed = Random.value * 1000f;
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneAIController)} on {name} has no Stats assigned.", this);
            return;
        }
        PickNewPatrolWaypoint();
        _committedAimPoint = _patrolWaypoint;
        RefreshTarget(force: true);
    }

    void FixedUpdate()
    {
        if (_model == null || Stats == null) return;

        if (Time.fixedTime >= _nextTargetRefresh)
        {
            RefreshTarget(force: false);
            _nextTargetRefresh = Time.fixedTime + Stats.TargetRefreshInterval;
        }

        SampleTargetForLag();
        UpdateState();

        if (Time.fixedTime >= _commitUntil)
        {
            _committedAimPoint = ResolveAimPoint();
            // Never commit to a point below the altitude floor, even when
            // chasing a target that dives for the deck.
            if (_committedAimPoint.y < Stats.PatrolMinWorldY)
                _committedAimPoint.y = Stats.PatrolMinWorldY;
            _commitUntil = Time.fixedTime + Random.Range(Stats.CommitMin, Stats.CommitMax);
        }

        var aimPoint = _committedAimPoint + ComputeAvoidance()
            + ComputeTerrainAvoidance() + ComputeAltitudeFloor();
        var toAim = aimPoint - _transform.position;
        var aimDistance = toAim.magnitude;
        if (aimDistance < 0.0001f) return;

        var dirWorld = toAim / aimDistance;
        var dirLocal = _transform.InverseTransformDirection(dirWorld);

        var pitchSign = _model.InvertPitch ? +1f : -1f;
        var targetPitch = Mathf.Clamp(dirLocal.y * Stats.PitchGain * pitchSign, -1f, 1f);
        var targetRoll = Mathf.Clamp(dirLocal.x * Stats.RollGain, -1f, 1f);
        var targetYaw = Mathf.Clamp(dirLocal.x * Stats.YawGain, -1f, 1f);

        var alpha = Stats.ReactionTime > 0f
            ? 1f - Mathf.Exp(-Time.fixedDeltaTime / Stats.ReactionTime)
            : 1f;
        _smoothPitch = Mathf.Lerp(_smoothPitch, targetPitch, alpha);
        _smoothRoll = Mathf.Lerp(_smoothRoll, targetRoll, alpha);
        _smoothYaw = Mathf.Lerp(_smoothYaw, targetYaw, alpha);

        _model.PitchInput = _smoothPitch;
        _model.RollInput = _smoothRoll;
        _model.YawInput = _smoothYaw;

        _model.Boost = false;

        // Hard global altitude floor. Below PatrolMinWorldY the AI abandons
        // whatever it was doing — target, avoidance, aim — and climbs out:
        // wings level, full nose-up, guns cold. No matter what. Hysteresis
        // (must climb a margin back above the floor) stops boundary chatter.
        var floor = Stats.PatrolMinWorldY;
        var y = _transform.position.y;
        if (_floorRecovering)
        {
            if (y >= floor + Mathf.Max(floor * 0.25f, 5f)) _floorRecovering = false;
        }
        else if (y < floor)
        {
            _floorRecovering = true;
        }

        if (_floorRecovering)
        {
            var climbPitch = _model.InvertPitch ? 1f : -1f;
            _smoothPitch = climbPitch;
            _smoothRoll = 0f;
            _smoothYaw = 0f;
            _model.PitchInput = climbPitch;
            _model.RollInput = 0f;   // 0 lets the flight model auto-level the wings
            _model.YawInput = 0f;
            if (_shooter != null)
            {
                _shooter.UseAimDirection = false;
                _shooter.Trigger = false;
            }
            return;
        }

        UpdateFiring();
    }

    Vector3 ComputeTerrainAvoidance()
    {
        if (Stats.TerrainLookAhead <= 0f || Stats.TerrainStrength <= 0f) return Vector3.zero;

        var origin = _transform.position;
        var dir = _transform.forward;
        var mask = ~(1 << gameObject.layer);

        if (!Physics.SphereCast(origin, Stats.TerrainSafetyRadius, dir, out var hit,
            Stats.TerrainLookAhead, mask, QueryTriggerInteraction.Ignore))
            return Vector3.zero;

        var perpNormal = hit.normal - dir * Vector3.Dot(hit.normal, dir);
        Vector3 avoidDir;
        if (perpNormal.sqrMagnitude < 0.01f) avoidDir = _transform.up;
        else avoidDir = perpNormal.normalized;

        var t = 1f - hit.distance / Stats.TerrainLookAhead;
        var weight = t * t;
        return avoidDir * Stats.TerrainStrength * weight;
    }

    // Hard altitude floor: as the plane nears (and especially drops below)
    // PatrolMinWorldY, push the aim point sharply upward so the AI climbs
    // away from the ground instead of chasing a target into it. Independent
    // of the forward terrain spherecast, which can miss a shallow descent
    // toward flat ground.
    Vector3 ComputeAltitudeFloor()
    {
        if (Stats.AltitudeRecoverStrength <= 0f) return Vector3.zero;

        var floor = Stats.PatrolMinWorldY;
        var band = Mathf.Max(floor * 0.5f, 1f);
        var y = _transform.position.y;
        if (y >= floor + band) return Vector3.zero;

        // 0 at the top of the soft band, 1 at the floor and below.
        var depth = Mathf.Clamp01((floor + band - y) / band);
        return Vector3.up * Stats.AltitudeRecoverStrength * (depth * depth);
    }

    Vector3 ComputeAvoidance()
    {
        if (Stats.AvoidanceRadius <= 0f || Stats.AvoidanceStrength <= 0f) return Vector3.zero;

        var bias = Vector3.zero;
        var all = Object.FindObjectsByType<PlaneFlightModel>(FindObjectsSortMode.None);
        var myPos = _transform.position;
        var myFwd = _transform.forward;
        var radSq = Stats.AvoidanceRadius * Stats.AvoidanceRadius;
        var targetGo = _target != null ? _target.gameObject : null;

        foreach (var p in all)
        {
            if (p == null || p == _model) continue;
            if (targetGo != null && p.gameObject == targetGo) continue;

            var d = p.transform.position - myPos;
            var distSq = d.sqrMagnitude;
            if (distSq > radSq || distSq < 0.0001f) continue;

            var dist = Mathf.Sqrt(distSq);
            var dir = d / dist;
            if (Vector3.Dot(myFwd, dir) < Stats.AvoidanceAheadDot) continue;

            var perp = dir - myFwd * Vector3.Dot(dir, myFwd);
            Vector3 avoidDir;
            if (perp.sqrMagnitude < 0.01f) avoidDir = _transform.up;
            else avoidDir = -perp.normalized;

            var weight = 1f - dist / Stats.AvoidanceRadius;
            bias += avoidDir * Stats.AvoidanceStrength * weight;
        }
        return bias;
    }

    Vector3 ResolveAimPoint()
    {
        switch (_state)
        {
            case AIState.Chasing:
                return _target != null ? GetLaggedTargetPos() : _patrolWaypoint;
            case AIState.Extending:
                return _extendAimPoint;
            default:
                return _patrolWaypoint;
        }
    }

    void UpdateState()
    {
        switch (_state)
        {
            case AIState.Patrolling:
                if (_target != null && IsTargetEngageable(_target))
                {
                    EnterChasing();
                    return;
                }
                var reachSq = Stats.PatrolWaypointReachDistance * Stats.PatrolWaypointReachDistance;
                if ((_patrolWaypoint - _transform.position).sqrMagnitude <= reachSq ||
                    Time.fixedTime >= _patrolWaypointDeadline)
                {
                    PickNewPatrolWaypoint();
                    _commitUntil = 0f;
                }
                break;

            case AIState.Chasing:
                if (_target == null || !IsTargetEngageable(_target))
                {
                    _state = AIState.Patrolling;
                    PickNewPatrolWaypoint();
                    _commitUntil = 0f;
                    return;
                }
                if (Time.fixedTime >= _nextExtendAllowed)
                {
                    var toTarget = _target.transform.position - _transform.position;
                    var distSq = toTarget.sqrMagnitude;
                    if (distSq > 0.0001f)
                    {
                        var aspectDot = Vector3.Dot(_transform.forward, toTarget.normalized);
                        if (aspectDot < Stats.BadAspectDot)
                        {
                            EnterExtending();
                            return;
                        }
                    }
                }
                break;

            case AIState.Extending:
                if (Time.fixedTime >= _extendUntil)
                {
                    if (_target != null && IsTargetEngageable(_target)) EnterChasing();
                    else
                    {
                        _state = AIState.Patrolling;
                        PickNewPatrolWaypoint();
                    }
                    _commitUntil = 0f;
                }
                break;
        }
    }

    void EnterChasing()
    {
        _state = AIState.Chasing;
        _commitUntil = 0f;
        _nextExtendAllowed = Time.fixedTime + Stats.RepositionDuration;
    }

    void EnterExtending()
    {
        _state = AIState.Extending;
        _extendAimPoint = _transform.position + _transform.forward * Stats.ExtendDistance;
        if (_extendAimPoint.y < Stats.PatrolMinWorldY) _extendAimPoint.y = Stats.PatrolMinWorldY;
        _extendUntil = Time.fixedTime + Random.Range(Stats.ExtendMin, Stats.ExtendMax);
        _commitUntil = 0f;
    }

    // AI gunnery: a deliberately weaker analogue of the player's lock-on.
    // Flight steering still uses lag pursuit (beatable); only the *gun*
    // aim gets an assist, gated by a soft lock and degraded by a hard
    // correction cap plus range/aspect-scaled aim noise.
    void UpdateFiring()
    {
        if (_shooter == null) return;

        var wantsFire = false;

        if (_state == AIState.Chasing && _target != null)
        {
            var targetPos = _target.transform.position;
            var toTarget = targetPos - _transform.position;
            var dist = toTarget.magnitude;
            var inRange = dist < Stats.FireRange && dist > Stats.FireMinDistance;
            var coneCos = Mathf.Cos(Stats.FireConeDeg * Mathf.Deg2Rad);

            if (dist > 0.0001f && inRange)
            {
                var trueDir = toTarget / dist;
                var noseDot = Vector3.Dot(_transform.forward, trueDir);

                // Build the soft lock while the target sits ahead; it
                // collapses the instant it leaves the cone (the player's
                // lock gets a grace period — the AI's does not).
                if (noseDot > coneCos)
                    _gunLockTime += Time.fixedDeltaTime;
                else
                    _gunLockTime = 0f;

                if (_gunLockTime >= Stats.GunLockAcquireTime)
                {
                    // Lead the target's CURRENT position, not the lagged
                    // pursuit point the flight path tracks.
                    var lead = targetPos + EstimateTargetVelocity() * Stats.GunLeadTime;
                    var desired = (lead - _transform.position).normalized;

                    // Correction cap: the solution may only bend so far off
                    // the nose, so a hard-jinking target outruns it and the
                    // AI must keep flying for position.
                    var maxRad = Stats.GunLockMaxCorrectionDeg * Mathf.Deg2Rad;
                    var aim = Vector3.RotateTowards(_transform.forward, desired, maxRad, 0f);

                    // Aim noise grows with range and off-tail aspect, so only
                    // close, saddled shots are accurate.
                    var rangeFrac = Mathf.Clamp01(dist / Mathf.Max(Stats.FireRange, 0.0001f));
                    var aspectErr = 1f - Mathf.Clamp01(noseDot);
                    var noiseDeg = Stats.GunAimNoiseDeg * (0.4f + 0.6f * rangeFrac) * (1f + aspectErr);

                    var nt = Time.time * GunNoiseFrequency;
                    var yaw = (Mathf.PerlinNoise(_aimNoiseSeed, nt) - 0.5f) * 2f * noiseDeg;
                    var pitch = (Mathf.PerlinNoise(nt, _aimNoiseSeed + 31.7f) - 0.5f) * 2f * noiseDeg;
                    aim = Quaternion.AngleAxis(yaw, _transform.up)
                        * Quaternion.AngleAxis(pitch, _transform.right) * aim;

                    _shooter.UseAimDirection = true;
                    _shooter.AimDirection = aim;

                    // Fire only if the capped+noised solution still points at
                    // the target — a jinker that beats the cap stays safe.
                    wantsFire = Vector3.Dot(aim.normalized, trueDir) > coneCos;
                }
                else
                {
                    _shooter.UseAimDirection = false;
                }
            }
            else
            {
                _gunLockTime = 0f;
                _shooter.UseAimDirection = false;
            }
        }
        else
        {
            _gunLockTime = 0f;
            _shooter.UseAimDirection = false;
        }

        _shooter.Trigger = ResolveBurstTrigger(wantsFire);
    }

    Vector3 EstimateTargetVelocity()
    {
        if (_lagCount < 2) return Vector3.zero;
        var i0 = (_lagHead - 1 + LagBufferSize) % LagBufferSize;
        var i1 = (_lagHead - 2 + LagBufferSize) % LagBufferSize;
        var dt = _lagTimes[i0] - _lagTimes[i1];
        if (dt <= 0.0001f) return Vector3.zero;
        return (_lagPositions[i0] - _lagPositions[i1]) / dt;
    }

    bool IsTargetEngageable(PlaneHealth t)
    {
        if (t == null || t.IsDead) return false;
        var d = t.transform.position - _anchor;
        return d.sqrMagnitude <= Stats.EngagementRadius * Stats.EngagementRadius;
    }

    void RefreshTarget(bool force)
    {
        var all = Object.FindObjectsByType<PlaneHealth>(FindObjectsSortMode.None);
        var myPos = transform.position;
        var engageSq = Stats.EngagementRadius * Stats.EngagementRadius;

        PlaneHealth best = null;
        var bestDistSq = float.MaxValue;
        foreach (var ph in all)
        {
            if (ph == null || ph == _health) continue;
            if (ph.IsDead) continue;
            if (!_health.IsHostileTo(ph)) continue;
            var dSq = (ph.transform.position - _anchor).sqrMagnitude;
            if (dSq > engageSq) continue;
            var myDistSq = (ph.transform.position - myPos).sqrMagnitude;
            if (myDistSq < bestDistSq)
            {
                bestDistSq = myDistSq;
                best = ph;
            }
        }

        if (best == null)
        {
            _target = null;
            ResetLagBuffer(null);
            return;
        }

        if (force || _target == null || _target.IsDead)
        {
            SetTarget(best);
            return;
        }

        var currentDistSq = (_target.transform.position - myPos).sqrMagnitude;
        if (bestDistSq < currentDistSq * Stats.TargetSwitchHysteresis * Stats.TargetSwitchHysteresis)
        {
            SetTarget(best);
        }
    }

    void SetTarget(PlaneHealth t)
    {
        if (_target == t) return;
        _target = t;
        ResetLagBuffer(t);
    }

    void ResetLagBuffer(PlaneHealth t)
    {
        _lagSampledTarget = t;
        _lagHead = 0;
        _lagCount = 0;
        _gunLockTime = 0f;
    }

    void SampleTargetForLag()
    {
        if (_target == null) return;
        if (_target != _lagSampledTarget) ResetLagBuffer(_target);

        _lagPositions[_lagHead] = _target.transform.position;
        _lagTimes[_lagHead] = Time.fixedTime;
        _lagHead = (_lagHead + 1) % LagBufferSize;
        if (_lagCount < LagBufferSize) _lagCount++;
    }

    Vector3 GetLaggedTargetPos()
    {
        if (_target == null) return _patrolWaypoint;
        if (_lagCount == 0 || Stats.LagSeconds <= 0f) return _target.transform.position;

        var targetTime = Time.fixedTime - Stats.LagSeconds;
        for (int i = 1; i <= _lagCount; i++)
        {
            var idx = (_lagHead - i + LagBufferSize) % LagBufferSize;
            if (_lagTimes[idx] <= targetTime) return _lagPositions[idx];
        }
        var oldestIdx = (_lagHead - _lagCount + LagBufferSize) % LagBufferSize;
        return _lagPositions[oldestIdx];
    }

    void PickNewPatrolWaypoint()
    {
        var horiz = Random.insideUnitCircle * Stats.PatrolRadius;
        var dy = Random.Range(-Stats.PatrolVerticalRange, Stats.PatrolVerticalRange);
        _patrolWaypoint = _anchor + new Vector3(horiz.x, dy, horiz.y);
        if (_patrolWaypoint.y < Stats.PatrolMinWorldY) _patrolWaypoint.y = Stats.PatrolMinWorldY;
        _patrolWaypointDeadline = Time.fixedTime + Stats.PatrolWaypointTimeout;
    }

    void OnDrawGizmos()
    {
        var isChasing = Application.isPlaying && _state == AIState.Chasing;
        var isExtending = Application.isPlaying && _state == AIState.Extending;
        Gizmos.color = isExtending ? Color.yellow : (isChasing ? Color.red : Color.green);

        DrawArrow(transform.position, transform.forward, 50f);
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
            _cooldownUntil = now + Random.Range(Stats.CooldownMin, Stats.CooldownMax);
            return false;
        }

        if (wantsFire)
        {
            _burstUntil = now + Random.Range(Stats.BurstMin, Stats.BurstMax);
            return true;
        }

        return false;
    }
}
