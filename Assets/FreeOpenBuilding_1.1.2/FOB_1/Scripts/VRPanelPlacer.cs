using UnityEngine;

// Ponlo en el objeto raíz de un Canvas (World Space) que se muestra de golpe, como el Game Over.
// Cada vez que se activa, se coloca frente a ti, a tu altura, mirando hacia ti, con el tamaño correcto.
public class VRPanelPlacer : MonoBehaviour
{
    public Transform head;            // la Main Camera del XR Origin (si la dejas vacía, usa la cámara principal)
    public float distance = 2.5f;     // distancia en METROS reales frente a ti
    public float baseScale = 0.001f;  // escala del canvas (0.001 = 1000 píxeles miden 1 metro)
    public float heightOffset = 0f;   // en metros, relativo a tu cabeza

    void OnEnable() { Place(); }

    public void Place()
    {
        if (head == null)
        {
            if (Camera.main == null) return;
            head = Camera.main.transform;
        }

        // El XR Origin está escalado: se compensa para que las medidas sean reales
        float s = head.lossyScale.x;

        Vector3 fwd = head.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();

        transform.position = head.position + fwd * distance * s + Vector3.up * heightOffset * s;
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        transform.localScale = Vector3.one * baseScale * s;
    }
}