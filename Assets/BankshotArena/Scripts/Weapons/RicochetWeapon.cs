using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hitscan rifle whose bullets RICOCHET off walls.
/// Each bounce adds damage (+damagePerBounce) and multiplies the kill score,
/// so bank shots around cover are the high-skill play.
/// </summary>
public class RicochetWeapon : MonoBehaviour
{
    [Header("References")]
    public Camera aimCamera;
    public Transform muzzle;
    public TracerPool tracers;
    public TemporaryArmor armor; // optional — supplies the Absorb & Return multiplier

    [Header("Damage")]
    public float baseDamage = 20f;
    [Tooltip("+50% per bounce at 0.5")]
    public float damagePerBounce = 0.5f;
    public int maxBounces = 3;
    public float maxDistance = 150f;
    [Tooltip("What bullets can hit. The player sits on the 'Ignore Raycast' layer, so the default excludes them.")]
    public LayerMask hitMask = Physics.DefaultRaycastLayers;

    [Header("Handling")]
    public float fireRate = 5f;        // shots per second
    public float spreadAngle = 0.6f;   // degrees (half-angle of the cone)
    public int magazineSize = 12;
    public float reloadTime = 1.3f;

    public int Ammo { get; private set; }
    public bool IsReloading => reloadEndTime > Time.time;
    public float Reload01 => IsReloading ? 1f - (reloadEndTime - Time.time) / reloadTime : 1f;

    /// <summary>(bounces, damage) — fired when a bullet hits something damageable.</summary>
    public event Action<int, float> OnHitConfirmed;

    float nextFireTime;
    float reloadEndTime;
    readonly List<Vector3> pathPoints = new List<Vector3>(8); // reused every shot: zero garbage

    void Awake()
    {
        Ammo = magazineSize;
        if (aimCamera == null) aimCamera = Camera.main;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.IsGameOver) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (IsReloading) return;

        if (Ammo <= 0 || (Input.GetKeyDown(KeyCode.R) && Ammo < magazineSize))
        {
            StartReload();
            return;
        }

        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + 1f / fireRate;
            Fire();
        }
    }

    void StartReload()
    {
        reloadEndTime = Time.time + reloadTime;
        Invoke(nameof(FinishReload), reloadTime);
    }

    void FinishReload() => Ammo = magazineSize;

    void Fire()
    {
        Ammo--;
        float chargeMultiplier = armor != null ? armor.ConsumeChargeMultiplier() : 1f;

        Transform cam = aimCamera.transform;
        Vector3 origin = cam.position;
        Vector3 direction = GameMath.RandomDirectionInCone(cam.forward, spreadAngle);
        float remaining = maxDistance;
        int bounces = 0;

        pathPoints.Clear();
        pathPoints.Add(muzzle != null ? muzzle.position : origin);

        while (true)
        {
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, remaining, hitMask, QueryTriggerInteraction.Ignore))
            {
                pathPoints.Add(origin + direction * remaining); // missed everything
                break;
            }

            pathPoints.Add(hit.point);

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                float damage = baseDamage * (1f + damagePerBounce * bounces) * chargeMultiplier;
                target.TakeDamage(new DamageInfo(damage, hit.point, direction, bounces));
                OnHitConfirmed?.Invoke(bounces, damage);
                break;
            }

            if (bounces >= maxBounces) break;

            remaining -= hit.distance;
            if (remaining <= 0f) break;

            // Ricochet: mirror the direction around the surface normal.
            direction = GameMath.Reflect(direction, hit.normal);
            // Nudge off the surface so the next ray doesn't immediately re-hit the same wall.
            origin = hit.point + hit.normal * 0.01f;
            bounces++;
        }

        if (tracers != null) tracers.Show(pathPoints, bounces, chargeMultiplier > 1.01f);
    }
}
