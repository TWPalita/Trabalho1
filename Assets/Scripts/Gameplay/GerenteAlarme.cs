using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controla o fluxo de introdução narrativa, exploração livre e ativação de emergência na espaçonave (SampleScene).
/// Apresenta o Diário de Bordo em um Canvas World Space imersivo com botões interativos de Avançar e Voltar (OnClick),
/// permite exploração livre e aciona o alarme sonoro com iluminação de emergência.
/// </summary>
public class GerenteAlarme : MonoBehaviour
{
    public static GerenteAlarme Instance { get; private set; }

    [Header("Configurações da Narrativa")]
    [Tooltip("Tempo em segundos antes da primeira exibição do holograma")]
    [SerializeField] private float initialMessageDelay = 0.8f;

    [Tooltip("Tempo em segundos que o jogador tem para explorar a nave livremente após o diário")]
    [SerializeField] private float explorationDelay = 5.0f;

    [Header("Textos do Diário de Bordo")]
    [SerializeField] private string diaryTitle = "DIÁRIO DE BORDO";

    [TextArea(2, 4)]
    [SerializeField] private string storyMessage1 = "Você está a bordo de uma nave espacial em uma longa jornada pela galáxia. Sua missão é atravessar regiões desconhecidas do espaço e chegar ao seu próximo destino.";

    [TextArea(2, 4)]
    [SerializeField] private string storyMessage2 = "Durante a viagem, a nave foi atacada por piratas espaciais. Embora você tenha conseguido sobreviver, alguns sistemas foram danificados e o funcionamento da embarcação ficou comprometido.";

    [TextArea(2, 4)]
    [SerializeField] private string storyMessage3 = "Após o incidente, você desperta em seu quarto. É necessário verificar o estado da nave antes de continuar a viagem. Explore os compartimentos e procure descobrir quais sistemas precisam ser restaurados.";

    [Header("Textos do Painel Sem Energia (Etapa 3)")]
    [SerializeField] private string panelUnavailableTitle = "SISTEMA INDISPONÍVEL";

    [TextArea(3, 6)]
    [SerializeField] private string panelUnavailableMessage = "O painel de controle está sem energia e não pode ativar o sistema de defesa. Os danos causados pelo ataque podem ter comprometido o fornecimento de energia da nave.\nVerifique o gerador auxiliar. Ele precisa de combustível para voltar a funcionar e restabelecer a energia dos sistemas.";

    [SerializeField] private string panelBlockedTitle = "ACESSO BLOQUEADO";

    [TextArea(2, 4)]
    [SerializeField] private string panelBlockedMessage = "O painel ainda está sem energia. Abasteça o gerador auxiliar e restaure o fornecimento de energia antes de ativar o sistema de defesa.";

    [Header("Textos do Gerador e Combustível (Etapas 4, 5 e 6)")]
    [SerializeField] private string generatorEmptyTitle = "GERADOR AUXILIAR";

    [TextArea(3, 6)]
    [SerializeField] private string generatorEmptyMessage = "Você encontrou o gerador responsável por fornecer energia aos sistemas auxiliares da nave. O equipamento está sem combustível e não pode ser iniciado.\nEncontre o galão de combustível, leve-o até o gerador e abasteça o reservatório.";

    [SerializeField] private string fuelCollectedTitle = "COMBUSTÍVEL COLETADO";

    [TextArea(2, 4)]
    [SerializeField] private string fuelCollectedMessage = "O galão está com você. Leve-o até o gerador auxiliar para abastecer o reservatório e permitir que o equipamento volte a funcionar.";

    [SerializeField] private string refuelingCompleteTitle = "ABASTECIMENTO CONCLUÍDO";

    [TextArea(2, 4)]
    [SerializeField] private string refuelingCompleteMessage = "O combustível foi colocado no reservatório. Agora, acione o botão de ignição para reiniciar o gerador e restabelecer o fornecimento de energia.";

    [SerializeField] private string energyRestoredTitle = "ENERGIA RESTAURADA";

    [TextArea(3, 6)]
    [SerializeField] private string energyRestoredMessage = "O gerador auxiliar voltou a funcionar e o fornecimento de energia foi restabelecido.\nOs sistemas da nave estão reiniciando. Entretanto, os sensores detectaram detritos espaciais em rota de colisão.\nRetorne ao painel de controle para ativar o sistema de defesa antes que os detritos atinjam a nave.";

    [Header("Textos do Alerta de Emergência")]
    [Tooltip("Tempo em segundos que o alerta de emergência fica visível antes de mostrar o objetivo")]
    [SerializeField] private float alertDuration = 5.0f;

    [SerializeField] private string alertTitle = "ALERTA";

    [TextArea(3, 6)]
    [SerializeField] private string alertMessage = "Detritos espaciais detectados!\nA nave está em rota de colisão.\n\nAtive o sistema de defesa imediatamente.";

    [Header("Texto do Objetivo do Jogador")]
    [SerializeField] private string objectiveTitle = "OBJETIVO";

    [TextArea(3, 6)]
    [SerializeField] private string objectiveMessage = "Corra até o painel de controle e ative o sistema de defesa.";

    [SerializeField] private string objectiveHint = "[ Aperte o botão na mesa no final da nave ]";

