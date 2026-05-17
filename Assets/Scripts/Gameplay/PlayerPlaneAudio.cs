using UnityEngine;

// Non-spatial (2D) cockpit/HUD audio cues. Player plane only.
[RequireComponent(typeof(PlaneHealth))]
public class PlayerPlaneAudio : MonoBehaviour
{
    public PlaneAudioStats Stats;

    PlaneHealth _health;
    PlaneShooter _shooter;
    PlaneLockOn _lockOn;
    AudioSource _2d;

    float _lastHitTime = -999f;
    float _nextRadioBeep;

    void Awake()
    {
        _health = GetComponent<PlaneHealth>();
        _shooter = GetComponent<PlaneShooter>();
        _lockOn = GetComponent<PlaneLockOn>();
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlayerPlaneAudio)} on {name} has no Stats assigned.", this);
            return;
        }

        var go = new GameObject("PlayerFeedbackAudio");
        go.transform.SetParent(transform, false);
        _2d = go.AddComponent<AudioSource>();
        _2d.playOnAwake = false;
        _2d.spatialBlend = 0f; // pure 2D: no attenuation, no panning
        _2d.dopplerLevel = 0f;

        if (_shooter != null)
        {
            _shooter.Killed += OnKill;
            _shooter.OverheatStarted += OnOverheat;
        }
        if (_lockOn != null) _lockOn.LockAcquired += OnLockAcquired;
        _health.Damaged += OnDamaged;

        ScheduleNextRadioBeep();
    }

    void OnDestroy()
    {
        if (_shooter != null)
        {
            _shooter.Killed -= OnKill;
            _shooter.OverheatStarted -= OnOverheat;
        }
        if (_lockOn != null) _lockOn.LockAcquired -= OnLockAcquired;
        if (_health != null) _health.Damaged -= OnDamaged;
    }

    void Update()
    {
        if (Stats == null || _2d == null || _health.IsDead) return;
        if (Stats.RadioBeeps == null || Stats.RadioBeeps.Length == 0) return;
        if (Time.time >= _nextRadioBeep)
        {
            PlayRandom(Stats.RadioBeeps, Stats.RadioBeepVolume);
            ScheduleNextRadioBeep();
        }
    }

    void OnKill() => PlayRandom(Stats.EnemyKill, Stats.EnemyKillVolume);

    void OnOverheat() => PlayRandom(Stats.Overheat, Stats.OverheatVolume);

    void OnLockAcquired() => PlayRandom(Stats.LockOn, Stats.LockOnVolume);

    void OnDamaged(float amount)
    {
        if (Time.time - _lastHitTime < Stats.GotHitCooldown) return;
        _lastHitTime = Time.time;
        PlayRandom(Stats.GotHit, Stats.GotHitVolume);
    }

    void PlayRandom(AudioClip[] clips, float volume)
    {
        if (_2d == null || clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) _2d.PlayOneShot(clip, volume);
    }

    void ScheduleNextRadioBeep()
    {
        var min = Mathf.Min(Stats.RadioBeepIntervalMin, Stats.RadioBeepIntervalMax);
        var max = Mathf.Max(Stats.RadioBeepIntervalMin, Stats.RadioBeepIntervalMax);
        _nextRadioBeep = Time.time + Random.Range(min, max);
    }
}
