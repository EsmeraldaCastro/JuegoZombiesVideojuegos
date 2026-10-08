using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 5f;
    public float runMultiplier = 1.8f; // al mantener Shift, la velocidad se multiplica por esto
    public float gravity = -20f;
    public float mouseSensitivity = 2f;
    public Transform cameraTransform;

    [Header("Vida")]
    public float maxHealth = 100f;
    public float regenDelay = 10f;   // segundos sin daño para empezar a regenerar
    public float regenPerSecond = 10f;

    [Header("UI de vida")]
    public Slider healthBar;
    public Image healthFill;             // el objeto "Fill" del Slider (cambia de verde a rojo)
    public TextMeshProUGUI healthText;   // "Vida: 100" (opcional)
    public Image damageFlash;            // imagen roja a pantalla completa (opcional)
    public float flashIntensity = 0.5f;  // qué tan roja se pone la pantalla al recibir daño
    public float flashFadeSpeed = 2f;

    float health;
    float lastDamageTime;
    float xRotation;
    float yVelocity;
    CharacterController cc;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        health = maxHealth;
        lastDamageTime = Time.time;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        UpdateHealthUI();
    }

    void Update()
    {
        FadeFlash();

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        Look();
        Move();
        Regenerate();
    }

    void Look()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        xRotation = Mathf.Clamp(xRotation - my, -85f, 85f);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mx);
    }

    void Move()
    {
        Vector3 dir = transform.right * Input.GetAxis("Horizontal") + transform.forward * Input.GetAxis("Vertical");
        if (cc.isGrounded && yVelocity < 0) yVelocity = -2f;
        yVelocity += gravity * Time.deltaTime;

        bool running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float currentSpeed = running ? speed * runMultiplier : speed;
        Vector3 move = dir * currentSpeed + Vector3.up * yVelocity;
        cc.Move(move * Time.deltaTime);
    }

    void Regenerate()
    {
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
        lastDamageTime = Time.time; // reinicia el conteo de 10 s
        UpdateHealthUI();

        // Destello rojo: más fuerte mientras menos vida te queda
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