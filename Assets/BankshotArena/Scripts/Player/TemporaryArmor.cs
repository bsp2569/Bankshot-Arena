using System;
using UnityEngine;

/// <summary>
/// THE TWIST — "Aegis Plate":
///  1. Temporary: lasts <c>duration</c> seconds OR until its points run out, whichever first.
///  2. Directional: only blocks hits coming from the front arc (dot-product cone test).
///     Get flanked and it does nothing.
///  3. Absorb &amp; Return: every point of damage it soaks fills a Charge meter.
///     Your next shot spends the charge for up to (1 + maxChargeBonus)x damage.
/// So picking up armor makes you want to turn INTO the fire, tank it, then punish.
/// </summary>
public class TemporaryArmor : MonoBehaviour
{
    [Header("Coverage")]
    [Tooltip("Total frontal arc the plate protects, in degrees. 140 = 70 each side of where you face.")]
    [Range(30f, 360f)] public float coverageAngle = 140f;

    [Header("Absorb & Return")]
    [Tooltip("Damage that must be absorbed to fill the charge meter.")]
    public float damageForFullCharge = 60f;
    [Tooltip("Extra damage multiplier at full charge. 1.5 => next shot does 2.5x.")]
    public float maxChargeBonus = 1.5f;
    [Tooltip("Charge slowly bleeds away so you can't bank it forever.")]
    public float chargeDecayPerSecond = 0.05f;

    [Header("Feedback")]
    public GameObject shieldVisual;
    [Tooltip("Seconds before expiry when the visual starts flickering.")]
    public float expiryWarningTime = 2f;

    float duration;
    float timeRemaining;
    float maxPoints;
    float points;
    float charge; // 0..1

    public bool IsActive => timeRemaining > 0f && points > 0f;
    public float TimeRemaining => Mathf.Max(0f, timeRemaining);
    public float Time01 => duration > 0f ? Mathf.Clamp01(timeRemaining / duration) : 0f;
    public float Points01 => maxPoints > 0f ? Mathf.Clamp01(points / maxPoints) : 0f;
    public float Charge01 => charge;
    public float CurrentChargeMultiplier => 1f + charge * maxChargeBonus;

    public event Action OnActivated;
    public event Action OnExpired;

    void Awake()
    {
        if (shieldVisual != null) shieldVisual.SetActive(false);
    }

    /// <summary>Called by ArmorPickup. Picking up while active refreshes it.</summary>
    public void Activate(float durationSeconds, float capacity)
    {
        duration = timeRemaining = durationSeconds;
        maxPoints = points = capacity;
        if (shieldVisual != null) shieldVisual.SetActive(true);
        OnActivated?.Invoke();
    }

    void Update()
    {
        if (charge > 0f) charge = Mathf.Max(0f, charge - chargeDecayPerSecond * Time.deltaTime);

        if (!IsActive) return;

        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
        {
            Expire();
            return;
        }

        // Flicker in the last seconds so the player knows it is about to drop.
        if (shieldVisual != null)
        {
            bool visible = timeRemaining > expiryWarningTime || Mathf.PingPong(Time.time * 8f, 1f) > 0.5f;
            if (shieldVisual.activeSelf != visible) shieldVisual.SetActive(visible);
        }
    }

    /// <summary>
    /// Try to soak a hit. Returns the damage that gets THROUGH to health.
    /// </summary>
    public float Absorb(DamageInfo info, out bool blocked)
    {
        blocked = false;
        if (!IsActive) return info.amount;

        // Direction from me toward whoever shot me is the opposite of the bullet's travel direction.
        Vector3 toAttacker = -info.direction;
        if (!GameMath.DirectionInCone(transform.forward, toAttacker, coverageAngle * 0.5f, flatten: true))
            return info.amount; // hit from the side/back: plate doesn't cover it

        float absorbed = Mathf.Min(info.amount, points);
        points -= absorbed;
        charge = Mathf.Clamp01(charge + absorbed / Mathf.Max(1f, damageForFullCharge));
        blocked = absorbed > 0f;

        if (points <= 0f) Expire();
        return info.amount - absorbed;
    }

    /// <summary>Weapon calls this once per shot. Returns the multiplier and empties the meter.</summary>
    public float ConsumeChargeMultiplier()
    {
        float multiplier = CurrentChargeMultiplier;
        charge = 0f;
        return multiplier;
    }

    void Expire()
    {
        timeRemaining = 0f;
        points = 0f;
        if (shieldVisual != null) shieldVisual.SetActive(false);
        OnExpired?.Invoke();
    }
}
