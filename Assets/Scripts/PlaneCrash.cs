using UnityEngine;

[RequireComponent(typeof(PlaneHealth))]
[RequireComponent(typeof(Rigidbody))]
public class PlaneCrash : MonoBehaviour
{
    PlaneHealth _health;
    Rigidbody _rigidbody;
    bool _crashed;
    Quaternion _diveRotation;
    float _rollAngle;

    public PlaneCrashStats Stats;
    public Behaviour[] DisableOnCrash;
    public ParticleSystem Smoke;

    void Awake()
    {
        _health = GetComponent<PlaneHealth>();
        _rigidbody = GetComponent<Rigidbody>();
        _health.DestroyOnDeath = false;
        _health.Died += Crash;
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

        var flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        _diveRotation = Quaternion.LookRotation(Vector3.down, flatForward.normalized);

        if (Smoke != null)
        {
            Smoke.gameObject.SetActive(true);
            Smoke.Play();
        }

        Destroy(gameObject, Stats.DestroyDelay);
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
    }

    void OnCollisionEnter(Collision collision)
    {
        if (Stats == null) return;
        if (!_crashed)
        {
            if (Stats.ExplodeOnCollision) Explode();
            return;
        }
        if (!Stats.DestroyOnGroundImpact) return;
        if ((Stats.GroundMask.value & (1 << collision.gameObject.layer)) == 0) return;
        Explode();
    }

    void Explode()
    {
        if (Stats != null && Stats.ExplosionPrefab != null)
        {
            Instantiate(Stats.ExplosionPrefab, transform.position, transform.rotation);
        }
        Debug.Log($"{gameObject.name} exploded");
        Destroy(gameObject);
    }
}
