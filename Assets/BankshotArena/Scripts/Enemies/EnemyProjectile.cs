using UnityEngine;

/// <summary>
/// Slow, visible enemy bolt (so the player can dodge it and enemy "lead" aiming matters).
/// Moves itself and does CONTINUOUS collision: it raycasts along the distance it will travel
/// this frame, so a fast bolt can't skip through a thin wall between two frames ("tunnelling").
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    public float lifetime = 4f;
    [Tooltip("Must include the player's 'Ignore Raycast' layer — the default (Everything) does.")]
    public LayerMask hitMask = ~0;

    Vector3 velocity;
    float damage;
    Transform owner;
    float dieAt;

    static readonly RaycastHit[] hitBuffer = new RaycastHit[8]; // shared, no per-frame allocation

    public void Launch(Vector3 velocity, float damage, Transform owner)
    {
        this.velocity = velocity;
        this.damage = damage;
        this.owner = owner;
        dieAt = Time.time + lifetime;
        if (velocity.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(velocity);
    }

    void Update()
    {
        if (Time.time >= dieAt) { Destroy(gameObject); return; }

        Vector3 step = velocity * Time.deltaTime;
        float distance = step.magnitude;
        if (distance <= 0f) return;

        Vector3 dir = step / distance;
        int count = Physics.RaycastNonAlloc(transform.position, dir, hitBuffer, distance, hitMask, QueryTriggerInteraction.Ignore);

        // Find the closest hit that isn't the enemy who fired us.
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (owner != null && hitBuffer[i].transform.IsChildOf(owner)) continue;
            if (hitBuffer[i].distance < bestDist)
            {
                bestDist = hitBuffer[i].distance;
                best = i;
            }
        }

        if (best >= 0)
        {
            RaycastHit hit = hitBuffer[best];
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && !(target is EnemyHealth)) // no friendly fire
                target.TakeDamage(new DamageInfo(damage, hit.point, dir));
            Destroy(gameObject);
            return;
        }

        transform.position += step;
    }
}
