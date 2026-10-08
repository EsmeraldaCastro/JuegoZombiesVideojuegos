using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Referencias")]
    public Gun gun;
    public Transform player;
    public GameObject zombiePrefab;

    [Header("Zona de aparición")]
    public Transform buildingRoot;            // arrastra aquí FOB_LOD (el edificio)
    public float edgeMargin = 5f;             // se aleja de los bordes del edificio
    public float minDistanceFromPlayer = 15f; // no aparecen encima del jugador

    [Header("Cuartos (opcional)")]
    // Si agregas SpawnRooms aquí, se usan en lugar del edificio completo
    public SpawnRoom[] rooms;
    public float navMeshSampleRadius = 10f;

    [Header("Dificultad (sube con kills y con tiempo)")]
    public float killsPerLevel = 10f;       // cada X kills sube 1 nivel
    public float secondsPerLevel = 30f;     // cada X segundos vivo sube 1 nivel
    public float startSpawnInterval = 3f;   // segundos entre zombies al inicio
    public float minSpawnInterval = 0.4f;   // el más rápido que puede llegar a aparecer
    [Range(0.5f, 1f)] public float intervalDecay = 0.88f; // cada nivel el intervalo se multiplica por esto
    public int startMaxAlive = 4;           // zombies simultáneos al inicio
    public float aliveGrowthPerLevel = 1.5f;// zombies extra por nivel
    public int maxAliveCap = 40;            // tope para que no se sature

    [Header("Mejoras de arma (opcional)")]
    public int startDamage = 20;
    public int[] upgradeKills = { 20, 60 };
    public int[] upgradeDamage = { 25, 35 };

    [Header("UI")]
    public TextMeshProUGUI killsText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI levelText;          // opcional: "Nivel 3"
    public TextMeshProUGUI damageUpgradeText;  // "Daño aumentado"
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverStatsText;

    public bool GameStarted { get; private set; }
    public bool IsGameOver { get; private set; }
    public int Kills => kills;

    int kills;
    int alive;
    int nextUpgrade;
    float elapsed;
    float spawnTimer;

    // Triángulos del NavMesh dentro del edificio (para elegir puntos al azar)
    readonly List<Vector3[]> triangles = new List<Vector3[]>();
    readonly List<float> cumulativeAreas = new List<float>();
    float totalArea;
    Bounds spawnBounds;
    bool hasSpawnArea;
    NavMeshPath pathCheck;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        pathCheck = new NavMeshPath();
    }

    void Start()
    {
        gameOverPanel.SetActive(false);
        damageUpgradeText.gameObject.SetActive(false);
        killsText.text = "Zombies: 0";
        timerText.text = "00:00";
        if (levelText != null) levelText.text = "Nivel 1";
    }

    public void StartGame()
    {
        GameStarted = true;
        gun.damage = startDamage;
        if (rooms == null || rooms.Length == 0) BuildSpawnArea();
    }

    // Busca en el NavMesh todos los triángulos que quedan dentro del edificio
    void BuildSpawnArea()
    {
        if (buildingRoot == null)
        {
            Debug.LogWarning("GameManager: falta asignar Building Root (FOB_LOD).");
            return;
        }

        Renderer[] renderers = buildingRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        b.Expand(-edgeMargin * 2f); // se encoge edgeMargin por cada lado
        spawnBounds = b;

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        for (int i = 0; i + 2 < tri.indices.Length; i += 3)
        {
            Vector3 v0 = tri.vertices[tri.indices[i]];
            Vector3 v1 = tri.vertices[tri.indices[i + 1]];
            Vector3 v2 = tri.vertices[tri.indices[i + 2]];
            Vector3 center = (v0 + v1 + v2) / 3f;
            if (!spawnBounds.Contains(center)) continue;

            totalArea += Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            triangles.Add(new[] { v0, v1, v2 });
            cumulativeAreas.Add(totalArea);
        }

        hasSpawnArea = triangles.Count > 0;
        if (!hasSpawnArea)
            Debug.LogWarning("GameManager: no hay NavMesh dentro del edificio. ¿Hiciste Bake?");
    }

    // Punto al azar sobre el NavMesh dentro del edificio (reparte por área)
    Vector3 RandomPointInBuilding()
    {
        float r = Random.value * totalArea;
        int idx = cumulativeAreas.BinarySearch(r);
        if (idx < 0) idx = ~idx;
        idx = Mathf.Clamp(idx, 0, triangles.Count - 1);

        Vector3[] t = triangles[idx];
        float u = Random.value, v = Random.value;
        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
        return t[0] + u * (t[1] - t[0]) + v * (t[2] - t[0]);
    }

    // Dificultad = kills/killsPerLevel + tiempo/secondsPerLevel
    float Difficulty => kills / killsPerLevel + elapsed / secondsPerLevel;
    float CurrentSpawnInterval => Mathf.Max(minSpawnInterval, startSpawnInterval * Mathf.Pow(intervalDecay, Difficulty));
    int CurrentMaxAlive => Mathf.Min(maxAliveCap, startMaxAlive + Mathf.FloorToInt(Difficulty * aliveGrowthPerLevel));

    void Update()
    {
        if (!GameStarted || IsGameOver) return;

        elapsed += Time.deltaTime;
        timerText.text = $"{(int)(elapsed / 60f):00}:{(int)(elapsed % 60f):00}";
        if (levelText != null) levelText.text = $"Nivel {1 + Mathf.FloorToInt(Difficulty)}";

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= CurrentSpawnInterval && alive < CurrentMaxAlive)
        {
            if (TrySpawn()) spawnTimer = 0f; // si falla, reintenta en el siguiente frame
        }
    }

    bool TrySpawn()
    {
        bool useRooms = rooms != null && rooms.Length > 0;
        if (!useRooms && !hasSpawnArea) return false;

        for (int i = 0; i < 25; i++)
        {
            Vector3 pos;
            if (useRooms)
            {
                Vector3 p = rooms[Random.Range(0, rooms.Length)].GetRandomPoint();
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas)) continue;
                pos = hit.position;
            }
            else
            {
                pos = RandomPointInBuilding();
            }

            if (Vector3.Distance(pos, player.position) < minDistanceFromPlayer) continue;
            if (!CanReachPlayer(pos)) continue; // evita zombies atrapados en zonas aisladas

            Instantiate(zombiePrefab, pos, Quaternion.identity);
            alive++;
            return true;
        }
        return false;
    }

    // Solo permite aparecer donde el zombie tiene camino completo hasta el jugador
    bool CanReachPlayer(Vector3 pos)
    {
        if (pathCheck == null) pathCheck = new NavMeshPath();
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit ph, 80f, NavMesh.AllAreas)) return true;
        return NavMesh.CalculatePath(pos, ph.position, NavMesh.AllAreas, pathCheck)
               && pathCheck.status == NavMeshPathStatus.PathComplete;
    }

    // Un zombie que se eliminó por error (sin contar como kill)
    public void OnZombieRemoved()
    {
        alive = Mathf.Max(0, alive - 1);
    }

    public void OnZombieKilled()
    {
        alive = Mathf.Max(0, alive - 1);
        kills++;
        killsText.text = $"Zombies: {kills}";

        if (nextUpgrade < upgradeKills.Length && kills >= upgradeKills[nextUpgrade])
        {
            gun.damage = upgradeDamage[nextUpgrade];
            nextUpgrade++;
            StopCoroutine(nameof(ShowUpgrade));
            StartCoroutine(nameof(ShowUpgrade));
        }
    }

    IEnumerator ShowUpgrade()
    {
        damageUpgradeText.text = "Daño aumentado";
        damageUpgradeText.gameObject.SetActive(true);
        yield return new WaitForSeconds(2.5f);
        damageUpgradeText.gameObject.SetActive(false);
    }

    public void GameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        gameOverPanel.SetActive(true);
        gameOverStatsText.text = $"Zombies: {kills}\nTiempo: {timerText.text}";
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Conéctalo al botón "Menú" del Game Over
    public string menuSceneName = "MenuPrincipal";
    public void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    // Conéctalo al botón "Reiniciar"
    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Muestra en la Scene (con el juego corriendo) la zona donde aparecen los zombies
    void OnDrawGizmosSelected()
    {
        if (!hasSpawnArea) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(spawnBounds.center, spawnBounds.size);
    }
}