using System;
using UnityEngine;

/// <summary>Enemy health with a hit flash. Kills report their bounce count for the score multiplier.</summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 60f;
    public int scoreValue = 100;
    public Color flashColor = Color.white;
    public float flashTime = 0.08f;

    float health;
    float flashUntil;
    bool dead;
    Renderer[] renderers;
    MaterialPropertyBlock block;
    EnemyAI ai;

    public event Action<EnemyHealth> OnDied;

    void Awake()
    {
        health = maxHealth;
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        ai = GetComponent<EnemyAI>();
    }

    public void TakeDamage(DamageInfo info)
    {
        if (dead) return;

        health -= info.amount;
        Flash(true);
        flashUntil = Time.time + flashTime;
        if (ai != null) ai.Alert(); // getting shot reveals where you are, even from behind

        if (health <= 0f)
        {
            dead = true;
            if (GameManager.Instance != null) GameManager.Instance.RegisterKill(scoreValue, info.bounces);
            OnDied?.Invoke(this);
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (flashUntil > 0f && Time.time >= flashUntil)
        {
            flashUntil = 0f;
            Flash(false);
        }
    }

    // MaterialPropertyBlock tints without creating a new material per enemy (keeps batching/memory sane).
    void Flash(bool on)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (on)
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", flashColor); // URP
                block.SetColor("_Color", flashColor);     // Built-in
                r.SetPropertyBlock(block);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }
}
