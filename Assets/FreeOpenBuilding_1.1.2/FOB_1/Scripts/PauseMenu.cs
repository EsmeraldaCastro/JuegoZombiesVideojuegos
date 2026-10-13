using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// Menú de pausa para VR.
// Va en un objeto que SIEMPRE está activo (PauseManager), NO en el Canvas del menú.
public class PauseMenu : MonoBehaviour
{
    public enum OpenButton { LeftGrip, LeftMenuButton, LeftPrimaryButton }

    [Header("Cómo se abre (control izquierdo)")]
    public OpenButton openWith = OpenButton.LeftGrip;   // LeftGrip = botón lateral; LeftMenuButton = botón ≡; LeftPrimaryButton = botón X

    [Header("Menú (el Canvas completo; se apaga solo al empezar)")]
    public GameObject menuRoot;
    public GameObject pausePanel;
    public GameObject optionsPanel;

    [Header("Botones de pausa")]
    public Button continueButton;
    public Button optionsButton;
    public Button quitButton;

    [Header("Botones de opciones")]
    public Button moveButton;
    public Button turnButton;
    public Button angleMinusButton;
    public Button anglePlusButton;
    public Button backButton;
    public TextMeshProUGUI moveLabel;
    public TextMeshProUGUI turnLabel;
    public TextMeshProUGUI angleLabel;
    public LocomotionSettings locomotion;

    InputAction openAction;
    float lastToggle = -10f;
    bool paused;

    public bool IsPaused => paused;

    void Awake()
    {
        Listen(continueButton, Resume);
        Listen(optionsButton, ShowOptions);
        Listen(quitButton, QuitGame);
        Listen(backButton, ShowPause);
        Listen(moveButton, () => { if (locomotion != null) locomotion.ToggleMove(); RefreshOptions(); });
        Listen(turnButton, () => { if (locomotion != null) locomotion.ToggleTurn(); RefreshOptions(); });
        Listen(angleMinusButton, () => { if (locomotion != null) locomotion.ChangeAngle(-1); RefreshOptions(); });
        Listen(anglePlusButton, () => { if (locomotion != null) locomotion.ChangeAngle(+1); RefreshOptions(); });

        if (menuRoot != null) menuRoot.SetActive(false);
    }

    static void Listen(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    void OnEnable()
    {
        openAction = new InputAction("AbrirPausa", InputActionType.Button);
        switch (openWith)
        {
            case OpenButton.LeftGrip:
                openAction.AddBinding("<XRController>{LeftHand}/{GripButton}");
                openAction.AddBinding("<XRController>{LeftHand}/gripPressed");
                break;
            case OpenButton.LeftMenuButton:
                openAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
                openAction.AddBinding("<XRController>{LeftHand}/menu");
                openAction.AddBinding("<XRController>{LeftHand}/menuButton");
                break;
            case OpenButton.LeftPrimaryButton:
                openAction.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
                break;
        }
        openAction.AddBinding("<Keyboard>/p");   // para probar en el editor sin lentes
        openAction.performed += OnOpenPressed;
        openAction.Enable();
    }

    void OnDisable()
    {
        if (openAction != null)
        {
            openAction.performed -= OnOpenPressed;
            openAction.Disable();
            openAction.Dispose();
            openAction = null;
        }
    }

    void OnDestroy()
    {
        if (paused) Time.timeScale = 1f;
    }

    void OnOpenPressed(InputAction.CallbackContext ctx)
    {
        // Evita aperturas dobles por pulsaciones repetidas
        if (Time.unscaledTime - lastToggle < 0.4f) return;
        lastToggle = Time.unscaledTime;

        if (paused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (paused || menuRoot == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        paused = true;
        Time.timeScale = 0f;
        ShowPause();
        menuRoot.SetActive(true);   // VRPanelPlacer lo coloca frente a ti al activarse
    }

    public void Resume()
    {
        if (!paused) return;
        paused = false;
        Time.timeScale = 1f;
        if (menuRoot != null) menuRoot.SetActive(false);
    }

    public void ShowPause()
    {
        if (pausePanel != null) pausePanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    public void ShowOptions()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
        RefreshOptions();
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
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

        // El ángulo solo se puede cambiar con el giro por pasos
        if (angleMinusButton != null) angleMinusButton.interactable = snap;
        if (anglePlusButton != null) anglePlusButton.interactable = snap;
    }
}