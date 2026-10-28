using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Chase -> spot (vision cone + line of sight) -> hold distance, strafe and fire LEADING shots.
/// leadAccuracy blends between aiming where you are (0) and where you'll be (1);
/// the spawner raises it every wave, so standing still or running in a straight line gets punished.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Transform muzzle;
    public EnemyProjectile projectilePrefab;

    [Header("Senses")]
    public float viewDistance = 35f;
    [Tooltip("Total vision cone in degrees.")]
    public float fieldOfView = 110f;
    [Tooltip("How long the enemy stays aware after being shot.")]
    public float alertDuration = 3f;
    [Tooltip("Must include the player's 'Ignore Raycast' layer.")]
    public LayerMask sightMask = ~0;

    [Header("Combat")]
    public float attackRange = 22f;
    public float preferredDistance = 11f;
    public float fireInterval = 1.6f;
    public float projectileSpeed = 22f;
    public float projectileDamage = 12f;
    [Range(0f, 1f)] public float leadAccuracy = 0.3f;
    [Tooltip("Degrees per second when turning to face the player.")]
    public float turnSpeed = 300f;
    [Tooltip("Re-path at most this often. Calling SetDestination every frame for every enemy is wasted CPU.")]
    public float repathInterval = 0.2f;

    NavMeshAgent agent;
    Transform player;
    PlayerController playerController;
    float nextFireTime;
    float nextRepathTime;
    float alertedUntil;
    float strafeSign = 1f;
    float nextStrafeFlip;

    const float PlayerAimHeight = 1.2f; // player pivot is at the feet

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        var ph = FindFirstObjectByType<PlayerHealth>();
        if (ph != null)
        {
            player = ph.transform;
            playerController = ph.GetComponent<PlayerController>();
        }
        nextFireTime = Time.time + Random.Range(0.5f, fireInterval); // de-sync volleys
        strafeSign = Random.value < 0.5f ? -1f : 1f;
    }

    public void Alert() => alertedUntil = Time.time + alertDuration;

    void Update()
    {
        var gm = GameManager.Instance;
        if (player == null || (gm != null && gm.IsGameOver))
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        Vector3 targetPoint = player.position + Vector3.up * PlayerAimHeight;
        Vector3 toPlayer = player.position - transform.position;
        float distance = toPlayer.magnitude;
        bool canSee = CanSee(targetPoint, distance);

        if (canSee && distance <= attackRange)
        {
            // Combat: face player ourselves, let the agent only handle positioning.
            agent.updateRotation = false;
            FaceTowards(toPlayer);

            if (Time.time >= nextRepathTime)
            {
                nextRepathTime = Time.time + repathInterval;
                if (Time.time >= nextStrafeFlip)
                {
                    strafeSign = -strafeSign;
                    nextStrafeFlip = Time.time + Random.Range(1.5f, 3f);
                }
                // Hold preferred range, and slide sideways (cross product gives the perpendicular).
                Vector3 away = -toPlayer.normalized * preferredDistance;
                Vector3 side = Vector3.Cross(Vector3.up, toPlayer.normalized) * strafeSign * 3f;
                SetDestination(player.position + away + side);
            }

            bool facing = GameMath.DirectionInCone(transform.forward, toPlayer, 12f, flatten: true);
            if (facing && Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireInterval;
                Fire(targetPoint);
            }
        }
        else
        {
            agent.updateRotation = true;
            if (Time.time >= nextRepathTime)
            {
                nextRepathTime = Time.time + repathInterval;
                SetDestination(player.position);
            }
        }
    }

    bool CanSee(Vector3 targetPoint, float distance)
    {
        if (distance > viewDistance) return false;

        Vector3 eye = muzzle != null ? muzzle.position : transform.position + Vector3.up;
        bool alerted = Time.time < alertedUntil;
        if (!alerted && !GameMath.PointInCone(eye, transform.forward, targetPoint, fieldOfView * 0.5f))
            return false;

        // Line of sight: is the first thing between us the player?
        Vector3 dir = targetPoint - eye;
        if (Physics.Raycast(eye, dir.normalized, out RaycastHit hit, dir.magnitude + 0.5f, sightMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform)) return false; // shouldn't happen, eye is outside our collider
            return hit.collider.GetComponentInParent<PlayerHealth>() != null;
        }
        return false;
    }

    void FaceTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-4f) return;
        Quaternion target = Quaternion.LookRotation(direction);
        // RotateTowards caps degrees per frame -> smooth, frame-rate independent turning.
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    void Fire(Vector3 targetPoint)
    {
        if (projectilePrefab == null || muzzle == null) return;

        Vector3 origin = muzzle.position;
        Vector3 direct = (targetPoint - origin).normalized;
        Vector3 aim = direct;

        Vector3 playerVelocity = playerController != null ? playerController.Velocity : Vector3.zero;
        playerVelocity.y = 0f; // ignore jump/gravity jitter when predicting

        if (GameMath.TryGetInterceptDirection(origin, targetPoint, playerVelocity, projectileSpeed, out Vector3 lead))
            aim = Vector3.Slerp(direct, lead, leadAccuracy).normalized; // spherical blend between two directions

        EnemyProjectile bolt = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(aim));
        bolt.Launch(aim * projectileSpeed, projectileDamage, transform);
    }

    void SetDestination(Vector3 point)
    {
        if (!agent.isOnNavMesh) return;
        agent.isStopped = false;
        // Snap the requested point onto the navmesh so we never ask for an unreachable spot.
        if (NavMesh.SamplePosition(point, out NavMeshHit navHit, 4f, NavMesh.AllAreas))
            agent.SetDestination(navHit.position);
    }

#if UNITY_EDITOR
    // Draw the vision cone in the Scene view — great for debugging and for demo GIFs.
    void OnDrawGizmosSelected()
    {
        Vector3 eye = muzzle != null ? muzzle.position : transform.position + Vector3.up;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Quaternion left = Quaternion.AngleAxis(-fieldOfView * 0.5f, Vector3.up);
        Quaternion right = Quaternion.AngleAxis(fieldOfView * 0.5f, Vector3.up);
        Gizmos.DrawRay(eye, left * transform.forward * viewDistance);
        Gizmos.DrawRay(eye, right * transform.forward * viewDistance);
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}
