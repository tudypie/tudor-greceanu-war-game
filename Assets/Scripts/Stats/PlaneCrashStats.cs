using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Crash Stats", fileName = "PlaneCrashStats")]
public class PlaneCrashStats : ScriptableObject
{
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
}