    [Header("Controle de Luzes de Emergência")]
    [SerializeField] private LuzesEmergencia emergencyLightsController;

    [Header("Posicionamento Fixo do Objetivo (World Space)")]
    [Tooltip("Canvas opcional dedicado para o objetivo (se não atribuído, utiliza narrativeCanvasObject)")]
    [SerializeField] private GameObject objectiveCanvasObject;
    [SerializeField] private Vector3 fixedObjectivePosition = new Vector3(-8.4399995803833f, 1.4908255338668824f, -1.3852607011795045f);
    [SerializeField] private Vector3 fixedObjectiveRotation = new Vector3(0f, 180f, 0f);

    [Header("Interface Holográfica em World Space (VR)")]
    [Tooltip("Se verdadeiro, exibe a interface holográfica World Space com a narrativa inicial")]
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
    [Tooltip("Texto do botão Avançar nas páginas 1 e 2")]
    [SerializeField] private string textoBotaoAvancar = "AVANÇAR";

    [Tooltip("Texto do botão Voltar nas páginas 2 e 3")]
    [SerializeField] private string textoBotaoVoltar = "VOLTAR";

    [Tooltip("Texto do botão na página 3 para iniciar a exploração")]
    [SerializeField] private string textoBotaoConcluir = "INICIAR EXPLORAÇÃO";

    [Tooltip("Texto do botão durante o alerta de emergência para fechar o canvas")]
    [SerializeField] private string textoBotaoAlerta = "ENTENDIDO";

    private bool isAlarmActive = false;
    private bool isGeneratorPowered = false;
    private bool isReadingDiary = true;
    private bool isShowingPanelNotice = false;
    private bool hasShownPanelUnavailable = false;
    private float lastPanelNoticeTime = -10f;
    private int currentStoryPage = 0;
    private Coroutine emergencyRoutine;
    private Coroutine transitionRoutine;
    private UnityEngine.UI.Image hologramBgImage;
    private UnityEngine.UI.Image hologramLineImage;
    private float lastPageChangeTime = -1f;
    private const float PageChangeCooldown = 0.3f;
    private bool isTransitioningPage = false;
    private bool wasPrimaryButtonPressed = false;

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
            storyMessage3 = "Após o incidente, você desperta em seu quarto. É necessário verificar o estado da nave antes de continuar a viagem. Explore os compartimentos e procure descobrir quais sistemas precisam ser restaurados.";

        if (string.IsNullOrEmpty(panelUnavailableTitle))
            panelUnavailableTitle = "SISTEMA INDISPONÍVEL";

        if (string.IsNullOrEmpty(panelUnavailableMessage))
            panelUnavailableMessage = "O painel de controle está sem energia e não pode ativar o sistema de defesa. Os danos causados pelo ataque podem ter comprometido o fornecimento de energia da nave.\nVerifique o gerador auxiliar. Ele precisa de combustível para voltar a funcionar e restabelecer a energia dos sistemas.";

        if (string.IsNullOrEmpty(panelBlockedTitle))
            panelBlockedTitle = "ACESSO BLOQUEADO";

        if (string.IsNullOrEmpty(panelBlockedMessage))
            panelBlockedMessage = "O painel ainda está sem energia. Abasteça o gerador auxiliar e restaure o fornecimento de energia antes de ativar o sistema de defesa.";

        if (string.IsNullOrEmpty(generatorEmptyTitle))
            generatorEmptyTitle = "GERADOR AUXILIAR";

        if (string.IsNullOrEmpty(generatorEmptyMessage))
            generatorEmptyMessage = "Você encontrou o gerador responsável por fornecer energia aos sistemas auxiliares da nave. O equipamento está sem combustível e não pode ser iniciado.\nEncontre o galão de combustível, leve-o até o gerador e abasteça o reservatório.";

        if (string.IsNullOrEmpty(fuelCollectedTitle))
            fuelCollectedTitle = "COMBUSTÍVEL COLETADO";

        if (string.IsNullOrEmpty(fuelCollectedMessage))
            fuelCollectedMessage = "O galão está com você. Leve-o até o gerador auxiliar para abastecer o reservatório e permitir que o equipamento volte a funcionar.";

        if (string.IsNullOrEmpty(refuelingCompleteTitle))
            refuelingCompleteTitle = "ABASTECIMENTO CONCLUÍDO";

        if (string.IsNullOrEmpty(refuelingCompleteMessage))
            refuelingCompleteMessage = "O combustível foi colocado no reservatório. Agora, acione o botão de ignição para reiniciar o gerador e restabelecer o fornecimento de energia.";

        if (string.IsNullOrEmpty(energyRestoredTitle))
            energyRestoredTitle = "ENERGIA RESTAURADA";

        if (string.IsNullOrEmpty(energyRestoredMessage))
            energyRestoredMessage = "O gerador auxiliar voltou a funcionar e o fornecimento de energia foi restabelecido.\nOs sistemas da nave estão reiniciando. Entretanto, os sensores detectaram detritos espaciais em rota de colisão.\nRetorne ao painel de controle para ativar o sistema de defesa antes que os detritos atinjam a nave.";

