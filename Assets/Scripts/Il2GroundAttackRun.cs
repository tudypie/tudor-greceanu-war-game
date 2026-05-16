using UnityEngine;

// MISSION 1 ONLY (Makievska). The Shturmovik's reason for being here: a
// scripted ground-attack run on the airfield. Added at spawn by
// MakievskaWaveDirector onto IL-2 clones. It coexists with the shared
// PlaneAIController WITHOUT modifying it — while the run is live it simply
// disables that component and drives PlaneFlightModel itself (same input
// contract: PitchInput/RollInput/YawInput/Boost). After the pass it hands the
// airframe BACK to PlaneAIController, so a surviving IL-2 then defends itself
// like any other enemy.
//
// Graceful degradation: no Airfield in the scene -> this disables itself
// immediately and the IL-2 is a plain dogfighter (pure air-to-air, exactly as
// before this mission content existed).
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(PlaneFlightModel))]
public class Il2GroundAttackRun : MonoBehaviour
{
    enum Phase { Ingress, Egress, Done }

    [Tooltip("Set by the wave director; used only to resolve the Airfield. Optional.")]
    public MakievskaWaveDirector Director;

    [Header("Run profile")]
    public float RunInAltitude = 70f;     // m above terrain on the attack leg
    public float DeliverRadius = 240f;    // horizontal dist to TargetPoint that counts as a delivered pass
    public float DamagePerPass = 12f;
    public float EgressAltitude = 320f;   // m above terrain to climb to on the way out
    public float EgressDuration = 7f;     // seconds flying out before handing back to the AI

    [Header("Steering")]
    public float PitchGain = 2.2f;
    public float YawGain = 1.4f;
    public float RollGain = 3.0f;
    public float ReactionTime = 0.18f;
    [Tooltip("Minimum metres above the sampled terrain the run is allowed to aim — keeps the low pass from mowing the steppe.")]
    public float MinClearance = 35f;

    PlaneFlightModel _model;
    PlaneHealth _health;
    PlaneShooter _shooter;
    PlaneAIController _ai;
    Terrain _terrain;
    float _terrainBaseY;

    Phase _phase = Phase.Ingress;
    Airfield _airfield;
    float _egressUntil;
    float _smoothPitch, _smoothRoll, _smoothYaw;

    void Start()
    {
        _model = GetComponent<PlaneFlightModel>();
        _health = GetComponent<PlaneHealth>();
        _shooter = GetComponent<PlaneShooter>();
        _ai = GetComponent<PlaneAIController>();

        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
        _terrainBaseY = _terrain != null ? _terrain.transform.position.y : 0f;

        _airfield = Director != null ? Director.FindAirfield() : FindFirstObjectByType<Airfield>();

        // No objective to run on, or it is already lost -> be a normal fighter.
        if (_airfield == null || _airfield.IsLost) { HandBack(); return; }

        if (_ai != null) _ai.enabled = false;   // we own the airframe for the run
    }

    float GroundY(Vector3 p) =>
        _terrain != null ? _terrainBaseY + _terrain.SampleHeight(p) : 0f;

    void FixedUpdate()
    {
        if (_phase == Phase.Done || _model == null) return;
        if (_health != null && _health.IsDead) { _phase = Phase.Done; return; }
        if (_airfield == null) { HandBack(); return; }

        var pos = transform.position;
        Vector3 aim;

        if (_phase == Phase.Ingress)
        {
            // Field lost while inbound -> no point pressing; rejoin the fight.
            if (_airfield.IsLost) { HandBack(); return; }

            var tgt = _airfield.TargetPoint;
            aim = tgt + Vector3.up * RunInAltitude;

            var flat = new Vector3(tgt.x - pos.x, 0f, tgt.z - pos.z);
            if (flat.magnitude <= Mathf.Max(DeliverRadius, _airfield.TargetRadius))
            {
                // Delivered.
                _airfield.ApplyAttackDamage(DamagePerPass);
                if (_shooter != null)
                {
                    _shooter.UseAimDirection = true;
                    _shooter.AimDirection = (tgt - pos).normalized;
                    _shooter.Trigger = true;        // brief muzzle/tracer toward the strip
                }
                _phase = Phase.Egress;
                _egressUntil = Time.fixedTime + EgressDuration;
            }
        }
        else // Egress
        {
            if (_shooter != null) _shooter.Trigger = false;
            // Climb out straight ahead, away from the field.
            var fwd = transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            aim = pos + fwd.normalized * 1000f + Vector3.up * (EgressAltitude - RunInAltitude);
            if (Time.fixedTime >= _egressUntil) { HandBack(); return; }
        }

        // Never aim into the steppe on the low leg.
        float floor = GroundY(aim) + MinClearance;
        if (aim.y < floor) aim.y = floor;

        Steer(aim);
        _model.Boost = _phase == Phase.Egress; // firewall it on the way out
    }

    void Steer(Vector3 aimPoint)
    {
        var to = aimPoint - transform.position;
        if (to.sqrMagnitude < 1e-4f) return;
        var dirLocal = transform.InverseTransformDirection(to.normalized);

        float pitchSign = _model.InvertPitch ? +1f : -1f;
        float tp = Mathf.Clamp(dirLocal.y * PitchGain * pitchSign, -1f, 1f);
        float tr = Mathf.Clamp(dirLocal.x * RollGain, -1f, 1f);
        float ty = Mathf.Clamp(dirLocal.x * YawGain, -1f, 1f);

        float a = ReactionTime > 0f
            ? 1f - Mathf.Exp(-Time.fixedDeltaTime / ReactionTime)
            : 1f;
        _smoothPitch = Mathf.Lerp(_smoothPitch, tp, a);
        _smoothRoll = Mathf.Lerp(_smoothRoll, tr, a);
        _smoothYaw = Mathf.Lerp(_smoothYaw, ty, a);

        _model.PitchInput = _smoothPitch;
        _model.RollInput = _smoothRoll;
        _model.YawInput = _smoothYaw;
    }

    // Return the airframe to the shared dogfight AI and stop touching it.
    void HandBack()
    {
        _phase = Phase.Done;
        if (_model != null)
        {
            _model.PitchInput = 0f;
            _model.RollInput = 0f;
            _model.YawInput = 0f;
            _model.Boost = false;
        }
        if (_shooter != null) { _shooter.Trigger = false; _shooter.UseAimDirection = false; }
        if (_ai != null) _ai.enabled = true;
        enabled = false;
    }

    void OnDrawGizmosSelected()
    {
        if (_airfield == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
        Gizmos.DrawLine(transform.position, _airfield.TargetPoint);
    }
}
