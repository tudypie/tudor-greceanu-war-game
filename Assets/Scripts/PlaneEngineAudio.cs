using UnityEngine;

// Gradually fades the looping engine AudioSource (the one already on the
// plane root, play-on-awake) up from a quiet idle to its authored volume
// over the takeoff: it starts low while parked and rises steadily over
// RiseSeconds, reaching the full default by liftoff, then locked there for
// the rest of the flight.
//
// Player-only by nature: it only does anything while the flight model is in
// the grounded takeoff/taxi model. AI and air-start planes are never
// grounded, so this locks the engine at its default volume and disables
// itself on the first frame — a true no-op for everything but the player.
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

        // Capture the authored volume here, before any Start() (ours or the
        // flight model's) or other script can touch it — it's the target the
        // fade rises to.
        _defaultVolume = EngineSource.volume;
    }

    void Update()
    {
        // The grounded/air-start decision must wait for the first Update:
        // PlaneFlightModel arms _grounded in its Start(), and Start() order
        // between components is undefined, but every Start() is guaranteed
        // done by the first Update of the frame — so IsGrounded is reliable
        // here and not in our Start().
        if (!_decided)
        {
            _decided = true;
            // Air-start / AI: never grounded, so there is no takeoff to fade
            // up. Hold the authored volume and step out of the way for good.
            if (_model == null || !_model.IsGrounded)
            {
                EngineSource.volume = _defaultVolume;
                enabled = false;
                return;
            }
        }

        // Steady time-based rise from the idle volume to the default while the
        // plane is on the ground taking off.
        if (_model.IsGrounded)
        {
            _elapsed += Time.deltaTime;
            var k = RiseSeconds > 0f ? Mathf.Clamp01(_elapsed / RiseSeconds) : 1f;
            EngineSource.volume = Mathf.Lerp(
                _defaultVolume * IdleVolumeFraction, _defaultVolume, k);
            return;
        }

        // Liftoff: the grounded model hands off one-way and never returns, so
        // settle at the default and stop running.
        EngineSource.volume = _defaultVolume;
        enabled = false;
    }
}
