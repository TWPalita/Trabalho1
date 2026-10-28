using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Gerencia a narrativa inicial (Diário de Bordo ao acordar no quarto) e o
/// HUD de Objetivos fixo no canto superior esquerdo da tela, mantendo toda a
/// história da espaçonave amarrada sem abrir múltiplos Canvases bloqueando a visão.
/// </summary>
public class GerenteAlarme : MonoBehaviour
{
    public static GerenteAlarme Instance { get; private set; }

    public enum HologramTheme
    {
        InfoCyan,
        WarningAmber,
        SuccessGreen,
        AlertRed
    }

    [Header("Configurações de Tempo")]
    [Tooltip("Atraso em segundos após o início da cena antes de mostrar a mensagem narrativa inicial")]
    [SerializeField] private float initialMessageDelay = 0.35f;

    [Tooltip("Tempo em segundos após ativar o gerador antes do alarme de detritos disparar")]
    [SerializeField] private float explorationDelay = 0.5f;

    [Header("Textos da Narrativa Inicial (3 Etapas — Diário de Bordo)")]
    [SerializeField] private string diaryTitle = "DIÁRIO DE BORDO";

    [TextArea(2, 4)]
    [SerializeField] private string storyMessage1 = "Você está a bordo de uma nave espacial em uma longa jornada pela galáxia. Sua missão é atravessar regiões desconhecidas do espaço e chegar ao seu próximo destino.";

    [TextArea(2, 4)]
    [SerializeField] private string storyMessage2 = "Durante a viagem, a nave foi atacada por piratas espaciais. Embora você tenha conseguido sobreviver, alguns sistemas foram danificados e o funcionamento da embarcação ficou comprometido.";

    [TextArea(2, 4)]
    [SerializeField] private string storyMessage3 = "Após o incidente, você desperta em seu quarto. É necessário verificar o estado da nave antes de continuar a viagem. Vá até a cabine de comando e verifique o painel principal.";

    // Campos serializados antigos são mantidos para preservar os valores já salvos nas cenas.
#pragma warning disable CS0414
    [Header("Textos de Aviso do Painel Sem Energia")]
    [SerializeField] private string panelUnavailableTitle = "PAINEL SEM ENERGIA";

    [TextArea(2, 4)]
    [SerializeField] private string panelUnavailableMessage = "Objetivo: Verifique o gerador auxiliar na sala lateral.";

    [SerializeField] private string panelBlockedTitle = "PAINEL SEM ENERGIA";

    [TextArea(2, 4)]
    [SerializeField] private string panelBlockedMessage = "Objetivo: Abasteça e ligue o gerador auxiliar na sala lateral.";

    [Header("Textos do Gerador e Combustível")]
    [SerializeField] private string generatorEmptyTitle = "GERADOR SEM COMBUSTÍVEL";

    [TextArea(2, 4)]
    [SerializeField] private string generatorEmptyMessage = "Objetivo: Pegue o galão vermelho nesta sala e abasteça o gerador.";

    [SerializeField] private string fuelCollectedTitle = "COMBUSTÍVEL COLETADO";

    [TextArea(2, 4)]
    [SerializeField] private string fuelCollectedMessage = "Objetivo: Aproxime o galão vermelho do gerador para abastecer.";

    [SerializeField] private string refuelingCompleteTitle = "RESERVATÓRIO ABASTECIDO";

    [TextArea(2, 4)]
    [SerializeField] private string refuelingCompleteMessage = "Objetivo: Aperte o botão na mesa ao lado do gerador para ligá-lo.";

    [SerializeField] private string energyRestoredTitle = "ALERTA DE COLISÃO";

    [TextArea(3, 6)]
    [SerializeField] private string energyRestoredMessage = "Objetivo: Corra até a cabine e aperte o botão de defesa!";

    [Header("Textos do Alerta de Emergência")]
    [SerializeField] private float alertDuration = 5.0f;
    [SerializeField] private string alertTitle = "ALERTA";

    [TextArea(3, 6)]
    [SerializeField] private string alertMessage = "Detritos espaciais detectados! A nave está em rota de colisão.\nAtive o sistema de defesa imediatamente.";

    [Header("Texto do Objetivo do Jogador")]
    [SerializeField] private string objectiveTitle = "OBJETIVO";

    [TextArea(3, 6)]
    [SerializeField] private string objectiveMessage = "Corra até o painel de controle e ative o sistema de defesa.";

    [SerializeField] private string objectiveHint = "[ Aperte o botão na mesa no final da nave ]";
#pragma warning restore CS0414

    [Header("Controle de Luzes de Emergência")]
    [SerializeField] private LuzesEmergencia emergencyLightsController;

    [Header("Posicionamento Fixo do Objetivo (Legado)")]
    [SerializeField] private GameObject objectiveCanvasObject;
    [SerializeField] private Vector3 fixedObjectivePosition = new Vector3(-8.44f, 1.52f, -1.38f);
    [SerializeField] private Vector3 fixedObjectiveRotation = new Vector3(0f, 180f, 0f);

    [Header("Interface Holográfica Inicial em World Space (Diário no Quarto)")]
    [SerializeField] private bool showWorldSpaceNarrativeUI = true;
    [SerializeField] private GameObject narrativeCanvasObject;
    [SerializeField] private TMP_Text narrativeText;
    [SerializeField] private CanvasGroup narrativeCanvasGroup;

    [Header("Botões do Diário em World Space")]
    [SerializeField] private Button btnAvancar;
    [SerializeField] private Button btnVoltar;
    [SerializeField] private TMP_Text txtBtnAvancar;
    [SerializeField] private TMP_Text txtBtnVoltar;

    [Header("Textos dos Botões (Editáveis no Inspector)")]
    [SerializeField] private string textoBotaoAvancar = "AVANÇAR  >>";
    [SerializeField] private string textoBotaoVoltar = "<<  VOLTAR";
    [SerializeField] private string textoBotaoConcluir = "EXPLORAR NAVE  >>";
    [SerializeField] private string textoBotaoAlerta = "ENTENDIDO";

    // Estado da história amarrada
    private bool isAlarmActive = false;
    private bool isGeneratorPowered = false;
    private bool isFuelCollected = false;
    private bool isGeneratorRefueled = false;
    private bool isReadingDiary = true;
    private bool isShowingPanelNotice = false;
    private int currentStoryPage = 0;

    private Coroutine emergencyRoutine;
    private Coroutine transitionRoutine;
    private Coroutine cornerPulseRoutine;

    // Referências do Canvas inicial (Diário de Bordo)
    private Image hologramBgImage;
    private Image hologramLineImage;
    private Image hologramOuterBorderImage;
    private Image hologramHeaderGlowImage;
    private TMP_Text hologramTitleText;
    private TMP_Text hologramFooterText;

    // Referências do HUD de Objetivos no Canto da Tela
    private GameObject cornerHudCanvasObj;
    private CanvasGroup cornerHudCanvasGroup;
    private RectTransform cornerHudPanelRt;
    private Image cornerHudBgImage;
    private Image cornerHudDividerImage;
    private readonly Image[] cornerHudStepSegments = new Image[4];
    private TMP_Text cornerHudHeaderText;
    private TMP_Text cornerHudObjectiveText;
    private TMP_Text cornerHudContextText;
    private Material cornerHudMainCardMaterial;
    private Material cornerHudElementMaterial;
    private Material cornerHudDividerMaterial;
    private Material cornerHudTextMaterial;
    private readonly List<Material> cornerHudRuntimeMaterials = new List<Material>();