        if (string.IsNullOrEmpty(textoBotaoAvancar) || textoBotaoAvancar == "Avançar")
            textoBotaoAvancar = "AVANÇAR";

        if (string.IsNullOrEmpty(textoBotaoVoltar) || textoBotaoVoltar == "Voltar")
            textoBotaoVoltar = "VOLTAR";

        if (string.IsNullOrEmpty(textoBotaoConcluir) || textoBotaoConcluir == "Explorar Nave")
            textoBotaoConcluir = "INICIAR EXPLORAÇÃO";

        if (string.IsNullOrEmpty(textoBotaoAlerta))
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

        if (narrativeCanvasObject != null)
        {
            narrativeCanvasObject.SetActive(true);
            ConfigureExistingCanvas(narrativeCanvasObject);
            return;
        }

        GameObject canvasObj = new GameObject("ShipWorldSpaceNarrativeCanvas");
        canvasObj.transform.SetParent(null);

        canvasObj.transform.position = new Vector3(-3.57f, 1.40f, -6.80f);
        canvasObj.transform.rotation = Quaternion.identity;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        canvas.worldCamera = Camera.main;

        canvas.additionalShaderChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.Normal |
            AdditionalCanvasShaderChannels.Tangent;

        RectTransform rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(850f, 400f);
        rt.localScale = new Vector3(0.0025f, 0.0025f, 0.0025f);

        narrativeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        narrativeCanvasGroup.alpha = 1f;

        GameObject bgObj = new GameObject("HologramBackground");
        bgObj.transform.SetParent(canvasObj.transform, false);

        UnityEngine.UI.Image bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();

        Texture2D whiteTex = Texture2D.whiteTexture;

        bgImage.sprite = Sprite.Create(
            whiteTex,
            new Rect(0, 0, whiteTex.width, whiteTex.height),
            new Vector2(0.5f, 0.5f)
        );

        bgImage.color = new Color(0.03f, 0.07f, 0.16f, 0.94f);

        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        GameObject lineObj = new GameObject("HologramLine");
        lineObj.transform.SetParent(canvasObj.transform, false);

        UnityEngine.UI.Image lineImage = lineObj.AddComponent<UnityEngine.UI.Image>();
        lineImage.sprite = bgImage.sprite;
        lineImage.color = new Color(0.22f, 0.74f, 0.97f, 0.85f);

