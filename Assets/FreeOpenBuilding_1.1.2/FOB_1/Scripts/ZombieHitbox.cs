using UnityEngine;

// Va en cada parte del zombie que recibe disparos (cuerpo y cabeza).
// El multiplicador cambia cuánto daño recibe esa parte.
public class ZombieHitbox : MonoBehaviour
{
    public float damageMultiplier = 1f;   // cuerpo = 1, cabeza = 1.5
    Zombie zombie;

    void Awake()
    {
        zombie = GetComponentInParent<Zombie>();
    }

    public void Hit(float baseDamage)
    {
        if (zombie != null) zombie.TakeDamage(baseDamage * damageMultiplier);
    }
}
