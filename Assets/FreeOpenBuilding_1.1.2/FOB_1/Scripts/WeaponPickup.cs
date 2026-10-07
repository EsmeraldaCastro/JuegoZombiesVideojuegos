using UnityEngine;

// Va en la pistola que está en el suelo.
// Hija de este objeto: un TextMeshPro (3D) con el texto "Recoge con E para comenzar".
public class WeaponPickup : MonoBehaviour
{
    public GameObject promptText;      // el texto 3D encima de la pistola
    public GameObject gunInHand;       // pistola hija de la cámara (desactivada al inicio)
    public Transform player;
    public float pickupDistance = 2.5f;

    void Start()
    {
        gunInHand.SetActive(false);
    }

    void Update()
    {
        float dist = Vector3.Distance(player.position, transform.position);
        promptText.SetActive(dist <= pickupDistance * 2f); // visible al acercarse

        if (dist <= pickupDistance && Input.GetKeyDown(KeyCode.E))
        {
            gunInHand.SetActive(true);
            GameManager.Instance.StartGame();
            gameObject.SetActive(false); // desaparece del suelo (y su texto)
        }
    }
}
