using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Script SOLO para la escena MenuPrincipal. Va en el objeto PauseManager (reemplaza a PauseMenu).
// - El menú está visible desde que empieza la escena y nunca se oculta.
// - No hay movimiento (solo mirar y girar); lo bloquea este mismo script.
// - Continuar -> carga la escena del juego. Opciones -> movimiento / giro / ángulo. Salir del juego -> cierra.
// Encuentra los botones y textos por su nombre dentro de PauseCanvas, no hay que arrastrar nada.
public class MenuPrincipalVR : MonoBehaviour
{
    [Header("Escena del juego (nombre exacto, sin .unity)")]
    public string gameSceneName = "JuegoZombies_VR";

    [Header("Texto del primer botón (vacío = no cambiarlo)")]
    public string playButtonText = "";

    [Header("Opcional: si los dejas vacíos se buscan solos")]
    public GameObject menuRoot;
    public LocomotionSettings locomotion;

    GameObject mainPanel, optionsPanel;
    Button playBtn, optionsBtn, quitBtn, backBtn, moveBtn, turnBtn, minusBtn, plusBtn;
    TextMeshProUGUI moveLabel, turnLabel, angleLabel;

    void Awake()
    {
        Time.timeScale = 1f;

        // Un PauseMenu en esta escena ocultaría el menú: se apaga
        PauseMenu leftover = GetComponent<PauseMenu>();
        if (leftover != null) leftover.enabled = false;

        if (locomotion == null) locomotion = GetComponent<LocomotionSettings>();

        if (menuRoot == null)
        {
            GameObject found = GameObject.Find("PauseCanvas");
            if (found == null) found = GameObject.Find("MainMenuCanvas");
            menuRoot = found;
        }
        if (menuRoot == null)
        {
            Debug.LogError("MenuPrincipalVR: no encontré el canvas del menú (PauseCanvas). Arrástralo al campo Menu Root.");
            return;
        }

        Transform root = menuRoot.transform;
        mainPanel = Find(root, "PanelPausa");
        optionsPanel = Find(root, "PanelOpciones");
        playBtn = FindComp<Button>(root, "BtnContinuar");
        optionsBtn = FindComp<Button>(root, "BtnOpciones");
        quitBtn = FindComp<Button>(root, "BtnSalir");
        backBtn = FindComp<Button>(root, "BtnAtras");
        moveBtn = FindComp<Button>(root, "BtnMovimiento");
        turnBtn = FindComp<Button>(root, "BtnGiro");
        minusBtn = FindComp<Button>(root, "BtnMenosGrados");
        plusBtn = FindComp<Button>(root, "BtnMasGrados");
        moveLabel = moveBtn != null ? moveBtn.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        turnLabel = turnBtn != null ? turnBtn.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        angleLabel = FindComp<TextMeshProUGUI>(root, "TxtAngulo");

        // Se quitan los clics que el botón traía de la escena del juego (por ejemplo "Continuar" de la pausa)
        Wire(playBtn, Play);
        Wire(optionsBtn, ShowOptions);
        Wire(quitBtn, Quit);
        Wire(backBtn, ShowMain);
        Wire(moveBtn, () => { if (locomotion != null) locomotion.ToggleMove(); RefreshOptions(); });
        Wire(turnBtn, () => { if (locomotion != null) locomotion.ToggleTurn(); RefreshOptions(); });
        Wire(minusBtn, () => { if (locomotion != null) locomotion.ChangeAngle(-1); RefreshOptions(); });
        Wire(plusBtn, () => { if (locomotion != null) locomotion.ChangeAngle(1); RefreshOptions(); });

        if (!string.IsNullOrEmpty(playButtonText) && playBtn != null)
        {
            TextMeshProUGUI t = playBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null) t.text = playButtonText;
        }

