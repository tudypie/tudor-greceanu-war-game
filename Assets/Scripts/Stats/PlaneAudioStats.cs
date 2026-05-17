using UnityEngine;

// Per-sound 3D attenuation; each cue carries its own copy.
[System.Serializable]
public class SpatialAudioSettings
{
    [Tooltip("Distance at which the sound is still at full volume.")]
    public float MinDistance = 8f;
    [Tooltip("Distance beyond which the sound is fully attenuated.")]
    public float MaxDistance = 600f;
    [Range(0f, 5f)] public float DopplerLevel = 0.2f;

    public SpatialAudioSettings() { }

    public SpatialAudioSettings(float minDistance, float maxDistance, float dopplerLevel)
    {
        MinDistance = minDistance;
        MaxDistance = maxDistance;
        DopplerLevel = dopplerLevel;
    }
}

[CreateAssetMenu(menuName = "War Game/Plane Audio Stats", fileName = "PlaneAudioStats")]
public class PlaneAudioStats : ScriptableObject
{
    [Header("Minigun (looped while firing)")]
    public AudioClip MinigunLoop;
    [Range(0f, 1f)] public float MinigunVolume = 0.6f;
    public SpatialAudioSettings MinigunSpatial = new(10f, 500f, 0.15f);
    [Tooltip("Seconds the minigun loop keeps playing after the last round. " +
             "Smooths bursty/AI fire so a single reused looping source covers " +
             "every shot instead of one source per round.")]
    public float MinigunReleaseTail = 0.12f;
    [Tooltip("Random +/- pitch applied each time the loop (re)starts so " +
             "repeated bursts don't sound mechanically identical.")]
    [Range(0f, 0.3f)] public float MinigunPitchJitter = 0.05f;

    [Header("Crash Alarm (looped while going down)")]
    public AudioClip CrashAlarm;
    [Range(0f, 1f)] public float CrashAlarmVolume = 0.7f;
    public SpatialAudioSettings CrashAlarmSpatial = new(15f, 700f, 0.2f);

    [Header("Explosion (3D, spawned detached on impact)")]
    [Tooltip("One is picked at random when a plane explodes. Plays from a " +
             "detached source so it survives the airframe being destroyed.")]
    public AudioClip[] Explosion;
    [Range(0f, 1f)] public float ExplosionVolume = 0.9f;
    public SpatialAudioSettings ExplosionSpatial = new(25f, 1200f, 0f);

    [Header("Enemy Kill (2D, player feedback)")]
    [Tooltip("One is picked at random per kill. A single clip is fine.")]
    public AudioClip[] EnemyKill;
    [Range(0f, 1f)] public float EnemyKillVolume = 0.9f;

    [Header("Got Hit (2D, player feedback)")]
    [Tooltip("One is picked at random when the player's plane is damaged.")]
    public AudioClip[] GotHit;
    [Range(0f, 1f)] public float GotHitVolume = 0.8f;
    [Tooltip("Minimum seconds between got-hit cues so sustained fire doesn't " +
             "machine-gun the sample.")]
    public float GotHitCooldown = 0.25f;

    [Header("Weapon Overheat (2D, player feedback)")]
    [Tooltip("One is picked at random when the gun hits its heat limit.")]
    public AudioClip[] Overheat;
    [Range(0f, 1f)] public float OverheatVolume = 0.8f;

    [Header("Target Acquired / Lock-On (2D, player feedback)")]
    [Tooltip("One is picked at random when a lock completes.")]
    public AudioClip[] LockOn;
    [Range(0f, 1f)] public float LockOnVolume = 0.7f;

    [Header("Radio Beeps (2D, random ambient chatter)")]
    public AudioClip[] RadioBeeps;
    [Range(0f, 1f)] public float RadioBeepVolume = 0.5f;
    public float RadioBeepIntervalMin = 6f;
    public float RadioBeepIntervalMax = 18f;
}
