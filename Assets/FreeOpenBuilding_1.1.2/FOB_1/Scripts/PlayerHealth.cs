using UnityEngine;
using UnityEngine.UI;
using TMPro;

// VR: va en el objeto raíz "XR Origin (XR Rig)".
// Maneja la vida del jugador. Los zombies y el GameManager lo usan igual que al PlayerController.
public class PlayerHealth : MonoBehaviour
{
    [Header("Referencias")]
    public Transform head;               // la Main Camera dentro del XR Origin

    [Header("Vida")]
    public float maxHealth = 100f;
    public float regenDelay = 10f;       // segundos sin daño para empezar a regenerar
    public float regenPerSecond = 10f;

    [Header("UI de vida")]
    public Slider healthBar;
    public Image healthFill;             // el objeto "Fill" del Slider
    public TextMeshProUGUI healthText;   // "Vida: 100"
    public Image damageFlash;            // imagen roja (canvas pegado a la cámara)
    public float flashIntensity = 0.5f;
    public float flashFadeSpeed = 2f;

    float health;
    float lastDamageTime;

    // Punto en el piso justo debajo de tu cabeza: hacia ahí caminan los zombies
    public Vector3 GroundPosition
    {
        get
        {
            Vector3 p = transform.position;
            if (head != null)
            {
                p.x = head.position.x;
                p.z = head.position.z;
            }
            return p;
        }
    }

    void Start()
    {
        health = maxHealth;
        lastDamageTime = Time.time;
        UpdateHealthUI();
    }

    void Update()
    {
        FadeFlash();

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        if (health < maxHealth && Time.time - lastDamageTime >= regenDelay)
        {
            health = Mathf.Min(maxHealth, health + regenPerSecond * Time.deltaTime);
            UpdateHealthUI();
        }
    }

    public void TakeDamage(float amount)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        health -= amount;
        lastDamageTime = Time.time;
        UpdateHealthUI();

        if (damageFlash != null)
        {
            Color c = damageFlash.color;
            c.a = Mathf.Clamp01(flashIntensity + (1f - health / maxHealth) * 0.3f);
            damageFlash.color = c;
        }

        if (health <= 0f)
        {
            health = 0f;
            UpdateHealthUI();
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
        }
    }

    void FadeFlash()
    {
        if (damageFlash == null) return;
        Color c = damageFlash.color;
        c.a = Mathf.MoveTowards(c.a, 0f, flashFadeSpeed * Time.unscaledDeltaTime);
        damageFlash.color = c;
    }

    void UpdateHealthUI()
    {
        float ratio = health / maxHealth;
        if (healthBar != null) healthBar.value = ratio;
        if (healthFill != null) healthFill.color = Color.Lerp(Color.red, Color.green, ratio);
        if (healthText != null) healthText.text = $"Vida: {Mathf.CeilToInt(health)}";
    }
}