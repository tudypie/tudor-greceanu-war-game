using UnityEngine;

// Wingman doctrine on the shared base: when it has no enemy it holds a tight
// formation slot on the player; when the player is shot it (and up to
// MaxGuardResponders squadmates) drops everything and hunts the attacker via
// the base retaliation lock; its autonomous target picks favour whatever
// threatens the player. Everything else (flight, terrain safety, firing) is
// inherited unchanged.
public class AllyAI : PlaneAIController
{
    const int SlotCount = 6; // only a wrap guard; the spawner fills 3 (OneShot)

    static int s_slotCounter;
    static int s_responders;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { s_slotCounter = 0; s_responders = 0; }

    PlaneHealth _player;
    Transform _playerTf;
    Rigidbody _playerBody;
    bool _subscribed;
    int _slot;
    bool _countedResponder;

    protected override void Start()
    {
        base.Start();
        _slot = s_slotCounter++ % SlotCount;
        TryBindPlayer();
    }

    protected override void OnDestroy()
    {
        UnbindPlayer();
        base.OnDestroy();
    }

    void TryBindPlayer()
    {
        if (_player != null && !_player.IsDead) return;
        UnbindPlayer();

        foreach (var ph in FindObjectsByType<PlaneHealth>(FindObjectsSortMode.None))
        {
            if (ph == null || ph.IsDead || ph.Faction != PlaneFaction.Player) continue;
            _player = ph;
            _playerTf = ph.transform;
            _playerBody = ph.GetComponent<Rigidbody>();
            ph.DamagedBy += OnPlayerDamagedBy;
            ph.Died += OnPlayerDied;
            _subscribed = true;
            return;
        }
    }

    void UnbindPlayer()
    {
        if (_subscribed && _player != null)
        {
            _player.DamagedBy -= OnPlayerDamagedBy;
            _player.Died -= OnPlayerDied;
        }
        _subscribed = false;
        _player = null;
        _playerTf = null;
        _playerBody = null;
    }

    void OnPlayerDied() => UnbindPlayer();

    // Formation slot relative to the player: an alternating left/right echelon
    // that steps further back and up for each rank, so any squadron size
    // spreads cleanly.
    Vector3 SlotOffset()
    {
        var side = (_slot % 2 == 0) ? 1f : -1f;
        var rank = _slot / 2;
        return new Vector3(
            side * Stats.FormationSpread * (rank + 1),
            Stats.FormationStack * rank,
            -Stats.FormationBack * (rank + 1));
    }

    protected override Vector3 PatrolPoint()
    {
        if (_player == null || _player.IsDead) TryBindPlayer();
        if (Stats == null || _player == null || _player.IsDead || _playerTf == null)
            return base.PatrolPoint();

        var vel = _playerBody != null ? _playerBody.linearVelocity : Vector3.zero;
        var slot = _playerTf.position + _playerTf.rotation * SlotOffset()
                   + vel * Stats.FormationLeadTime;

        // Anti-weave: once settled near the slot, aim a touch ahead along the
        // player's heading instead of pinning the exact point, so the ally
        // flies parallel and stops oscillating around it.
        var settle = Mathf.Max(Stats.FormationSettleDistance, 0f);
        if ((_transform.position - slot).sqrMagnitude <= settle * settle)
            return slot + _playerTf.forward * Mathf.Max(settle, 1f);
        return slot;
    }

    void OnPlayerDamagedBy(float amount, PlaneHealth attacker)
    {
        if (Stats == null || !Stats.GuardPlayerWhenShot) return;
        if (attacker == null || attacker.IsDead) return;
        if (_health == null || !_health.IsHostileTo(attacker)) return;
        if (Stats.GuardResponseRange > 0f)
        {
            var dSq = (attacker.transform.position - _transform.position).sqrMagnitude;
            if (dSq > Stats.GuardResponseRange * Stats.GuardResponseRange) return;
        }
        // A new responder consumes a scene-wide slot; an ally already guarding
        // just re-points onto the latest attacker for free.
        if (!RetaliationActive && Stats.MaxGuardResponders > 0 &&
            s_responders >= Stats.MaxGuardResponders) return;

        BeginRetaliation(attacker);
    }

    // Keep the scene-wide responder count a pure function of observed state so
    // it can't leak on a missed transition or a domain reload.
    protected override void OnFixedUpdate()
    {
        var guarding = RetaliationActive;
        if (guarding && !_countedResponder)
        {
            s_responders++;
            _countedResponder = true;
        }
        else if (!guarding && _countedResponder)
        {
            s_responders = Mathf.Max(0, s_responders - 1);
            _countedResponder = false;
        }
    }

    // Autonomous picks defend the player's airspace: prefer the enemy closest
    // to the player (a tail threat on the ally itself is handled by base
    // retaliation). Falls back to nearest-to-self when there is no player.
    protected override float ScoreCandidate(
        PlaneHealth ph, float distSqFromSelf, float playerScoreMul)
    {
        if (_player == null || _player.IsDead || _playerTf == null)
            return base.ScoreCandidate(ph, distSqFromSelf, playerScoreMul);
        return (ph.transform.position - _playerTf.position).sqrMagnitude;
    }
}
