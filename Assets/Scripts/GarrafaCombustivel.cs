using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class GarrafaCombustivel : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;
    private bool jaMostrouAvisoColeta = false;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(AoPegarGalao);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(AoPegarGalao);
        }
    }

    private void AoPegarGalao(SelectEnterEventArgs args)
    {
        if (jaMostrouAvisoColeta)
            return;

        jaMostrouAvisoColeta = true;

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayClick();
        }

        if (GerenteAlarme.Instance != null)
        {
            GerenteAlarme.Instance.MostrarAvisoCombustivelColetado();
        }
    }

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
