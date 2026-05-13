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

    public Behaviour[] DisableOnCrash;
    public ParticleSystem Smoke;

    [Header("Dive")]
    public float DiveAlignTime = 0.4f;
    public float RollSpeed = 360f;

    [Header("Drag")]
    public float LinearDamping = 0.2f;
    public float AngularDamping = 5f;

    [Header("Cleanup")]
    public float DestroyDelay = 8f;
    public bool DestroyOnGroundImpact = true;
    public LayerMask GroundMask = ~0;

    [Header("Collision")]
    public bool ExplodeOnCollision;
    public GameObject ExplosionPrefab;

    void Awake()
    {
        _health = GetComponent<PlaneHealth>();
        _rigidbody = GetComponent<Rigidbody>();
        _health.DestroyOnDeath = false;
        _health.Died += Crash;
    }

    void OnDestroy()
    {
        if (_health != null) _health.Died -= Crash;
    }

    void Crash()
    {
        if (_crashed) return;
        _crashed = true;

        if (DisableOnCrash != null)
        {
            for (var i = 0; i < DisableOnCrash.Length; i++)
            {
                if (DisableOnCrash[i] != null) DisableOnCrash[i].enabled = false;
            }
        }

        _rigidbody.useGravity = true;
        _rigidbody.linearDamping = LinearDamping;
        _rigidbody.angularDamping = AngularDamping;
        _rigidbody.angularVelocity = Vector3.zero;

        var flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        _diveRotation = Quaternion.LookRotation(Vector3.down, flatForward.normalized);

        if (Smoke != null)
        {
            Smoke.gameObject.SetActive(true);
            Smoke.Play();
        }

        Destroy(gameObject, DestroyDelay);
    }

    void FixedUpdate()
    {
        if (!_crashed) return;
        var alpha = DiveAlignTime > 0f
            ? 1f - Mathf.Exp(-Time.fixedDeltaTime / DiveAlignTime)
            : 1f;
        _rollAngle += RollSpeed * Time.fixedDeltaTime;
        var target = _diveRotation * Quaternion.AngleAxis(_rollAngle, Vector3.forward);
        _rigidbody.MoveRotation(Quaternion.Slerp(_rigidbody.rotation, target, alpha));
        _rigidbody.angularVelocity = Vector3.zero;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!_crashed)
        {
            if (ExplodeOnCollision) Explode();
            return;
        }
        if (!DestroyOnGroundImpact) return;
        if ((GroundMask.value & (1 << collision.gameObject.layer)) == 0) return;
        Explode();
    }

    void Explode()
    {
        if (ExplosionPrefab != null)
        {
            Instantiate(ExplosionPrefab, transform.position, transform.rotation);
        }
        Destroy(gameObject);
    }
}
