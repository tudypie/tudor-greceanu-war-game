using UnityEngine;

// Mission-1 only: makes a spawned enemy fly strafing runs at the Airfield
// instead of dogfighting. Added at spawn by WaveDirector to designated planes.
// While active it disables the plane's PlaneAIController (so the two don't
// fight for the flight inputs) and drives the flight model + guns itself. With
// no Airfield in the scene it is inert (self-disables), so it never affects
// other missions. The base AI's GCAS is gone with the AI, so this keeps its
// own simple ground clamp instead.
[RequireComponent(typeof(PlaneFlightModel))]
public class AirfieldStrikeRun : MonoBehaviour
{
    public bool UseBoost = true;
    public float FireRange = 320f;
    public float FireConeDeg = 12f;
    public float MinAltitudeAGL = 35f;
    public float PullOutDistance = 140f;
    public float PullOutSeconds = 3.5f;
    [Tooltip("0 = keep attacking until shot down.")]
    public int MaxRuns = 0;
    [Tooltip("When MaxRuns is reached, re-enable the dogfight AI instead of just stopping.")]
    public bool HandBackToDogfightAfterRuns = true;

    [Header("Steering")]
    public float PitchGain = 2.5f;
    public float RollGain = 3f;
    public float YawGain = 1.5f;
    public float ReactionTime = 0.25f;

    PlaneFlightModel _model;
    PlaneShooter _shooter;
    PlaneAIController _ai;
    Transform _tf;
    Terrain _terrain;
    float _terrainBaseY;

    Airfield _airfield;
    enum Phase { Ingress, PullOut }
    Phase _phase = Phase.Ingress;
    float _pullOutUntil;
    int _runs;
    float _smoothPitch, _smoothRoll, _smoothYaw;
    bool _disabledAI;

    void Start()
    {
        _tf = transform;
        _model = GetComponent<PlaneFlightModel>();
        _shooter = GetComponent<PlaneShooter>();
        _ai = GetComponent<PlaneAIController>();

        _airfield = Airfield.Instance;
        if (_airfield == null || _airfield.IsDestroyed)
        {
            enabled = false; // inert: leave the AI to dogfight
            return;
        }

        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
        _terrainBaseY = _terrain != null ? _terrain.transform.position.y : 0f;

        if (_ai != null) { _ai.enabled = false; _disabledAI = true; }
    }

    void OnDisable() => HandBack();
    void OnDestroy() => HandBack();

    void HandBack()
    {
        if (_disabledAI && _ai != null && HandBackToDogfightAfterRuns)
            _ai.enabled = true;
        _disabledAI = false;
    }

    void FixedUpdate()
    {
        if (_airfield == null) return;
        if (_airfield.IsDestroyed) { FinishRuns(); return; }

        var target = _airfield.transform.position;
        var toTarget = target - _tf.position;
        var dist = toTarget.magnitude;

        if (_phase == Phase.Ingress &&
            (dist < PullOutDistance || Vector3.Dot(_tf.forward, toTarget) < 0f))
        {
            _phase = Phase.PullOut;
            _pullOutUntil = Time.fixedTime + PullOutSeconds;
            _runs++;
        }

        Vector3 aimPoint;
        if (_phase == Phase.PullOut)
        {
            aimPoint = _tf.position + _tf.forward * 200f + Vector3.up * 160f;
            DriveGuns(false, default);
            if (Time.fixedTime >= _pullOutUntil)
            {
                if (MaxRuns > 0 && _runs >= MaxRuns && HandBackToDogfightAfterRuns)
                {
                    FinishRuns();
                    return;
                }
                _phase = Phase.Ingress;
            }
        }
        else
        {
            aimPoint = target;
        }

        // Own ground clamp (PlaneAIController's GCAS is off with the AI).
        var floorY = GroundY(aimPoint) + MinAltitudeAGL;
        if (aimPoint.y < floorY) aimPoint.y = floorY;
        if (_tf.position.y < GroundY(_tf.position) + MinAltitudeAGL)
            aimPoint = _tf.position + _tf.forward * 100f + Vector3.up * 120f;

        Steer(aimPoint);

        if (_phase == Phase.Ingress)
        {
            var dir = dist > 0.0001f ? toTarget / dist : _tf.forward;
            var aligned = Vector3.Dot(_tf.forward, dir) >
                          Mathf.Cos(FireConeDeg * Mathf.Deg2Rad);
            DriveGuns(dist < FireRange && aligned, dir);
        }
    }

    void Steer(Vector3 aimPoint)
    {
        var to = aimPoint - _tf.position;
        if (to.sqrMagnitude < 0.0001f) return;
        var local = _tf.InverseTransformDirection(to.normalized);

        var pitchSign = _model.InvertPitch ? +1f : -1f;
        var tPitch = Mathf.Clamp(local.y * PitchGain * pitchSign, -1f, 1f);
        var tRoll = Mathf.Clamp(local.x * RollGain, -1f, 1f);
        var tYaw = Mathf.Clamp(local.x * YawGain, -1f, 1f);

        var a = ReactionTime > 0f
            ? 1f - Mathf.Exp(-Time.fixedDeltaTime / ReactionTime)
            : 1f;
        _smoothPitch = Mathf.Lerp(_smoothPitch, tPitch, a);
        _smoothRoll = Mathf.Lerp(_smoothRoll, tRoll, a);
        _smoothYaw = Mathf.Lerp(_smoothYaw, tYaw, a);

        _model.PitchInput = _smoothPitch;
        _model.RollInput = _smoothRoll;
        _model.YawInput = _smoothYaw;
        _model.Boost = UseBoost;
    }

    void DriveGuns(bool fire, Vector3 dir)
    {
        if (_shooter == null) return;
        _shooter.UseAimDirection = fire;
        if (fire) _shooter.AimDirection = dir;
        _shooter.Trigger = fire;
    }

    void FinishRuns()
    {
        DriveGuns(false, default);
        HandBack();
        Destroy(this);
    }

    float GroundY(Vector3 p)
    {
        if (_terrain == null) return 0f;
        return _terrainBaseY + _terrain.SampleHeight(p);
    }
}
