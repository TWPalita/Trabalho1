using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gerencia a transição narrativa na DefenseScene por meio de um HUD Holográfico Sci-Fi
/// no canto superior esquerdo do visor, renderizado via SDF + Degradê na GPU (Custom/HologramHUDCard
/// e TextMeshPro/Distance Field - HologramHUD) com zero reconstrução de malha na CPU.
/// </summary>
public class IntroducaoDefesaVR : MonoBehaviour
{
    public static IntroducaoDefesaVR Instance { get; private set; }

    [Header("Canvases de Instrução (Legado - Desativados para não poluir a tela)")]
    [SerializeField] private GameObject canvas1Protocolo;
    [SerializeField] private GameObject canvas2Arma;
    [SerializeField] private CanvasGroup group1;
    [SerializeField] private CanvasGroup group2;

    [Header("Arma Física do Cockpit")]
    [SerializeField] private GameObject physicalWeaponObj;

    private bool isIntroActive = false;
    private bool hasEquippedWeaponOnce = false;
    private bool hasStartedDefense = false;
    private bool hasDismissedAfterFirstQuestion = false;
    private float weaponEquippedTime = 0f;
    private float combatCompactBlend = 0f;

    // Luz guia sobre a arma física no console
    private Light weaponGuideLight;

    // Referências do HUD de Objetivos no Canto da Tela
    private GameObject cornerHudCanvasObj;
    private CanvasGroup cornerHudCanvasGroup;
    private RectTransform cornerHudPanelRt;
    private Image cornerHudMainCardImage;
    private Image cornerHudDividerImage;
    private readonly Image[] cornerHudStepImages = new Image[2];
    private readonly RectTransform[] holoMoteRects = new RectTransform[10];
    private readonly Image[] holoMoteImages = new Image[10];
    private TMP_Text cornerHudHeaderText;
    private TMP_Text cornerHudObjectiveText;
    private TMP_Text cornerHudContextText;
    private Coroutine cornerPulseRoutine;

    // Materiais GPU de Holograma e Dissolução em Degradê (zero rebuild de Canvas/TMP)
    private Material cardMainMaterial;
    private Material cardElementMaterial;
    private Material cardDividerMaterial;
    private Material holoTmpMaterial;

    private static Sprite cachedSoftCircleSprite;

    // IDs de propriedades globais/materiais dos Shaders na GPU
    private static readonly int PropWorldToLocal = Shader.PropertyToID("_HoloHudWorldToLocal");
    private static readonly int PropDissolve = Shader.PropertyToID("_HoloHudDissolve");
    private static readonly int PropAccentColor = Shader.PropertyToID("_HoloHudAccentColor");
    private static readonly int PropSecondaryColor = Shader.PropertyToID("_HoloHudSecondaryColor");
    private static readonly int PropBgLeftColor = Shader.PropertyToID("_HoloHudBgLeftColor");
    private static readonly int PropBgRightColor = Shader.PropertyToID("_HoloHudBgRightColor");
    private static readonly int PropLocalDissolve = Shader.PropertyToID("_Dissolve");
    private static readonly int PropCardMode = Shader.PropertyToID("_CardMode");

    private readonly Vector3 cornerHudScale = new Vector3(0.00050f, 0.00050f, 0.00050f);

