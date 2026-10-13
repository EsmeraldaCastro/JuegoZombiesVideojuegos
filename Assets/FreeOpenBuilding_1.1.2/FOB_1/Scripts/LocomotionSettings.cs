using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

// Cambia cómo se mueve y gira el jugador en VR (movimiento suave o teletransporte; giro suave o por pasos).
// Va en el PauseManager. Busca solo los componentes del XR Origin: no hay que arrastrar nada.
// Guarda la elección, así se conserva al reiniciar la escena y al volver a abrir el juego.
public class LocomotionSettings : MonoBehaviour
{
    public enum MoveMode { Smooth = 0, Teleport = 1 }
    public enum TurnMode { Smooth = 0, Snap = 1 }

    [Header("Valores la primera vez que se juega")]
    public MoveMode defaultMove = MoveMode.Smooth;
    public TurnMode defaultTurn = TurnMode.Smooth;
    public float defaultSnapAngle = 45f;

    [Header("Ángulo del giro por pasos (grados)")]
    public float minSnapAngle = 15f;
    public float maxSnapAngle = 90f;
    public float snapAngleStep = 15f;

    [Header("Alcance del teletransporte (palanca derecha arriba/abajo mientras apuntas)")]
    public float reachDefault = 2f;     // 1 = alcance original del starter
    public float reachMin = 0.5f;
    public float reachMax = 10f;
    public float reachSpeed = 4f;       // cuánto cambia por segundo con la palanca a tope

    public MoveMode MoveSetting { get; private set; }
    public TurnMode TurnSetting { get; private set; }
    public float SnapAngle { get; private set; }

    const string KeyMove = "vr_move_mode";
    const string KeyTurn = "vr_turn_mode";
    const string KeyAngle = "vr_snap_angle";
    const string KeyReach = "vr_tp_reach";

    bool loggedReach;
    float reach;
    InputAction reachAction;
    Behaviour leftRay;                       // XRRayInteractor del teletransporte izquierdo
    GameObject leftRayObject;
    float baseVelocity = -1f, baseDistance = -1f;

    // Componentes del starter de XR Interaction Toolkit, encontrados por nombre de tipo
    readonly List<Behaviour> inputManagers = new List<Behaviour>();      // ControllerInputActionManager (uno por mano)
    readonly List<Behaviour> smoothMoveProviders = new List<Behaviour>();
    readonly List<Behaviour> teleportProviders = new List<Behaviour>();
    readonly List<Behaviour> smoothTurnProviders = new List<Behaviour>();
    readonly List<Behaviour> snapTurnProviders = new List<Behaviour>();
    readonly List<GameObject> teleportInteractors = new List<GameObject>();
    readonly List<GameObject> rightTeleportInteractors = new List<GameObject>();   // los de la mano derecha (se anulan)
    bool warnedMissing;

    void Awake()
    {
        MoveSetting = (MoveMode)PlayerPrefs.GetInt(KeyMove, (int)defaultMove);
        TurnSetting = (TurnMode)PlayerPrefs.GetInt(KeyTurn, (int)defaultTurn);
        SnapAngle = Mathf.Clamp(PlayerPrefs.GetFloat(KeyAngle, defaultSnapAngle), minSnapAngle, maxSnapAngle);
        reach = Mathf.Clamp(PlayerPrefs.GetFloat(KeyReach, reachDefault), reachMin, reachMax);
    }

    void OnEnable()
    {
        reachAction = new InputAction("TeleportReach", InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}");
        reachAction.Enable();
    }

    void OnDisable()
    {
        if (reachAction != null) { reachAction.Disable(); reachAction.Dispose(); reachAction = null; }
    }

    // Mientras el rayo de teletransporte está abierto, la palanca derecha arriba/abajo cambia el alcance
    void Update()
    {
        if (MoveSetting != MoveMode.Teleport || reachAction == null) return;
        if (leftRayObject == null) FindLeftRay();
        if (leftRayObject == null) return;
        if (!leftRayObject.activeInHierarchy) return;

        float y = reachAction.ReadValue<Vector2>().y;
        if (Mathf.Abs(y) < 0.4f) return;
        if (!loggedReach)
        {
            loggedReach = true;
            Debug.Log($"LocomotionSettings: ajustando alcance. rayo={(leftRay != null ? leftRay.GetType().Name : "NO ENCONTRADO")}");
        }

        reach = Mathf.Clamp(reach + y * reachSpeed * Time.unscaledDeltaTime, reachMin, reachMax);
        PlayerPrefs.SetFloat(KeyReach, reach);
        ApplyReach();
    }

