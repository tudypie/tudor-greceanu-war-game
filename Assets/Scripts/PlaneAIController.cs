using UnityEngine;

// Enemy fighter AI. State machine: Patrol -> Engage -> Disengage.
//
// Disengage is parameterised by reason: Window/Overshoot keep the target (and
// its crowd-cap slot) and re-engage after a short timer; LoseInterest drops the
// target (freeing its slot so the swarm reshuffles) and wanders for longer. A
// global crowd cap stops a swarm dogpiling one plane; retaliation (turn on
// whoever shot us) overrides the player bias and all disengages.
//
// Flight safety is an OVERLAY, not a state: a predictive ground-collision
// threat blends the aim to a climb-out point and, at high threat, commands a
// hard wings-level pull with guns cold. A Layer-1 aim clamp and Layer-3 hard
// floor-recovery sit under it, mirrored for the ceiling and map boundary.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlaneFlightModel))]
[RequireComponent(typeof(PlaneHealth))]
public class PlaneAIController : MonoBehaviour
{
    enum AIState { Patrol, Engage, Disengage }
    enum DisengageReason { Window, LoseInterest, Overshoot }

    PlaneFlightModel _model;
    PlaneShooter _shooter;
    PlaneHealth _health;
    Transform _transform;

    public PlaneAIStats Stats;

    float _smoothPitch, _smoothRoll, _smoothYaw;

    Vector3 _anchor;
    Vector3 _patrolWaypoint;
    float _patrolWaypointDeadline;
    AIState _state = AIState.Patrol;

    PlaneHealth _target;
    float _nextTargetRefresh;
    float _targetOutOfRangeSince = -1f;

    // Global crowd cap: how many AIs (across the scene) pursue each friendly
    // plane. _countedTarget is the plane THIS AI is counted against, kept in
    // sync through SetTarget/OnDestroy so the count never leaks.
    static readonly System.Collections.Generic.Dictionary<PlaneHealth, int>
        s_attackerCount = new();
    PlaneHealth _countedTarget;

    // Retaliation: when shot by a hostile, lock onto the attacker; overrides
    // the player-target bias, ignores range loss, and skips disengages
    // (terrain safety still wins). Each fresh hit pushes the deadline out.
    PlaneHealth _retaliateTarget;
    float _retaliateUntil = -1f;
    bool RetaliationActive =>
        _retaliateTarget != null && !_retaliateTarget.IsDead &&
        Time.fixedTime < _retaliateUntil;

    // Disengage scheduling.
    DisengageReason _disengageReason;
    float _engageBreakAt;
    float _disengageUntil;
    float _nextBreakAllowed;
    float _nextDistractionRollAt;
    Vector3 _breakAimPoint;

    // Gun aim-assist.
    float _gunLockTime;
    float _aimNoiseSeed;
    float _burstUntil;
    float _cooldownUntil;

    bool _floorRecovering;

    // Predictive ground-collision avoidance (GCAS).
    float _threat;
    float _gcaWorstFloorAhead;
    Vector3 _gcaGroundDir = Vector3.forward;
    Vector3 _gcaClimbOutPoint;
    readonly float[] _rayYaw = new float[3];
    readonly System.Collections.Generic.List<Vector3> _gcaTrack = new();

    Terrain _terrain;
    float _terrainBaseY;

    PlaneFlightModel[] _avoidPlanes;
    float _nextAvoidRefresh;

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
        if (_health != null) _health.DamagedBy += OnDamagedBy;
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

    void OnDestroy()
    {
        if (_health != null) _health.DamagedBy -= OnDamagedBy;
        RegisterAttacker(null);
    }

    // Runs from PlaneHealth.TakeDamage (outside FixedUpdate): touches fields
    // and sets the target immediately so retaliation is responsive; the state
    // transition happens in the next UpdateState.
    void OnDamagedBy(float amount, PlaneHealth attacker)
    {
        if (Stats == null || !Stats.RetaliateWhenShot) return;
        if (attacker == null || attacker == _health || attacker.IsDead) return;
        if (_health == null || !_health.IsHostileTo(attacker)) return;
        _retaliateTarget = attacker;
        _retaliateUntil = Time.fixedTime + Stats.RetaliationDuration;
        SetTarget(attacker);
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

        // Computed before UpdateState so transitions and the steering overlay
        // below see the current threat.
        UpdateGroundThreat();

        UpdateState();

        // As the terrain threat rises, terrain wins: pursuit lead and
        // plane-vs-plane avoidance are suppressed and the aim blends toward
        // the recoverable climb-out point.
        var w = Stats.GcaEnabled
            ? Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(Stats.GcaSoftThreat, Stats.GcaHardThreat, _threat))
            : 0f;
        var avoidanceScale = 1f - w;
        var leadScale = 1f - w;