    private static readonly int PropCardMode = Shader.PropertyToID("_CardMode");
    private static readonly int PropDepthTest = Shader.PropertyToID("_ZTest");
    private static readonly int PropAccentColor = Shader.PropertyToID("_HoloHudAccentColor");
    private static readonly int PropSecondaryColor = Shader.PropertyToID("_HoloHudSecondaryColor");
    private static readonly int PropBgLeftColor = Shader.PropertyToID("_HoloHudBgLeftColor");
    private static readonly int PropBgRightColor = Shader.PropertyToID("_HoloHudBgRightColor");
    private static readonly int PropDissolve = Shader.PropertyToID("_HoloHudDissolve");

    private float lastPageChangeTime = -1f;
    private const float PageChangeCooldown = 0.22f;
    private bool isTransitioningPage = false;
    private bool wasAdvanceInputPressed = false;
    private bool wasBackInputPressed = false;
    private readonly List<UnityEngine.XR.InputDevice> cachedInputDevices = new List<UnityEngine.XR.InputDevice>();
    private readonly Vector3 baseCanvasScale = new Vector3(0.0024f, 0.0024f, 0.0024f);
    private readonly Vector3 cornerHudScale = new Vector3(0.00050f, 0.00050f, 0.00050f);

    public bool IsAlarmActive => isAlarmActive;
    public bool IsGeneratorPowered => isGeneratorPowered;

    private void OnValidate()
    {
        MigrateDefaultTextsIfNeeded();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        MigrateDefaultTextsIfNeeded();
        EnsureEmergencyLightsController();
        EnsureWorldSpaceUI();
        EnsureButtonsUI();
        EnsureCornerObjectiveHUD();
    }

    private void MigrateDefaultTextsIfNeeded()
    {
        if (string.IsNullOrEmpty(diaryTitle))
            diaryTitle = "DIÁRIO DE BORDO";

        if (string.IsNullOrEmpty(storyMessage1) || storyMessage1.Contains("viajante espacial"))
            storyMessage1 = "Você está a bordo de uma nave espacial em uma longa jornada pela galáxia. Sua missão é atravessar regiões desconhecidas do espaço e chegar ao seu próximo destino.";

        if (string.IsNullOrEmpty(storyMessage2) || storyMessage2.Contains("segue tranquilamente"))
            storyMessage2 = "Durante a viagem, a nave foi atacada por piratas espaciais. Embora você tenha conseguido sobreviver, alguns sistemas foram danificados e o funcionamento da embarcação ficou comprometido.";

        if (string.IsNullOrEmpty(storyMessage3) || storyMessage3.Contains("prepare-se para continuar"))
            storyMessage3 = "Após o incidente, você desperta em seu quarto. É necessário verificar o estado da nave antes de continuar a viagem. Vá até a cabine de comando e verifique o painel principal.";

        if (string.IsNullOrEmpty(textoBotaoAvancar) || textoBotaoAvancar == "Avançar" || textoBotaoAvancar == "AVANÇAR" || textoBotaoAvancar.Contains("▶"))
            textoBotaoAvancar = "AVANÇAR  >>";

        if (string.IsNullOrEmpty(textoBotaoVoltar) || textoBotaoVoltar == "Voltar" || textoBotaoVoltar == "VOLTAR" || textoBotaoVoltar.Contains("◀"))
            textoBotaoVoltar = "<<  VOLTAR";

        if (string.IsNullOrEmpty(textoBotaoConcluir) || textoBotaoConcluir == "Explorar Nave" || textoBotaoConcluir == "INICIAR EXPLORAÇÃO" || textoBotaoConcluir.Contains("▶"))
            textoBotaoConcluir = "EXPLORAR NAVE  >>";

        if (string.IsNullOrEmpty(textoBotaoAlerta) || textoBotaoAlerta == "ENTENDIDO" || textoBotaoAlerta.Contains("✓"))
            textoBotaoAlerta = "ENTENDIDO";
    }

    private void EnsureEmergencyLightsController()
    {
        if (emergencyLightsController == null)
        {
            emergencyLightsController = GetComponent<LuzesEmergencia>();
            if (emergencyLightsController == null)
            {
                emergencyLightsController = gameObject.AddComponent<LuzesEmergencia>();
            }
        }

        emergencyLightsController.ResetToNormal();
    }

    // =========================================================================
    // 1. HUD DE OBJETIVOS NO CANTO DA TELA (NÃO BLOQUEIA A VISÃO)
    // =========================================================================

    private void EnsureCornerObjectiveHUD()
    {
        if (cornerHudCanvasObj != null) return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = GameObject.FindWithTag("MainCamera");
            if (camObj != null) cam = camObj.GetComponent<Camera>();
        }

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        EnsureCornerHudMaterials();
        EnsureCornerHudTextMaterial(font);

        cornerHudCanvasObj = new GameObject("CanvasHudObjetivoCanto");
        if (cam != null)
        {
            cornerHudCanvasObj.transform.SetParent(cam.transform, false);
        }

        Canvas canvas = cornerHudCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 120;
        canvas.worldCamera = cam;

        CanvasScaler scaler = cornerHudCanvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3.0f;

        RectTransform rootRt = cornerHudCanvasObj.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(640f, 230f);
        rootRt.localScale = cornerHudScale;

        cornerHudCanvasGroup = cornerHudCanvasObj.AddComponent<CanvasGroup>();
        cornerHudCanvasGroup.alpha = 0f;
        cornerHudCanvasGroup.interactable = false;
        cornerHudCanvasGroup.blocksRaycasts = false;

        // Painel container animado
        GameObject panelObj = new GameObject("PainelCanto", typeof(RectTransform));
        panelObj.transform.SetParent(cornerHudCanvasObj.transform, false);
        cornerHudPanelRt = panelObj.GetComponent<RectTransform>();
        cornerHudPanelRt.anchorMin = Vector2.zero;
        cornerHudPanelRt.anchorMax = Vector2.one;
        cornerHudPanelRt.offsetMin = Vector2.zero;
        cornerHudPanelRt.offsetMax = Vector2.zero;

