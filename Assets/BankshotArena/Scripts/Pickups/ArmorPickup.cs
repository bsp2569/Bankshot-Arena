using UnityEngine;

/// <summary>
/// Floating armor crystal. Walk through it to get the temporary Aegis Plate.
/// Respawns after a delay, so the map has "armor control" points worth fighting over.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ArmorPickup : MonoBehaviour
{
    [Header("Armor granted")]
    public float armorDuration = 8f;
    public float armorCapacity = 80f;

    [Header("Pickup")]
    public float respawnTime = 15f;
    public Transform visual;

    [Header("Animation")]
    public float bobHeight = 0.25f;
    public float bobSpeed = 2f;
    public float spinSpeed = 90f;

    bool available = true;
    float respawnAt;
    Collider trigger;

    public bool IsAvailable => available;

    void Awake()
    {
        trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    void Update()
    {
        if (!available && Time.time >= respawnAt) SetAvailable(true);

        if (available && visual != null)
        {
            // Sine wave bob + constant spin: the classic "pick me up" motion.
            visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
            visual.localRotation = Quaternion.Euler(0f, Time.time * spinSpeed, 45f);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!available) return;

        var armor = other.GetComponentInParent<TemporaryArmor>();
        if (armor == null) return;

        armor.Activate(armorDuration, armorCapacity);
        SetAvailable(false);
        respawnAt = Time.time + respawnTime;
    }

    void SetAvailable(bool value)
    {
        available = value;
        trigger.enabled = value;
        if (visual != null) visual.gameObject.SetActive(value);
    }
}
