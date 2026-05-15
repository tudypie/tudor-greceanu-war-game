using UnityEngine;

// Enemy fighter AI.
//
// Engagement model (per design):
//   * It patrols around its spawn anchor ONLY while it has no target.
//   * The instant a hostile is within AcquireRange of its CURRENT position it
//     commits and chases the closest target as far as it has to — there is no
//     spawn-point leash. It only gives up if the target dies or stays beyond
//     LoseRange for LoseTargetTime.
//   * It periodically performs a DELIBERATE, time-boxed break-off so the
//     player gets a window to reposition, then turns back in.
//
// Terrain: the floor is terrain-relative. Ground height is sampled under and
// ahead of the plane via Terrain.SampleHeight (no physics layers involved, so
// it works regardless of how the terrain collider is layered) and the AI
// climbs over ridges before reaching them. A three-layer floor is preserved:
//   1. the final aim point's Y is clamped to the anticipated terrain floor,
//   2. a soft upward bias ramps in within a band above the floor,
//   3. a hard override below the actual floor: wings-level, full nose-up,
//      guns cold, with hysteresis.
//
// Ceiling: the same idea, inverted. The AI clamps its aim point below
// (PlaneFlightStats.ServiceCeiling - CeilingClearance) and adds a soft
// DOWNWARD bias within that band, so it levels off on its own instead of
// porpoising where PlaneFlightModel would force its nose down anyway. The
// hard physical limit itself is enforced by PlaneFlightModel for every plane.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlaneFlightModel))]
[RequireComponent(typeof(PlaneHealth))]
public class PlaneAIController : MonoBehaviour
{
    enum AIState { Patrolling, Pursuing, BreakOff }

    PlaneFlightModel _model;
    PlaneShooter _shooter;
    PlaneHealth _health;
    Transform _transform;

    public PlaneAIStats Stats;

    float _smoothPitch, _smoothRoll, _smoothYaw;

    Vector3 _anchor;
    Vector3 _patrolWaypoint;
    float _patrolWaypointDeadline;
    AIState _state = AIState.Patrolling;

    PlaneHealth _target;
    float _nextTargetRefresh;
    float _targetOutOfRangeSince = -1f;

    // Deliberate break-off scheduling.
    float _engageBreakAt;
    float _breakOffUntil;
    float _nextBreakAllowed;
    Vector3 _breakAimPoint;

    // Gun aim-assist.
    float _gunLockTime;
    float _aimNoiseSeed;
    float _burstUntil;
    float _cooldownUntil;
    // High enough that the aim jitters noticeably between shots in a burst
    // (FireInterval ~0.08 s), so a burst sprays rather than drifting onto the
    // target as one coherent block.
    const float GunNoiseFrequency = 9f;

    bool _floorRecovering;

    // Terrain.
    Terrain _terrain;
    float _terrainBaseY;

    // Cached "other planes" list for collision avoidance (refreshed on an
    // interval instead of every physics step).
    PlaneFlightModel[] _avoidPlanes;
    float _nextAvoidRefresh;
    const float AvoidRefreshInterval = 0.5f;