        // Fundo holográfico SDF com degradê e bordas neon, igual ao HUD da DefenseScene.
        GameObject bgObj = new GameObject("FundoHologramaSDF", typeof(RectTransform));
        bgObj.transform.SetParent(panelObj.transform, false);
        cornerHudBgImage = bgObj.AddComponent<Image>();
        cornerHudBgImage.material = cornerHudMainCardMaterial;
        cornerHudBgImage.color = Color.white;
        cornerHudBgImage.raycastTarget = false;
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // Cabeçalho da etapa (ex: "OBJETIVO ATUAL [1/4]")
        GameObject headerObj = new GameObject("TituloHudCanto", typeof(RectTransform));
        headerObj.transform.SetParent(panelObj.transform, false);
        cornerHudHeaderText = headerObj.AddComponent<TextMeshProUGUI>();
        if (font != null) cornerHudHeaderText.font = font;
        if (cornerHudTextMaterial != null) cornerHudHeaderText.fontSharedMaterial = cornerHudTextMaterial;
        cornerHudHeaderText.fontSize = 22f;
        cornerHudHeaderText.fontStyle = FontStyles.Bold;
        cornerHudHeaderText.characterSpacing = 2.5f;
        cornerHudHeaderText.alignment = TextAlignmentOptions.MidlineLeft;
        cornerHudHeaderText.color = Color.white;
        cornerHudHeaderText.raycastTarget = false;
        RectTransform headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 0.72f);
        headerRt.anchorMax = new Vector2(0.74f, 0.96f);
        headerRt.offsetMin = new Vector2(36f, 0f);
        headerRt.offsetMax = new Vector2(-8f, 0f);

        // Barra visual de progresso em 4 segmentos no canto superior direito do card
        for (int i = 0; i < 4; i++)
        {
            GameObject segObj = new GameObject($"ProgressoEtapa_{i + 1}", typeof(RectTransform));
            segObj.transform.SetParent(panelObj.transform, false);
            Image segImg = segObj.AddComponent<Image>();
            segImg.material = cornerHudElementMaterial;
            segImg.color = new Color(0.22f, 0.78f, 0.98f, 0.25f);
            segImg.raycastTarget = false;
            RectTransform segRt = segObj.GetComponent<RectTransform>();
            segRt.anchorMin = new Vector2(1f, 0.84f);
            segRt.anchorMax = new Vector2(1f, 0.84f);
            segRt.pivot = new Vector2(1f, 0.5f);
            float rightOffset = -18f - ((3 - i) * 34f);
            segRt.anchoredPosition = new Vector2(rightOffset, 0f);
            segRt.sizeDelta = new Vector2(28f, 10f);
            cornerHudStepSegments[i] = segImg;
        }

        // Linha divisória sutil abaixo do cabeçalho
        GameObject divObj = new GameObject("LinhaDivisoriaHud", typeof(RectTransform));
        divObj.transform.SetParent(panelObj.transform, false);
        cornerHudDividerImage = divObj.AddComponent<Image>();
        cornerHudDividerImage.material = cornerHudDividerMaterial;
        cornerHudDividerImage.color = new Color(1f, 1f, 1f, 0.65f);
        cornerHudDividerImage.raycastTarget = false;
        RectTransform divRt = divObj.GetComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0f, 0.70f);
        divRt.anchorMax = new Vector2(1f, 0.708f);
        divRt.offsetMin = new Vector2(24f, 0f);
        divRt.offsetMax = new Vector2(-18f, 0f);

        // Texto do Objetivo Principal (ex: "Objetivo: Verifique o gerador auxiliar.")
        GameObject objTextGO = new GameObject("TextoObjetivoPrincipal", typeof(RectTransform));
        objTextGO.transform.SetParent(panelObj.transform, false);
        cornerHudObjectiveText = objTextGO.AddComponent<TextMeshProUGUI>();
        if (font != null) cornerHudObjectiveText.font = font;
        if (cornerHudTextMaterial != null) cornerHudObjectiveText.fontSharedMaterial = cornerHudTextMaterial;
        cornerHudObjectiveText.fontSize = 27.5f;
        cornerHudObjectiveText.fontStyle = FontStyles.Bold;
        cornerHudObjectiveText.alignment = TextAlignmentOptions.MidlineLeft;
        cornerHudObjectiveText.textWrappingMode = TextWrappingModes.Normal;
        cornerHudObjectiveText.color = Color.white;
        cornerHudObjectiveText.raycastTarget = false;
        RectTransform objTextRt = objTextGO.GetComponent<RectTransform>();
        objTextRt.anchorMin = new Vector2(0f, 0.34f);
        objTextRt.anchorMax = new Vector2(1f, 0.69f);
        objTextRt.offsetMin = new Vector2(36f, 0f);
        objTextRt.offsetMax = new Vector2(-18f, 0f);

        // Subtexto de contexto da história
        GameObject ctxTextGO = new GameObject("TextoContextoHistoria", typeof(RectTransform));
        ctxTextGO.transform.SetParent(panelObj.transform, false);
        cornerHudContextText = ctxTextGO.AddComponent<TextMeshProUGUI>();
        if (font != null) cornerHudContextText.font = font;
        if (cornerHudTextMaterial != null) cornerHudContextText.fontSharedMaterial = cornerHudTextMaterial;
        cornerHudContextText.fontSize = 21f;
        cornerHudContextText.alignment = TextAlignmentOptions.MidlineLeft;
        cornerHudContextText.textWrappingMode = TextWrappingModes.Normal;
        cornerHudContextText.color = new Color(0.48f, 0.82f, 0.98f, 0.96f);
        cornerHudContextText.raycastTarget = false;
        RectTransform ctxTextRt = ctxTextGO.GetComponent<RectTransform>();
        ctxTextRt.anchorMin = new Vector2(0f, 0.05f);
        ctxTextRt.anchorMax = new Vector2(1f, 0.35f);
        ctxTextRt.offsetMin = new Vector2(36f, 0f);
        ctxTextRt.offsetMax = new Vector2(-18f, 0f);

        UpdateCornerHudPlacement();
        cornerHudCanvasObj.SetActive(false);
    }

    private void EnsureCornerHudMaterials()
    {
        Shader cardShader = Shader.Find("Custom/HologramHUDCard") ?? Shader.Find("UI/Default");
        if (cardShader == null) return;

        cornerHudMainCardMaterial = new Material(cardShader) { name = "SampleHUD_MainCard_Mat" };
        cornerHudElementMaterial = new Material(cardShader) { name = "SampleHUD_Element_Mat" };
        cornerHudDividerMaterial = new Material(cardShader) { name = "SampleHUD_Divider_Mat" };
        cornerHudRuntimeMaterials.Add(cornerHudMainCardMaterial);
        cornerHudRuntimeMaterials.Add(cornerHudElementMaterial);
        cornerHudRuntimeMaterials.Add(cornerHudDividerMaterial);

        cornerHudMainCardMaterial.SetFloat(PropCardMode, 0f);
        cornerHudElementMaterial.SetFloat(PropCardMode, 1f);
        cornerHudDividerMaterial.SetFloat(PropCardMode, 2f);
        SetCornerHudDepthTest(cornerHudMainCardMaterial);
        SetCornerHudDepthTest(cornerHudElementMaterial);
        SetCornerHudDepthTest(cornerHudDividerMaterial);
    }

    private static void SetCornerHudDepthTest(Material material)
    {
        if (material != null && material.HasProperty(PropDepthTest))
        {
            material.SetFloat(PropDepthTest, (float)UnityEngine.Rendering.CompareFunction.Always);
        }
    }

    private void EnsureCornerHudTextMaterial(TMP_FontAsset font)
    {
        if (font == null || font.material == null) return;

        Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");
        if (overlayShader == null)
        {
            Debug.LogError("Shader TextMeshPro/Distance Field Overlay não foi encontrado; o HUD poderá ser ocultado por paredes.", this);
            return;
        }

        cornerHudTextMaterial = new Material(font.material)
        {
            name = "SampleHUD_Text_Overlay_Mat",
            shader = overlayShader
        };
        cornerHudRuntimeMaterials.Add(cornerHudTextMaterial);
    }

    private void UpdateCornerHudPlacement()
    {
        if (cornerHudCanvasObj == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        if (cornerHudCanvasObj.transform.parent != cam.transform)
        {
            cornerHudCanvasObj.transform.SetParent(cam.transform, false);
        }

        const float depth = 0.48f;
        float halfH = depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfW = halfH * cam.aspect;

        // Dimensiona automaticamente para ocupar ~28% da altura da tela (grande e super legível!)
        float targetWorldHeight = UnityEngine.XR.XRSettings.isDeviceActive ? (halfH * 0.46f) : (halfH * 0.56f);
        float dynamicScale = targetWorldHeight / 230f;

        float cardHalfW = (640f * dynamicScale) * 0.5f;
        float cardHalfH = (230f * dynamicScale) * 0.5f;

        float localX;
        float localY;

        if (UnityEngine.XR.XRSettings.isDeviceActive)
        {
            // No headset VR, posiciona bem visível no quadrante superior esquerdo
            localX = Mathf.Clamp(-halfW * 0.46f, -0.26f, -0.12f);
            localY = Mathf.Clamp(halfH * 0.46f, 0.10f, 0.19f);
        }
        else
        {
            // Na tela (Editor / XR Simulator), fixa grande e legível no canto superior esquerdo
            localX = -halfW + cardHalfW + (halfW * 0.035f);
            localY = halfH - cardHalfH - (halfH * 0.045f);
        }

        cornerHudCanvasObj.transform.localPosition = new Vector3(localX, localY, depth);
        cornerHudCanvasObj.transform.localRotation = Quaternion.Euler(2f, -4f, 0f);
        cornerHudCanvasObj.transform.localScale = new Vector3(dynamicScale, dynamicScale, dynamicScale);
    }

    /// <summary>
    /// Atualiza o HUD de Objetivos no canto da tela com animação suave, sem abrir Canvases extras no mundo.
    /// </summary>
    public void AtualizarObjetivoNoCanto(string header, string mainObjective, string narrativeContext, HologramTheme theme)
    {
        EnsureCornerObjectiveHUD();
        if (cornerHudCanvasObj == null) return;

        // Garante que qualquer canvas legado de objetivo no mundo fique oculto
        if (objectiveCanvasObject != null && objectiveCanvasObject != narrativeCanvasObject)
        {
            objectiveCanvasObject.SetActive(false);
        }

        // Se o Diário de Bordo ainda estava aberto porque o jogador saiu andando e interagiu com algo, fecha o Diário
        if (isReadingDiary)
        {
            isReadingDiary = false;
            if (narrativeCanvasObject != null)
            {
                narrativeCanvasObject.SetActive(false);
            }
        }

        Color accentColor = theme switch
        {
            HologramTheme.WarningAmber => new Color(0.98f, 0.75f, 0.14f, 1f),
            HologramTheme.SuccessGreen => new Color(0.20f, 0.86f, 0.55f, 1f),
            HologramTheme.AlertRed     => new Color(0.98f, 0.32f, 0.32f, 1f),
            _                          => new Color(0.22f, 0.78f, 0.98f, 1f)
        };

        Color bgColor = theme switch
        {
            HologramTheme.WarningAmber => new Color(0.11f, 0.07f, 0.02f, 0.92f),
            HologramTheme.SuccessGreen => new Color(0.02f, 0.10f, 0.07f, 0.92f),
            HologramTheme.AlertRed     => new Color(0.14f, 0.03f, 0.04f, 0.93f),
            _                          => new Color(0.02f, 0.05f, 0.12f, 0.90f)
        };

        Color secondaryColor = new Color(
            accentColor.r * 0.55f,
            accentColor.g * 0.65f,
            Mathf.Min(1f, accentColor.b + 0.2f),
            1f);
        Color bgRightColor = new Color(
            bgColor.r * 1.8f + 0.02f,
            bgColor.g * 2.1f + 0.05f,
            bgColor.b * 2f + 0.08f,
            0.56f);

        Shader.SetGlobalColor(PropAccentColor, accentColor);
        Shader.SetGlobalColor(PropSecondaryColor, secondaryColor);
        Shader.SetGlobalColor(PropBgLeftColor, bgColor);
        Shader.SetGlobalColor(PropBgRightColor, bgRightColor);
        Shader.SetGlobalFloat(PropDissolve, 0f);

        if (cornerHudMainCardMaterial != null)
        {
            cornerHudMainCardMaterial.SetColor("_AccentColor", accentColor);
            cornerHudMainCardMaterial.SetColor("_SecondaryColor", secondaryColor);
            cornerHudMainCardMaterial.SetColor("_BgLeftColor", bgColor);
            cornerHudMainCardMaterial.SetColor("_BgRightColor", bgRightColor);
        }
        if (cornerHudElementMaterial != null)
        {
            cornerHudElementMaterial.SetColor("_AccentColor", accentColor);
            cornerHudElementMaterial.SetColor("_SecondaryColor", secondaryColor);
        }
        if (cornerHudDividerMaterial != null)
        {
            cornerHudDividerMaterial.SetColor("_AccentColor", accentColor);
            cornerHudDividerMaterial.SetColor("_SecondaryColor", secondaryColor);
        }

        if (cornerHudHeaderText != null)
        {
            cornerHudHeaderText.color = Color.white;
            cornerHudHeaderText.text = header;
        }

        // Atualiza os 4 segmentos visuais de progresso conforme a etapa atual ([1/4] .. [4/4])
        int activeSteps = 1;
        if (header != null)
        {
            if (header.Contains("4/4")) activeSteps = 4;
            else if (header.Contains("3/4")) activeSteps = 3;
            else if (header.Contains("2/4")) activeSteps = 2;
        }

        for (int i = 0; i < cornerHudStepSegments.Length; i++)
        {
            if (cornerHudStepSegments[i] != null)
            {
                cornerHudStepSegments[i].color = (i < activeSteps)
                    ? accentColor
                    : new Color(0.35f, 0.45f, 0.60f, 0.25f);
            }
        }

        if (cornerHudObjectiveText != null)
        {
            cornerHudObjectiveText.text = mainObjective;
        }

        if (cornerHudContextText != null)
        {
            cornerHudContextText.text = narrativeContext ?? "";
            cornerHudContextText.gameObject.SetActive(!string.IsNullOrEmpty(narrativeContext));
        }

        UpdateCornerHudPlacement();
        cornerHudCanvasObj.SetActive(true);

        if (cornerPulseRoutine != null) StopCoroutine(cornerPulseRoutine);
        cornerPulseRoutine = StartCoroutine(AnimateCornerHudUpdate());
    }

    private IEnumerator AnimateCornerHudUpdate()
    {
        if (cornerHudCanvasGroup == null || cornerHudPanelRt == null) yield break;

        float duration = 0.26f;
        float elapsed = 0f;
        float startAlpha = Mathf.Max(0.4f, cornerHudCanvasGroup.alpha);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.075f;

            cornerHudCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
            cornerHudPanelRt.localScale = new Vector3(pulse, pulse, 1f);
            yield return null;
        }

        cornerHudCanvasGroup.alpha = 1f;
        cornerHudPanelRt.localScale = Vector3.one;
    }

    // =========================================================================
    // 2. DIÁRIO DE BORDO INICIAL (ÚNICO CANVAS NO QUARTO AO ACORDAR)
    // =========================================================================

    private void EnsureWorldSpaceUI()
    {
        if (!showWorldSpaceNarrativeUI)
        {
            if (narrativeCanvasObject != null)
            {
                narrativeCanvasObject.SetActive(false);
            }
            return;
        }

        if (narrativeCanvasObject == null)
        {
            narrativeCanvasObject = GameObject.Find("CanvasNarrativa") ?? GameObject.Find("ShipWorldSpaceNarrativeCanvas");
        }

        if (narrativeCanvasObject == null)
        {
            narrativeCanvasObject = new GameObject("CanvasNarrativa");
            narrativeCanvasObject.transform.SetParent(null);
            narrativeCanvasObject.transform.position = new Vector3(-3.57f, 1.42f, -6.80f);
            narrativeCanvasObject.transform.rotation = Quaternion.identity;
        }

        narrativeCanvasObject.SetActive(true);
        UpgradeAndConfigureCanvas(narrativeCanvasObject);
    }

    private void UpgradeAndConfigureCanvas(GameObject canvasObj)
    {
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;
        canvas.additionalShaderChannels |=
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.Normal |
            AdditionalCanvasShaderChannels.Tangent;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 2.5f;

        if (canvasObj.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        if (canvasObj.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
        {
            canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        RectTransform rootRt = canvasObj.GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.sizeDelta = new Vector2(880f, 440f);
            canvasObj.transform.localScale = baseCanvasScale;
        }

        narrativeCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
        if (narrativeCanvasGroup == null)
        {
            narrativeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        }
        narrativeCanvasGroup.interactable = true;
        narrativeCanvasGroup.blocksRaycasts = true;

        TMP_FontAsset defaultFont = null;
        if (narrativeText != null && narrativeText.font != null)
        {
            defaultFont = narrativeText.font;
        }
        else
        {
            defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        Transform bgTr = canvasObj.transform.Find("FundoHolograma");
        GameObject bgObj = bgTr != null ? bgTr.gameObject : new GameObject("FundoHolograma", typeof(RectTransform));
        if (bgTr == null) bgObj.transform.SetParent(canvasObj.transform, false);
        hologramBgImage = bgObj.GetComponent<Image>();
        if (hologramBgImage == null) hologramBgImage = bgObj.AddComponent<Image>();
        hologramBgImage.color = new Color(0.02f, 0.05f, 0.12f, 0.95f);
        hologramBgImage.raycastTarget = false;
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bgObj.transform.SetAsFirstSibling();

        Transform outerTr = canvasObj.transform.Find("BordaExternaHolograma");
        GameObject outerObj = outerTr != null ? outerTr.gameObject : new GameObject("BordaExternaHolograma", typeof(RectTransform));
        if (outerTr == null) outerObj.transform.SetParent(canvasObj.transform, false);
        hologramOuterBorderImage = outerObj.GetComponent<Image>();
        if (hologramOuterBorderImage == null) hologramOuterBorderImage = outerObj.AddComponent<Image>();
        hologramOuterBorderImage.color = new Color(0.18f, 0.72f, 0.96f, 0.35f);
        hologramOuterBorderImage.raycastTarget = false;
        RectTransform outerRt = outerObj.GetComponent<RectTransform>();
        outerRt.anchorMin = Vector2.zero;
        outerRt.anchorMax = Vector2.one;
        outerRt.offsetMin = new Vector2(-6f, -6f);
        outerRt.offsetMax = new Vector2(6f, 6f);
        outerObj.transform.SetAsFirstSibling();

        Transform glowTr = canvasObj.transform.Find("FaixaTopoHolograma");
        GameObject glowObj = glowTr != null ? glowTr.gameObject : new GameObject("FaixaTopoHolograma", typeof(RectTransform));
        if (glowTr == null) glowObj.transform.SetParent(canvasObj.transform, false);
        hologramHeaderGlowImage = glowObj.GetComponent<Image>();
        if (hologramHeaderGlowImage == null) hologramHeaderGlowImage = glowObj.AddComponent<Image>();
        hologramHeaderGlowImage.color = new Color(0.12f, 0.45f, 0.72f, 0.25f);
        hologramHeaderGlowImage.raycastTarget = false;
        RectTransform glowRt = glowObj.GetComponent<RectTransform>();
        glowRt.anchorMin = new Vector2(0f, 0.80f);
        glowRt.anchorMax = new Vector2(1f, 1f);
        glowRt.offsetMin = Vector2.zero;
        glowRt.offsetMax = Vector2.zero;

        Transform lineTr = canvasObj.transform.Find("LinhaHolograma");
        GameObject lineObj = lineTr != null ? lineTr.gameObject : new GameObject("LinhaHolograma", typeof(RectTransform));
        if (lineTr == null) lineObj.transform.SetParent(canvasObj.transform, false);
        hologramLineImage = lineObj.GetComponent<Image>();
        if (hologramLineImage == null) hologramLineImage = lineObj.AddComponent<Image>();
        hologramLineImage.color = new Color(0.22f, 0.78f, 0.98f, 0.9f);
        hologramLineImage.raycastTarget = false;
        RectTransform lineRt = lineObj.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.06f, 0.795f);
        lineRt.anchorMax = new Vector2(0.94f, 0.805f);
        lineRt.offsetMin = Vector2.zero;
        lineRt.offsetMax = Vector2.zero;

        Transform titleTr = canvasObj.transform.Find("TituloHolograma");
        GameObject titleObj = titleTr != null ? titleTr.gameObject : new GameObject("TituloHolograma", typeof(RectTransform));
        if (titleTr == null) titleObj.transform.SetParent(canvasObj.transform, false);
        hologramTitleText = titleObj.GetComponent<TMP_Text>();
        if (hologramTitleText == null) hologramTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (defaultFont != null) hologramTitleText.font = defaultFont;
        hologramTitleText.fontSize = 27f;
        hologramTitleText.fontStyle = FontStyles.Bold;
        hologramTitleText.characterSpacing = 4f;
        hologramTitleText.alignment = TextAlignmentOptions.Center;
        hologramTitleText.color = new Color(0.22f, 0.78f, 0.98f, 1f);
        hologramTitleText.raycastTarget = false;
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.06f, 0.81f);
        titleRt.anchorMax = new Vector2(0.94f, 0.97f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        if (narrativeText == null)
        {
            Transform bodyTr = canvasObj.transform.Find("TextoNarrativa");
            if (bodyTr != null) narrativeText = bodyTr.GetComponent<TMP_Text>();
            if (narrativeText == null) narrativeText = canvasObj.GetComponentInChildren<TMP_Text>();
        }

        if (narrativeText != null)
        {
            if (defaultFont != null && narrativeText.font == null) narrativeText.font = defaultFont;
            narrativeText.fontSize = 22.5f;
            narrativeText.lineSpacing = 8f;
            narrativeText.alignment = TextAlignmentOptions.Center;
            narrativeText.textWrappingMode = TextWrappingModes.Normal;
            narrativeText.overflowMode = TextOverflowModes.Overflow;
            narrativeText.color = new Color(0.95f, 0.97f, 1f, 1f);
            narrativeText.raycastTarget = false;

            RectTransform bodyRt = narrativeText.GetComponent<RectTransform>();
            if (bodyRt != null)
            {
                bodyRt.anchorMin = new Vector2(0.07f, 0.28f);
                bodyRt.anchorMax = new Vector2(0.93f, 0.76f);
                bodyRt.anchoredPosition = Vector2.zero;
                bodyRt.sizeDelta = Vector2.zero;
            }
        }

        Transform footerTr = canvasObj.transform.Find("RodapeStatusHolograma");
        GameObject footerObj = footerTr != null ? footerTr.gameObject : new GameObject("RodapeStatusHolograma", typeof(RectTransform));
        if (footerTr == null) footerObj.transform.SetParent(canvasObj.transform, false);
        hologramFooterText = footerObj.GetComponent<TMP_Text>();
        if (hologramFooterText == null) hologramFooterText = footerObj.AddComponent<TextMeshProUGUI>();
        if (defaultFont != null) hologramFooterText.font = defaultFont;
        hologramFooterText.fontSize = 16f;
        hologramFooterText.fontStyle = FontStyles.Bold;
        hologramFooterText.characterSpacing = 3f;
        hologramFooterText.alignment = TextAlignmentOptions.Center;
        hologramFooterText.color = new Color(0.58f, 0.68f, 0.82f, 0.9f);
        hologramFooterText.raycastTarget = false;
        RectTransform footerRt = footerObj.GetComponent<RectTransform>();
        footerRt.anchorMin = new Vector2(0.08f, 0.19f);
        footerRt.anchorMax = new Vector2(0.92f, 0.27f);
        footerRt.offsetMin = Vector2.zero;
        footerRt.offsetMax = Vector2.zero;
    }

    private void EnsureButtonsUI()
    {
        if (narrativeCanvasObject == null) return;

        Transform canvasTransform = narrativeCanvasObject.transform;

        if (btnVoltar == null)
        {
            Transform voltarTr = canvasTransform.Find("BtnVoltar");
            if (voltarTr != null) btnVoltar = voltarTr.GetComponent<Button>();
        }
        if (btnVoltar == null)
        {
            btnVoltar = CreateHologramButton(
                canvasTransform,
                "BtnVoltar",
                textoBotaoVoltar,
                new Vector2(-195f, -155f),
                new Vector2(230f, 54f),
                out txtBtnVoltar
            );
        }
        else
        {
            if (txtBtnVoltar == null) txtBtnVoltar = btnVoltar.GetComponentInChildren<TMP_Text>();
            UpgradeExistingButton(btnVoltar, new Vector2(-195f, -155f), new Vector2(230f, 54f));
        }

        if (btnAvancar == null)
        {
            Transform avancarTr = canvasTransform.Find("BtnAvancar");
            if (avancarTr != null) btnAvancar = avancarTr.GetComponent<Button>();
        }
        if (btnAvancar == null)
        {
            btnAvancar = CreateHologramButton(
                canvasTransform,
                "BtnAvancar",
                textoBotaoAvancar,
                new Vector2(185f, -155f),
                new Vector2(280f, 54f),
                out txtBtnAvancar
            );
        }
        else
        {
            if (txtBtnAvancar == null) txtBtnAvancar = btnAvancar.GetComponentInChildren<TMP_Text>();
            UpgradeExistingButton(btnAvancar, new Vector2(185f, -155f), new Vector2(280f, 54f));
        }
    }

    private void UpgradeExistingButton(Button btn, Vector2 anchoredPos, Vector2 size)
    {
        if (btn == null) return;

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = true;
            img.color = new Color(0.08f, 0.36f, 0.62f, 0.95f);
        }

        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.09f, 0.38f, 0.66f, 0.95f);
        cb.highlightedColor = new Color(0.20f, 0.76f, 0.98f, 1f);
        cb.pressedColor = new Color(0.05f, 0.22f, 0.42f, 1f);
        cb.selectedColor = new Color(0.16f, 0.62f, 0.88f, 1f);
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        BoxCollider col = btn.GetComponent<BoxCollider>();
        if (col == null) col = btn.gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(size.x, size.y, 24f);
        col.center = Vector3.zero;

        if (btn.GetComponent<XRSimpleInteractable>() == null)
        {
            btn.gameObject.AddComponent<XRSimpleInteractable>();
        }

        TMP_Text txt = btn.GetComponentInChildren<TMP_Text>();
        if (txt != null)
        {
            txt.fontSize = 20f;
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 2f;
            txt.raycastTarget = false;
        }
    }

    private Button CreateHologramButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPos,
        Vector2 size,
        out TMP_Text labelText)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.09f, 0.38f, 0.66f, 0.95f);
        img.raycastTarget = true;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject txtObj = new GameObject("Texto" + name);
        txtObj.transform.SetParent(btnObj.transform, false);

        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        labelText = txtObj.AddComponent<TextMeshProUGUI>();
        if (narrativeText != null && narrativeText.font != null)
        {
            labelText.font = narrativeText.font;
        }
        labelText.text = label;
        labelText.fontSize = 20f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.characterSpacing = 2f;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.raycastTarget = false;

        UpgradeExistingButton(btn, anchoredPos, size);
        return btn;
    }

    private void SetButtonListener(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);

        XRSimpleInteractable xri = btn.GetComponent<XRSimpleInteractable>();
        if (xri == null) xri = btn.gameObject.AddComponent<XRSimpleInteractable>();

        xri.selectEntered.RemoveAllListeners();
        xri.activated.RemoveAllListeners();
        xri.selectEntered.AddListener(_ => action.Invoke());
        xri.activated.AddListener(_ => action.Invoke());
    }

    private void Start()
    {
        if (showWorldSpaceNarrativeUI)
        {
            StartCoroutine(InitialNarrativeRoutine());
        }
        else
        {
            isReadingDiary = false;
            MostrarObjetivoInicialNoCanto();
        }
    }

    private void AlignHologramWithPlayer(float distanceMeters = 1.65f)
    {
        if (narrativeCanvasObject == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 targetPos = cam.transform.position + forward * distanceMeters;
        targetPos.y = Mathf.Clamp(cam.transform.position.y - 0.04f, 1.18f, 1.65f);

        narrativeCanvasObject.transform.position = targetPos;
        narrativeCanvasObject.transform.rotation = Quaternion.LookRotation(forward);
        narrativeCanvasObject.transform.localScale = baseCanvasScale;
    }

    private IEnumerator InitialNarrativeRoutine()
    {
        if (narrativeCanvasObject != null)
        {
            narrativeCanvasObject.SetActive(true);
            if (narrativeCanvasGroup != null) narrativeCanvasGroup.alpha = 0f;

            yield return new WaitForSeconds(initialMessageDelay);

            isReadingDiary = true;
            currentStoryPage = 0;
            AlignHologramWithPlayer(1.68f);
            DisplayStoryPage(0);

            yield return StartCoroutine(AnimateCanvasShow(0.28f));
        }
    }

    public void AvancarPagina()
    {
        if (!isReadingDiary)
        {
            if (isShowingPanelNotice) FecharAvisoPainel();
            return;
        }

        if (isTransitioningPage || Time.unscaledTime - lastPageChangeTime < PageChangeCooldown)
            return;

        lastPageChangeTime = Time.unscaledTime;

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayClick();
        }

        if (currentStoryPage < 2)
        {
            currentStoryPage++;
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(TransitionToPageRoutine(currentStoryPage));
        }
        else
        {
            ConcluirDiarioEIniciarExploracao();
        }
    }

    public void VoltarPagina()
    {
        if (!isReadingDiary || currentStoryPage <= 0)
            return;

        if (isTransitioningPage || Time.unscaledTime - lastPageChangeTime < PageChangeCooldown)
            return;

        lastPageChangeTime = Time.unscaledTime;

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayClick();
        }

        currentStoryPage--;
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(TransitionToPageRoutine(currentStoryPage));
    }

    private IEnumerator TransitionToPageRoutine(int page)
    {
        isTransitioningPage = true;

        if (narrativeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(0.15f, 0.09f));
        }

        DisplayStoryPage(page);

        if (narrativeCanvasGroup != null)
        {
            yield return StartCoroutine(AnimateCanvasShow(0.16f));
        }

        isTransitioningPage = false;
    }

    private void DisplayStoryPage(int page)
    {
        currentStoryPage = Mathf.Clamp(page, 0, 2);

        string message = currentStoryPage switch
        {
            0 => storyMessage1,
            1 => storyMessage2,
            _ => $"<color=#A7F3D0>{storyMessage3}</color>"
        };

        string headerTitle = string.IsNullOrEmpty(diaryTitle) ? "DIÁRIO DE BORDO" : diaryTitle;
        string footerBadge = $"•  PÁGINA {currentStoryPage + 1} DE 3  •";

        if (hologramTitleText != null) hologramTitleText.text = headerTitle;
        if (narrativeText != null) narrativeText.text = message;
        if (hologramFooterText != null)
        {
            hologramFooterText.text = footerBadge;
            hologramFooterText.gameObject.SetActive(true);
        }

        if (btnVoltar != null)
        {
            btnVoltar.gameObject.SetActive(currentStoryPage > 0);
            RectTransform vRt = btnVoltar.GetComponent<RectTransform>();
            if (vRt != null)
            {
                vRt.anchoredPosition = new Vector2(-195f, -155f);
                vRt.sizeDelta = new Vector2(230f, 54f);
            }
            if (txtBtnVoltar != null) txtBtnVoltar.text = textoBotaoVoltar;
            SetButtonListener(btnVoltar, VoltarPagina);
        }

        if (btnAvancar != null)
        {
            btnAvancar.gameObject.SetActive(true);
            RectTransform btnRt = btnAvancar.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.anchoredPosition = (currentStoryPage > 0) ? new Vector2(185f, -155f) : new Vector2(0f, -155f);
                btnRt.sizeDelta = new Vector2(280f, 54f);
                BoxCollider col = btnAvancar.GetComponent<BoxCollider>();
                if (col != null) col.size = new Vector3(280f, 54f, 24f);
            }

            if (currentStoryPage == 2)
            {
                Image btnImg = btnAvancar.GetComponent<Image>();
                if (btnImg != null) btnImg.color = new Color(0.10f, 0.56f, 0.36f, 0.96f);
            }

            SetButtonListener(btnAvancar, AvancarPagina);
            if (txtBtnAvancar != null)
            {
                txtBtnAvancar.text = (currentStoryPage == 2) ? textoBotaoConcluir : textoBotaoAvancar;
            }
        }
    }

    public void ConcluirDiarioEIniciarExploracao()
    {
        if (!isReadingDiary) return;
        isReadingDiary = false;

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayClick();
        }

        StartCoroutine(ConcluirDiarioRoutine());
    }

    private IEnumerator ConcluirDiarioRoutine()
    {
        if (btnAvancar != null) btnAvancar.gameObject.SetActive(false);
        if (btnVoltar != null) btnVoltar.gameObject.SetActive(false);

        yield return StartCoroutine(FadeCanvasGroup(0f, 0.20f));

        if (narrativeCanvasObject != null)
        {
            narrativeCanvasObject.SetActive(false);
        }

        // Ativa o HUD de Objetivos no canto da tela com o Objetivo 1 da história
        MostrarObjetivoInicialNoCanto();
    }

    private void MostrarObjetivoInicialNoCanto()
    {
        if (isAlarmActive)
        {
            MostrarObjetivo();
            return;
        }

        if (isGeneratorRefueled)
        {
            MostrarAvisoAbastecimentoConcluido();
            return;
        }

        if (isFuelCollected)
        {
            MostrarAvisoCombustivelColetado();
            return;
        }

        AtualizarObjetivoNoCanto(
            "OBJETIVO ATUAL  [1/4]",
            "Objetivo: Verifique o painel na cabine de comando.",
            "Saia do quarto e siga pelo corredor até a frente da nave.",
            HologramTheme.InfoCyan
        );
    }

    // =========================================================================
    // 3. EVENTOS DE INTERAÇÃO QUE ATUALIZAM O OBJETIVO NO CANTO DA TELA
    // =========================================================================

    /// <summary>
    /// Chamado quando o jogador clica no botão do painel da cabine sem energia.
    /// Atualiza o objetivo no canto da tela em vez de abrir um Canvas no meio do cenário.
    /// </summary>
    public void MostrarAvisoPainelSemEnergia()
    {
        if (isGeneratorPowered || isAlarmActive) return;

        if (isGeneratorRefueled)
        {
            AtualizarObjetivoNoCanto(
                "[!] GERADOR DESLIGADO  [3/4]",
                "Objetivo: Aperte o botão ao lado do gerador.",
                "O gerador já tem combustível, mas precisa ser ligado na sala lateral.",
                HologramTheme.WarningAmber
            );
            return;
        }

        if (isFuelCollected)
        {
            AtualizarObjetivoNoCanto(
                "[!] PAINEL SEM ENERGIA  [2/4]",
                "Objetivo: Abasteça o gerador na sala lateral.",
                "Encoste o galão vermelho no gerador auxiliar para restaurar a energia.",
                HologramTheme.WarningAmber
            );
            return;
        }

        AtualizarObjetivoNoCanto(
            "[!] PAINEL SEM ENERGIA  [2/4]",
            "Objetivo: Verifique o gerador na sala lateral.",
            "O ataque pirata cortou a energia. Ligue o gerador para ativar o painel.",
            HologramTheme.WarningAmber
        );
    }

    /// <summary>
    /// Chamado quando o jogador clica no botão do gerador sem combustível.
    /// </summary>
    public void MostrarAvisoGeradorSemCombustivel()
    {
        if (isGeneratorPowered || isAlarmActive) return;

        if (isFuelCollected)
        {
            AtualizarObjetivoNoCanto(
                "[!] GERADOR VAZIO  [2/4]",
                "Objetivo: Aproxime o galão do gerador.",
                "Você já está com o galão! Encoste-o no gerador para abastecer.",
                HologramTheme.WarningAmber
            );
            return;
        }

        AtualizarObjetivoNoCanto(
            "[!] GERADOR VAZIO  [2/4]",
            "Objetivo: Pegue o galão vermelho e abasteça o gerador.",
            "O reservatório está sem combustível. O galão está nesta mesma sala.",
            HologramTheme.WarningAmber
        );
    }

    /// <summary>
    /// Chamado quando o jogador pega o galão de combustível.
    /// </summary>
    public void MostrarAvisoCombustivelColetado()
    {
        if (isGeneratorPowered || isAlarmActive) return;
        isFuelCollected = true;

        AtualizarObjetivoNoCanto(
            "COMBUSTÍVEL COLETADO  [2/4]",
            "Objetivo: Aproxime o galão do gerador auxiliar.",
            "Leve o galão até o gerador nesta sala para encher o reservatório.",
            HologramTheme.InfoCyan
        );
    }

    /// <summary>
    /// Chamado quando o galão abastece o gerador.
    /// </summary>
    public void MostrarAvisoAbastecimentoConcluido()
    {
        if (isGeneratorPowered || isAlarmActive) return;
        isGeneratorRefueled = true;

        AtualizarObjetivoNoCanto(
            "[OK] GERADOR ABASTECIDO  [3/4]",
            "Objetivo: Aperte o botão na mesa ao lado do gerador.",
            "Combustível transferido! Acione a ignição para religar a energia da nave.",
            HologramTheme.SuccessGreen
        );
    }

    public void FecharAvisoPainel()
    {
        isShowingPanelNotice = false;
        if (narrativeCanvasObject != null && !isReadingDiary)
        {
            narrativeCanvasObject.SetActive(false);
        }
    }

    private IEnumerator AnimateCanvasShow(float duration)
    {
        if (narrativeCanvasGroup == null || narrativeCanvasObject == null) yield break;

        float elapsed = 0f;
        Vector3 startScale = baseCanvasScale * 0.90f;
        Vector3 endScale = baseCanvasScale;

        narrativeCanvasGroup.alpha = 0f;
        narrativeCanvasObject.transform.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);

            narrativeCanvasGroup.alpha = easeOut;
            narrativeCanvasObject.transform.localScale = Vector3.LerpUnclamped(startScale, endScale, easeOut);
            yield return null;
        }

        narrativeCanvasGroup.alpha = 1f;
        narrativeCanvasObject.transform.localScale = endScale;
    }

    private IEnumerator FadeCanvasGroup(float targetAlpha, float duration)
    {
        if (narrativeCanvasGroup == null) yield break;

        float startAlpha = narrativeCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            narrativeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        narrativeCanvasGroup.alpha = targetAlpha;
    }

    private void Update()
    {
        // Enquanto o Diário de Bordo inicial estiver aberto no quarto:
        if (isReadingDiary && narrativeCanvasObject != null && narrativeCanvasObject.activeInHierarchy)
        {
            // Se o jogador sair andando do quarto (> 3.2m do Diário), fecha o Diário automaticamente
            // e mostra o objetivo no canto da tela para não prender o jogador!
            if (Camera.main != null)
            {
                float distToDiary = Vector3.Distance(Camera.main.transform.position, narrativeCanvasObject.transform.position);
                if (distToDiary > 3.2f)
                {
                    ConcluirDiarioEIniciarExploracao();
                    return;
                }
            }

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (TryRaycastCanvasButtons())
                {
                    return;
                }
            }
#endif

            CheckControllerAndKeyboardInput(out bool advanceJustPressed, out bool backJustPressed);
            if (advanceJustPressed) AvancarPagina();
            else if (backJustPressed) VoltarPagina();
        }
    }

    private bool TryRaycastCanvasButtons()
    {
        Camera cam = Camera.main;
        if (cam == null || narrativeCanvasObject == null) return false;

#if ENABLE_INPUT_SYSTEM
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);
        RaycastHit[] hits = Physics.RaycastAll(ray, 25f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;
            Button btn = hits[i].collider.GetComponentInParent<Button>();
            if (btn != null && btn.interactable && btn.transform.IsChildOf(narrativeCanvasObject.transform))
            {
                btn.onClick.Invoke();
                return true;
            }
        }