    void ApplyReach()
    {
        if (leftRay == null) return;
        if (baseVelocity < 0f)
        {
            object v = GetMember(leftRay, "m_Velocity");
            object d = GetMember(leftRay, "m_MaxRaycastDistance");
            baseVelocity = v is float fv ? fv : 10f;
            baseDistance = d is float fd ? fd : 30f;
        }
        // Con curva el alcance sale de la velocidad (crece con su cuadrado); con línea recta, de la distancia máxima
        SetMember(leftRay, "velocity", "m_Velocity", baseVelocity * Mathf.Sqrt(reach));
        SetMember(leftRay, "maxRaycastDistance", "m_MaxRaycastDistance", baseDistance * reach);
    }

    void Start()
    {
        Discover();
        Apply();
    }

    // ---------- Cambios que pide el menú ----------
    public void ToggleMove()
    {
        MoveSetting = MoveSetting == MoveMode.Smooth ? MoveMode.Teleport : MoveMode.Smooth;
        SaveAndApply();
    }

    public void ToggleTurn()
    {
        TurnSetting = TurnSetting == TurnMode.Smooth ? TurnMode.Snap : TurnMode.Smooth;
        SaveAndApply();
    }

    // direction: -1 = menos grados, +1 = más grados
    public void ChangeAngle(int direction)
    {
        SnapAngle = Mathf.Clamp(SnapAngle + direction * snapAngleStep, minSnapAngle, maxSnapAngle);
        SaveAndApply();
    }

    void SaveAndApply()
    {
        PlayerPrefs.SetInt(KeyMove, (int)MoveSetting);
        PlayerPrefs.SetInt(KeyTurn, (int)TurnSetting);
        PlayerPrefs.SetFloat(KeyAngle, SnapAngle);
        PlayerPrefs.Save();
        Apply();
    }

    // ---------- Búsqueda de componentes ----------
    Transform FindRig()
    {
        CharacterController cc = FindFirstObjectByType<CharacterController>();
        if (cc != null) return cc.transform;
        return Camera.main != null ? Camera.main.transform.root : null;
    }

    void Discover()
    {
        Transform rig = FindRig();
        if (rig == null)
        {
            Debug.LogWarning("LocomotionSettings: no encontré el XR Origin.");
            return;
        }

        foreach (MonoBehaviour mb in rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string type = mb.GetType().Name;

            switch (type)
            {
                case "DynamicMoveProvider":
                case "ContinuousMoveProvider":
                    smoothMoveProviders.Add(mb);
                    break;
                case "TeleportationProvider":
                    teleportProviders.Add(mb);
                    break;
                case "ContinuousTurnProvider":
                    smoothTurnProviders.Add(mb);
                    break;
                case "SnapTurnProvider":
                    snapTurnProviders.Add(mb);
                    break;
                case "ControllerInputActionManager":
                    inputManagers.Add(mb);
                    Component tp = GetMember(mb, "m_TeleportInteractor") as Component;
                    if (tp != null && !teleportInteractors.Contains(tp.gameObject))
                        teleportInteractors.Add(tp.gameObject);
                    if (tp != null && !mb.gameObject.name.Contains("Left") && !rightTeleportInteractors.Contains(tp.gameObject))
                        rightTeleportInteractors.Add(tp.gameObject);
                    break;
            }

            // Respaldo: interactores de teletransporte por nombre
            if (type.Contains("Interactor") && mb.gameObject.name.Contains("Teleport")
                && !teleportInteractors.Contains(mb.gameObject))
            {
                teleportInteractors.Add(mb.gameObject);
                if (IsUnderRight(mb.transform)) rightTeleportInteractors.Add(mb.gameObject);
            }
        }

        Debug.Log($"LocomotionSettings: managers={inputManagers.Count}, moveSuave={smoothMoveProviders.Count}, " +
                  $"teletransporte={teleportProviders.Count}, giroSuave={smoothTurnProviders.Count}, " +
                  $"giroPasos={snapTurnProviders.Count}, rayosTeletransporte={teleportInteractors.Count}");
    }

