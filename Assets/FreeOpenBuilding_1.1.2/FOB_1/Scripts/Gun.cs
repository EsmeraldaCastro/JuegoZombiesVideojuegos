using UnityEngine;

public class Gun : MonoBehaviour
{
    public float damage = 20f;
    public float fireRate = 0.3f;
    public Camera playerCamera;
    public ParticleSystem muzzleFlash; // opcional
    public AudioSource shotSound;      // opcional

    [Header("Alcance y daño por distancia")]
    public float range = 1000f;              // distancia máxima a la que puede pegar
    public float fullDamageRange = 150f;     // hasta aquí hace el daño completo
    [Range(0f, 1f)] public float minDamageMultiplier = 0.4f; // daño al límite del alcance (0.4 = 40%)

    [Header("Depuración")]
    public bool debugHits = true;      // escribe en la Consola qué golpea cada disparo

    float nextFire;

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        if (Input.GetMouseButton(0) && Time.time >= nextFire)
        {
            nextFire = Time.time + fireRate;
            Shoot();
        }
    }

    // 1 de cerca, baja poco a poco hasta minDamageMultiplier al final del alcance
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

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
        {
            float finalDamage = damage * DistanceMultiplier(hit.distance);

            // Primero busca la parte exacta que se golpeó (cuerpo o cabeza)
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

            // Si el zombie no tiene hitboxes, daño normal
            Zombie z = hit.collider.GetComponentInParent<Zombie>();
            if (z != null) z.TakeDamage(finalDamage);
        }
        else if (debugHits)
        {
            Debug.Log("Disparo no golpeó nada (alcance corto o apuntando al cielo)");
        }
    }
}