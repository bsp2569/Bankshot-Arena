using System;
using UnityEngine;

/// <summary>Player health. Every hit is offered to the armor first.</summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;
    [Tooltip("Seconds without taking damage before health starts regenerating.")]
    public float regenDelay = 5f;
    public float regenPerSecond = 6f;

    public TemporaryArmor armor;

    float health;
    float lastDamageTime = -999f;

    public float Health => health;
    public float Health01 => health / maxHealth;
    public bool IsDead { get; private set; }

    /// <summary>(direction the shot travelled, fully blocked by armor?) — HUD draws the damage arrow.</summary>
    public event Action<Vector3, bool> OnDamaged;

    void Awake()
    {
        health = maxHealth;
        if (armor == null) armor = GetComponent<TemporaryArmor>();
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead) return;

        bool blocked = false;
        float through = armor != null ? armor.Absorb(info, out blocked) : info.amount;

        OnDamaged?.Invoke(info.direction, through <= 0f);
        if (through <= 0f) return;

        health -= through;
        lastDamageTime = Time.time;

        if (health <= 0f)
        {
            health = 0f;
            IsDead = true;
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
        }
    }

    void Update()
    {
        if (IsDead || health >= maxHealth) return;
        if (Time.time - lastDamageTime >= regenDelay)
            health = Mathf.Min(maxHealth, health + regenPerSecond * Time.deltaTime);
    }
}
