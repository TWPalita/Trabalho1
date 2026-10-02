using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Representa um objeto espacial / detrito com um número associado no Sistema de Defesa.
/// Mantém o número posicionado diretamente abaixo do objeto enquanto ambos flutuam juntos,
/// garantindo visibilidade clara para o jogador na cabine do piloto em VR.
/// </summary>
public class Detrito : MonoBehaviour
{
    [Header("Exibição do Número")]
    [Tooltip("Distância vertical do número abaixo do centro do objeto")]
    [SerializeField] private float numberYOffset = 1.35f;
    [SerializeField] private TMP_Text textoDoNumero;
    [SerializeField] private Transform labelContainer;
    [SerializeField] private Image badgeBackground;

    [Header("Componentes")]
    [SerializeField] private Renderer meshRenderer;
    [SerializeField] private XRSimpleInteractable xrInteractable;

    [Header("Comportamento Espacial")]
    [SerializeField] private float floatSpeed = 1.0f;
    [SerializeField] private float floatAmplitude = 0.35f;
    [SerializeField] private float rotationSpeed = 25.0f;
    [SerializeField] private bool billboardText = true;

    [Header("Cores e Destaque VR")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color hoverTextColor = new Color(0.2f, 1f, 0.95f, 1f);
    [SerializeField] private Color badgeNormalColor = new Color(0.04f, 0.08f, 0.16f, 0.85f);
    [SerializeField] private Color badgeHoverColor = new Color(0.1f, 0.45f, 0.65f, 0.95f);
    [SerializeField] private Color hoverHighlightColor = new Color(0.3f, 1f, 0.9f, 1f);

    public int Number { get; private set; }
    public bool IsHovered { get; private set; } = false;
    public bool IsHit { get; private set; } = false;

    public void MarkHit() => IsHit = true;

    private Vector3 initialPosition;
    private float timeOffset;
    private GerenteDefesa manager;
    private Material targetMaterial;
    private Color originalMeshColor = Color.white;

    private void Awake()
    {
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<Renderer>();
        if (xrInteractable == null)
        {
            xrInteractable = GetComponent<XRSimpleInteractable>();
            if (xrInteractable == null)
            {
                xrInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            }
        }

        if (meshRenderer != null)
        {
            targetMaterial = meshRenderer.material;
            originalMeshColor = targetMaterial.color;
        }

        // Configura o colisor para cobrir tanto o objeto quanto a etiqueta numérica abaixo dele
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            col.center = new Vector3(0f, -numberYOffset * 0.5f, 0f);
            col.size = new Vector3(1.6f, 1.1f + numberYOffset, 1.6f);
        }

        SetupNumberLabel();
    }

    private void SetupNumberLabel()
    {
        // 1. Localiza ou cria o Canvas da etiqueta numérica
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("NumberCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObj.AddComponent<CanvasScaler>();
        }

        labelContainer = canvas.transform;

        RectTransform canvasRT = canvas.GetComponent<RectTransform>();
        if (canvasRT != null)
        {
            canvasRT.sizeDelta = new Vector2(140f, 75f);
            canvasRT.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            canvasRT.anchoredPosition = Vector2.zero;
            canvasRT.localPosition = new Vector3(0f, -numberYOffset, 0f);
        }

        // 2. Fundo/Placa escura de alto contraste para destacar contra estrelas e espaço
        Transform bgTransform = canvas.transform.Find("BadgeBackground");
        if (bgTransform == null)
        {
            GameObject badge = new GameObject("BadgeBackground");
            badge.transform.SetParent(canvas.transform, false);
            badge.transform.SetAsFirstSibling();
            badgeBackground = badge.AddComponent<Image>();
            badgeBackground.color = badgeNormalColor;

            RectTransform bgRT = badge.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            bgRT.anchoredPosition = Vector2.zero;
        }
        else
        {
            badgeBackground = bgTransform.GetComponent<Image>();
            if (badgeBackground != null) badgeBackground.color = badgeNormalColor;
        }

        // 3. Localiza e configura o TMP_Text
        if (textoDoNumero == null)
        {
            textoDoNumero = canvas.GetComponentInChildren<TMP_Text>();
        }

        if (textoDoNumero != null)
        {
            textoDoNumero.alignment = TextAlignmentOptions.Center;
            textoDoNumero.fontSize = 56;
            textoDoNumero.fontStyle = FontStyles.Bold;
            textoDoNumero.color = normalTextColor;

            RectTransform textRT = textoDoNumero.rectTransform;
            if (textRT != null)
            {
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.sizeDelta = Vector2.zero;
                textRT.anchoredPosition = Vector2.zero;
            }
        }
    }

