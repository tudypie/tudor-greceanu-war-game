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
    public bool DestroyOnGroundImpact = true;
    public LayerMask GroundMask = ~0;
    [Tooltip("Backstop independent of physics layers: a downed plane explodes " +
             "once its pivot is within this many metres of the terrain surface " +
             "(Terrain.SampleHeight), even if the collider tunnels/grazes and " +
             "OnCollisionEnter never fires. <= 0 disables the backstop.")]
    public float TerrainImpactHeight = 3f;

    [Header("Collision")]
    public bool ExplodeOnCollision;
    public GameObject ExplosionPrefab;
}