        RectTransform lineRt = lineObj.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.08f, 0.77f);
        lineRt.anchorMax = new Vector2(0.92f, 0.78f);
        lineRt.offsetMin = Vector2.zero;
        lineRt.offsetMax = Vector2.zero;

        GameObject textObj = new GameObject("NarrativeText");
        textObj.transform.SetParent(canvasObj.transform, false);

        narrativeText = textObj.AddComponent<TextMeshProUGUI>();

        TMP_FontAsset font =
            Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        if (font != null)
        {
            narrativeText.font = font;
        }

        narrativeText.fontSize = 30f;
        narrativeText.alignment = TextAlignmentOptions.Center;
        narrativeText.textWrappingMode = TextWrappingModes.Normal;
        narrativeText.color = Color.white;

        RectTransform textRt = textObj.GetComponent<RectTransform>();

        textRt.anchorMin = new Vector2(0.5f, 0.5f);
        textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = new Vector2(0f, 53f);
        textRt.sizeDelta = new Vector2(740f, 250f);

        hologramBgImage = bgImage;
        hologramLineImage = lineImage;

        narrativeCanvasObject = canvasObj;
    }

    private void ConfigureExistingCanvas(GameObject canvasObj)
    {
        Canvas canvas = canvasObj.GetComponent<Canvas>();

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;

            if (canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;

            canvas.additionalShaderChannels |=
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;
        }

        var raycasterType = System.Type.GetType(
            "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit"
        );

        if (raycasterType != null &&
            canvasObj.GetComponent(raycasterType) == null)
        {
            canvasObj.AddComponent(raycasterType);
        }

        if (narrativeCanvasGroup == null)
        {
            narrativeCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
        }

        if (narrativeText == null)
        {
            narrativeText = canvasObj.GetComponentInChildren<TMP_Text>();
        }

        UnityEngine.UI.Image[] images =
            canvasObj.GetComponentsInChildren<UnityEngine.UI.Image>(true);

        foreach (var img in images)
        {
            if (img.gameObject.name.Contains("Background") ||
                img.gameObject.name.Contains("Fundo"))
            {
                hologramBgImage = img;
            }
            else if (
                img.gameObject.name.Contains("Line") ||
                img.gameObject.name.Contains("Header") ||
                img.gameObject.name.Contains("Linha"))
            {
                hologramLineImage = img;
            }
        }
    }

    private void EnsureButtonsUI()
    {
        if (narrativeCanvasObject == null)
            return;

        if (btnAvancar == null)
        {
            Transform t = narrativeCanvasObject.transform.Find("BtnAvancar");

            if (t != null)
                btnAvancar = t.GetComponent<Button>();
        }

        if (btnVoltar == null)
        {
            Transform t = narrativeCanvasObject.transform.Find("BtnVoltar");

            if (t != null)
                btnVoltar = t.GetComponent<Button>();
        }

        // Fallback procedural apenas se não existirem na hierarquia
        if (btnVoltar == null)
        {
            btnVoltar = CreateHolographicButton(
                narrativeCanvasObject.transform,
                "BtnVoltar",
                new Vector2(-190f, -135f),
                new Vector2(230f, 52f),
                textoBotaoVoltar,
                out txtBtnVoltar
            );
        }
        else if (txtBtnVoltar == null)
        {
            txtBtnVoltar = btnVoltar.GetComponentInChildren<TMP_Text>();
        }

        if (btnAvancar == null)
        {
            btnAvancar = CreateHolographicButton(
                narrativeCanvasObject.transform,
                "BtnAvancar",
                new Vector2(190f, -135f),
                new Vector2(230f, 52f),
                textoBotaoAvancar,
                out txtBtnAvancar
            );
        }
        else if (txtBtnAvancar == null)
        {
            txtBtnAvancar = btnAvancar.GetComponentInChildren<TMP_Text>();
        }

        if (btnAvancar != null)
        {
            SetButtonListener(btnAvancar, AvancarPagina);
        }

        if (btnVoltar != null)
        {
            SetButtonListener(btnVoltar, VoltarPagina);
        }
    }

    private void ClearAllButtonListeners(Button button)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
        }
    }

    private void SetButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        ClearAllButtonListeners(button);
        if (action != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private Button CreateHolographicButton(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        string initialText,
        out TMP_Text textComponent)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        btnObj.AddComponent<CanvasRenderer>();

        UnityEngine.UI.Image img =
            btnObj.AddComponent<UnityEngine.UI.Image>();

        Texture2D whiteTex = Texture2D.whiteTexture;

        img.sprite = Sprite.Create(
            whiteTex,
            new Rect(0, 0, whiteTex.width, whiteTex.height),
            new Vector2(0.5f, 0.5f)
        );

        img.color = new Color(0.08f, 0.32f, 0.55f, 0.88f);
        img.raycastTarget = true;

        Button btn = btnObj.AddComponent<Button>();

        ColorBlock cb = btn.colors;

        cb.normalColor = new Color(0.10f, 0.38f, 0.65f, 0.90f);
        cb.highlightedColor = new Color(0.22f, 0.74f, 0.98f, 1.00f);
        cb.pressedColor = new Color(0.06f, 0.22f, 0.40f, 1.00f);
        cb.selectedColor = new Color(0.18f, 0.60f, 0.85f, 1.00f);
        cb.disabledColor = new Color(0.12f, 0.18f, 0.25f, 0.40f);

        btn.colors = cb;
        btn.targetGraphic = img;

        BoxCollider collider = btnObj.AddComponent<BoxCollider>();
        collider.size = new Vector3(size.x, size.y, 8f);
        collider.isTrigger = true;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRt = textObj.AddComponent<RectTransform>();

        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        textObj.AddComponent<CanvasRenderer>();

        TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();

        if (narrativeText != null && narrativeText.font != null)
        {
            tmp.font = narrativeText.font;
        }
        else
        {
            TMP_FontAsset font =
                Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (font != null)
                tmp.font = font;
        }

        tmp.fontSize = 22f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.text = initialText;

        textComponent = tmp;

        return btn;
    }

    private void SetHologramTheme(bool emergency)
    {
        if (hologramBgImage != null)
        {
            hologramBgImage.color = emergency
                ? new Color(0.14f, 0.02f, 0.02f, 0.95f)
                : new Color(0.03f, 0.07f, 0.16f, 0.94f);
        }

        if (hologramLineImage != null)
        {
            hologramLineImage.color = emergency
                ? new Color(0.95f, 0.25f, 0.25f, 0.9f)
                : new Color(0.22f, 0.74f, 0.97f, 0.85f);
        }

        if (btnAvancar != null)
        {
            ColorBlock cb = btnAvancar.colors;

            if (emergency)
            {
                cb.normalColor = new Color(0.70f, 0.15f, 0.15f, 0.95f);
                cb.highlightedColor = new Color(0.95f, 0.25f, 0.25f, 1.0f);
                cb.pressedColor = new Color(0.45f, 0.08f, 0.08f, 1.0f);
                cb.selectedColor = new Color(0.85f, 0.20f, 0.20f, 1.0f);
            }
            else
            {
                cb.normalColor = new Color(0.10f, 0.38f, 0.65f, 0.90f);
                cb.highlightedColor = new Color(0.22f, 0.74f, 0.98f, 1.00f);
                cb.pressedColor = new Color(0.06f, 0.22f, 0.40f, 1.00f);
                cb.selectedColor = new Color(0.18f, 0.60f, 0.85f, 1.00f);
            }

            btnAvancar.colors = cb;
        }
    }

    private void Start()
    {
        SetHologramTheme(false);
        AlignHologramWithPlayer();
        StartCoroutine(StartDiarySequenceRoutine());
    }

    private void AlignHologramWithPlayer()
    {
        if (narrativeCanvasObject == null)
            return;

        Camera cam = Camera.main;

        if (cam != null && cam.isActiveAndEnabled)
        {
            Vector3 camFwd = cam.transform.forward;
            camFwd.y = 0f;

            if (camFwd.sqrMagnitude > 0.05f)
            {
                camFwd.Normalize();

                Vector3 targetPos =
                    cam.transform.position + camFwd * 1.8f;

                targetPos.y = cam.transform.position.y + 0.05f;

                narrativeCanvasObject.transform.position = targetPos;
                narrativeCanvasObject.transform.rotation =
                    Quaternion.LookRotation(camFwd);
            }
        }
    }

    private IEnumerator StartDiarySequenceRoutine()
    {
        if (emergencyLightsController != null)
            emergencyLightsController.ResetToNormal();

        if (showWorldSpaceNarrativeUI &&
            narrativeCanvasObject != null)
        {
            narrativeCanvasObject.SetActive(true);

            if (narrativeCanvasGroup != null)
                narrativeCanvasGroup.alpha = 0f;

            yield return new WaitForSeconds(initialMessageDelay);

            isReadingDiary = true;
            currentStoryPage = 0;

            DisplayStoryPage(0);

            yield return StartCoroutine(
                FadeCanvasGroup(1f, 0.35f)
            );
        }
    }

    public void AvancarPagina()
    {
        if (!isReadingDiary)
            return;

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

            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);

            transitionRoutine =
                StartCoroutine(
                    TransitionToPageRoutine(currentStoryPage)
                );
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

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine =
            StartCoroutine(
                TransitionToPageRoutine(currentStoryPage)
            );
    }

    private IEnumerator TransitionToPageRoutine(int page)
    {
        isTransitioningPage = true;

        if (narrativeCanvasGroup != null)
        {
            yield return StartCoroutine(
                FadeCanvasGroup(0.3f, 0.12f)
            );
        }

        DisplayStoryPage(page);

        if (narrativeCanvasGroup != null)
        {
            yield return StartCoroutine(
                FadeCanvasGroup(1f, 0.15f)
            );
        }

        isTransitioningPage = false;
    }

    private void DisplayStoryPage(int page)
    {
        currentStoryPage = Mathf.Clamp(page, 0, 2);

        string message = "";

        switch (currentStoryPage)
        {
            case 0:
                message = storyMessage1;
                break;

            case 1:
                message = storyMessage2;
                break;

            case 2:
                message = storyMessage3;
                break;
        }

        string headerTitle = string.IsNullOrEmpty(diaryTitle) ? "DIÁRIO DE BORDO" : diaryTitle;

        SetMessageText(
            $"<size=85%><color=#38BDF8><b>{headerTitle}</b></color></size>\n\n\n\n" +
            $"<size=80%>{message}</size>\n\n" +
            $"<size=60%><color=#94A3B8>[ {currentStoryPage + 1} / 3 ]</color></size>"
        );

        if (btnVoltar != null)
        {
            btnVoltar.gameObject.SetActive(currentStoryPage > 0);

            if (txtBtnVoltar != null)
                txtBtnVoltar.text = textoBotaoVoltar;

            SetButtonListener(btnVoltar, VoltarPagina);
        }

        if (btnAvancar != null)
        {
            btnAvancar.gameObject.SetActive(true);

            RectTransform btnRt = btnAvancar.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.anchoredPosition = new Vector2(190f, -135f);
                if (btnRt.sizeDelta.x < 280f)
                {
                    btnRt.sizeDelta = new Vector2(280f, btnRt.sizeDelta.y);
                    BoxCollider col = btnAvancar.GetComponent<BoxCollider>();
                    if (col != null) col.size = new Vector3(280f, col.size.y, col.size.z);
                }
            }

            SetButtonListener(btnAvancar, AvancarPagina);

            if (txtBtnAvancar != null)
            {
                txtBtnAvancar.text =
                    (currentStoryPage == 2)
                    ? textoBotaoConcluir
                    : textoBotaoAvancar;
            }
        }
    }

    /// <summary>
    /// Chamado ao clicar no botão final 'INICIAR EXPLORAÇÃO' da página 3.
    /// </summary>
    public void ConcluirDiarioEIniciarExploracao()
    {
        if (!isReadingDiary)
            return;

        isReadingDiary = false;

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayClick();
        }

        StartCoroutine(ConcluirDiarioRoutine());
    }

    private IEnumerator ConcluirDiarioRoutine()
    {
        if (btnAvancar != null)
            btnAvancar.gameObject.SetActive(false);

        if (btnVoltar != null)
            btnVoltar.gameObject.SetActive(false);

        yield return StartCoroutine(
            FadeCanvasGroup(0f, 0.35f)
        );

        if (narrativeCanvasObject != null)
        {
            narrativeCanvasObject.SetActive(false);
        }

        Debug.Log(
            "[GerenteAlarme] Diário concluído. Exploração livre iniciada."
        );
    }

    /// <summary>
    /// Exibe o aviso de painel sem energia quando o jogador tenta acionar o botão físico na CabinePiloto antes de ligar o gerador.
    /// Na primeira tentativa mostra "SISTEMA INDISPONÍVEL"; nas seguintes mostra "ACESSO BLOQUEADO".
    /// </summary>
    public void MostrarAvisoPainelSemEnergia()
    {
        if (isGeneratorPowered || isAlarmActive)
            return;

        if (Time.unscaledTime - lastPanelNoticeTime < 0.5f)
            return;

        lastPanelNoticeTime = Time.unscaledTime;
        isReadingDiary = false;
        isShowingPanelNotice = true;

        if (narrativeCanvasObject == null)
        {
            EnsureWorldSpaceUI();
            EnsureButtonsUI();
        }

        if (narrativeCanvasObject == null)
            return;

        bool isFirstTime = !hasShownPanelUnavailable;
        hasShownPanelUnavailable = true;

        string title = isFirstTime
            ? (string.IsNullOrEmpty(panelUnavailableTitle) ? "SISTEMA INDISPONÍVEL" : panelUnavailableTitle)
            : (string.IsNullOrEmpty(panelBlockedTitle) ? "ACESSO BLOQUEADO" : panelBlockedTitle);

        string body = isFirstTime
            ? panelUnavailableMessage
            : panelBlockedMessage;

        SetHologramTheme(true);
        AlignHologramWithPlayer();
        narrativeCanvasObject.SetActive(true);

        if (isFirstTime)
        {
            SetMessageText(
                $"<size=85%><color=#F87171><b>{title}</b></color></size>\n\n\n\n" +
                $"<size=72%>{body}</size>\n"
            );
        }
        else
        {
            SetMessageText(
                $"<size=85%><color=#F87171><b>{title}</b></color></size>\n\n\n\n\n" +
                $"<size=80%>{body}</size>\n\n\n"
            );
        }

        if (btnVoltar != null)
            btnVoltar.gameObject.SetActive(false);

        if (btnAvancar != null)
        {
            btnAvancar.gameObject.SetActive(true);

            RectTransform btnRt = btnAvancar.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.anchoredPosition = new Vector2(0f, -135f);
                if (btnRt.sizeDelta.x < 240f)
                {
                    btnRt.sizeDelta = new Vector2(240f, btnRt.sizeDelta.y);
                    BoxCollider col = btnAvancar.GetComponent<BoxCollider>();
                    if (col != null) col.size = new Vector3(240f, col.size.y, col.size.z);
                }
            }

            if (txtBtnAvancar != null)
                txtBtnAvancar.text = string.IsNullOrEmpty(textoBotaoAlerta) ? "ENTENDIDO" : textoBotaoAlerta;

            SetButtonListener(btnAvancar, FecharAvisoPainel);
        }

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        if (narrativeCanvasGroup != null)
            narrativeCanvasGroup.alpha = 0f;

        transitionRoutine = StartCoroutine(FadeCanvasGroup(1f, 0.25f));
    }

    /// <summary>
    /// Exibe a mensagem da Etapa 4 ("GERADOR AUXILIAR") quando o jogador aperta o botão do gerador sem combustível.
    /// </summary>
    public void MostrarAvisoGeradorSemCombustivel()
    {
        if (isGeneratorPowered || isAlarmActive)
            return;

        if (Time.unscaledTime - lastPanelNoticeTime < 0.5f)
            return;

        lastPanelNoticeTime = Time.unscaledTime;
        isReadingDiary = false;
        isShowingPanelNotice = true;

        if (narrativeCanvasObject == null)
        {
            EnsureWorldSpaceUI();
            EnsureButtonsUI();
        }

        if (narrativeCanvasObject == null)
            return;

        string title = string.IsNullOrEmpty(generatorEmptyTitle) ? "GERADOR AUXILIAR" : generatorEmptyTitle;
        string body = generatorEmptyMessage;

        SetHologramTheme(true);
        narrativeCanvasObject.transform.position = new Vector3(-8.4399995803833f, 1.4908255338668824f, -1.3852607011795045f);
        narrativeCanvasObject.transform.rotation = new Quaternion(0.0f, -1.0f, 0.0f, 0.0f);
        narrativeCanvasObject.transform.localScale = new Vector3(0.0024999999441206457f, 0.0024999999441206457f, 0.0024999999441206457f);
        narrativeCanvasObject.SetActive(true);

        SetMessageText(
            $"<size=85%><color=#F87171><b>{title}</b></color></size>\n\n\n\n\n" +
            $"<size=72%>{body}</size>\n\n"
        );

        if (btnVoltar != null)
            btnVoltar.gameObject.SetActive(false);

        if (btnAvancar != null)
        {
            btnAvancar.gameObject.SetActive(true);

            RectTransform btnRt = btnAvancar.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.anchoredPosition = new Vector2(0f, -135f);
                if (btnRt.sizeDelta.x < 240f)
                {
                    btnRt.sizeDelta = new Vector2(240f, btnRt.sizeDelta.y);
                    BoxCollider col = btnAvancar.GetComponent<BoxCollider>();
                    if (col != null) col.size = new Vector3(240f, col.size.y, col.size.z);
                }
            }

            if (txtBtnAvancar != null)
                txtBtnAvancar.text = string.IsNullOrEmpty(textoBotaoAlerta) ? "ENTENDIDO" : textoBotaoAlerta;

            SetButtonListener(btnAvancar, FecharAvisoPainel);
        }

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        if (narrativeCanvasGroup != null)
            narrativeCanvasGroup.alpha = 0f;

        transitionRoutine = StartCoroutine(FadeCanvasGroup(1f, 0.25f));
    }

    /// <summary>
    /// Exibe a mensagem da Etapa 5 ("COMBUSTÍVEL COLETADO") quando o jogador pega o galão de combustível.
    /// </summary>
    public void MostrarAvisoCombustivelColetado()
    {
        if (isGeneratorPowered || isAlarmActive)
            return;

        lastPanelNoticeTime = Time.unscaledTime;
        isReadingDiary = false;
        isShowingPanelNotice = true;

        if (narrativeCanvasObject == null)
        {
            EnsureWorldSpaceUI();
            EnsureButtonsUI();
        }

        if (narrativeCanvasObject == null)
            return;

        string title = string.IsNullOrEmpty(fuelCollectedTitle) ? "COMBUSTÍVEL COLETADO" : fuelCollectedTitle;
        string body = fuelCollectedMessage;

        SetHologramTheme(false);
        AlignHologramWithPlayer();
        narrativeCanvasObject.transform.localScale = new Vector3(0.0024999999441206457f, 0.0024999999441206457f, 0.0024999999441206457f);
        narrativeCanvasObject.SetActive(true);

        SetMessageText(
            $"<size=85%><color=#38BDF8><b>{title}</b></color></size>\n\n\n\n\n" +
            $"<size=80%>{body}</size>\n\n\n"
        );

        if (btnVoltar != null)
            btnVoltar.gameObject.SetActive(false);

        if (btnAvancar != null)
        {
            btnAvancar.gameObject.SetActive(true);

            RectTransform btnRt = btnAvancar.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.anchoredPosition = new Vector2(0f, -135f);
                if (btnRt.sizeDelta.x < 240f)
                {
                    btnRt.sizeDelta = new Vector2(240f, btnRt.sizeDelta.y);
                    BoxCollider col = btnAvancar.GetComponent<BoxCollider>();
                    if (col != null) col.size = new Vector3(240f, col.size.y, col.size.z);
                }
            }

            if (txtBtnAvancar != null)
                txtBtnAvancar.text = string.IsNullOrEmpty(textoBotaoAlerta) ? "ENTENDIDO" : textoBotaoAlerta;

            SetButtonListener(btnAvancar, FecharAvisoPainel);
        }

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        if (narrativeCanvasGroup != null)
            narrativeCanvasGroup.alpha = 0f;

        transitionRoutine = StartCoroutine(FadeCanvasGroup(1f, 0.25f));
    }

    /// <summary>
    /// Exibe a mensagem da Etapa 6 ("ABASTECIMENTO CONCLUÍDO") na posição do gerador quando o combustível é colocado.
    /// </summary>
    public void MostrarAvisoAbastecimentoConcluido()
    {
        if (isGeneratorPowered || isAlarmActive)
            return;

        lastPanelNoticeTime = Time.unscaledTime;
        isReadingDiary = false;
        isShowingPanelNotice = false;

        if (narrativeCanvasObject == null)
        {
            EnsureWorldSpaceUI();
            EnsureButtonsUI();
        }

        if (narrativeCanvasObject == null)
            return;

        string title = string.IsNullOrEmpty(refuelingCompleteTitle) ? "ABASTECIMENTO CONCLUÍDO" : refuelingCompleteTitle;
        string body = refuelingCompleteMessage;

        SetHologramTheme(false);
        narrativeCanvasObject.transform.position = new Vector3(-8.4399995803833f, 1.4908255338668824f, -1.3852607011795045f);
        narrativeCanvasObject.transform.rotation = new Quaternion(0.0f, -1.0f, 0.0f, 0.0f);
        narrativeCanvasObject.transform.localScale = new Vector3(0.0024999999441206457f, 0.0024999999441206457f, 0.0024999999441206457f);
        narrativeCanvasObject.SetActive(true);

        SetMessageText(
            $"<size=85%><color=#38BDF8><b>{title}</b></color></size>\n\n\n\n\n" +
            $"<size=80%>{body}</size>\n\n\n"
        );

        if (btnVoltar != null)
            btnVoltar.gameObject.SetActive(false);

        if (btnAvancar != null)
            btnAvancar.gameObject.SetActive(false);

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        if (narrativeCanvasGroup != null)
            narrativeCanvasGroup.alpha = 0f;

        transitionRoutine = StartCoroutine(FadeCanvasGroup(1f, 0.25f));
    }

    public void FecharAvisoPainel()
    {
        if (!isShowingPanelNotice)
            return;

        isShowingPanelNotice = false;

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayClick();
        }

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(DismissHologramRoutine());
    }

    private void SetMessageText(string text)
    {
        if (narrativeText != null)
        {
            narrativeText.text = text;
        }
    }

    private IEnumerator FadeCanvasGroup(
        float targetAlpha,
        float duration)
    {
        if (narrativeCanvasGroup == null)
            yield break;

        float startAlpha =
            narrativeCanvasGroup.alpha;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            narrativeCanvasGroup.alpha =
                Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    elapsed / duration
                );

            yield return null;
        }

        narrativeCanvasGroup.alpha = targetAlpha;
    }

    private void Update()
    {
        if (isReadingDiary)
        {
            if (CheckAdvanceInput())
            {
                AvancarPagina();
            }
        }
        else if (
            isShowingPanelNotice &&
            narrativeCanvasObject != null &&
            narrativeCanvasObject.activeSelf)
        {
            if (CheckAdvanceInput())
            {
                FecharAvisoPainel();
            }
        }
    }

    private bool CheckAdvanceInput()
    {
        try
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                Keyboard.current.spaceKey.wasPressedThisFrame)
                return true;
