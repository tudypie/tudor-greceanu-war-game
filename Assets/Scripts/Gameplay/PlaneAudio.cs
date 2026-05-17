using UnityEngine;

// Spatial (3D) plane SFX on every plane prefab: a reused looping minigun
// source plus a looping crash alarm, in its own child AudioSources.
[RequireComponent(typeof(PlaneHealth))]
public class PlaneAudio : MonoBehaviour
{
    public PlaneAudioStats Stats;

    PlaneShooter _shooter;
    PlaneHealth _health;
    PlaneCrash _crash;

    AudioSource _minigun;
    AudioSource _alarm;
    float _minigunUntil;

    void Awake()
    {
        _health = GetComponent<PlaneHealth>();
        _shooter = GetComponent<PlaneShooter>();
        _crash = GetComponent<PlaneCrash>();
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneAudio)} on {name} has no Stats assigned.", this);
            return;
        }

        if (_shooter != null && Stats.MinigunLoop != null)
        {
            _minigun = CreateSpatialSource("MinigunAudio", Stats.MinigunLoop,
                Stats.MinigunVolume, Stats.MinigunSpatial, loop: true);
            _shooter.Shot += OnShot;
        }

        if (Stats.CrashAlarm != null)
        {
            _alarm = CreateSpatialSource("CrashAlarmAudio", Stats.CrashAlarm,
                Stats.CrashAlarmVolume, Stats.CrashAlarmSpatial, loop: true);
            _health.Died += OnDied;
        }

        if (_crash != null) _crash.Exploded += OnExploded;
    }

    void OnDestroy()
    {
        if (_shooter != null) _shooter.Shot -= OnShot;
        if (_health != null) _health.Died -= OnDied;
        if (_crash != null) _crash.Exploded -= OnExploded;
    }

    void OnShot()
    {
        _minigunUntil = Time.time + Mathf.Max(Stats.MinigunReleaseTail, 0f);
        if (_minigun != null && !_minigun.isPlaying)
        {
            var j = Stats.MinigunPitchJitter;
            _minigun.pitch = 1f + Random.Range(-j, j);
            _minigun.Play();
        }
    }

    void Update()
    {
        if (_minigun != null && _minigun.isPlaying && Time.time >= _minigunUntil)
            _minigun.Stop();
    }

    void OnDied()
    {
        if (_alarm != null && !_alarm.isPlaying) _alarm.Play();
    }

    // Airframe is destroyed this frame, so play the explosion on a
    // short-lived detached source that cleans itself up.
    void OnExploded()
    {
        if (Stats.Explosion == null || Stats.Explosion.Length == 0) return;
        var clip = Stats.Explosion[Random.Range(0, Stats.Explosion.Length)];
        if (clip == null) return;
        SpawnDetachedSpatialOneShot(clip, transform.position, Stats.ExplosionVolume,
            Stats.ExplosionSpatial);
    }

    static void SpawnDetachedSpatialOneShot(AudioClip clip, Vector3 pos, float volume,
        SpatialAudioSettings spatial)
    {
        var go = new GameObject("ExplosionAudio");
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.loop = false;
        src.playOnAwake = false;
        ApplySpatial(src, spatial);
        src.Play();
        Destroy(go, clip.length + 0.25f);
    }

    AudioSource CreateSpatialSource(string childName, AudioClip clip, float volume,
        SpatialAudioSettings spatial, bool loop)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.loop = loop;
        src.playOnAwake = false;
        ApplySpatial(src, spatial);
        return src;
    }

    static void ApplySpatial(AudioSource src, SpatialAudioSettings spatial)
    {
        src.spatialBlend = 1f; // fully 3D
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = spatial.MinDistance;
        src.maxDistance = spatial.MaxDistance;
        src.dopplerLevel = spatial.DopplerLevel;
    }
}
