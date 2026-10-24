using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class GarrafaCombustivel : MonoBehaviour
{
    [Header("Configuração de Abastecimento")]
    [Tooltip("Distância em metros para abastecer automaticamente ao aproximar o galão do gerador")]
    [SerializeField] private float distanciaAbastecimentoProximidade = 0.90f;

    private XRGrabInteractable grabInteractable;
    private GeradorCombustivel geradorCached;
    private bool jaMostrouAvisoColeta = false;
    private bool jaAbasteceu = false;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void Start()
    {
        geradorCached = FindFirstObjectByType<GeradorCombustivel>();
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

    private void Update()
    {
        if (jaAbasteceu || !jaMostrouAvisoColeta)
            return;

        if (geradorCached == null)
        {
            geradorCached = FindFirstObjectByType<GeradorCombustivel>();
            if (geradorCached == null) return;
        }

        float distancia = Vector3.Distance(transform.position, geradorCached.transform.position);
        if (distancia <= distanciaAbastecimentoProximidade)
        {
            AbastecerGerador(geradorCached);
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
        if (jaAbasteceu) return;

        GeradorCombustivel gerador =
            collision.collider.GetComponentInParent<GeradorCombustivel>();

        if (gerador != null)
        {
            AbastecerGerador(gerador);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (jaAbasteceu) return;

        GeradorCombustivel gerador =
            other.GetComponentInParent<GeradorCombustivel>();

        if (gerador != null)
        {
            AbastecerGerador(gerador);
        }
    }

    private void AbastecerGerador(GeradorCombustivel gerador)
    {
        if (jaAbasteceu || gerador == null) return;
        jaAbasteceu = true;

        gerador.ReceberCombustivel();
        Destroy(gameObject);
    }
}
