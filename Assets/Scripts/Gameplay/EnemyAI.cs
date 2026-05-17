using UnityEngine;

// Default fighter: prefers allies, committing to the player only when it is
// the lone or vastly-closer hostile.
public class EnemyAI : PlaneAIController
{
    protected override float ScoreCandidate(
        PlaneHealth ph, float distSqFromSelf, float playerScoreMul)
        => ph.Faction == PlaneFaction.Player
            ? distSqFromSelf * playerScoreMul
            : distSqFromSelf;

    // Mission-1: an untargeted fighter pushes toward the airfield instead of
    // loitering at spawn. No airfield -> normal random patrol.
    protected override Vector3 PatrolPoint()
    {
        var af = Airfield.Instance;
        return af != null && !af.IsDestroyed
            ? af.transform.position
            : base.PatrolPoint();
    }
}
