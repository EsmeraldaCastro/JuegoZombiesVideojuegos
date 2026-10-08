using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Zombie : MonoBehaviour
{
    [Header("Vida y ataque")]
    public float health = 50f;
    public float attackDamage = 10f;
    public float attackRange = 30f;     // distancia HORIZONTAL para golpear
    public float verticalReach = 60f;   // diferencia de altura máxima para golpear (evita pegar a través del piso)
    public float attackCooldown = 1f;

    [Header("Movimiento")]
    public float moveSpeed = 35f;
    [Range(0f, 0.5f)] public float speedVariation = 0.15f; // cada zombie es un poco distinto, así no se amontonan
    public float acceleration = 200f;

    [Header("Efecto al recibir daño")]
    public Color hitColor = Color.red;
    public float hitFlashTime = 0.1f;

    NavMeshAgent agent;
    PlayerController deskPlayer;
    PlayerHealth vrPlayer;
    Renderer[] renderers;
    MaterialPropertyBlock block;
    Coroutine flashRoutine;
    float nextAttack;
    float nextRepath;
    float offNavMeshTime;
    bool dead;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        vrPlayer = FindFirstObjectByType<PlayerHealth>();
        if (vrPlayer == null) deskPlayer = FindFirstObjectByType<PlayerController>();
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();

        agent.speed = moveSpeed * Random.Range(1f - speedVariation, 1f + speedVariation);
        agent.acceleration = acceleration;
        agent.angularSpeed = 360f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(30, 70); // prioridades distintas para que se esquiven entre ellos

        AlignToFloor();
    }

    Vector3 TargetPosition => vrPlayer != null ? vrPlayer.GroundPosition : deskPlayer.transform.position;

    // Ajusta la altura para que el punto más bajo del zombie toque el piso (arregla zombies hundidos o flotando)
    void AlignToFloor()
    {
        Collider[] cols = GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return;

        Bounds b = cols[0].bounds;
        foreach (Collider c in cols) b.Encapsulate(c.bounds);
        agent.baseOffset = transform.position.y - b.min.y;
    }

    void Update()
    {
        if (dead || (vrPlayer == null && deskPlayer == null)) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        // Si el zombie quedó fuera del NavMesh (bug), se elimina a los 2 segundos
        if (!agent.isOnNavMesh)
        {
            offNavMeshTime += Time.deltaTime;
            if (offNavMeshTime > 2f) Remove();
            return;
        }
        offNavMeshTime = 0f;

        // Actualiza el destino 5 veces por segundo, ajustado al NavMesh
        if (Time.time >= nextRepath)
        {
            nextRepath = Time.time + 0.2f;
            if (NavMesh.SamplePosition(TargetPosition, out NavMeshHit hit, 80f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        // Distancia horizontal (el jugador tiene su centro a media altura, no a la del piso)
        Vector3 a = transform.position;
        Vector3 p = TargetPosition;
        float heightDiff = Mathf.Abs(p.y - a.y);
        a.y = 0f;
        p.y = 0f;
        float dist = Vector3.Distance(a, p);

        if (dist <= attackRange && heightDiff <= verticalReach && Time.time >= nextAttack)
        {
            nextAttack = Time.time + attackCooldown;
            if (vrPlayer != null) vrPlayer.TakeDamage(attackDamage);
            else deskPlayer.TakeDamage(attackDamage);
        }
    }

    public void TakeDamage(float amount)
    {
        if (dead) return;
        health -= amount;
        if (health <= 0f)
        {
            Die();
            return;
        }

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlash());
    }

    IEnumerator HitFlash()
    {
        block.SetColor("_BaseColor", hitColor); // URP
        block.SetColor("_Color", hitColor);     // shader estándar
        foreach (Renderer r in renderers) r.SetPropertyBlock(block);
        yield return new WaitForSeconds(hitFlashTime);
        foreach (Renderer r in renderers) r.SetPropertyBlock(null);
    }

    void Die()
    {
        dead = true;
        if (GameManager.Instance != null) GameManager.Instance.OnZombieKilled();
        Destroy(gameObject);
    }

    void Remove()
    {
        dead = true;
        if (GameManager.Instance != null) GameManager.Instance.OnZombieRemoved();
        Destroy(gameObject);
    }
}