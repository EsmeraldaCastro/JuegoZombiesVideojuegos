using UnityEngine;
using TMPro;

// Muestra una "X" breve sobre la mira cuando le das a un zombie.
// Blanca = cuerpo, roja = cabeza. Ponlo en cualquier objeto (por ejemplo el Canvas).
public class HitMarker : MonoBehaviour
{
    public static HitMarker Instance;

    public TextMeshProUGUI marker;        // texto "X" en el centro de la pantalla
    public Color bodyColor = Color.white;
    public Color headColor = Color.red;
    public float duration = 0.15f;

    float timer;

    void Awake()
    {
        Instance = this;
        if (marker != null) marker.alpha = 0f;
    }

    public void Show(bool headshot)
    {
        if (marker == null) return;
        marker.text = "X";
        marker.color = headshot ? headColor : bodyColor;
        marker.alpha = 1f;
        timer = duration;
    }

    void Update()
    {
        if (timer > 0f)
        {
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f && marker != null) marker.alpha = 0f;
        }
    }
}