    // ---------- Aplicar ----------
    public void Apply()
    {
        bool smoothMove = MoveSetting == MoveMode.Smooth;
        bool smoothTurn = TurnSetting == TurnMode.Smooth;

        if (inputManagers.Count > 0)
        {
            // Forma oficial del starter: cada mando tiene "Smooth Motion Enabled" y "Smooth Turn Enabled".
            // Movimiento suave: solo la mano izquierda mueve. Teletransporte: ninguna mano mueve y las dos teletransportan.
            foreach (Behaviour m in inputManagers)
            {
                bool isLeft = m.gameObject.name.Contains("Left");
                bool ok1 = SetMember(m, "smoothTurnEnabled", "m_SmoothTurnEnabled", smoothTurn);
                bool ok2 = SetMember(m, "smoothMotionEnabled", "m_SmoothMotionEnabled", smoothMove && isLeft);
                if (!(ok1 && ok2)) WarnMissing("smoothMotionEnabled / smoothTurnEnabled");
            }
        }
        else
        {
            // Sin managers: se activan y desactivan los proveedores directamente
            SetEnabled(smoothMoveProviders, smoothMove);
            SetEnabled(teleportProviders, !smoothMove);
            SetEnabled(smoothTurnProviders, smoothTurn);
            SetEnabled(snapTurnProviders, !smoothTurn);
        }

        foreach (Behaviour s in snapTurnProviders)
            SetMember(s, "turnAmount", "m_TurnAmount", SnapAngle);

        // El giro es solo del joystick derecho: se anula la entrada de la mano izquierda en los dos tipos de giro
        DisableLeftTurnInput(smoothTurnProviders);
        DisableLeftTurnInput(snapTurnProviders);

        if (smoothMove)
        {
            HideTeleportRays();
        }
        else
        {
            // Teletransporte: el rayo arranca apagado y el control lo enciende al empujar el joystick hacia adelante
            SetEnabled(teleportProviders, true);
            // Solo teletransporta la mano izquierda: en la derecha se apagan sus componentes
            foreach (GameObject go in teleportInteractors)
            {
                if (go == null) continue;
                bool allowed = !rightTeleportInteractors.Contains(go);
                foreach (Behaviour b in go.GetComponents<Behaviour>()) b.enabled = allowed;
                foreach (LineRenderer lr in go.GetComponents<LineRenderer>()) lr.enabled = allowed;
                go.SetActive(false);
            }
            FindLeftRay();
            ApplyReach();
            WarnIfNoTeleportAreas();
        }
    }

    void DisableLeftTurnInput(List<Behaviour> providers)
    {
        foreach (Behaviour p in providers)
        {
            if (p == null) continue;
            bool done = false;
            for (System.Type t = p.GetType(); t != null && !done; t = t.BaseType)
            {
                FieldInfo fi = t.GetField("m_LeftHandTurnInput", Flags | BindingFlags.DeclaredOnly);
                if (fi == null) continue;
                object reader = fi.GetValue(p);                       // struct XRInputValueReader<Vector2>
                PropertyInfo mode = reader.GetType().GetProperty("inputSourceMode", Flags);
                if (mode != null && mode.CanWrite)
                {
                    mode.SetValue(reader, System.Enum.Parse(mode.PropertyType, "Unused"));
                    fi.SetValue(p, reader);
                    done = true;
                }
            }
            if (!done)
                Debug.LogWarning($"LocomotionSettings: no pude anular el giro de la mano izquierda en {p.GetType().Name}.");
        }
    }

    void FindLeftRay()
    {
        leftRay = null; leftRayObject = null;
        foreach (GameObject go in teleportInteractors)
        {
            if (go == null || rightTeleportInteractors.Contains(go)) continue;
            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name.Contains("RayInteractor")) { leftRay = mb; leftRayObject = go; return; }
        }
    }

    static bool IsUnderRight(Transform t)
    {
        for (; t != null; t = t.parent)
            if (t.name.Contains("Right")) return true;
        return false;
    }

    void WarnIfNoTeleportAreas()
    {
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n == "TeleportationArea" || n == "TeleportationAnchor") return;
        }
        Debug.LogWarning("LocomotionSettings: no hay ningún Teleportation Area en la escena. " +
                         "Sin él el rayo de teletransporte no tiene dónde caer. Agrégalo al piso y a las escaleras.");
    }

    // En modo suave no debe verse el rayo de teletransporte
    void LateUpdate()
    {
        if (MoveSetting == MoveMode.Smooth) HideTeleportRays();
    }

    void HideTeleportRays()
    {
        foreach (GameObject go in teleportInteractors)
            if (go != null && go.activeSelf) go.SetActive(false);
    }

    static void SetEnabled(List<Behaviour> list, bool value)
    {
        foreach (Behaviour b in list)
            if (b != null) b.enabled = value;
    }

    void WarnMissing(string what)
    {
        if (warnedMissing) return;
        warnedMissing = true;
        Debug.LogWarning($"LocomotionSettings: no pude cambiar {what}. Es probable que tu versión del XR Interaction Toolkit use otros nombres.");
    }

    // ---------- Reflexión (para no depender de la versión del paquete XR) ----------
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static object GetMember(object target, string field)
    {
        for (System.Type t = target.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo fi = t.GetField(field, Flags | BindingFlags.DeclaredOnly);
            if (fi != null) return fi.GetValue(target);
        }
        return null;
    }

    static bool SetMember(object target, string property, string field, object value)
    {
        System.Type type = target.GetType();
        PropertyInfo pi = type.GetProperty(property, Flags);
        if (pi != null && pi.CanWrite)
        {
            pi.SetValue(target, value);
            return true;
        }

        for (System.Type t = type; t != null; t = t.BaseType)
        {
            FieldInfo fi = t.GetField(field, Flags | BindingFlags.DeclaredOnly);
            if (fi != null)
            {
                fi.SetValue(target, value);
                return true;
            }
        }
        return false;
    }
}