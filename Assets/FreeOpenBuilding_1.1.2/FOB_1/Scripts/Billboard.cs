using UnityEngine;

// Pónselo al texto 3D para que siempre mire al jugador.
public class Billboard : MonoBehaviour
{
    Transform cam;
    void Start() { cam = Camera.main.transform; }
    void LateUpdate()
    {
        transform.rotation = Quaternion.LookRotation(transform.position - cam.position);
    }
}
