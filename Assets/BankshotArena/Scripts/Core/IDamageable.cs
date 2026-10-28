using UnityEngine;

/// <summary>
/// Everything a hit needs to know. Passing a struct (instead of 4 loose parameters)
/// lets us add fields later (e.g. damage type) without touching every implementer.
/// </summary>
public struct DamageInfo
{
    public float amount;
    public Vector3 point;       // where the hit landed
    public Vector3 direction;   // direction the shot was TRAVELLING (attacker sits at -direction)
    public int bounces;         // how many ricochets the bullet made before landing (0 = direct hit)

    public DamageInfo(float amount, Vector3 point, Vector3 direction, int bounces = 0)
    {
        this.amount = amount;
        this.point = point;
        this.direction = direction.normalized;
        this.bounces = bounces;
    }
}

/// <summary>Anything that can be shot: player, enemies, destructibles.</summary>
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}
