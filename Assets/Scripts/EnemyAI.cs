using UnityEngine;

// Default fighter: shared base behavior plus the enemy's player-target bias —
// it prefers allies and only commits to the human when the player is the lone
// or vastly-closer hostile (overridden while retaliating).
public class EnemyAI : PlaneAIController
{
    protected override float ScoreCandidate(
        PlaneHealth ph, float distSqFromSelf, float playerScoreMul)
        => ph.Faction == PlaneFaction.Player
            ? distSqFromSelf * playerScoreMul
            : distSqFromSelf;

    // Mission-1 ingress: with a defended airfield in the scene, a fighter with
    // no target flies toward it instead of loitering at its spawn, so the whole
    // wave (not just the designated strikers) pushes the objective until a
    // hostile comes into range and the FSM commits to the chase. No airfield
    // (other missions) -> normal random patrol.
    protected override Vector3 PatrolPoint()
    {
        var af = Airfield.Instance;
        return af != null && !af.IsDestroyed
            ? af.transform.position
            : base.PatrolPoint();
    }
}
