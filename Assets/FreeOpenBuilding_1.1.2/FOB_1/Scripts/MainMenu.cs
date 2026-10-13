using UnityEngine;
using UnityEngine.SceneManagement;

// Va en un objeto vacío de la escena MenuPrincipal. Conecta sus funciones a los botones (On Click).
public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "JuegoZombies_VR";

    public void Play()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}