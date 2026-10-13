using UnityEngine;

// Ponlo en un objeto vacío por cada cuarto del edificio.
// El cuadro rojo (visible en la Scene) marca dónde pueden aparecer zombies.
// Hazlo DELGADO en Y y a la altura del piso, para que el punto caiga en el NavMesh del piso correcto.
public class SpawnRoom : MonoBehaviour
{
    public Vector3 size = new Vector3(10f, 1f, 10f);

    public Vector3 GetRandomPoint()
    {
        Vector3 local = new Vector3(
            Random.Range(-0.5f, 0.5f) * size.x,
            Random.Range(-0.5f, 0.5f) * size.y,
            Random.Range(-0.5f, 0.5f) * size.z);
        return transform.TransformPoint(local);
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawCube(Vector3.zero, size);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
