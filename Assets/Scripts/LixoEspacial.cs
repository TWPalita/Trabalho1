using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

/// <summary>
/// Script dos detritos espaciais compatível com o protótipo existente e com o novo sistema XR.
/// </summary>
public class LixoEspacial : MonoBehaviour
{
    public TMP_Text textoDoNumero;
    public float velocidade = 0f; // 0 para manter estável no campo de visão em VR, configurável no Inspector

    [HideInInspector]
    public int valorDesteLixo;

    private XRSimpleInteractable interactable;
    private Renderer meshRenderer;
    private Color originalColor = Color.white;
    private Material matInstance;

    private void Awake()
    {
        if (textoDoNumero == null) textoDoNumero = GetComponentInChildren<TMP_Text>();

        meshRenderer = GetComponent<Renderer>();
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<Renderer>();

        if (meshRenderer != null)
        {
            matInstance = meshRenderer.material;
            originalColor = matInstance.color;
        }

        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null) interactable = gameObject.AddComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.firstHoverEntered.AddListener(OnHoverEnter);
            interactable.lastHoverExited.AddListener(OnHoverExit);
            interactable.activated.AddListener(OnActivated);
            interactable.selectEntered.AddListener(OnSelected);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.firstHoverEntered.RemoveListener(OnHoverEnter);
            interactable.lastHoverExited.RemoveListener(OnHoverExit);
            interactable.activated.RemoveListener(OnActivated);
            interactable.selectEntered.RemoveListener(OnSelected);
        }
    }

    public void ConfigurarLixo(int numero)
    {
        valorDesteLixo = numero;
        if (textoDoNumero != null)
        {
            textoDoNumero.text = numero.ToString();
        }
    }

    private void Update()
    {
        if (velocidade > 0f)
        {
            transform.Translate(Vector3.back * velocidade * Time.deltaTime);
        }

        // Se houver Debris ativo, ele já gerencia a posição e visibilidade da etiqueta
        if (GetComponent<Debris>() != null) return;

        // Mantém o texto fixado abaixo do objeto e virado para a cabeça do jogador em VR
        if (textoDoNumero != null)
        {
            textoDoNumero.transform.position = transform.position - Vector3.up * 1.35f;
            if (Camera.main != null)
            {
                Vector3 lookDir = textoDoNumero.transform.position - Camera.main.transform.position;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    textoDoNumero.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }
    }

    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        SetHighlight(true);
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        SetHighlight(false);
    }

    private void OnActivated(ActivateEventArgs args)
    {
        DispararContraLixo();
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        DispararContraLixo();
    }

    public void DispararContraLixo()
    {
        // Se houver DefenseManager
        if (DefenseManager.Instance != null)
        {
            DefenseManager.Instance.OnLegacyLixoHit(this);
            return;
        }

        // Fallback para GerenteMatematica
        if (GerenteMatematica.Instance != null)
        {
            GerenteMatematica.Instance.TestarAcerto(valorDesteLixo, gameObject);
        }
    }

    private void SetHighlight(bool highlighted)
    {
        Color hl = new Color(0.3f, 1f, 0.9f, 1f);

        if (matInstance != null)
        {
            matInstance.color = highlighted ? hl : originalColor;
            if (matInstance.HasProperty("_EmissionColor"))
            {
                matInstance.SetColor("_EmissionColor", highlighted ? hl * 0.8f : Color.black);
                if (highlighted) matInstance.EnableKeyword("_EMISSION");
                else matInstance.DisableKeyword("_EMISSION");
            }
        }

        if (textoDoNumero != null)
        {
            textoDoNumero.color = highlighted ? hl : Color.white;
        }
    }
}