        var aimPoint = ResolveAimPoint(leadScale)
            + ComputeAvoidance() * avoidanceScale;

        // Layer 1: never aim below the anticipated terrain floor.
        var anticipatedFloor = AnticipatedFloorY();
        if (aimPoint.y < anticipatedFloor) aimPoint.y = anticipatedFloor;

        // Legacy fallback only (GCAS off): soft upward bias toward the floor.
        if (!Stats.GcaEnabled)
            aimPoint += ComputeAltitudeSoftBias(anticipatedFloor);

        // Ceiling (mirror of the floor), but only when the cap is above the
        // terrain floor, so a ridge poking above it is never clamped back down.
        var ceilingCap = CeilingCapY();
        if (ceilingCap > anticipatedFloor)
        {
            if (aimPoint.y > ceilingCap) aimPoint.y = ceilingCap;
            aimPoint += ComputeCeilingSoftBias(ceilingCap);
        }

        // Map boundary (horizontal mirror of the ceiling).
        var boundary = MapBoundary.Instance;
        if (boundary != null)
        {
            aimPoint = boundary.ClampInsideXZ(aimPoint, Stats.BoundaryClearance);
            aimPoint += ComputeBoundarySoftBias(boundary);
        }

        // GCAS overlay: blend the whole aim toward the climb-out point.
        if (w > 0f) aimPoint = Vector3.Lerp(aimPoint, _gcaClimbOutPoint, w);

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

        // GCAS overlay: roll toward wings-level so pitch authority climbs.
        if (w > 0f) _smoothRoll = Mathf.Lerp(_smoothRoll, 0f, w);

        // GCAS overlay: at high threat, decisive full nose-up wings-level pull
        // (bypasses proportional PitchGain) while still recoverable.
        var gcaHardPull = Stats.GcaEnabled && _threat >= Stats.GcaHardThreat;
        if (gcaHardPull)
        {
            _smoothPitch = ClimbInputSign();
            _smoothRoll = 0f;
            _smoothYaw = 0f;
        }

        _model.PitchInput = _smoothPitch;
        _model.RollInput = _smoothRoll;
        _model.YawInput = _smoothYaw;
        _model.Boost = false;

