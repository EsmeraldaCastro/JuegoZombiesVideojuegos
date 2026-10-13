using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Sirve para teclado y mouse (useVR = false) y para VR (useVR = true).
public class Gun : MonoBehaviour
{
    public float damage = 20f;
    public float fireRate = 0.3f;

    [Header("Modo")]
    public bool useVR = false;

    [Header("Teclado y mouse")]
    public Camera playerCamera;

    [Header("VR")]
    public Transform muzzle;            // punta del cañón (su flecha azul apunta hacia donde sale el disparo)
    public LineRenderer laser;          // opcional: línea que muestra hacia dónde apuntas
    public float laserWidth = 0.3f;
    public float laserLength = 400f;
    public GameObject promptText;       // el texto "Agarra la pistola..." (se oculta al empezar)
    public float returnDelay = 1.5f;    // si la sueltas, regresa a su lugar después de este tiempo

    [Header("Efectos (opcional)")]
    public ParticleSystem muzzleFlash;
    public AudioSource shotSound;

    [Header("Alcance y daño por distancia")]
    public float range = 1000f;
    public float fullDamageRange = 150f;
    [Range(0f, 1f)] public float minDamageMultiplier = 0.4f;

    [Header("Depuración")]
    public bool debugHits = true;

    [Header("Capas del disparo")]
    public LayerMask shotMask = ~0;    // desmarca aquí la capa "Arma" para que el disparo no choque con la propia pistola

    XRGrabInteractable grab;
    bool triggerHeld;
    bool started;
    float nextFire;
    Vector3 startPos;
    Quaternion startRot;

    void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        if (useVR)
        {
            grab = GetComponent<XRGrabInteractable>();
            if (laser != null)
            {
                laser.useWorldSpace = true;
                laser.positionCount = 2;
                laser.widthMultiplier = laserWidth;
                laser.enabled = false;
            }
        }
    }

    void Start()
    {
        if (useVR) IgnoreBodyCollisions();
    }

    // El Character Controller del XR Origin no debe chocar con la pistola.
    // Sin esto, el collider de la pistola en tu mano empuja o sostiene al personaje (flotar en escaleras).
    // No afecta al agarre: las manos detectan la pistola igual.
    void IgnoreBodyCollisions()
    {
        CharacterController body = FindFirstObjectByType<CharacterController>();
        if (body == null)
        {
            Debug.LogWarning("Gun: no encontré el Character Controller del XR Origin.");
            return;
        }

        foreach (Collider c in GetComponentsInChildren<Collider>(true))
            if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                Physics.IgnoreCollision(body, c, true);
    }

    void OnEnable()
    {
        if (grab == null) return;
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
        grab.activated.AddListener(OnTriggerDown);
        grab.deactivated.AddListener(OnTriggerUp);
    }

    void OnDisable()
    {
        if (grab == null) return;
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
        grab.activated.RemoveListener(OnTriggerDown);
        grab.deactivated.RemoveListener(OnTriggerUp);
    }

    // --- Eventos VR ---
    void OnGrabbed(SelectEnterEventArgs args)
    {
        CancelInvoke(nameof(ReturnToStart));
        if (!started)
        {
            started = true;
            if (promptText != null) promptText.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.StartGame();
        }
    }

    void OnReleased(SelectExitEventArgs args)
    {
        triggerHeld = false;
        Invoke(nameof(ReturnToStart), returnDelay);
    }

    void OnTriggerDown(ActivateEventArgs args) { triggerHeld = true; }
    void OnTriggerUp(DeactivateEventArgs args) { triggerHeld = false; }

    void ReturnToStart()
    {
        if (grab != null && grab.isSelected) return;
        transform.SetPositionAndRotation(startPos, startRot);
    }

    // --- Disparo ---
    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            // Al morir, suelta la pistola: una mano que sostiene un objeto no puede usar el UI
            if (useVR && grab != null && grab.enabled) grab.enabled = false;
            if (laser != null) laser.enabled = false;
            return;
        }

        if (Time.timeScale <= 0f) return;   // en pausa no se dispara

        if (useVR)
        {
            UpdateLaser();
            if (started && triggerHeld && Time.time >= nextFire)
            {
                nextFire = Time.time + fireRate;
                Shoot();
            }
        }
        else if (Input.GetMouseButton(0) && Time.time >= nextFire)
        {
            nextFire = Time.time + fireRate;
            Shoot();
        }
    }

    void UpdateLaser()
    {
        if (laser == null || muzzle == null) return;

        bool held = grab != null && grab.isSelected;
        laser.enabled = held;
        if (!held) return;

        Vector3 end = muzzle.position + muzzle.forward * laserLength;
        if (Physics.Raycast(muzzle.position, muzzle.forward, out RaycastHit h, laserLength, shotMask, QueryTriggerInteraction.Ignore))
            end = h.point;

        laser.SetPosition(0, muzzle.position);
        laser.SetPosition(1, end);
    }

    float DistanceMultiplier(float distance)
    {
        if (distance <= fullDamageRange) return 1f;
        float t = Mathf.InverseLerp(fullDamageRange, range, distance);
        return Mathf.Lerp(1f, minDamageMultiplier, t);
    }

    void Shoot()
    {
        if (muzzleFlash != null) muzzleFlash.Play();
        if (shotSound != null) shotSound.Play();

        Ray ray = useVR
            ? new Ray(muzzle.position, muzzle.forward)
            : playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, range, shotMask, QueryTriggerInteraction.Ignore))
        {
            float finalDamage = damage * DistanceMultiplier(hit.distance);

            ZombieHitbox hitbox = hit.collider.GetComponent<ZombieHitbox>();
            if (hitbox != null)
            {
                if (debugHits)
                    Debug.Log($"Disparo en {hit.collider.name} a {hit.distance:F0} unidades: {finalDamage * hitbox.damageMultiplier:F1} de daño");
                if (HitMarker.Instance != null) HitMarker.Instance.Show(hitbox.damageMultiplier > 1f);
                hitbox.Hit(finalDamage);
                return;
            }

            if (debugHits) Debug.Log($"Disparo golpeó: {hit.collider.name} a {hit.distance:F0} unidades");

            Zombie z = hit.collider.GetComponentInParent<Zombie>();
            if (z != null) z.TakeDamage(finalDamage);
        }
        else if (debugHits)
        {
            Debug.Log("Disparo no golpeó nada");
        }
    }

    // Ayuda visual en la Scene: flecha azul = hacia dónde dispara
    void OnDrawGizmosSelected()
    {
        if (muzzle == null) return;
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(muzzle.position, muzzle.forward * 50f);
    }
}