using System;
using UnityEngine;

public enum PlaneFaction
{
    Player,
    Ally,
    Enemy,
}

public class PlaneHealth : MonoBehaviour
{
    public PlaneHealthStats Stats;
    public bool DestroyOnDeath = true;
    public PlaneFaction Faction = PlaneFaction.Enemy;

    float _health;
    bool _dead;

    public event Action Died;
    public event Action<float> Damaged;
    // Like Damaged, but carries the attacker (null if anonymous); the AI
    // subscribes to this to retaliate.
    public event Action<float, PlaneHealth> DamagedBy;

    public float Health => _health;
    public float MaxHealth => Stats != null ? Stats.MaxHealth : 0f;
    public float HealthNormalized => Mathf.Clamp01(_health / Mathf.Max(MaxHealth, 0.0001f));
    public bool IsDead => _dead;

    public static bool AreHostile(PlaneFaction a, PlaneFaction b)
    {
        if (a == b) return false;
        return a == PlaneFaction.Enemy || b == PlaneFaction.Enemy;
    }

    public bool IsHostileTo(PlaneHealth other)
    {
        return other != null && AreHostile(Faction, other.Faction);
    }

    void Awake()
    {
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneHealth)} on {name} has no Stats assigned.", this);
            return;
        }
        _health = Stats.MaxHealth;
    }

    public void TakeDamage(float amount) => TakeDamage(amount, null);

    public void TakeDamage(float amount, PlaneHealth attacker)
    {
        if (_dead || amount <= 0f) return;
        _health -= amount;
        Damaged?.Invoke(amount);
        DamagedBy?.Invoke(amount, attacker);
        if (_health <= 0f)
        {
            _health = 0f;
            Die();
        }
    }

    void Die()
    {
        _dead = true;
        Died?.Invoke();
        Debug.Log($"{gameObject.name} died");
        if (DestroyOnDeath) Destroy(gameObject);
    }
}