#endif
        return false;
    }

    private void CheckControllerAndKeyboardInput(out bool advanceJustPressed, out bool backJustPressed)
    {
        advanceJustPressed = false;
        backJustPressed = false;

        bool advanceNow = false;
        bool backNow = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            var kb = Keyboard.current;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                advanceNow = true;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
                backNow = true;
        }
#endif

        cachedInputDevices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.Controller,
            cachedInputDevices
        );

        for (int i = 0; i < cachedInputDevices.Count; i++)
        {
            var dev = cachedInputDevices[i];
            if (!dev.isValid) continue;

            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool prim) && prim)
                advanceNow = true;

            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool sec) && sec)
                backNow = true;
        }

        advanceJustPressed = advanceNow && !wasAdvanceInputPressed;
        backJustPressed = backNow && !wasBackInputPressed;

        wasAdvanceInputPressed = advanceNow;
        wasBackInputPressed = backNow;
    }

    private void LateUpdate()
    {
        // Mantém o Diário de Bordo inicial suavemente orientado para a câmera enquanto estiver no quarto
        if (isReadingDiary && narrativeCanvasObject != null && narrativeCanvasObject.activeInHierarchy && Camera.main != null)
        {
            Vector3 lookDir = narrativeCanvasObject.transform.position - Camera.main.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.04f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized);
                narrativeCanvasObject.transform.rotation = Quaternion.Slerp(
                    narrativeCanvasObject.transform.rotation,
                    targetRot,
                    Time.deltaTime * 6.5f
                );
            }
        }

    }

    private void OnDestroy()
    {
        for (int i = 0; i < cornerHudRuntimeMaterials.Count; i++)
        {
            if (cornerHudRuntimeMaterials[i] != null)
                Destroy(cornerHudRuntimeMaterials[i]);
        }

        cornerHudRuntimeMaterials.Clear();
    }

    // =========================================================================
    // 4. SEQUÊNCIA DE EMERGÊNCIA QUANDO O GERADOR É LIGADO
    // =========================================================================

    public void IniciarContagemAlerta()
    {
        isGeneratorPowered = true;
        isShowingPanelNotice = false;

        if (emergencyRoutine != null)
        {
            StopCoroutine(emergencyRoutine);
            emergencyRoutine = null;
        }

        StartCoroutine(AguardarAlerta());
    }

    private IEnumerator AguardarAlerta()
    {
        yield return new WaitForSeconds(explorationDelay);
        TriggerAlarm();
    }

    public void TriggerAlarm()
    {
        isGeneratorPowered = true;
        isShowingPanelNotice = false;

        if (isAlarmActive) return;
        isAlarmActive = true;

        Debug.Log("[GerenteAlarme] Sequência de emergência ativada!");

        if (emergencyRoutine != null) StopCoroutine(emergencyRoutine);
        emergencyRoutine = StartCoroutine(EmergencySequenceRoutine());
    }

    private IEnumerator EmergencySequenceRoutine()
    {
        MostrarObjetivo();
        yield break;
    }

    public void PositionObjectiveCanvas()
    {
        // Mantido por compatibilidade; o objetivo agora aparece no HUD de canto da tela.
    }

    public void StopAlarm()
    {
        if (!isAlarmActive) return;
        isAlarmActive = false;

        Debug.Log("[GerenteAlarme] Alarme e emergência desligados.");

        if (emergencyRoutine != null)
        {
            StopCoroutine(emergencyRoutine);
            emergencyRoutine = null;
        }

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.StopAlarm();
        }

        if (emergencyLightsController != null)
        {
            emergencyLightsController.StopEmergencyLights();
        }

        if (narrativeCanvasObject != null)
        {
            narrativeCanvasObject.SetActive(false);
        }

        if (objectiveCanvasObject != null)
        {
            objectiveCanvasObject.SetActive(false);
        }
    }

    /// <summary>
    /// Ativa o alarme sonoro/luzes de emergência e atualiza o HUD no canto da tela
    /// com o objetivo final: correr até a cabine e ativar o sistema de defesa.
    /// </summary>
    public void MostrarObjetivo()
    {
        isGeneratorPowered = true;
        isAlarmActive = true;
        isReadingDiary = false;
        isShowingPanelNotice = false;

        if (narrativeCanvasObject != null)
        {
            narrativeCanvasObject.SetActive(false);
        }

        if (objectiveCanvasObject != null && objectiveCanvasObject != narrativeCanvasObject)
        {
            objectiveCanvasObject.SetActive(false);
        }

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.StartAlarm();
        }

        if (emergencyLightsController != null)
        {
            emergencyLightsController.StartEmergencyLights();
        }

        AtualizarObjetivoNoCanto(
            "[!] ALERTA DE COLISÃO  [4/4]",
            "Objetivo: Corra à cabine e ative o painel de defesa!",
            "Energia restaurada! Detritos detectados em rota de colisão com a nave.",
            HologramTheme.AlertRed
        );
    }
}

public class AlarmManager : GerenteAlarme
{
}
