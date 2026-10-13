using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Láser y selección para el menú de pausa en VR.
// Funciona por sí solo: NO depende del sistema de UI del XR Interaction Toolkit.
// Va en el PauseManager. Con el menú abierto muestra un láser desde el control
// y, con el gatillo, pulsa el botón al que apuntas.
public class VRMenuPointer : MonoBehaviour
{
    public enum Hand { Left, Right }

    [Header("Control")]
    public Hand hand = Hand.Left;

    [Header("Menú (si lo dejas vacío usa el del PauseMenu)")]
    public GameObject menuRoot;

    [Header("Láser (unidades del mundo; el XR Origin está a escala 34)")]
    public float laserWidth = 0.25f;
    public float missLength = 120f;      // largo del láser cuando no apuntas al menú
    public float maxDistance = 2000f;
    public Color idleColor = new Color(1f, 1f, 1f, 0.85f);
    public Color hoverColor = new Color(0.2f, 1f, 0.6f, 1f);

    [Header("Si el láser sale torcido")]
    public Transform aimOverride;        // arrastra aquí otro objeto del control si hace falta
    public float pitchOffset = 0f;       // grados hacia arriba (+) o abajo (-)

    InputAction clickAction;
    LineRenderer line;
    RectTransform canvasRT;
    Transform aim;
    Button hovered;
    bool clickRequested;
    float lastClick = -10f;
    bool warnedAim;
    readonly List<Button> buttons = new List<Button>();

    void Awake()
    {
        GameObject lineGO = new GameObject("MenuLaser");
        lineGO.transform.SetParent(transform, false);
        line = lineGO.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.widthMultiplier = laserWidth;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null) line.material = new Material(shader);

        SetLaserColor(idleColor);
        line.enabled = false;
    }

    void Start()
    {
        if (menuRoot == null)
        {
            PauseMenu pause = GetComponent<PauseMenu>();
            if (pause != null) menuRoot = pause.menuRoot;
        }
        if (menuRoot != null) canvasRT = menuRoot.GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        string h = hand == Hand.Left ? "LeftHand" : "RightHand";
        clickAction = new InputAction("MenuClick", InputActionType.Button);
        clickAction.AddBinding("<XRController>{" + h + "}/{TriggerButton}");
        clickAction.AddBinding("<XRController>{" + h + "}/triggerPressed");
        clickAction.AddBinding("<XRController>{" + h + "}/trigger");
        clickAction.performed += ctx => clickRequested = true;
        clickAction.Enable();
    }

    void OnDisable()
    {
        if (clickAction != null)
        {
            clickAction.Disable();
            clickAction.Dispose();
            clickAction = null;
        }
        SetHover(null);
        if (line != null) line.enabled = false;
    }

    void Update()
    {
        if (menuRoot == null || !menuRoot.activeInHierarchy)
        {
            clickRequested = false;
            SetHover(null);
            if (line != null) line.enabled = false;
            return;
        }

        if (aim == null) aim = FindAim();
        if (aim == null)
        {
            if (line != null) line.enabled = false;
            return;
        }

        Vector3 dir = Quaternion.AngleAxis(-pitchOffset, aim.right) * aim.forward;
        Ray ray = new Ray(aim.position, dir);

        // ¿A qué botón apuntas?
        Button best = null;
        float bestDist = maxDistance;
        Vector3 bestPoint = Vector3.zero;

        menuRoot.GetComponentsInChildren(false, buttons);
        foreach (Button b in buttons)
        {
            if (b == null || !b.interactable) continue;

            RectTransform rt = (RectTransform)b.transform;
            Plane plane = new Plane(rt.forward, rt.position);
            if (!plane.Raycast(ray, out float enter) || enter > bestDist) continue;

            Vector3 point = ray.GetPoint(enter);
            Vector3 local = rt.InverseTransformPoint(point);
            if (rt.rect.Contains(new Vector2(local.x, local.y)))
            {
                best = b;
                bestDist = enter;
                bestPoint = point;
            }
        }

        SetHover(best);

        // Dónde termina el láser: en el botón, en el panel o al aire
        Vector3 end = ray.origin + ray.direction * missLength;
        if (best != null)
        {
            end = bestPoint;
        }
        else if (canvasRT != null)
        {
            Plane canvasPlane = new Plane(canvasRT.forward, canvasRT.position);
            if (canvasPlane.Raycast(ray, out float ce) && ce < maxDistance)
            {
                Vector3 p = ray.GetPoint(ce);
                Vector3 cl = canvasRT.InverseTransformPoint(p);
                if (canvasRT.rect.Contains(new Vector2(cl.x, cl.y))) end = p;
            }
        }

        line.enabled = true;
        line.SetPosition(0, ray.origin);
        line.SetPosition(1, end);
        SetLaserColor(best != null ? hoverColor : idleColor);

        // Clic con el gatillo
        if (clickRequested)
        {
            clickRequested = false;
            if (best != null && Time.unscaledTime - lastClick > 0.25f)
            {
                lastClick = Time.unscaledTime;
                best.onClick.Invoke();
            }
        }
    }

    void SetHover(Button b)
    {
        if (b == hovered) return;

        if (hovered != null)
            ExecuteEvents.Execute(hovered.gameObject, NewEventData(), ExecuteEvents.pointerExitHandler);

        hovered = b;

        if (hovered != null)
            ExecuteEvents.Execute(hovered.gameObject, NewEventData(), ExecuteEvents.pointerEnterHandler);
    }

    static PointerEventData NewEventData()
    {
        return new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
    }

    void SetLaserColor(Color c)
    {
        if (line == null) return;
        line.startColor = c;
        line.endColor = c;
        if (line.material != null) line.material.color = c;
    }

    // ---------- Dónde apunta el control ----------
    Transform FindAim()
    {
        if (aimOverride != null) return aimOverride;

        string side = hand == Hand.Left ? "Left" : "Right";

        // 1) El interactor de rayo de ese control: su posición ya es la pose de apuntado
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            string type = mb.GetType().Name;
            if (type != "NearFarInteractor" && type != "XRRayInteractor") continue;
            if (!HasAncestorNamed(mb.transform, side)) continue;
            if (HasAncestorNamed(mb.transform, "Teleport")) continue;
            return mb.transform;
        }

        // 2) Respaldo: el objeto del control
        GameObject controller = GameObject.Find(side + " Controller");
        if (controller != null)
        {
            if (!warnedAim)
            {
                warnedAim = true;
                Debug.LogWarning("VRMenuPointer: no encontré el interactor de rayo; uso '" + side +
                                 " Controller'. Si el láser sale torcido, ajusta Pitch Offset.");
            }
            return controller.transform;
        }

        return null;
    }

    static bool HasAncestorNamed(Transform t, string part)
    {
        for (; t != null; t = t.parent)
            if (t.name.Contains(part)) return true;
        return false;
    }
}
