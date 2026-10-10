using UnityEngine;

public class GarrafaCombustivel : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        // Procura o gerador no objeto atingido
        // ou em algum dos seus objetos pais.
        GeradorCombustivel gerador =
            collision.collider.GetComponentInParent<GeradorCombustivel>();

        if (gerador != null)
        {
            gerador.ReceberCombustivel();
            Destroy(gameObject);
        }
    }
}