    public bool IsIntroActive => isIntroActive;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        BeginIntroSequence();
    }

    private void OnDestroy()
    {
        Shader.SetGlobalFloat(PropDissolve, 0f);
        if (cardMainMaterial != null) Destroy(cardMainMaterial);
        if (cardElementMaterial != null) Destroy(cardElementMaterial);
        if (cardDividerMaterial != null) Destroy(cardDividerMaterial);
        if (holoTmpMaterial != null) Destroy(holoTmpMaterial);
    }

    public void BeginIntroSequence()
    {
        isIntroActive = true;
        hasEquippedWeaponOnce = false;
        hasStartedDefense = false;
        hasDismissedAfterFirstQuestion = false;
        weaponEquippedTime = 0f;
        combatCompactBlend = 0f;

        Shader.SetGlobalFloat(PropDissolve, 0f);

        // Desativa quaisquer canvases legados se existirem na cena
        if (canvas1Protocolo != null) canvas1Protocolo.SetActive(false);
        if (canvas2Arma != null) canvas2Arma.SetActive(false);

        // 1. Garante a arma física configurada no console do cockpit e cria luz guia sobre ela
        EnsureWeaponInCockpit();
        EnsureWeaponGuideLight();

        // 2. Cria o HUD holográfico Sci-Fi no canto superior esquerdo da tela
        EnsureCornerObjectiveHUD();

        // 3. Prepara o visor do cockpit e aguarda o jogador empunhar a arma laser para iniciar as rodadas
        StartCoroutine(StartDefenseWithCornerObjectiveRoutine());
    }

    private IEnumerator StartDefenseWithCornerObjectiveRoutine()
    {
        yield return null;

        if (InterfaceVR.Instance != null)
        {
            InterfaceVR.Instance.HideGameOverPanel();
            InterfaceVR.Instance.SetCockpitHUDVisible(true);
            InterfaceVR.Instance.SetHUDRootElementsVisible(true);
            InterfaceVR.Instance.UpdateRound(1);
            InterfaceVR.Instance.UpdateMathQuestion("PEGUE A ARMA");
        }

        ArmaVR.SetAllCanShoot(true);

        // Se a arma já estiver equipada (ex: reinício rápido), inicia a defesa imediatamente
        if (IsAnyWeaponGrabbed())
        {
            ActivateDefenseAfterWeaponPickup();
            yield break;
        }

        // Exibe o objetivo inicial [1/2] da Defesa com paleta Âmbar-Dourado holográfica
        SetCornerObjective(
            "PROTOCOLO DE DEFESA  [1/2]",
            "Pegue a arma laser sobre o console à direita.",
            "A mira automática falhou! Aponte para a arma e pressione Gatilho ou Grip.",
            new Color(1.00f, 0.78f, 0.18f, 1f),
            new Color(0.95f, 0.42f, 0.08f, 1f),
            new Color(0.10f, 0.06f, 0.015f, 0.94f),
            new Color(0.22f, 0.12f, 0.03f, 0.58f)
        );
    }

    private void ActivateDefenseAfterWeaponPickup()
    {
        if (hasEquippedWeaponOnce) return;

        hasEquippedWeaponOnce = true;
        hasStartedDefense = true;
        isIntroActive = false;
        weaponEquippedTime = Time.unscaledTime;
        combatCompactBlend = 0f;

        if (weaponGuideLight != null)
        {
            weaponGuideLight.enabled = false;
        }

        if (GerenteDefesa.Instance != null && !GerenteDefesa.Instance.IsDefenseActive)
        {
            GerenteDefesa.Instance.StartDefense();
        }

        SetCornerObjective(
            "INTERCEPTAÇÃO ATIVA  [2/2]",
            "Dispare no meteoro com o resultado exato da conta.",
            "Atenção: atingir valores incorretos consome os escudos da nave!",
            new Color(0.18f, 0.92f, 1.00f, 1f),
            new Color(0.12f, 0.45f, 0.98f, 1f),
            new Color(0.012f, 0.045f, 0.12f, 0.94f),
            new Color(0.03f, 0.16f, 0.32f, 0.56f)
        );
    }

    private void Update()
    {
        if (cornerHudCanvasObj == null)
            return;

        // Pulsa suavemente a luz guia da arma enquanto o jogador ainda não a pegou
        if (!hasEquippedWeaponOnce && weaponGuideLight != null && weaponGuideLight.enabled)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            weaponGuideLight.intensity = Mathf.Lerp(0.85f, 1.55f, pulse);
        }

        // Permite pegar a arma clicando nela com o mouse no Editor (fallback rápido sem óculos VR)
        if (!hasEquippedWeaponOnce)
        {
            TryEditorMouseWeaponPickup();
        }

        // Assim que o jogador empunhar a arma laser no cockpit, inicia as rodadas e avança para [2/2]
        if (!hasEquippedWeaponOnce && IsAnyWeaponGrabbed())
        {
            ActivateDefenseAfterWeaponPickup();
        }

        // Oculta o HUD de canto quando a partida chega ao fim (Vitória ou Derrota) ou a partir da Rodada 2
        if (hasStartedDefense && GerenteDefesa.Instance != null)
        {
            bool defenseRunning = GerenteDefesa.Instance.IsDefenseActive;
            int currentRound = GerenteDefesa.Instance.CurrentRound;

            if (!defenseRunning)
            {
                if (cornerHudCanvasObj.activeSelf)
                {
                    cornerHudCanvasObj.SetActive(false);
                }
                return;
            }

            if (currentRound >= 2 && !hasDismissedAfterFirstQuestion)
            {
                DismissObjectiveHologramAfterFirstQuestion();
            }
        }
    }

    /// <summary>
    /// Chamado explicitamente pelo GerenteDefesa.StartDefense() ao iniciar ou reiniciar a partida.
    /// </summary>
    public void OnDefenseRestarted()
    {
        if (!hasEquippedWeaponOnce) return;

        if (cornerPulseRoutine != null)
        {
            StopCoroutine(cornerPulseRoutine);
            cornerPulseRoutine = null;
        }

        hasDismissedAfterFirstQuestion = false;
        weaponEquippedTime = Time.unscaledTime;
        combatCompactBlend = 0f;
        ResetHologramShaderState();
        SetCornerObjective(
            "INTERCEPTAÇÃO ATIVA  [2/2]",
            "Dispare no meteoro com o resultado exato da conta.",
            "Atenção: atingir valores incorretos consome os escudos da nave!",
            new Color(0.18f, 0.92f, 1.00f, 1f),
            new Color(0.12f, 0.45f, 0.98f, 1f),
            new Color(0.012f, 0.045f, 0.12f, 0.94f),
            new Color(0.03f, 0.16f, 0.32f, 0.56f)
        );
    }

    /// <summary>
    /// Desliga o HUD de objetivo com dissolução holográfica em degradê contínuo na GPU após a 1ª conta.
    /// </summary>
    public void DismissObjectiveHologramAfterFirstQuestion()
    {
        if (hasDismissedAfterFirstQuestion) return;
        hasDismissedAfterFirstQuestion = true;

        if (cornerHudCanvasObj == null || !cornerHudCanvasObj.activeSelf)
            return;

        if (cornerPulseRoutine != null) StopCoroutine(cornerPulseRoutine);
        cornerPulseRoutine = StartCoroutine(AnimateHologramGradientDissolveRoutine());
    }

    private void TryEditorMouseWeaponPickup()
    {
#if ENABLE_INPUT_SYSTEM
        if (physicalWeaponObj == null || UnityEngine.InputSystem.Mouse.current == null) return;
        if (!UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, 5f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;
            if (hits[i].collider.transform == physicalWeaponObj.transform ||
                hits[i].collider.transform.IsChildOf(physicalWeaponObj.transform))
            {
                var grab = physicalWeaponObj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                var interactor = FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor>();
                if (grab != null && interactor != null)
                {
                    grab.activated.Invoke(new UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs
                    {
                        interactorObject = interactor,
                        interactableObject = grab
                    });
                }
                break;
            }
        }
#endif
    }

    private void EnsureWeaponGuideLight()
    {
        if (weaponGuideLight != null || physicalWeaponObj == null) return;

        Transform existing = physicalWeaponObj.transform.Find("LuzGuia_ArmaConsole");
        if (existing != null)
        {
            weaponGuideLight = existing.GetComponent<Light>();
            return;
        }

        GameObject lightObj = new GameObject("LuzGuia_ArmaConsole");
        lightObj.transform.SetParent(physicalWeaponObj.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 4.5f, 0.5f);

        weaponGuideLight = lightObj.AddComponent<Light>();
        weaponGuideLight.type = LightType.Point;
        weaponGuideLight.color = new Color(0.20f, 0.95f, 0.65f, 1f);
        weaponGuideLight.intensity = 1.25f;
        weaponGuideLight.range = 0.85f;
        weaponGuideLight.shadows = LightShadows.None;
    }

    private void LateUpdate()
    {
        if (cornerHudCanvasObj != null && cornerHudCanvasObj.activeSelf)
        {
            Shader.SetGlobalMatrix(PropWorldToLocal, cornerHudCanvasObj.transform.worldToLocalMatrix);
        }
    }

    private bool IsAnyWeaponGrabbed()
    {
        for (int i = 0; i < ArmaVR.ActiveControllers.Count; i++)
        {
            ArmaVR ctrl = ArmaVR.ActiveControllers[i];
            if (ctrl != null && ctrl.IsGrabbed)
            {
                return true;
            }
        }
        return false;
    }

    private void EnsureWeaponInCockpit()
    {
        ArmaVR configuredWeapon = ArmaVR.EnsureScenePhysicalWeaponConfigured();
        if (configuredWeapon != null)
        {
            physicalWeaponObj = configuredWeapon.gameObject;
        }
    }

    public void OnClickEntendiProtocolo() { }
    public void OnClickIniciarDefesa() { }

    // =========================================================================
    // HUD HOLOGRÁFICO SCI-FI NO CANTO DA TELA (GPU SDF + DEGRADÊ CONTÍNUO)
    // =========================================================================

    private void EnsureHologramMaterials(TMP_FontAsset font)
    {
        Shader cardShader = Shader.Find("Custom/HologramHUDCard") ?? Shader.Find("UI/Default");

        if (cardMainMaterial == null)
        {
            cardMainMaterial = new Material(cardShader) { name = "HoloHUD_MainCard_Mat" };
            cardMainMaterial.SetFloat(PropCardMode, 0f);
        }

        if (cardElementMaterial == null)
        {
            cardElementMaterial = new Material(cardShader) { name = "HoloHUD_Element_Mat" };
            cardElementMaterial.SetFloat(PropCardMode, 1f);
        }

        if (cardDividerMaterial == null)
        {
            cardDividerMaterial = new Material(cardShader) { name = "HoloHUD_Divider_Mat" };
            cardDividerMaterial.SetFloat(PropCardMode, 2f);
        }

        if (holoTmpMaterial == null && font != null && font.material != null)
        {
            holoTmpMaterial = new Material(font.material) { name = "LiberationSans_HoloHUD_Mat" };
            Shader tmpHoloShader = Shader.Find("TextMeshPro/Distance Field - HologramHUD");
            if (tmpHoloShader != null)
            {
                holoTmpMaterial.shader = tmpHoloShader;
            }
        }

        EnsureSoftCircleSprite();
    }

    private static void EnsureSoftCircleSprite()
    {
        if (cachedSoftCircleSprite != null) return;

        const int res = 32;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            name = "HoloMoteRadial_Tex",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float center = (res - 1) * 0.5f;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float alpha = Mathf.Pow(1f - d, 2.4f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply(false, false);

        cachedSoftCircleSprite = Sprite.Create(tex, new Rect(0f, 0f, res, res), new Vector2(0.5f, 0.5f), 100f);
    }

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
        EnsureHologramMaterials(font);

        cornerHudCanvasObj = new GameObject("CanvasHudObjetivoDefesa");
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

        GameObject panelObj = new GameObject("PainelCanto", typeof(RectTransform));
        panelObj.transform.SetParent(cornerHudCanvasObj.transform, false);
        cornerHudPanelRt = panelObj.GetComponent<RectTransform>();
        cornerHudPanelRt.anchorMin = Vector2.zero;
        cornerHudPanelRt.anchorMax = Vector2.one;
        cornerHudPanelRt.offsetMin = Vector2.zero;
        cornerHudPanelRt.offsetMax = Vector2.zero;

        // 1. Corpo Principal do Holograma Sci-Fi (SDF chanfrado + degradê de vidro + scanlines + cantoneiras na GPU)
        GameObject cardObj = new GameObject("FundoHologramaSDF", typeof(RectTransform));
        cardObj.transform.SetParent(panelObj.transform, false);
        cornerHudMainCardImage = cardObj.AddComponent<Image>();
        cornerHudMainCardImage.material = cardMainMaterial;
        cornerHudMainCardImage.color = Color.white;
        cornerHudMainCardImage.raycastTarget = false;
        RectTransform cardRt = cardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = Vector2.zero;
        cardRt.anchorMax = Vector2.one;
        cardRt.offsetMin = Vector2.zero;
        cardRt.offsetMax = Vector2.zero;

        // 2. Título Tático Superior
        GameObject headerObj = new GameObject("TituloHudCanto", typeof(RectTransform));
        headerObj.transform.SetParent(panelObj.transform, false);
        cornerHudHeaderText = headerObj.AddComponent<TextMeshProUGUI>();
        if (font != null) cornerHudHeaderText.font = font;
        if (holoTmpMaterial != null) cornerHudHeaderText.fontSharedMaterial = holoTmpMaterial;
        cornerHudHeaderText.fontSize = 22f;
        cornerHudHeaderText.fontStyle = FontStyles.Bold;
        cornerHudHeaderText.characterSpacing = 3.5f;
        cornerHudHeaderText.alignment = TextAlignmentOptions.MidlineLeft;
        cornerHudHeaderText.color = new Color(0.22f, 0.92f, 1f, 1f);
        cornerHudHeaderText.raycastTarget = false;
        RectTransform headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 0.72f);
        headerRt.anchorMax = new Vector2(0.74f, 0.95f);
        headerRt.offsetMin = new Vector2(30f, 0f);
        headerRt.offsetMax = new Vector2(-8f, 0f);

        // 3. Indicadores de Estágio (2 barras de energia no canto superior direito)
        for (int i = 0; i < 2; i++)
        {
            GameObject stepObj = new GameObject($"ProgressoDefesa_{i + 1}", typeof(RectTransform));
            stepObj.transform.SetParent(panelObj.transform, false);
            Image stepImg = stepObj.AddComponent<Image>();
            stepImg.material = cardElementMaterial;
            stepImg.color = new Color(0.22f, 0.88f, 1f, 0.25f);
            stepImg.raycastTarget = false;
            RectTransform stepRt = stepObj.GetComponent<RectTransform>();
            float xMin = 0.75f + (i * 0.105f);
            float xMax = xMin + 0.088f;
            stepRt.anchorMin = new Vector2(xMin, 0.80f);
            stepRt.anchorMax = new Vector2(xMax, 0.87f);
            stepRt.offsetMin = Vector2.zero;
            stepRt.offsetMax = Vector2.zero;
            cornerHudStepImages[i] = stepImg;
        }

        // 4. Linha Divisória Laser em Degradê Horizontal
        GameObject divObj = new GameObject("LinhaDivisoriaDegrade", typeof(RectTransform));
        divObj.transform.SetParent(panelObj.transform, false);
        cornerHudDividerImage = divObj.AddComponent<Image>();
        cornerHudDividerImage.material = cardDividerMaterial;
        cornerHudDividerImage.color = new Color(1f, 1f, 1f, 0.65f);
        cornerHudDividerImage.raycastTarget = false;
        RectTransform divRt = divObj.GetComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0f, 0.695f);
        divRt.anchorMax = new Vector2(1f, 0.708f);
        divRt.offsetMin = new Vector2(28f, 0f);
        divRt.offsetMax = new Vector2(-24f, 0f);

        // 5. Texto do Objetivo Principal
        GameObject objTextGO = new GameObject("TextoObjetivoPrincipal", typeof(RectTransform));
        objTextGO.transform.SetParent(panelObj.transform, false);
        cornerHudObjectiveText = objTextGO.AddComponent<TextMeshProUGUI>();
        if (font != null) cornerHudObjectiveText.font = font;
        if (holoTmpMaterial != null) cornerHudObjectiveText.fontSharedMaterial = holoTmpMaterial;
        cornerHudObjectiveText.fontSize = 27.5f;
        cornerHudObjectiveText.fontStyle = FontStyles.Bold;
        cornerHudObjectiveText.alignment = TextAlignmentOptions.MidlineLeft;
        cornerHudObjectiveText.textWrappingMode = TextWrappingModes.Normal;
        cornerHudObjectiveText.color = new Color(0.97f, 0.99f, 1f, 1f);
        cornerHudObjectiveText.raycastTarget = false;
        RectTransform objTextRt = objTextGO.GetComponent<RectTransform>();
        objTextRt.anchorMin = new Vector2(0f, 0.35f);
        objTextRt.anchorMax = new Vector2(1f, 0.68f);
        objTextRt.offsetMin = new Vector2(30f, 0f);
        objTextRt.offsetMax = new Vector2(-22f, 0f);

        // 6. Subtexto de Contexto Tático
        GameObject ctxTextGO = new GameObject("TextoContextoHistoria", typeof(RectTransform));
        ctxTextGO.transform.SetParent(panelObj.transform, false);
        cornerHudContextText = ctxTextGO.AddComponent<TextMeshProUGUI>();
        if (font != null) cornerHudContextText.font = font;
        if (holoTmpMaterial != null) cornerHudContextText.fontSharedMaterial = holoTmpMaterial;
        cornerHudContextText.fontSize = 20.5f;
        cornerHudContextText.alignment = TextAlignmentOptions.MidlineLeft;
        cornerHudContextText.textWrappingMode = TextWrappingModes.Normal;
        cornerHudContextText.color = new Color(0.76f, 0.90f, 1.00f, 0.92f);
        cornerHudContextText.raycastTarget = false;
        RectTransform ctxTextRt = ctxTextGO.GetComponent<RectTransform>();
        ctxTextRt.anchorMin = new Vector2(0f, 0.06f);
        ctxTextRt.anchorMax = new Vector2(1f, 0.34f);
        ctxTextRt.offsetMin = new Vector2(30f, 0f);
        ctxTextRt.offsetMax = new Vector2(-24f, 0f);

        // 7. Partículas de Fótons Holográficos (pré-alocadas para flutuar na borda do degradê ao fechar)
        for (int i = 0; i < holoMoteRects.Length; i++)
        {
            GameObject moteObj = new GameObject($"HoloPhotonMote_{i}", typeof(RectTransform));
            moteObj.transform.SetParent(panelObj.transform, false);
            Image moteImg = moteObj.AddComponent<Image>();
            moteImg.sprite = cachedSoftCircleSprite;
            moteImg.color = new Color(0.35f, 0.96f, 1f, 0f);
            moteImg.raycastTarget = false;

            RectTransform moteRt = moteObj.GetComponent<RectTransform>();
            moteRt.anchorMin = new Vector2(0.5f, 0.5f);
            moteRt.anchorMax = new Vector2(0.5f, 0.5f);
            float sz = (i % 3 == 0) ? 16f : 10f;
            moteRt.sizeDelta = new Vector2(sz, sz);

            moteObj.SetActive(false);
            holoMoteRects[i] = moteRt;
            holoMoteImages[i] = moteImg;
        }

        UpdateCornerHudPlacement();
        Shader.SetGlobalMatrix(PropWorldToLocal, cornerHudCanvasObj.transform.worldToLocalMatrix);
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

        const float depth = 0.78f;
        float halfH = depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfW = halfH * cam.aspect;

        float baseWorldHeight = UnityEngine.XR.XRSettings.isDeviceActive ? (halfH * 0.46f) : (halfH * 0.56f);
        float compactMultiplier = Mathf.Lerp(1f, 0.68f, combatCompactBlend);
        float dynamicScale = (baseWorldHeight * compactMultiplier) / 230f;

        float cardHalfW = (640f * dynamicScale) * 0.5f;
        float cardHalfH = (230f * dynamicScale) * 0.5f;

        float localX;
        float localY;

        if (UnityEngine.XR.XRSettings.isDeviceActive)
        {
            localX = Mathf.Clamp(-halfW * 0.46f, -0.26f, -0.12f);
            localY = Mathf.Clamp(halfH * Mathf.Lerp(0.46f, 0.54f, combatCompactBlend), 0.10f, 0.22f);
        }
        else
        {
            localX = -halfW + cardHalfW + (halfW * 0.028f);
            localY = halfH - cardHalfH - (halfH * Mathf.Lerp(0.042f, 0.025f, combatCompactBlend));
        }

        cornerHudCanvasObj.transform.localPosition = new Vector3(localX, localY, depth);
        cornerHudCanvasObj.transform.localRotation = Quaternion.Euler(2f, -4f, 0f);
        cornerHudCanvasObj.transform.localScale = new Vector3(dynamicScale, dynamicScale, dynamicScale);
    }

    public void SetCornerObjective(string header, string mainObjective, string context, Color accentColor, Color bgColor)
    {
        Color secondary = new Color(accentColor.r * 0.55f, accentColor.g * 0.65f, Mathf.Min(1f, accentColor.b + 0.2f), 1f);
        Color bgRight = new Color(bgColor.r * 1.8f + 0.02f, bgColor.g * 2.1f + 0.05f, bgColor.b * 2.0f + 0.08f, 0.56f);
        SetCornerObjective(header, mainObjective, context, accentColor, secondary, bgColor, bgRight);
    }

    public void SetCornerObjective(
        string header,
        string mainObjective,
        string context,
        Color accentColor,
        Color secondaryColor,
        Color bgLeftColor,
        Color bgRightColor)
    {
        EnsureCornerObjectiveHUD();
        if (cornerHudCanvasObj == null) return;

        ResetHologramShaderState();
        ApplyHologramPalette(accentColor, secondaryColor, bgLeftColor, bgRightColor);

        int activeSteps = (!string.IsNullOrEmpty(header) && header.Contains("[2/2]")) ? 2 : 1;
        for (int i = 0; i < cornerHudStepImages.Length; i++)
        {
            if (cornerHudStepImages[i] == null) continue;
            cornerHudStepImages[i].color = (i < activeSteps)
                ? new Color(accentColor.r, accentColor.g, accentColor.b, 0.96f)
                : new Color(accentColor.r, accentColor.g, accentColor.b, 0.20f);
        }

        if (cornerHudHeaderText != null)
        {
            cornerHudHeaderText.color = accentColor;
            cornerHudHeaderText.text = header;
        }

        if (cornerHudObjectiveText != null)
        {
            cornerHudObjectiveText.text = mainObjective;
        }

        if (cornerHudContextText != null)
        {
            cornerHudContextText.text = context ?? "";
            cornerHudContextText.gameObject.SetActive(!string.IsNullOrEmpty(context));
        }

        UpdateCornerHudPlacement();
        Shader.SetGlobalMatrix(PropWorldToLocal, cornerHudCanvasObj.transform.worldToLocalMatrix);
        cornerHudCanvasObj.SetActive(true);

        if (cornerPulseRoutine != null) StopCoroutine(cornerPulseRoutine);
        cornerPulseRoutine = StartCoroutine(AnimateCornerHudAppearRoutine());
    }

    private void ApplyHologramPalette(Color accent, Color secondary, Color bgLeft, Color bgRight)
    {
        Shader.SetGlobalColor(PropAccentColor, accent);
        Shader.SetGlobalColor(PropSecondaryColor, secondary);
        Shader.SetGlobalColor(PropBgLeftColor, bgLeft);
        Shader.SetGlobalColor(PropBgRightColor, bgRight);

        if (cardMainMaterial != null)
        {
            cardMainMaterial.SetColor("_AccentColor", accent);
            cardMainMaterial.SetColor("_SecondaryColor", secondary);
            cardMainMaterial.SetColor("_BgLeftColor", bgLeft);
            cardMainMaterial.SetColor("_BgRightColor", bgRight);
        }
        if (cardDividerMaterial != null)
        {
            cardDividerMaterial.SetColor("_AccentColor", accent);
            cardDividerMaterial.SetColor("_SecondaryColor", secondary);
        }
    }

    private void SetHologramDissolveValue(float dissolve)
    {
        Shader.SetGlobalFloat(PropDissolve, dissolve);
        if (cardMainMaterial != null) cardMainMaterial.SetFloat(PropLocalDissolve, dissolve);
        if (cardElementMaterial != null) cardElementMaterial.SetFloat(PropLocalDissolve, dissolve);
        if (cardDividerMaterial != null) cardDividerMaterial.SetFloat(PropLocalDissolve, dissolve);
    }

    private void ResetHologramShaderState()
    {
        SetHologramDissolveValue(0f);

        if (cornerHudPanelRt != null)
        {
            cornerHudPanelRt.localScale = Vector3.one;
            cornerHudPanelRt.anchoredPosition = Vector2.zero;
        }

        for (int i = 0; i < holoMoteRects.Length; i++)
        {
            if (holoMoteRects[i] != null)
            {
                holoMoteRects[i].gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator AnimateCornerHudAppearRoutine()
    {
        if (cornerHudCanvasGroup == null || cornerHudPanelRt == null) yield break;

        // Entrada em degradê suave da esquerda para a direita
        const float duration = 0.34f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            cornerHudCanvasGroup.alpha = ease;
            SetHologramDissolveValue(Mathf.Lerp(0.45f, 0f, ease));
            cornerHudPanelRt.localScale = Vector3.Lerp(new Vector3(0.96f, 0.96f, 1f), Vector3.one, ease);
            yield return null;
        }

        cornerHudCanvasGroup.alpha = 1f;
        SetHologramDissolveValue(0f);
        cornerHudPanelRt.localScale = Vector3.one;
        cornerPulseRoutine = null;
    }

    private IEnumerator AnimateHologramGradientDissolveRoutine()
    {
        if (cornerHudCanvasGroup == null || cornerHudPanelRt == null)
        {
            if (cornerHudCanvasObj != null) cornerHudCanvasObj.SetActive(false);
            cornerPulseRoutine = null;
            yield break;
        }

        // 1. Confirmação visual instantânea de objetivo assimilado (transição suave para Ciano-Esmeralda luminoso)
        Color startAccent = new Color(0.18f, 0.92f, 1.00f, 1f);
        Color doneAccent = new Color(0.20f, 1.00f, 0.82f, 1f);
        Color doneSecondary = new Color(0.12f, 0.72f, 1.00f, 1f);
        Color bgLeft = new Color(0.012f, 0.055f, 0.12f, 0.94f);
        Color bgRight = new Color(0.03f, 0.20f, 0.34f, 0.56f);

        if (cornerHudHeaderText != null)
        {
            cornerHudHeaderText.text = "SISTEMA DE MIRA CALIBRADO  [OK]";
            cornerHudHeaderText.color = doneAccent;
        }

        ApplyHologramPalette(doneAccent, doneSecondary, bgLeft, bgRight);

        // Ativa partículas sutis de fótons holográficos que acompanharão a frente do degradê
        for (int i = 0; i < holoMoteRects.Length; i++)
        {
            if (holoMoteRects[i] != null)
            {
                holoMoteRects[i].gameObject.SetActive(true);
            }
        }

        // 2. Dissolução contínua em degradê de 62% de largura na GPU (zero rebuild de CPU, 90 FPS cravados)
        const float duration = 0.88f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);

            // Curva cúbica suave (aceleração orgânica e final sedoso)
            float smoothT = rawT * rawT * (3f - 2f * rawT);

            // Atualiza o degradê na GPU (afeta fundo, borda, scanlines e textos simultaneamente)
            SetHologramDissolveValue(smoothT);

            // Fótons luminosos flutuam suavemente na faixa onde o degradê está evaporando
            float frontX = Mathf.Lerp(280f, -290f, smoothT);
            float moteGlobalAlpha = Mathf.Sin(rawT * Mathf.PI);

            for (int i = 0; i < holoMoteRects.Length; i++)
            {
                RectTransform mRt = holoMoteRects[i];
                Image mImg = holoMoteImages[i];
                if (mRt == null || mImg == null) continue;

                float normIdx = (i / (float)holoMoteRects.Length) - 0.5f;
                float offsetX = Mathf.Sin(i * 2.1f + rawT * 5f) * 55f;
                float baseY = normIdx * 175f;
                float driftY = rawT * (22f + (i % 3) * 12f);

                mRt.anchoredPosition = new Vector2(frontX + offsetX, baseY + driftY);
                float individualPulse = 0.5f + 0.5f * Mathf.Sin(rawT * 10f + i * 1.7f);
                mImg.color = new Color(0.35f, 0.98f, 1f, moteGlobalAlpha * individualPulse * 0.78f);
            }

            // Transparência global complementar nos últimos 35% para garantir desaparecimento 100% imperceptível
            float tailFade = 1f - Mathf.SmoothStep(0.62f, 1.0f, rawT);
            cornerHudCanvasGroup.alpha = tailFade;

            // Leve deslize espacial de 1.5% enquanto desmaterializa
            cornerHudPanelRt.localScale = new Vector3(
                Mathf.Lerp(1f, 0.985f, smoothT),
                Mathf.Lerp(1f, 0.975f, smoothT),
                1f
            );

            yield return null;
        }

        cornerHudCanvasGroup.alpha = 0f;
        ResetHologramShaderState();

        if (cornerHudCanvasObj != null)
        {
            cornerHudCanvasObj.SetActive(false);
        }
        cornerPulseRoutine = null;
    }
}
