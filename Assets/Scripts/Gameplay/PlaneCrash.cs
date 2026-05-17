using System;
using UnityEngine;

[RequireComponent(typeof(PlaneHealth))]
[RequireComponent(typeof(Rigidbody))]
public class PlaneCrash : MonoBehaviour
{
    // Raised once, just before the GameObject is destroyed on explosion.
    public event Action Exploded;

    PlaneHealth _health;
    Rigidbody _rigidbody;
    PlaneFlightModel _model;
    bool _crashed;
    bool _exploded;
    Quaternion _diveRotation;
    float _rollAngle;

    // Terrain backstop so a downed plane explodes even if the collider misses.
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

        // Cut control + flight authority programmatically; PlaneFlightModel
        // hard-sets linearVelocity every step so the plane won't fall while
        // it's alive. DisableOnCrash still runs for extra designer scripts.
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

        // Zero horizontal + upward coast and kick to DiveSpeed so it drops
        // straight down hard instead of arcing downrange.
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

        // No timed destroy: keep diving until ground impact in OnCollisionEnter.
    }

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

        // Heightfield backstop in case OnCollisionEnter misses a fast dive.
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
            // A grounded plane rests on the TerrainCollider; not a crash.
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
        // Guard against a second same-frame collision before Destroy resolves.
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

    // Mission-1: a crash on the field damages the airfield; inert if none.
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