        // Layer 3: hard terrain-relative override. Below the actual floor the
        // AI abandons everything and climbs out wings-level, guns cold, until
        // it claws back a hysteresis margin above the floor.
        var floorNow = CurrentFloorY();
        var margin = Mathf.Max(Stats.TerrainClearance * Stats.FloorRecoverMarginFraction,
            Stats.FloorRecoverMarginMin);
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
            var climbPitch = ClimbInputSign();
            _smoothPitch = climbPitch;
            _smoothRoll = 0f;
            _smoothYaw = 0f;
            _model.PitchInput = climbPitch;
            _model.RollInput = 0f; // 0 lets the flight model auto-level the wings
            _model.YawInput = 0f;
            if (_shooter != null)
            {
                _shooter.UseAimDirection = false;
                _shooter.Trigger = false;
            }
            return;
        }

        if (gcaHardPull || (Stats.GcaEnabled && _threat >= Stats.GcaGunColdThreat))
        {
            if (_shooter != null)
            {
                _shooter.UseAimDirection = false;
                _shooter.Trigger = false;
            }
            return;
        }

        UpdateFiring();
    }

    // --- Terrain floor ---

    float TerrainGroundY(Vector3 worldPos)
    {
        if (_terrain == null) return float.MinValue;
        return _terrainBaseY + _terrain.SampleHeight(worldPos);
    }

    // Clearance above the ground, but never below the absolute
    // PatrolMinWorldY (which also covers the no-terrain case).
    float WorkingFloorAt(Vector3 worldPos)
    {
        var g = TerrainGroundY(worldPos);
        if (g <= float.MinValue * 0.5f) return Stats.PatrolMinWorldY;
        return Mathf.Max(Stats.PatrolMinWorldY, g + Stats.TerrainClearance);
    }

    float CurrentFloorY() => WorkingFloorAt(_transform.position);

    // Highest working floor along a..b, so a route is lifted above any hill
    // it would otherwise pass straight through (not just its endpoint).
    float WorstFloorAlong(Vector3 a, Vector3 b, int samples)
    {
        var n = Mathf.Clamp(samples, 2, 16);
        var worst = float.MinValue;
        for (int i = 0; i <= n; i++)
        {
            var f = WorkingFloorAt(Vector3.Lerp(a, b, i / (float)n));
            if (f > worst) worst = f;
        }
        return worst;
    }

    Vector3 FlatForward()
    {
        var f = _transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) f = new Vector3(_transform.right.x, 0f, _transform.right.z);
        if (f.sqrMagnitude < 0.0001f) return Vector3.forward;
        return f.normalized;
    }

    float LookAheadDistance() =>
        Mathf.Max(_model.CurrentSpeed, Stats.TerrainLookAheadMinSpeed)
        * Mathf.Max(Stats.TerrainLookAheadTime, 0f);

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

    // CeilingClearance under the flight model's effective hard ceiling.
    float CeilingCapY()
    {
        if (_model == null) return float.MaxValue;
        return _model.EffectiveServiceCeiling - Stats.CeilingClearance;
    }

    Vector3 ComputeCeilingSoftBias(float cap)
    {
        if (Stats.AltitudeRecoverStrength <= 0f) return Vector3.zero;
        var band = Mathf.Max(Stats.CeilingClearance, 1f);
        var y = _transform.position.y;
        if (y <= cap - band) return Vector3.zero;
        var depth = Mathf.Clamp01((y - (cap - band)) / band);
        return Vector3.down * Stats.AltitudeRecoverStrength * (depth * depth);
    }

    // --- Map boundary ---

    Vector3 ComputeBoundarySoftBias(MapBoundary boundary)
    {
        if (Stats.AltitudeRecoverStrength <= 0f) return Vector3.zero;
        var inset = Stats.BoundaryClearance;
        var band = Mathf.Max(inset, 1f);
        var pos = _transform.position;
        var d = boundary.OutsideDistanceXZ(pos, inset);
        if (d < 0.0001f) return Vector3.zero;
        var depth = Mathf.Clamp01(d / band);
        var inward = boundary.ClampInsideXZ(pos, inset) - pos;
        inward.y = 0f;
        if (inward.sqrMagnitude < 0.0001f) return Vector3.zero;
        return inward.normalized * Stats.AltitudeRecoverStrength * (depth * depth);
    }

    // --- Predictive ground-collision avoidance (GCAS) ---

    // Signed pitch input that produces a nose-UP command for this airframe.
    float ClimbInputSign() => _model.InvertPitch ? 1f : -1f;

    // True travel velocity (so a dive is sensed down its real path, not the
    // horizontal nose projection); falls back to the nose when near-stationary.
    Vector3 CurrentVelocity()
    {
        var body = _model.Body;
        var v = body != null ? body.linearVelocity : Vector3.zero;
        if (v.sqrMagnitude < 1f) v = _transform.forward * Mathf.Max(_model.CurrentSpeed, 1f);
        return v;
    }

    static Vector3 FlatDir(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }

    // Projects the trajectory forward and turns the worst predicted terrain
    // breach into a continuous threat in [0,1] plus a recoverable climb-out
    // point. Straight-line along true velocity by default; an optional
    // pull-up recovery sim relaxes false positives over rising terrain. A
    // 3-ray lateral fan covers the curved track a banking plane flies.
    void UpdateGroundThreat()
    {
        if (!Stats.GcaEnabled || _terrain == null)
        {
            _threat = 0f;
            if (_gcaTrack.Count > 0) _gcaTrack.Clear();
            return;
        }

        var pos = _transform.position;
        var vel = CurrentVelocity();
        var speed = Mathf.Max(vel.magnitude, 1f);
        _gcaGroundDir = FlatDir(vel);

        var step = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        var horizon = Mathf.Max(Stats.GcaProbeHorizonTime, step * 4f);
        int steps = Mathf.Clamp(Mathf.CeilToInt(horizon / step), 4, 600);
        int stride = Mathf.Clamp(Stats.GcaProbeStride, 1, 8);

        var bank = _transform.right.y;
        var turnRate = _model.Stats != null ? -bank * _model.Stats.BankTurnSpeed : 0f;
        var midTurn = turnRate * (horizon * 0.5f);
        var fan = Mathf.Max(Stats.GcaFanHalfAngleDeg, 0f);
        _rayYaw[0] = midTurn;
        _rayYaw[1] = midTurn - fan;
        _rayYaw[2] = midTurn + fan;

        var useSim = Stats.GcaUsePredictiveSim && _model.Stats != null;
        var tauR = Mathf.Max(Stats.ReactionTime, 0.0001f);
        var tauP = _model.Stats != null ? Mathf.Max(_model.Stats.PitchResponseTime, 0.0001f) : 0.0001f;
        var rMax = _model.Stats != null ? _model.Stats.PitchIncreaseSpeed : 60f;
        var thrust = _model.Stats != null ? _model.Stats.NormalThrust : speed;
        var drag = _model.Stats != null ? _model.Stats.DragMultiplier : 0f;
        var stallSpd = _model.Stats != null ? _model.Stats.StallSpeed : 0f;
        const float sinkBias = 2f; // conservative stand-in for un-modelled sink

        var maxBreach = 0f;
        var tHit = float.PositiveInfinity;
        var worstFloorAhead = WorkingFloorAt(pos);

        _gcaTrack.Clear();

        for (int r = 0; r < 3; r++)
        {
            var dir3 = Quaternion.AngleAxis(_rayYaw[r], Vector3.up) * vel;
            var p = pos;
            var prevFloor = WorkingFloorAt(p);

            var ground = FlatDir(dir3);
            var thetaDeg = Mathf.Asin(Mathf.Clamp(dir3.y / speed, -1f, 1f)) * Mathf.Rad2Deg;
            var inputRamp = 0f;
            var rate = 0f;

            for (int i = 1; i <= steps; i++)
            {
                if (useSim)
                {
                    inputRamp = Mathf.Lerp(inputRamp, 1f, 1f - Mathf.Exp(-step / tauR));
                    rate = Mathf.Lerp(rate, rMax * inputRamp, 1f - Mathf.Exp(-step / tauP));
                    thetaDeg = Mathf.Min(thetaDeg + rate * step, 85f);
                    var thetaRad = thetaDeg * Mathf.Deg2Rad;
                    var spd = Mathf.Max(Mathf.Max(thrust * (1f - Mathf.Sin(thetaRad) * drag), stallSpd), 1f);
                    p += ground * (spd * Mathf.Cos(thetaRad) * step);
                    p.y += (spd * Mathf.Sin(thetaRad) - sinkBias) * step;
                }
                else
                {
                    p += dir3 * step;
                }

                if (i % stride != 0 && i != steps) continue;

                var f = WorkingFloorAt(p);
                var floorBracket = Mathf.Max(f, prevFloor); // conservative vs. spikes
                prevFloor = f;
                if (f > worstFloorAhead) worstFloorAhead = f;

                var breach = floorBracket - p.y;
                if (breach > maxBreach) maxBreach = breach;
                if (breach > 0f && i * step < tHit) tHit = i * step;

                if (r == 0) _gcaTrack.Add(p);
            }
        }

        var depthScore = Mathf.Clamp01(maxBreach / Mathf.Max(Stats.GcaDepthRef, 0.0001f));
        var recT = Mathf.Max(Stats.GcaRecoverTime, 0.0001f);
        var timeScore = float.IsInfinity(tHit) ? 0f : Mathf.Clamp01((recT - tHit) / recT);
        var raw = Mathf.Max(depthScore, timeScore); // single authority, not additive

        // Asymmetric smoothing: arm fast for safety, release slow to kill chatter.
        var tau = raw > _threat
            ? Mathf.Max(Stats.GcaThreatAttackTime, 0.0001f)
            : Mathf.Max(Stats.GcaThreatReleaseTime, 0.0001f);
        _threat = Mathf.Lerp(_threat, raw, 1f - Mathf.Exp(-step / tau));

        var lookDist = Mathf.Max(speed * recT, speed * step * steps * 0.5f);
        var cp = pos + _gcaGroundDir * Mathf.Max(lookDist, 1f);
        cp.y = worstFloorAhead + Stats.GcaClimbOutMargin;
        var cap = CeilingCapY();
        if (cap > AnticipatedFloorY() && cp.y > cap) cp.y = cap;
        _gcaClimbOutPoint = cp;
        _gcaWorstFloorAhead = worstFloorAhead;
    }

    // --- Plane-vs-plane avoidance ---

    Vector3 ComputeAvoidance()
    {
        if (Stats.AvoidanceRadius <= 0f || Stats.AvoidanceStrength <= 0f) return Vector3.zero;

        if (_avoidPlanes == null || Time.fixedTime >= _nextAvoidRefresh)
        {
            _avoidPlanes = Object.FindObjectsByType<PlaneFlightModel>(FindObjectsSortMode.None);
            _nextAvoidRefresh = Time.fixedTime + Stats.AvoidanceRefreshInterval;
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

    // --- Aim point ---

    Vector3 ResolveAimPoint(float leadScale)
    {
        switch (_state)
        {
            case AIState.Engage:
                return _target != null
                    ? PredictedTargetPoint(Stats.SteerLeadTime * leadScale)
                    : _patrolWaypoint;
            case AIState.Disengage:
                return _disengageReason == DisengageReason.LoseInterest
                    ? _patrolWaypoint
                    : _breakAimPoint;
            default:
                return _patrolWaypoint;
        }
    }

    // Lagged target position plus a velocity lead (lead pursuit) so the AI
    // cuts the corner and closes for guns instead of tail-chasing forever.
    Vector3 PredictedTargetPoint(float leadTime)
    {
        if (_target == null) return _patrolWaypoint;
        var basePos = GetLaggedTargetPos();
        return basePos + EstimateTargetVelocity() * leadTime;
    }

    // --- State machine ---

    void UpdateState()
    {
        // Retaliation override: whoever shot us gets hunted now, cutting a
        // disengage or patrol short. Terrain safety is an overlay, not a
        // state, so nothing here yields to it.
        if (RetaliationActive && _target != null &&
            (_state == AIState.Patrol || _state == AIState.Disengage))
        {
            EnterEngage();
            return;
        }

        switch (_state)
        {
            case AIState.Patrol:
                if (_target != null)
                {
                    // Don't always commit: on a failed roll, peel off and
                    // wander so the swarm scatters instead of dogpiling.
                    if (Stats.EngageChance < 1f && Random.value > Stats.EngageChance)
                        EnterDisengage(DisengageReason.LoseInterest);
                    else
                        EnterEngage();
                    return;
                }
                RefreshPatrolWaypoint();
                break;

            case AIState.Engage:
                if (_target == null)
                {
                    EnterPatrol();
                    return;
                }
                // Scheduled, deliberate window break-off.
                if (!RetaliationActive &&
                    Time.fixedTime >= _engageBreakAt && Time.fixedTime >= _nextBreakAllowed)
                {
                    EnterDisengage(DisengageReason.Window);
                    return;
                }
                // Lose-interest: a small per-refresh chance to break contact
                // and wander. Rolled on the target-refresh cadence so the
                // documented mean attention span holds.
                if (!RetaliationActive && Stats.DistractionChance > 0f &&
                    Time.fixedTime >= _nextDistractionRollAt)
                {
                    _nextDistractionRollAt = Time.fixedTime + Stats.TargetRefreshInterval;
                    if (Random.value < Stats.DistractionChance)
                    {
                        EnterDisengage(DisengageReason.LoseInterest);
                        return;
                    }
                }
                // Emergency break to avoid a merge/overshoot.
                var toT = _target.transform.position - _transform.position;
                var dist = toT.magnitude;
                if (dist > 0.0001f && dist < Stats.MergeBreakDistance &&
                    Time.fixedTime >= _nextBreakAllowed &&
                    Vector3.Dot(_transform.forward, toT / dist) < Stats.BadAspectDot)
                {
                    EnterDisengage(DisengageReason.Overshoot);
                    return;
                }
                break;

            case AIState.Disengage:
                if (_disengageReason == DisengageReason.LoseInterest)
                {
                    RefreshPatrolWaypoint();
                    if (Time.fixedTime >= _disengageUntil) EnterPatrol();
                }
                else if (Time.fixedTime >= _disengageUntil)
                {
                    if (_target != null && !_target.IsDead) EnterEngage();
                    else EnterPatrol();
                }
                break;
        }
    }

    void EnterPatrol()
    {
        _state = AIState.Patrol;
        SetTarget(null);
        PickNewPatrolWaypoint();
    }

    void EnterEngage()
    {
        _state = AIState.Engage;
        _engageBreakAt = Time.fixedTime + Random.Range(Stats.EngageDurationMin, Stats.EngageDurationMax);
        _nextDistractionRollAt = Time.fixedTime + Stats.TargetRefreshInterval;
    }

    // Single deliberate-disengage entry. Window/Overshoot keep the target (and
    // its crowd-cap slot) and re-engage after a short timer; LoseInterest drops
    // the target (freeing its slot so the swarm reshuffles) and wanders for a
    // longer one. Retaliation overrides all of them.
    void EnterDisengage(DisengageReason reason)
    {
        _state = AIState.Disengage;
        _disengageReason = reason;

        if (reason == DisengageReason.LoseInterest)
        {
            SetTarget(null);
            PickNewPatrolWaypoint();
            _disengageUntil = Time.fixedTime +
                Random.Range(Stats.DistractedDurationMin, Stats.DistractedDurationMax);
            return;
        }

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
        var floor = WorstFloorAlong(_transform.position, _breakAimPoint,
            Stats.GcaRouteProbeCount);
        if (_breakAimPoint.y < floor) _breakAimPoint.y = floor;

        _disengageUntil = Time.fixedTime +
            Random.Range(Stats.BreakOffDurationMin, Stats.BreakOffDurationMax);
        _nextBreakAllowed = _disengageUntil + Stats.BreakOffCooldown;
    }

    // Shared patrol-waypoint cycling, reused by Patrol and the LoseInterest
    // wander: pick a fresh point once the current one is reached or times out.
    void RefreshPatrolWaypoint()
    {
        var reachSq = Stats.PatrolWaypointReachDistance * Stats.PatrolWaypointReachDistance;
        if ((_patrolWaypoint - _transform.position).sqrMagnitude <= reachSq ||
            Time.fixedTime >= _patrolWaypointDeadline)
        {
            PickNewPatrolWaypoint();
        }
    }

    // --- Targeting ---

    void UpdateTargetLoss()
    {
        if (_target == null || _state == AIState.Patrol) { _targetOutOfRangeSince = -1f; return; }
        if (RetaliationActive) { _targetOutOfRangeSince = -1f; return; }
        if (_target.IsDead) return;

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

    // Selection only. The decision to commit or peel off lives in the FSM.
    void RefreshTarget(bool force)
    {
        // Locked on whoever shot us: ignore the bias and the closest-hostile
        // switch. A killed/expired attacker falls through to normal selection.
        if (RetaliationActive)
        {
            SetTarget(_retaliateTarget);
            return;
        }

        // Suppress selection for the whole LoseInterest window so it actually
        // stays distracted (the state entry already dropped the target).
        if (_state == AIState.Disengage && _disengageReason == DisengageReason.LoseInterest)
            return;

        if (_target != null && (_target.IsDead || (!force && TargetLostByRange())))
        {
            SetTarget(null);
        }

        var all = Object.FindObjectsByType<PlaneHealth>(FindObjectsSortMode.None);
        var myPos = _transform.position;

        // The human player is scored as PlayerTargetBias times farther than it
        // really is, so the AI prefers allies and only commits to the player
        // when it is the only hostile or vastly closer. The AcquireRange gate
        // below still uses the TRUE distance.
        var playerScoreMul = Stats.PlayerTargetBias * Stats.PlayerTargetBias;

        PlaneHealth best = null;
        var bestScoreSq = float.MaxValue;
        var bestDistSq = float.MaxValue;
        foreach (var ph in all)
        {
            if (ph == null || ph == _health || ph.IsDead) continue;
            if (!_health.IsHostileTo(ph)) continue;
            if (TargetSlotFull(ph)) continue;
            var dSq = (ph.transform.position - myPos).sqrMagnitude;
            var scoreSq = ph.Faction == PlaneFaction.Player ? dSq * playerScoreMul : dSq;
            if (scoreSq < bestScoreSq)
            {
                bestScoreSq = scoreSq;
                bestDistSq = dSq;
                best = ph;
            }
        }

        if (best == null) return;

        var acquireSq = Stats.AcquireRange * Stats.AcquireRange;

        // No target: adopt one inside AcquireRange of where we ARE now (TRUE
        // distance). Whether to commit or wander is the Patrol state's call.
        if (_target == null)
        {
            if (force || bestDistSq <= acquireSq) SetTarget(best);
            return;
        }

        // Already chasing: switch only to a clearly-better (bias-weighted)
        // hostile so it won't ditch an ally for a slightly-closer player.
        if (best == _target) return;
        var curDistSq = (_target.transform.position - myPos).sqrMagnitude;
        var curScoreSq = _target.Faction == PlaneFaction.Player
            ? curDistSq * playerScoreMul : curDistSq;
        if (bestDistSq <= acquireSq &&
            bestScoreSq < curScoreSq * Stats.TargetSwitchHysteresis * Stats.TargetSwitchHysteresis)
        {
            SetTarget(best);
        }
    }

    void SetTarget(PlaneHealth t)
    {
        if (_target == t) return;
        _target = t;
        _targetOutOfRangeSince = -1f;
        RegisterAttacker(t);
        ResetLagBuffer(t);
    }

    // --- Crowd cap ---

    // Wipe the shared count before every play session so it survives "Enter
    // Play Mode" with domain reload disabled (no ghost attackers).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCrowdRegistry() => s_attackerCount.Clear();

    static int AttackerCount(PlaneHealth t) =>
        t != null && s_attackerCount.TryGetValue(t, out var n) ? n : 0;

    // Move this AI's single occupancy slot from its old target to the new one
    // (null = release). The only place s_attackerCount is mutated.
    void RegisterAttacker(PlaneHealth t)
    {
        if (_countedTarget == t) return;
        if (_countedTarget != null && s_attackerCount.TryGetValue(_countedTarget, out var n))
        {
            if (n <= 1) s_attackerCount.Remove(_countedTarget);
            else s_attackerCount[_countedTarget] = n - 1;
        }
        _countedTarget = t;
        if (t != null)
            s_attackerCount[t] = AttackerCount(t) + 1;
    }

    // Is this hostile at its crowd cap? Counts only OTHER AIs (we discount our
    // own slot) so we never evict ourselves off a target we already hold.
    bool TargetSlotFull(PlaneHealth t)
    {
        if (t == null) return false;
        var cap = t.Faction == PlaneFaction.Player
            ? Stats.MaxAttackersOnPlayer
            : Stats.MaxAttackersPerTarget;
        if (cap <= 0) return false; // unlimited
        var others = AttackerCount(t);
        if (_countedTarget == t) others--;
        return others >= cap;
    }

    // --- Gun aim-assist ---

    void UpdateFiring()
    {
        if (_shooter == null) return;

        var wantsFire = false;

        if (_state == AIState.Engage && _target != null)
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
                    // The gun is HITSCAN, so the solution points at the
                    // target's CURRENT position from the muzzle — leading a
                    // crossing target throws an instant bullet ahead of it.
                    // GunLeadTime only compensates the solve→shot sub-step.
                    var muzzle = _transform.position + _transform.forward * _shooter.MuzzleOffsetZ;
                    var aimPoint = targetPos + EstimateTargetVelocity() * Stats.GunLeadTime;
                    var desired = (aimPoint - muzzle).normalized;

                    var maxRad = Stats.GunLockMaxCorrectionDeg * Mathf.Deg2Rad;
                    var solution = Vector3.RotateTowards(_transform.forward, desired, maxRad, 0f);

                    // Fire on the UNDEGRADED solution so the gate stays clean:
                    // it shoots when genuinely lined up; the residual spray
                    // below decides spread, not whether it shoots.
                    var aimToTarget = (targetPos - muzzle).normalized;
                    wantsFire = Vector3.Dot(solution.normalized, aimToTarget) > coneCos
                                && HasShotLineOfSight(muzzle, targetPos, dist);

                    // Small residual spray, scaled up with range / off-tail
                    // aspect, jittered fast so a burst spreads instead of
                    // walking on as one coherent block.
                    var rangeFrac = Mathf.Clamp01(dist / Mathf.Max(Stats.FireRange, 0.0001f));
                    var aspectErr = 1f - Mathf.Clamp01(noseDot);
                    var rangeScale = Mathf.Lerp(Stats.GunAimNoisePointBlankScale, 1f, rangeFrac);
                    var noiseDeg = Stats.GunAimNoiseDeg * rangeScale * (1f + aspectErr);

                    var aim = solution;
                    if (noiseDeg > 0.0001f)
                    {
                        var nt = Time.time * Stats.GunAimNoiseFrequency;
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

    // Mirrors PlaneShooter's hitscan: the shot is only worth taking if the
    // first body the ray meets is the target (else terrain/another plane
    // masks it).
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
            if (ph == _health) continue;
            anyBlocker = true;
            if (h.distance < nearest)
            {
                nearest = h.distance;
                nearestIsTarget = _target != null && ph == _target;
            }
        }

        return !anyBlocker || nearestIsTarget;
    }

    // --- Lag buffer ---

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

    // --- Patrol ---

    void PickNewPatrolWaypoint()
    {
        var horiz = Random.insideUnitCircle * Stats.PatrolRadius;
        var dy = Random.Range(-Stats.PatrolVerticalRange, Stats.PatrolVerticalRange);
        _patrolWaypoint = _anchor + new Vector3(horiz.x, dy, horiz.y);
        // Clamp inside the map BEFORE the floor sample so the route is sampled
        // at the corrected XZ (a spawn near the edge would otherwise throw
        // patrol legs into the hard turn-back).
        var boundary = MapBoundary.Instance;
        if (boundary != null)
            _patrolWaypoint = boundary.ClampInsideXZ(_patrolWaypoint, Stats.BoundaryClearance);
        var floor = WorstFloorAlong(_transform.position, _patrolWaypoint,
            Stats.GcaRouteProbeCount);
        if (_patrolWaypoint.y < floor) _patrolWaypoint.y = floor;
        _patrolWaypointDeadline = Time.fixedTime + Stats.PatrolWaypointTimeout;
    }

    // --- Gizmos ---

    void OnDrawGizmos()
    {
        var playing = Application.isPlaying;
        Color stateColor;
        if (playing && _state == AIState.Disengage) stateColor = Color.yellow;
        else if (playing && _state == AIState.Engage) stateColor = Color.red;
        else stateColor = Color.green;
        Gizmos.color = stateColor;
        DrawArrow(transform.position, transform.forward, 50f);

        if (!playing || Stats == null || !Stats.GcaEnabled) return;

        // Predicted ground track coloured by the live threat (green→amber→red)
        // with a marker at the climb-out point — confirms the threat ramps
        // BEFORE impact.
        var threatColor = _threat < 0.5f
            ? Color.Lerp(Color.green, Color.yellow, _threat * 2f)
            : Color.Lerp(Color.yellow, Color.red, (_threat - 0.5f) * 2f);
        Gizmos.color = threatColor;
        for (int i = 1; i < _gcaTrack.Count; i++)
            Gizmos.DrawLine(_gcaTrack[i - 1], _gcaTrack[i]);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_gcaClimbOutPoint, 15f);
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

    // Firing envelope (when selected): FireConeDeg half-angle, FireRange long,
    // with the FireMinDistance dead zone near the nose.
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

        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
        var capCentre = o + fwd * range;
        var capR = range * tan;
        DrawRing(capCentre, right, up, capR, 40);
        Gizmos.DrawLine(o, capCentre);
        for (int i = 0; i < 8; i++)
        {
            var a = i / 8f * Mathf.PI * 2f;
            Gizmos.DrawLine(o, capCentre + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * capR);
        }

        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.8f);
        DrawRing(o + fwd * minD, right, up, Mathf.Max(minD * tan, 0.001f), 24);

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
