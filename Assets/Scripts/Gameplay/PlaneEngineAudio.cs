using UnityEngine;

// Fades the looping engine AudioSource from a quiet idle up to its authored
// volume over RiseSeconds during the grounded takeoff, then locks it there.
// No-op for AI/air-start planes, which are never grounded.
[RequireComponent(typeof(PlaneFlightModel))]
public class PlaneEngineAudio : MonoBehaviour
{
    [Tooltip("Looping engine source on the plane root. Falls back to an " +
        "AudioSource on this GameObject if left empty.")]
    [SerializeField] AudioSource EngineSource;

    [Tooltip("Engine volume (as a fraction of its authored default) at the " +
        "moment the plane spawns parked. It rises from here to the full " +
        "default over Rise Seconds.")]
    [Range(0f, 1f)] public float IdleVolumeFraction = 0.1f;

    [Tooltip("Seconds to fade from the idle volume up to the authored " +
        "default. Liftoff locks it at the default regardless.")]
    public float RiseSeconds = 6f;

    PlaneFlightModel _model;
    float _defaultVolume;
    float _elapsed;
    bool _decided;

    void Awake()
    {
        _model = GetComponent<PlaneFlightModel>();
        if (EngineSource == null) EngineSource = GetComponent<AudioSource>();
        if (EngineSource == null)
        {
            Debug.LogError($"{nameof(PlaneEngineAudio)} on {name} has no engine " +
                "AudioSource.", this);
            enabled = false;
            return;
        }

        // Capture the authored volume before any Start() can touch it; it's
        // the target the fade rises to.
        _defaultVolume = EngineSource.volume;
    }

    void Update()
    {
        // Decide here, not in Start(): PlaneFlightModel arms IsGrounded in its
        // own Start() and Start() order between components is undefined.
        if (!_decided)
        {
            _decided = true;
            // Never grounded (AI/air-start): hold the default and bow out.
            if (_model == null || !_model.IsGrounded)
            {
                EngineSource.volume = _defaultVolume;
                enabled = false;
                return;
            }
        }

        // Steady rise from idle to default while taking off.
        if (_model.IsGrounded)
        {
            _elapsed += Time.deltaTime;
            var k = RiseSeconds > 0f ? Mathf.Clamp01(_elapsed / RiseSeconds) : 1f;
            EngineSource.volume = Mathf.Lerp(
                _defaultVolume * IdleVolumeFraction, _defaultVolume, k);
            return;
        }

        // Liftoff: grounded model hands off one-way, so settle and stop.
        EngineSource.volume = _defaultVolume;
        enabled = false;
    }
}
