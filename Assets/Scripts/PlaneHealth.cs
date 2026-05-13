using System;
using UnityEngine;

public class PlaneHealth : MonoBehaviour
{
    public float MaxHealth = 100f;
    public bool DestroyOnDeath = true;

    float _health;
    bool _dead;

    public event Action Died;
    public event Action<float> Damaged;

    public float Health => _health;
    public float HealthNormalized => Mathf.Clamp01(_health / Mathf.Max(MaxHealth, 0.0001f));
    public bool IsDead => _dead;

    void Awake()
    {
        _health = MaxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (_dead || amount <= 0f) return;
        _health -= amount;
        Damaged?.Invoke(amount);
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
        if (DestroyOnDeath) Destroy(gameObject);
    }
}
