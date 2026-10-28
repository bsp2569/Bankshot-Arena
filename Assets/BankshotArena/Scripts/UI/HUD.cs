using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Health, Aegis Plate (points + timer), charge meter, ammo, wave, score,
/// a hitmarker, ricochet popups and a directional damage indicator.
/// Bars are scaled RectTransforms (pivot on the left), so they need no sprite.
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("Sources")]
    public PlayerHealth player;
    public TemporaryArmor armor;
    public RicochetWeapon weapon;
    public WaveSpawner spawner;

    [Header("Bars (pivot x = 0)")]
    public RectTransform healthFill;
    public RectTransform armorFill;
    public RectTransform armorTimeFill;
    public RectTransform chargeFill;
    public GameObject armorGroup;

    [Header("Text")]
    public Text ammoText;
    public Text waveText;
    public Text scoreText;
    public Text armorText;
    public Text popupText;
    public Text centerText;

    [Header("Feedback")]
    public Image hitmarker;
    public RectTransform damageIndicatorPivot;
    public Image damageIndicator;
    public Color damageColor = new Color(1f, 0.25f, 0.2f, 1f);
    public Color blockedColor = new Color(0.3f, 0.8f, 1f, 1f);

    float hitmarkerUntil;
    float popupUntil;
    float damageIndicatorUntil;
    Vector3 lastHitDirection;

    void OnEnable()
    {
        if (player != null) player.OnDamaged += HandleDamaged;
        if (weapon != null) weapon.OnHitConfirmed += HandleHit;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnKillScored += HandleKill;
            GameManager.Instance.OnGameOver += HandleGameOver;
        }
    }

    void Start()
    {
        // GameManager might Awake after us; subscribe here if OnEnable missed it.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnKillScored -= HandleKill;
            GameManager.Instance.OnGameOver -= HandleGameOver;
            GameManager.Instance.OnKillScored += HandleKill;
            GameManager.Instance.OnGameOver += HandleGameOver;
        }
        SetAlpha(hitmarker, 0f);
        SetAlpha(damageIndicator, 0f);
        if (popupText != null) popupText.text = "";
        if (centerText != null) centerText.text = "";
    }

    void OnDisable()
    {
        if (player != null) player.OnDamaged -= HandleDamaged;
        if (weapon != null) weapon.OnHitConfirmed -= HandleHit;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnKillScored -= HandleKill;
            GameManager.Instance.OnGameOver -= HandleGameOver;
        }
    }

    void Update()
    {
        if (player != null) SetBar(healthFill, player.Health01);

        if (armor != null)
        {
            bool active = armor.IsActive;
            if (armorGroup != null && armorGroup.activeSelf != active) armorGroup.SetActive(active);
            SetBar(armorFill, armor.Points01);
            SetBar(armorTimeFill, armor.Time01);
            SetBar(chargeFill, armor.Charge01);
            if (armorText != null)
                armorText.text = active ? $"AEGIS  {armor.TimeRemaining:0.0}s" : "";
        }

        if (weapon != null && ammoText != null)
            ammoText.text = weapon.IsReloading ? "RELOADING" : $"{weapon.Ammo} / {weapon.magazineSize}";

        if (spawner != null && waveText != null)
        {
            waveText.text = spawner.NextWaveCountdown > 0f
                ? $"WAVE {spawner.CurrentWave + 1} IN {Mathf.CeilToInt(spawner.NextWaveCountdown)}"
                : $"WAVE {spawner.CurrentWave}   ENEMIES {spawner.Alive}";
        }

        if (GameManager.Instance != null && scoreText != null)
            scoreText.text = $"SCORE {GameManager.Instance.Score}";

        // Fades
        SetAlpha(hitmarker, Mathf.Clamp01((hitmarkerUntil - Time.time) / 0.15f));
        if (popupText != null && Time.time > popupUntil) popupText.text = "";

        if (damageIndicator != null && damageIndicatorPivot != null)
        {
            float a = Mathf.Clamp01((damageIndicatorUntil - Time.time) / 1f);
            SetAlpha(damageIndicator, a);
            if (a > 0f && player != null)
            {
                // Arrow points TOWARD the attacker, which is opposite the shot's travel direction.
                float yaw = GameMath.SignedYawAngle(player.transform, -lastHitDirection);
                // UI rotates counter-clockwise for +z, but +yaw means "to the right", so negate.
                damageIndicatorPivot.localEulerAngles = new Vector3(0f, 0f, -yaw);
            }
        }
    }

    void HandleDamaged(Vector3 hitDirection, bool blocked)
    {
        lastHitDirection = hitDirection;
        damageIndicatorUntil = Time.time + 1f;
        if (damageIndicator != null) damageIndicator.color = blocked ? blockedColor : damageColor;
    }

    void HandleHit(int bounces, float damage)
    {
        hitmarkerUntil = Time.time + 0.15f;
        if (hitmarker != null)
            hitmarker.color = bounces > 0 ? blockedColor : Color.white;
    }

    void HandleKill(int points, int bounces)
    {
        if (popupText == null) return;
        popupText.text = bounces > 0 ? $"RICOCHET x{bounces + 1}!  +{points}" : $"+{points}";
        popupUntil = Time.time + 1.2f;
    }

    void HandleGameOver()
    {
        if (centerText == null || GameManager.Instance == null) return;
        var gm = GameManager.Instance;
        int wave = spawner != null ? spawner.CurrentWave : 0;
        centerText.text = $"YOU DIED\n\nWave {wave}   Score {gm.Score}\n" +
                          $"Kills {gm.Kills}   Ricochet kills {gm.RicochetKills}   Best bank shot {gm.BestBounceKill} bounces\n\n" +
                          "Press R to restart";
    }

    static void SetBar(RectTransform bar, float value01)
    {
        if (bar == null) return;
        bar.localScale = new Vector3(Mathf.Clamp01(value01), 1f, 1f);
    }

    static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        Color c = g.color;
        c.a = a;
        g.color = c;
    }
}