#endif

            var devices =
                new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();

            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.Controller,
                devices
            );

            bool isAnyPrimaryPressed = false;
            foreach (var device in devices)
            {
                if (
                    device.TryGetFeatureValue(
                        UnityEngine.XR.CommonUsages.primaryButton,
                        out bool pb
                    ) &&
                    pb)
                {
                    isAnyPrimaryPressed = true;
                    break;
                }
            }

            bool justPressed = isAnyPrimaryPressed && !wasPrimaryButtonPressed;
            wasPrimaryButtonPressed = isAnyPrimaryPressed;
            return justPressed;
        }
        catch
        {
        }

        return false;
    }

    private void LateUpdate()
    {
        // O canvas permanece em local fixo no mundo 3D após posicionado na frente do personagem,
        // sem flutuar, girar ou seguir dinamicamente a câmera.
    }

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

        if (isAlarmActive)
            return;

        isAlarmActive = true;

        Debug.Log(
            "[GerenteAlarme] Sequência de emergência ativada!"
        );

        if (emergencyRoutine != null)
            StopCoroutine(emergencyRoutine);

        emergencyRoutine =
            StartCoroutine(
                EmergencySequenceRoutine()
            );
    }

    private IEnumerator EmergencySequenceRoutine()
    {
        MostrarObjetivo();
        yield break;
    }

    public void PositionObjectiveCanvas()
    {
        GameObject targetObj = GetObjectiveCanvasObject();
        if (targetObj == null) return;

        targetObj.transform.position = new Vector3(-8.4399995803833f, 1.4908255338668824f, -1.3852607011795045f);
        targetObj.transform.rotation = new Quaternion(0.0f, -1.0f, 0.0f, 0.0f);
        targetObj.transform.localScale = new Vector3(0.0024999999441206457f, 0.0024999999441206457f, 0.0024999999441206457f);
    }

    private GameObject GetObjectiveCanvasObject()
    {
        if (objectiveCanvasObject != null) return objectiveCanvasObject;

        GameObject found = GameObject.Find("CanvasObjetivo") 
                        ?? GameObject.Find("Canvas_Objetivo") 
                        ?? GameObject.Find("ObjectiveCanvas");
        if (found != null)
        {
            objectiveCanvasObject = found;
            return objectiveCanvasObject;
        }

        return narrativeCanvasObject;
    }

    public void StopAlarm()
    {
        if (!isAlarmActive)
            return;

        isAlarmActive = false;

        Debug.Log(
            "[GerenteAlarme] Alarme e emergência desligados."
        );

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

        if (narrativeCanvasObject != null || objectiveCanvasObject != null)
        {
            StartCoroutine(
                DismissHologramRoutine()
            );
        }
    }

    private IEnumerator DismissHologramRoutine()
    {
        yield return StartCoroutine(
            FadeCanvasGroup(0f, 0.3f)
        );

        if (narrativeCanvasObject != null)
        {
            narrativeCanvasObject.SetActive(false);
        }

        if (objectiveCanvasObject != null)
        {
            objectiveCanvasObject.SetActive(false);
        }
    }

    public void MostrarObjetivo()
    {
        isGeneratorPowered = true;
        isAlarmActive = true;
        isReadingDiary = false;
        isShowingPanelNotice = false;

        GameObject objCanvas = GetObjectiveCanvasObject();
        if (objCanvas == null)
        {
            Debug.LogWarning(
                "[GerenteAlarme] Canvas ou texto não configurado."
            );
            return;
        }

        PositionObjectiveCanvas();
        SetHologramTheme(true);
        objCanvas.SetActive(true);

        // No momento em que o gerador é ligado, a música de alarme e as luzes de emergência iniciam juntas!
        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.StartAlarm();
        }

        if (emergencyLightsController != null)
        {
            emergencyLightsController.StartEmergencyLights();
        }

        string title = string.IsNullOrEmpty(energyRestoredTitle) ? "ENERGIA RESTAURADA" : energyRestoredTitle;
        string body = energyRestoredMessage;

        TMP_Text txt = objCanvas.GetComponentInChildren<TMP_Text>();
        if (txt == null) txt = narrativeText;
        if (txt != null)
        {
            txt.text =
                $"<size=85%><color=#F87171><b>{title}</b></color></size>\n\n\n\n\n" +
                $"<size=70%>{body}</size>\n";
        }

        if (btnVoltar != null)
            btnVoltar.gameObject.SetActive(false);

        if (btnAvancar != null)
            btnAvancar.gameObject.SetActive(false);

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        CanvasGroup cg = objCanvas.GetComponent<CanvasGroup>();
        if (cg == null) cg = narrativeCanvasGroup;
        if (cg != null) cg.alpha = 1f;
    }
}

public class AlarmManager : GerenteAlarme
{
}