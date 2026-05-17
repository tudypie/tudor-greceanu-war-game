using System;
using UnityEngine;

[RequireComponent(typeof(PlaneHealth))]
[RequireComponent(typeof(Rigidbody))]
public class PlaneCrash : MonoBehaviour
{
    // Raised once, the instant the airframe explodes (collision or ground
    // impact), before the GameObject is destroyed. Audio reads transform
    // here and spawns a DETACHED one-shot, since this object dies same frame.
    public event Action Exploded;

    PlaneHealth _health;
    Rigidbody _rigidbody;
    PlaneFlightModel _model;
    bool _crashed;
    bool _exploded;
    Quaternion _diveRotation;
    float _rollAngle;

    // Terrain backstop (no physics layers involved, mirroring
    // PlaneAIController / PlaneSpawner) so a downed plane is guaranteed to
    // explode at the surface even when the collider misses it.
    Terrain _terrain;
    float _terrainBaseY;

    public PlaneCrashStats Stats;
    public Behaviour[] DisableOnCrash;
    public ParticleSystem Smoke;

    void Awake()
    {
        _health = GetComponent<PlaneHealth>();
        _rigidbody = GetComponent<Rigidbody>();
        _model = GetComponent<PlaneFlightModel>();
        _health.DestroyOnDeath = false;
        _health.Died += Crash;
        _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
        _terrainBaseY = _terrain != null ? _terrain.transform.position.y : 0f;
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneCrash)} on {name} has no Stats assigned.", this);
        }
    }

    void OnDestroy()
    {
        if (_health != null) _health.Died -= Crash;
    }

    void Crash()
    {
        if (_crashed) return;
        _crashed = true;
        if (Stats == null) return;

        // Cut all control + flight authority. PlaneFlightModel is the one that
        // hard-assigns rigidbody.linearVelocity every physics step, so while it
        // (or the player/AI feeding it input) is alive the plane keeps flying
        // and stays steerable instead of going down. Done programmatically so
        // it can't be defeated by a missing DisableOnCrash inspector entry;
        // the array is still honoured for any extra designer-specified scripts.
        DisableComponent<PlaneFlightModel>();
        DisableComponent<PlanePlayerInput>();
        DisableComponent<PlaneAIController>();
        DisableComponent<PlaneShooter>();
        if (DisableOnCrash != null)
        {
            for (var i = 0; i < DisableOnCrash.Length; i++)
            {
                if (DisableOnCrash[i] != null) DisableOnCrash[i].enabled = false;
            }
        }

        _rigidbody.useGravity = true;
        _rigidbody.linearDamping = Stats.LinearDamping;
        _rigidbody.angularDamping = Stats.AngularDamping;
        _rigidbody.angularVelocity = Vector3.zero;

        // Straight down: drop the forward airspeed PlaneFlightModel was holding
        // (and any upward coast if it was shot mid-climb) so it goes vertically
        // instead of arcing downrange, then kick it to DiveSpeed so it comes
        // down hard rather than waiting for gravity to build up from zero.
        var v = _rigidbody.linearVelocity;
        v.x = 0f;
        v.z = 0f;
        v.y = Mathf.Min(v.y, -Stats.DiveSpeed);
        _rigidbody.linearVelocity = v;

        var flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        _diveRotation = Quaternion.LookRotation(Vector3.down, flatForward.normalized);

        if (Smoke != null)
        {
            Smoke.gameObject.SetActive(true);
            Smoke.Play();
        }

        // No timed destroy here: a downed plane must keep diving until it
        // actually reaches the ground. Destruction (+ explosion FX/SFX) only
        // happens on ground impact, in OnCollisionEnter -> Explode().
    }

    // Disable the component of type T on this plane if it has one. Behaviour
    // (MonoBehaviour) so .enabled actually stops its Update/FixedUpdate.
    void DisableComponent<T>() where T : Behaviour
    {
        var c = GetComponent<T>();
        if (c != null) c.enabled = false;
    }

    void FixedUpdate()
    {
        if (!_crashed || Stats == null) return;
        var alpha = Stats.DiveAlignTime > 0f
            ? 1f - Mathf.Exp(-Time.fixedDeltaTime / Stats.DiveAlignTime)
            : 1f;
        _rollAngle += Stats.RollSpeed * Time.fixedDeltaTime;
        var target = _diveRotation * Quaternion.AngleAxis(_rollAngle, Vector3.forward);
        _rigidbody.MoveRotation(Quaternion.Slerp(_rigidbody.rotation, target, alpha));
        _rigidbody.angularVelocity = Vector3.zero;

        // Terrain backstop: OnCollisionEnter can miss a fast dive (tunnelling
        // or grazing the TerrainCollider) or be filtered out by GroundMask,
        // leaving the plane diving/sliding forever with smoke on. Sampling the
        // heightfield directly is layer-independent and always catches it.
        if (Stats.DestroyOnGroundImpact && Stats.TerrainImpactHeight > 0f && _terrain != null)
        {
            var pos = transform.position;
            var groundY = _terrainBaseY + _terrain.SampleHeight(pos);
            if (pos.y <= groundY + Stats.TerrainImpactHeight) Explode();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (Stats == null) return;
        if (!_crashed)
        {
            // A live plane parked/taxiing on the strip rests its collider on
            // the TerrainCollider by design — that contact must not be read as
            // a crash. (ExplodeOnCollision also defaults off, but don't rely
            // on the SO flag.)
            if (_model != null && _model.IsGrounded) return;
            if (Stats.ExplodeOnCollision) Explode();
            return;
        }
        if (!Stats.DestroyOnGroundImpact) return;
        if ((Stats.GroundMask.value & (1 << collision.gameObject.layer)) == 0) return;
        Explode();
    }

    void Explode()
    {
        // A second collision the same frame can re-enter before the deferred
        // Destroy resolves; guard so the explosion (FX + SFX) fires once.
        if (_exploded) return;
        _exploded = true;
        Exploded?.Invoke();
        if (Stats != null && Stats.ExplosionRadius > 0f)
        {
            Fireball.Spawn(transform.position, Stats.ExplosionRadius, Stats.ExplosionLife);
        }
        DamageAirfieldBlast();
        Debug.Log($"{gameObject.name} exploded");
        Destroy(gameObject);
    }

    // Mission-1: an airframe going down on the field hurts the objective.
    // Inert wherever there is no Airfield (null Instance), like AirfieldStrikeRun.
    void DamageAirfieldBlast()
    {
        if (Stats == null || Stats.AirfieldBlastDamage <= 0f) return;
        var airfield = Airfield.Instance;
        if (airfield == null || airfield.IsDestroyed) return;

        var radius = Stats.AirfieldBlastRadius;
        if (radius <= 0f) return;
        var dist = Vector3.Distance(transform.position, airfield.transform.position);
        if (dist >= radius) return;

        var damage = Stats.AirfieldBlastDamage * (1f - dist / radius);
        airfield.ApplyDamage(damage);
    }
}
