using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Crash Stats", fileName = "PlaneCrashStats")]
public class PlaneCrashStats : ScriptableObject
{
    [Header("Dive")]
    public float DiveAlignTime = 0.4f;
    public float RollSpeed = 360f;
    [Tooltip("Straight-down speed the airframe is kicked to the instant it " +
             "goes down (gravity then keeps adding). Higher = it hits the " +
             "ground sooner.")]
    public float DiveSpeed = 80f;

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

    [Header("Explosion")]
    [Tooltip("Procedural fireball size on impact (world units). Much smaller " +
             "than FunMode's nukes — this is a single airframe, not a warhead. " +
             "<= 0 spawns no fireball.")]
    public float ExplosionRadius = 16f;
    [Tooltip("Fireball lifetime in seconds.")]
    public float ExplosionLife = 0.7f;

    [Header("Airfield Blast")]
    [Tooltip("Mission-1 only: HP a crash deals to the Airfield objective when " +
             "the airframe explodes within AirfieldBlastRadius of it, falling " +
             "off linearly to zero at the edge. <= 0 disables it (and it is " +
             "inert in scenes with no Airfield).")]
    public float AirfieldBlastDamage = 800f;
    [Tooltip("Max distance from the Airfield centre for a crash to damage it.")]
    public float AirfieldBlastRadius = 120f;
}