    // Lagged-position ring buffer (steering lag + target velocity estimate).
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
        CacheTerrain();
        PickNewPatrolWaypoint();
        RefreshTarget(force: true);
    }

    void CacheTerrain()
    {
        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
        _terrainBaseY = _terrain != null ? _terrain.transform.position.y : 0f;
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
        UpdateTargetLoss();
        UpdateState();

        var aimPoint = ResolveAimPoint()
            + ComputeAvoidance()
            + ComputeTerrainLateral();

        // Layer 1: never aim below the anticipated terrain floor.
        var anticipatedFloor = AnticipatedFloorY();
        if (aimPoint.y < anticipatedFloor) aimPoint.y = anticipatedFloor;

        // Layer 2: soft upward bias as we sink toward that floor.
        aimPoint += ComputeAltitudeSoftBias(anticipatedFloor);

        // Ceiling (mirror of the floor): keep the aim point below the soft cap
        // and add a soft downward bias as we climb into it — but only when the
        // cap is actually above the terrain floor, so a ridge poking above the
        // ceiling never gets clamped back down into the hillside.
        var ceilingCap = CeilingCapY();
        if (ceilingCap > anticipatedFloor)
        {
            if (aimPoint.y > ceilingCap) aimPoint.y = ceilingCap;
            aimPoint += ComputeCeilingSoftBias(ceilingCap);
        }

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

        // Layer 3: hard terrain-relative override. Below the ACTUAL floor the
        // AI abandons everything and climbs out wings-level, guns cold, until
        // it has clawed back a hysteresis margin above the floor.
        var floorNow = CurrentFloorY();
        var margin = Mathf.Max(Stats.TerrainClearance * 0.4f, 5f);
        var y = _transform.position.y;
        if (_floorRecovering)
        {
            if (y >= floorNow + margin) _floorRecovering = false;
        }
        else if (y < floorNow)
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

    // --- Terrain floor ------------------------------------------------------

    float TerrainGroundY(Vector3 worldPos)
    {
        if (_terrain == null) return float.MinValue;
        return _terrainBaseY + _terrain.SampleHeight(worldPos);
    }

    // Working floor at a world XZ: clearance above the ground, but never below
    // the absolute PatrolMinWorldY (which also covers the no-terrain case).
    float WorkingFloorAt(Vector3 worldPos)
    {
        var g = TerrainGroundY(worldPos);
        if (g <= float.MinValue * 0.5f) return Stats.PatrolMinWorldY;
        return Mathf.Max(Stats.PatrolMinWorldY, g + Stats.TerrainClearance);
    }

    float CurrentFloorY() => WorkingFloorAt(_transform.position);

    Vector3 FlatForward()
    {
        var f = _transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) f = new Vector3(_transform.right.x, 0f, _transform.right.z);
        if (f.sqrMagnitude < 0.0001f) return Vector3.forward;
        return f.normalized;
    }

    float LookAheadDistance() =>
        Mathf.Max(_model.CurrentSpeed, 20f) * Mathf.Max(Stats.TerrainLookAheadTime, 0f);

    // Highest working floor between here and the look-ahead point, so a ridge
    // ahead lifts the floor (and the clamped aim point) before we reach it.
    float AnticipatedFloorY()
    {
        var pos = _transform.position;
        var floor = WorkingFloorAt(pos);
        var look = LookAheadDistance();
        if (look <= 0.01f || _terrain == null) return floor;

        var fwd = FlatForward();
        int n = Mathf.Clamp(Stats.TerrainProbeCount, 2, 16);
        for (int i = 1; i <= n; i++)
        {
            var p = pos + fwd * (look * i / n);
            var f = WorkingFloorAt(p);
            if (f > floor) floor = f;
        }
        return floor;
    }

    Vector3 ComputeAltitudeSoftBias(float floor)
    {
        if (Stats.AltitudeRecoverStrength <= 0f) return Vector3.zero;
        var band = Mathf.Max(Stats.TerrainClearance, 1f);
        var y = _transform.position.y;
        if (y >= floor + band) return Vector3.zero;
        var depth = Mathf.Clamp01((floor + band - y) / band);
        return Vector3.up * Stats.AltitudeRecoverStrength * (depth * depth);
    }

    // Soft cap the AI keeps below: CeilingClearance under the flight model's
    // hard ServiceCeiling. MaxValue (no clamp) if there is no flight stats.
    float CeilingCapY()
    {
        if (_model == null || _model.Stats == null) return float.MaxValue;
        return _model.Stats.ServiceCeiling - Stats.CeilingClearance;
    }

    // Mirror of ComputeAltitudeSoftBias: a downward bias that ramps in as the
    // plane climbs through the band below the cap, so it noses over on its own
    // before the flight model has to force it.
    Vector3 ComputeCeilingSoftBias(float cap)
    {
        if (Stats.AltitudeRecoverStrength <= 0f) return Vector3.zero;
        var band = Mathf.Max(Stats.CeilingClearance, 1f);
        var y = _transform.position.y;
        if (y <= cap - band) return Vector3.zero;
        var depth = Mathf.Clamp01((y - (cap - band)) / band);
        return Vector3.down * Stats.AltitudeRecoverStrength * (depth * depth);
    }

    // If a tall ridge sits dead ahead, bias sideways toward whichever flank is
    // lower so the AI flies AROUND a mountain instead of stalling up its face.
    Vector3 ComputeTerrainLateral()
    {
        if (_terrain == null || Stats.TerrainAvoidLateralStrength <= 0f) return Vector3.zero;

        var pos = _transform.position;
        var look = LookAheadDistance();
        if (look <= 0.01f) return Vector3.zero;

        var fwd = FlatForward();
        var lookPoint = pos + fwd * look;
        var centreFloor = WorkingFloorAt(lookPoint);

        // Only react if the path ahead actually climbs above us.
        var exceed = centreFloor - pos.y;
        if (exceed <= 0f) return Vector3.zero;

        var right = new Vector3(fwd.z, 0f, -fwd.x);
        var lat = look * 0.5f;
        var leftFloor = WorkingFloorAt(lookPoint - right * lat);
        var rightFloor = WorkingFloorAt(lookPoint + right * lat);

        // Already going to need a big climb; nudge toward the lower side.
        var diff = leftFloor - rightFloor;
        if (Mathf.Abs(diff) < 1f) return Vector3.zero;

        var sign = diff > 0f ? 1f : -1f; // right side lower -> bias +right
        var weight = Mathf.Clamp01(exceed / Mathf.Max(Stats.TerrainClearance, 1f));
        return right * sign * Stats.TerrainAvoidLateralStrength * weight;
    }

    // --- Plane-vs-plane avoidance ------------------------------------------

    Vector3 ComputeAvoidance()
    {
        if (Stats.AvoidanceRadius <= 0f || Stats.AvoidanceStrength <= 0f) return Vector3.zero;

        if (_avoidPlanes == null || Time.fixedTime >= _nextAvoidRefresh)
        {
            _avoidPlanes = Object.FindObjectsByType<PlaneFlightModel>(FindObjectsSortMode.None);
            _nextAvoidRefresh = Time.fixedTime + AvoidRefreshInterval;
        }

        var bias = Vector3.zero;
        var myPos = _transform.position;
        var myFwd = _transform.forward;
        var radSq = Stats.AvoidanceRadius * Stats.AvoidanceRadius;
        var targetGo = _target != null ? _target.gameObject : null;

        foreach (var p in _avoidPlanes)
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

    // --- Aim point ----------------------------------------------------------

    Vector3 ResolveAimPoint()
    {
        switch (_state)
        {
            case AIState.Pursuing:
                return _target != null ? PredictedTargetPoint(Stats.SteerLeadTime) : _patrolWaypoint;
            case AIState.BreakOff:
                return _breakAimPoint;
            default:
                return _patrolWaypoint;
        }
    }

    // Lagged target position plus a velocity lead — lead pursuit, so the AI
    // cuts the corner and closes for guns instead of tail-chasing forever.
    Vector3 PredictedTargetPoint(float leadTime)
    {
        if (_target == null) return _patrolWaypoint;
        var basePos = GetLaggedTargetPos();
        return basePos + EstimateTargetVelocity() * leadTime;
    }

    // --- State machine ------------------------------------------------------

    void UpdateState()
    {
        switch (_state)
        {
            case AIState.Patrolling:
                if (_target != null)
                {
                    EnterPursuing();
                    return;
                }
                var reachSq = Stats.PatrolWaypointReachDistance * Stats.PatrolWaypointReachDistance;
                if ((_patrolWaypoint - _transform.position).sqrMagnitude <= reachSq ||
                    Time.fixedTime >= _patrolWaypointDeadline)
                {
                    PickNewPatrolWaypoint();
                }
                break;

            case AIState.Pursuing:
                if (_target == null)
                {
                    EnterPatrolling();
                    return;
                }
                // Scheduled, deliberate break-off (gated by the cooldown).
                if (Time.fixedTime >= _engageBreakAt && Time.fixedTime >= _nextBreakAllowed)
                {
                    EnterBreakOff();
                    return;
                }
                // Emergency break to avoid a merge/overshoot.
                var toT = _target.transform.position - _transform.position;
                var dist = toT.magnitude;
                if (dist > 0.0001f && dist < Stats.MergeBreakDistance &&
                    Time.fixedTime >= _nextBreakAllowed)
                {
                    if (Vector3.Dot(_transform.forward, toT / dist) < Stats.BadAspectDot)
                    {
                        EnterBreakOff();
                        return;
                    }
                }
                break;

            case AIState.BreakOff:
                if (Time.fixedTime >= _breakOffUntil)
                {
                    if (_target != null) EnterPursuing();
                    else EnterPatrolling();
                }
                break;
        }
    }

    void EnterPatrolling()
    {
        _state = AIState.Patrolling;
        SetTarget(null);
        PickNewPatrolWaypoint();
    }

    void EnterPursuing()
    {
        _state = AIState.Pursuing;
        // Schedule the next deliberate break-off.
        _engageBreakAt = Time.fixedTime + Random.Range(Stats.EngageDurationMin, Stats.EngageDurationMax);
    }

    void EnterBreakOff()
    {
        _state = AIState.BreakOff;

        // Extend along the current heading, biased slightly away from the
        // target, so the AI separates and the player gets a clean window.
        var fwd = _transform.forward;
        var away = fwd;
        if (_target != null)
        {
            var toT = _target.transform.position - _transform.position;
            if (toT.sqrMagnitude > 0.0001f)
                away = (fwd - toT.normalized * 0.5f).normalized;
        }
        _breakAimPoint = _transform.position + away * Stats.ExtendDistance;
        var floor = WorkingFloorAt(_breakAimPoint);
        if (_breakAimPoint.y < floor) _breakAimPoint.y = floor;

        var duration = Random.Range(Stats.BreakOffDurationMin, Stats.BreakOffDurationMax);
        _breakOffUntil = Time.fixedTime + duration;
        // No second break-off until the cooldown after this one ends.
        _nextBreakAllowed = _breakOffUntil + Stats.BreakOffCooldown;
    }

    // --- Targeting ----------------------------------------------------------

    void UpdateTargetLoss()
    {
        if (_target == null || _state == AIState.Patrolling) { _targetOutOfRangeSince = -1f; return; }
        if (_target.IsDead) return; // handled in RefreshTarget/UpdateState

        var distSq = (_target.transform.position - _transform.position).sqrMagnitude;
        if (distSq <= Stats.LoseRange * Stats.LoseRange)
        {
            _targetOutOfRangeSince = -1f;
        }
        else if (_targetOutOfRangeSince < 0f)
        {
            _targetOutOfRangeSince = Time.fixedTime;
        }
    }

    bool TargetLostByRange()
    {
        return _targetOutOfRangeSince >= 0f &&
               Time.fixedTime - _targetOutOfRangeSince >= Stats.LoseTargetTime;
    }

    void RefreshTarget(bool force)
    {
        // Drop a dead/destroyed or run-away target.
        if (_target != null && (_target.IsDead || (!force && TargetLostByRange())))
        {
            SetTarget(null);
        }

        var all = Object.FindObjectsByType<PlaneHealth>(FindObjectsSortMode.None);
        var myPos = _transform.position;

        PlaneHealth best = null;
        var bestDistSq = float.MaxValue;
        foreach (var ph in all)
        {
            if (ph == null || ph == _health || ph.IsDead) continue;
            if (!_health.IsHostileTo(ph)) continue;
            var dSq = (ph.transform.position - myPos).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                best = ph;
            }
        }

        if (best == null) return;

        var acquireSq = Stats.AcquireRange * Stats.AcquireRange;

        // No target: only adopt one inside AcquireRange of where we ARE now.
        if (_target == null)
        {
            if (force || bestDistSq <= acquireSq) SetTarget(best);
            return;
        }

        // Already chasing: keep it, but switch to a clearly-closer hostile so
        // the AI mostly fights whatever is nearest.
        if (best == _target) return;
        var currentDistSq = (_target.transform.position - myPos).sqrMagnitude;
        if (bestDistSq <= acquireSq &&
            bestDistSq < currentDistSq * Stats.TargetSwitchHysteresis * Stats.TargetSwitchHysteresis)
        {
            SetTarget(best);
        }
    }

    void SetTarget(PlaneHealth t)
    {
        if (_target == t) return;
        _target = t;
        _targetOutOfRangeSince = -1f;
        ResetLagBuffer(t);
    }

    // --- Gun aim-assist (genuinely dangerous when it gets position) --------

    void UpdateFiring()
    {
        if (_shooter == null) return;

        var wantsFire = false;

        if (_state == AIState.Pursuing && _target != null)
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

                if (noseDot > coneCos)
                    _gunLockTime += Time.fixedDeltaTime;
                else
                    _gunLockTime = 0f;

                if (_gunLockTime >= Stats.GunLockAcquireTime)
                {
                    // The gun is HITSCAN (PlaneShooter does an instant
                    // Physics.Raycast), so the solution must point at the
                    // target's CURRENT position from the MUZZLE — leading a
                    // crossing target throws an instant bullet meters ahead of
                    // it. GunLeadTime is only a tiny compensation for the
                    // sub-step between this solve and the shot.
                    var muzzle = _transform.position + _transform.forward * _shooter.MuzzleOffsetZ;
                    var aimPoint = targetPos + EstimateTargetVelocity() * Stats.GunLeadTime;
                    var desired = (aimPoint - muzzle).normalized;

                    var maxRad = Stats.GunLockMaxCorrectionDeg * Mathf.Deg2Rad;
                    var solution = Vector3.RotateTowards(_transform.forward, desired, maxRad, 0f);

                    // The decision to fire is made on the UNDEGRADED solution:
                    // the AI shoots when it genuinely has the target lined up
                    // (and terrain isn't masking it). This must stay decoupled
                    // from the noised shot below — gating on the noisy aim
                    // would make it only fire when the spray happens to align,
                    // i.e. it'd still mostly hit. We want it to fire and miss.
                    var aimToTarget = (targetPos - muzzle).normalized;
                    wantsFire = Vector3.Dot(solution.normalized, aimToTarget) > coneCos
                                && HasShotLineOfSight(muzzle, targetPos, dist);

                    // The SHOT is deliberately a poor gunner: noise is always
                    // present (even point-blank, dead on the tail) and grows
                    // with range / off-tail aspect, so the majority of rounds
                    // miss and only the occasional one chips the player. Fast
                    // jitter so a burst sprays instead of walking on as a block.
                    var rangeFrac = Mathf.Clamp01(dist / Mathf.Max(Stats.FireRange, 0.0001f));
                    var aspectErr = 1f - Mathf.Clamp01(noseDot);
                    var noiseDeg = Stats.GunAimNoiseDeg * (0.7f + 0.3f * rangeFrac) * (1f + aspectErr);

                    var aim = solution;
                    if (noiseDeg > 0.0001f)
                    {
                        var nt = Time.time * GunNoiseFrequency;
                        var yaw = (Mathf.PerlinNoise(_aimNoiseSeed, nt) - 0.5f) * 2f * noiseDeg;
                        var pitch = (Mathf.PerlinNoise(nt, _aimNoiseSeed + 31.7f) - 0.5f) * 2f * noiseDeg;
                        aim = Quaternion.AngleAxis(yaw, _transform.up)
                            * Quaternion.AngleAxis(pitch, _transform.right) * solution;
                    }

                    _shooter.UseAimDirection = true;
                    _shooter.AimDirection = aim;
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

    static readonly RaycastHit[] _losHits = new RaycastHit[16];

    // Does the muzzle have a clear line to the target, or is terrain (or
    // another body) masking it? Mirrors PlaneShooter's hitscan: the shot is
    // only worth taking if the FIRST thing the ray meets is the target.
    bool HasShotLineOfSight(Vector3 muzzle, Vector3 targetPos, float dist)
    {
        var to = targetPos - muzzle;
        var len = to.magnitude;
        if (len < 0.0001f) return true;
        var dir = to / len;

        int n = Physics.RaycastNonAlloc(muzzle, dir, _losHits, len + 1f, ~0,
            QueryTriggerInteraction.Ignore);

        var nearest = float.MaxValue;
        var nearestIsTarget = false;
        var anyBlocker = false;
        for (int i = 0; i < n; i++)
        {
            var h = _losHits[i];
            var ph = h.collider.GetComponentInParent<PlaneHealth>();
            if (ph == _health) continue; // ignore our own airframe
            anyBlocker = true;
            if (h.distance < nearest)
            {
                nearest = h.distance;
                nearestIsTarget = _target != null && ph == _target;
            }
        }

        // Nothing in the way, or the closest body the ray meets IS the
        // target — clear to shoot. Terrain/anything else first = masked.
        return !anyBlocker || nearestIsTarget;
    }

    // --- Lag buffer ---------------------------------------------------------

    Vector3 EstimateTargetVelocity()
    {
        if (_lagCount < 2) return Vector3.zero;
        var i0 = (_lagHead - 1 + LagBufferSize) % LagBufferSize;
        var i1 = (_lagHead - 2 + LagBufferSize) % LagBufferSize;
        var dt = _lagTimes[i0] - _lagTimes[i1];
        if (dt <= 0.0001f) return Vector3.zero;
        return (_lagPositions[i0] - _lagPositions[i1]) / dt;
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

    // --- Patrol -------------------------------------------------------------

    void PickNewPatrolWaypoint()
    {
        var horiz = Random.insideUnitCircle * Stats.PatrolRadius;
        var dy = Random.Range(-Stats.PatrolVerticalRange, Stats.PatrolVerticalRange);
        _patrolWaypoint = _anchor + new Vector3(horiz.x, dy, horiz.y);
        var floor = WorkingFloorAt(_patrolWaypoint);
        if (_patrolWaypoint.y < floor) _patrolWaypoint.y = floor;
        _patrolWaypointDeadline = Time.fixedTime + Stats.PatrolWaypointTimeout;
    }

    // --- Gizmos -------------------------------------------------------------

    void OnDrawGizmos()
    {
        var playing = Application.isPlaying;
        var isBreak = playing && _state == AIState.BreakOff;
        var isChase = playing && _state == AIState.Pursuing;
        Gizmos.color = isBreak ? Color.yellow : (isChase ? Color.red : Color.green);
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

    // Firing envelope, shown when the plane is selected: the cone is
    // FireConeDeg half-angle and FireRange long, with the FireMinDistance
    // dead zone marked near the nose.
    void OnDrawGizmosSelected()
    {
        if (Stats == null) return;

        var o = transform.position;
        var fwd = transform.forward;
        var right = transform.right;
        var up = transform.up;

        var half = Mathf.Max(Stats.FireConeDeg, 0f) * Mathf.Deg2Rad;
        var range = Mathf.Max(Stats.FireRange, 0f);
        var minD = Mathf.Max(Stats.FireMinDistance, 0f);
        var tan = Mathf.Tan(half);

        // Cone body + end cap at FireRange.
        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
        var capCentre = o + fwd * range;
        var capR = range * tan;
        DrawRing(capCentre, right, up, capR, 40);
        Gizmos.DrawLine(o, capCentre); // boresight / range
        for (int i = 0; i < 8; i++)
        {
            var a = i / 8f * Mathf.PI * 2f;
            Gizmos.DrawLine(o, capCentre + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * capR);
        }

        // FireMinDistance dead zone.
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.8f);
        DrawRing(o + fwd * minD, right, up, Mathf.Max(minD * tan, 0.001f), 24);

        // Acquire range, for context (faint sphere).
        Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(o, Stats.AcquireRange);
    }

    static void DrawRing(Vector3 c, Vector3 ax1, Vector3 ax2, float r, int seg)
    {
        var prev = c + ax1 * r;
        for (int i = 1; i <= seg; i++)
        {
            var a = i / (float)seg * Mathf.PI * 2f;
            var p = c + (ax1 * Mathf.Cos(a) + ax2 * Mathf.Sin(a)) * r;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
