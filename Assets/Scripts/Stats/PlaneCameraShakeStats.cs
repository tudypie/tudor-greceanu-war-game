using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Camera Shake Stats", fileName = "PlaneCameraShakeStats")]
public class PlaneCameraShakeStats : ScriptableObject
{
    [Tooltip("Peak angular shake in degrees at full intensity.")]
    public float Magnitude = 1.6f;

    [Tooltip("How long a shake takes to fully decay.")]
    public float Duration = 0.35f;

    [Tooltip("Damage amount that produces a full-magnitude shake. Smaller hits scale down linearly.")]
    public float DamageReference = 25f;

    [Tooltip("Noise sample rate — higher = more jittery, lower = more swaying.")]
    public float Frequency = 28f;
}