        // El pointer toma este canvas aunque no haya PauseMenu
        VRMenuPointer pointer = GetComponent<VRMenuPointer>();
        if (pointer != null && pointer.menuRoot == null) pointer.menuRoot = menuRoot;
    }

    void Start()
    {
        if (menuRoot == null) return;
        menuRoot.SetActive(true);
        ShowMain();
        StartCoroutine(KeepMenuVisible());
    }

    // El menú queda siempre visible y se coloca frente a ti mientras el casco empieza a dar su posición
    IEnumerator KeepMenuVisible()
    {
        VRPanelPlacer placer = menuRoot.GetComponent<VRPanelPlacer>();
        float[] waits = { 0.3f, 1f, 2f };
        foreach (float w in waits)
        {
            yield return new WaitForSecondsRealtime(w);
            if (!menuRoot.activeSelf) menuRoot.SetActive(true);
            if (placer != null) placer.Place();
        }
    }

    void Update()
    {
        // Seguro: si otro script lo apagara, se vuelve a mostrar
        if (menuRoot != null && !menuRoot.activeSelf) menuRoot.SetActive(true);
    }

    // ---------- Sin movimiento en el menú (no depende de LocomotionSettings) ----------
    readonly System.Collections.Generic.List<Behaviour> moveBlockers = new System.Collections.Generic.List<Behaviour>();
    readonly System.Collections.Generic.List<GameObject> teleportRays = new System.Collections.Generic.List<GameObject>();
    bool foundRig;

    void FindMovement()
    {
        foundRig = true;
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (mb == null) continue;
            string type = mb.GetType().Name;
            if (type == "DynamicMoveProvider" || type == "ContinuousMoveProvider" || type == "TeleportationProvider")
                moveBlockers.Add(mb);
            else if (type.Contains("Interactor") && mb.gameObject.name.Contains("Teleport") && !teleportRays.Contains(mb.gameObject))
                teleportRays.Add(mb.gameObject);
        }
    }

    // Se repite cada cuadro para que ningún otro script (como LocomotionSettings) vuelva a activar el movimiento
    void LateUpdate()
    {
        if (!foundRig) FindMovement();
        foreach (Behaviour b in moveBlockers)
            if (b != null && b.enabled) b.enabled = false;
        foreach (GameObject go in teleportRays)
            if (go != null && go.activeSelf) go.SetActive(false);
    }

    // ---------- Pantallas ----------
    public void ShowMain()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    public void ShowOptions()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
        RefreshOptions();
    }

    // ---------- Acciones ----------
    public void Play()
    {
        Time.timeScale = 1f;

        if (Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }

#if UNITY_EDITOR
        // En el editor, si la escena no está en Build Profiles, se busca por nombre en el proyecto
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Scene " + gameSceneName);
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == gameSceneName)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
        }
#endif
        Debug.LogError($"MenuPrincipalVR: no encontré la escena \"{gameSceneName}\". Revisa Game Scene Name y que esté en Build Profiles.");
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void RefreshOptions()
    {
        if (locomotion == null) return;
        bool snap = locomotion.TurnSetting == LocomotionSettings.TurnMode.Snap;

        if (moveLabel != null)
            moveLabel.text = locomotion.MoveSetting == LocomotionSettings.MoveMode.Smooth
                ? "Movimiento Smooth" : "Movimiento Transport";
        if (turnLabel != null)
            turnLabel.text = snap ? "Snap Turn" : "Smooth Turn";
        if (angleLabel != null)
            angleLabel.text = $"Ángulo de giro: {locomotion.SnapAngle:0}°";

        if (minusBtn != null) minusBtn.interactable = snap;
        if (plusBtn != null) plusBtn.interactable = snap;
    }

    // ---------- Ayudas ----------
    static void Wire(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(action);
    }

    static GameObject Find(Transform root, string name)
    {
        Transform t = FindDeep(root, name);
        return t != null ? t.gameObject : null;
    }

    static T FindComp<T>(Transform root, string name) where T : Component
    {
        Transform t = FindDeep(root, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    // Busca por nombre en todos los hijos, también en los desactivados
    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform r = FindDeep(child, name);
            if (r != null) return r;
        }
        return null;
    }
}