    private void OnEnable()
    {
        if (xrInteractable != null)
        {
            xrInteractable.firstHoverEntered.AddListener(OnXRHoverEntered);
            xrInteractable.lastHoverExited.AddListener(OnXRHoverExited);
            xrInteractable.selectEntered.AddListener(OnXRSelected);
            xrInteractable.activated.AddListener(OnXRActivated);
        }
    }

    private void OnDisable()
    {
        if (xrInteractable != null)
        {
            xrInteractable.firstHoverEntered.RemoveListener(OnXRHoverEntered);
            xrInteractable.lastHoverExited.RemoveListener(OnXRHoverExited);
            xrInteractable.selectEntered.RemoveListener(OnXRSelected);
            xrInteractable.activated.RemoveListener(OnXRActivated);
        }
    }

    public void Init(int value, GerenteDefesa defManager)
    {
        Number = value;
        manager = defManager;
        initialPosition = transform.position;
        timeOffset = Random.Range(0f, 10f);

        if (textoDoNumero != null)
        {
            textoDoNumero.text = value.ToString();
        }
    }

    private void Update()
    {
        // Flutuação espacial do objeto (sobe e desce em seno)
        float yOffset = Mathf.Sin((Time.time + timeOffset) * floatSpeed) * floatAmplitude;
        transform.position = initialPosition + new Vector3(0f, yOffset, 0f);

        // Rotação sutil do detrito em torno do próprio eixo
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }

    private void LateUpdate()
    {
        // Garante que o número fique fixado diretamente abaixo do objeto,
        // flutuando junto com ele mas sem girar em órbita quando o detrito roda.
        if (labelContainer != null)
        {
            labelContainer.position = transform.position - Vector3.up * numberYOffset;

            // Mantém a etiqueta de número sempre voltada para os olhos do jogador na cabine VR
            if (billboardText)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    Vector3 lookDir = labelContainer.position - mainCam.transform.position;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        labelContainer.rotation = Quaternion.LookRotation(lookDir);
                    }
                }
            }
        }
    }

    private void OnXRHoverEntered(HoverEnterEventArgs args)
    {
        SetHighlight(true);
    }

    private void OnXRHoverExited(HoverExitEventArgs args)
    {
        SetHighlight(false);
    }

    private void OnXRSelected(SelectEnterEventArgs args)
    {
        TriggerHit();
    }

    private void OnXRActivated(ActivateEventArgs args)
    {
        TriggerHit();
    }

    private void TriggerHit()
    {
        if (IsHit) return;
        IsHit = true;

        if (manager != null)
        {
            manager.OnDebrisHit(this);
        }
    }

    public void SetHighlight(bool highlighted)
    {
        IsHovered = highlighted;

        if (targetMaterial != null)
        {
            targetMaterial.color = highlighted ? hoverHighlightColor : originalMeshColor;
            if (targetMaterial.HasProperty("_EmissionColor"))
            {
                targetMaterial.SetColor("_EmissionColor", highlighted ? hoverHighlightColor * 0.8f : Color.black);
                if (highlighted) targetMaterial.EnableKeyword("_EMISSION");
                else targetMaterial.DisableKeyword("_EMISSION");
            }
        }

        if (textoDoNumero != null)
        {
            textoDoNumero.color = highlighted ? hoverTextColor : normalTextColor;
        }

        if (badgeBackground != null)
        {
            badgeBackground.color = highlighted ? badgeHoverColor : badgeNormalColor;
        }
    }

    public void Explode(bool isCorrect)
    {
        IsHit = true;
        Destroy(gameObject);
    }
}

public class Debris : Detrito { }
