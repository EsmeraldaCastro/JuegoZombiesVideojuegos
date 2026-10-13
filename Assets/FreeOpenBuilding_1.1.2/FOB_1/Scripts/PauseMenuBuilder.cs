#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// DEBE estar dentro de una carpeta llamada "Editor" (por ejemplo Assets/Editor).
// Uso: menú superior GameObject > Zombies VR > Crear menú de pausa
// Crea el PauseManager y el PauseCanvas con todos sus botones, y los conecta entre sí.
public static class PauseMenuBuilder
{
    [MenuItem("GameObject/Zombies VR/Crear menú de pausa", false, 10)]
    public static void Build()
    {
        if (UnityEngine.Object.FindFirstObjectByType<PauseMenu>() != null)
        {
            EditorUtility.DisplayDialog("Menú de pausa",
                "Ya existe un PauseManager en la escena. Bórralo antes de crear otro.", "OK");
            return;
        }

        // ---- Manager (siempre activo) ----
        GameObject manager = new GameObject("PauseManager");
        Undo.RegisterCreatedObjectUndo(manager, "Crear menú de pausa");
        PauseMenu menu = manager.AddComponent<PauseMenu>();
        LocomotionSettings loco = manager.AddComponent<LocomotionSettings>();
        manager.AddComponent<VRMenuPointer>();   // láser y gatillo del control izquierdo

        // ---- Canvas en el mundo ----
        GameObject canvasGO = new GameObject("PauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Crear menú de pausa");
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;

        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(1000f, 700f);
        canvasRT.localScale = Vector3.one * 0.001f;

        // Raycaster para los rayos de los controles (el nombre exacto cambia entre versiones)
        Type tracked = FindType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster", "TrackedDeviceGraphicRaycaster");
        if (tracked != null) canvasGO.AddComponent(tracked);
        else Debug.LogWarning("PauseMenuBuilder: no encontré TrackedDeviceGraphicRaycaster. Agrégalo a PauseCanvas con Add Component.");

        // Se coloca solo frente a ti cada vez que se abre
        VRPanelPlacer placer = canvasGO.AddComponent<VRPanelPlacer>();
        placer.distance = 1.5f;
        placer.baseScale = 0.001f;

        // ---- Fondo ----
        RectTransform bg = CreateStretched(canvasGO.transform, "Fondo");
        Image bgImage = bg.gameObject.AddComponent<Image>();
        bgImage.color = new Color(0.06f, 0.07f, 0.10f, 0.94f);
        bgImage.raycastTarget = false;

        // ---- Panel de pausa ----
        RectTransform pausePanel = CreateStretched(canvasGO.transform, "PanelPausa");
        CreateText(pausePanel, "Titulo", "PAUSA", 96f, new Vector2(0f, 245f), new Vector2(800f, 130f));
        TextMeshProUGUI unused;
        Button continueBtn = CreateButton(pausePanel, "BtnContinuar", "Continuar", 56f, new Vector2(0f, 70f), new Vector2(720f, 120f), out unused);
        Button optionsBtn = CreateButton(pausePanel, "BtnOpciones", "Opciones", 56f, new Vector2(0f, -70f), new Vector2(720f, 120f), out unused);
        Button quitBtn = CreateButton(pausePanel, "BtnSalir", "Salir del juego", 56f, new Vector2(0f, -210f), new Vector2(720f, 120f), out unused);

        // ---- Panel de opciones ----
        RectTransform optionsPanel = CreateStretched(canvasGO.transform, "PanelOpciones");
        CreateText(optionsPanel, "Titulo", "OPCIONES", 84f, new Vector2(0f, 275f), new Vector2(800f, 120f));
        TextMeshProUGUI moveText, turnText, backText;
        Button moveBtn = CreateButton(optionsPanel, "BtnMovimiento", "Movimiento Smooth", 48f, new Vector2(0f, 140f), new Vector2(820f, 100f), out moveText);
        Button turnBtn = CreateButton(optionsPanel, "BtnGiro", "Smooth Turn", 48f, new Vector2(0f, 20f), new Vector2(820f, 100f), out turnText);
        TextMeshProUGUI angleText = CreateText(optionsPanel, "TxtAngulo", "Ángulo de giro: 45°", 44f, new Vector2(0f, -100f), new Vector2(520f, 100f));
        Button minusBtn = CreateButton(optionsPanel, "BtnMenosGrados", "-", 64f, new Vector2(-330f, -100f), new Vector2(130f, 100f), out unused);
        Button plusBtn = CreateButton(optionsPanel, "BtnMasGrados", "+", 64f, new Vector2(330f, -100f), new Vector2(130f, 100f), out unused);
        Button backBtn = CreateButton(optionsPanel, "BtnAtras", "Atrás", 52f, new Vector2(0f, -250f), new Vector2(520f, 100f), out backText);
        optionsPanel.gameObject.SetActive(false);

        // ---- Conexiones ----
        menu.menuRoot = canvasGO;
        menu.pausePanel = pausePanel.gameObject;
        menu.optionsPanel = optionsPanel.gameObject;
        menu.continueButton = continueBtn;
        menu.optionsButton = optionsBtn;
        menu.quitButton = quitBtn;
        menu.moveButton = moveBtn;
        menu.turnButton = turnBtn;
        menu.angleMinusButton = minusBtn;
        menu.anglePlusButton = plusBtn;
        menu.backButton = backBtn;
        menu.moveLabel = moveText;
        menu.turnLabel = turnText;
        menu.angleLabel = angleText;
        menu.locomotion = loco;

        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(manager.scene);
        Selection.activeGameObject = manager;
        Debug.Log("Menú de pausa creado: PauseManager y PauseCanvas. Guarda la escena con Ctrl+S.");
    }

    // ---------- Ayudas ----------
    static RectTransform CreateStretched(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, Vector2 position, Vector2 boxSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = boxSize;
        rt.anchoredPosition = position;

        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    static Button CreateButton(Transform parent, string name, string label, float fontSize, Vector2 position, Vector2 size, out TextMeshProUGUI labelText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.22f, 0.25f, 0.34f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.72f, 0.85f, 1f, 1f);
        colors.pressedColor = new Color(0.5f, 0.65f, 0.95f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        button.colors = colors;

        labelText = CreateText(go.transform, "Texto", label, fontSize, Vector2.zero, size);
        return button;
    }

    static Type FindType(string fullName, string shortName)
    {
        foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(fullName);
            if (t != null) return t;
        }

        foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
            catch (Exception) { continue; }

            foreach (Type t in types)
                if (t != null && t.Name == shortName) return t;
        }
        return null;
    }
}
#endif