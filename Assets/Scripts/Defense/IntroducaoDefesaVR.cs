using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>
/// Controla a sequência de introdução narrativa na DefenseScene antes do início da batalha espacial:
/// 1. Exibe o Canvas 1: PROTOCOLO DE INTERCEPTAÇÃO / SISTEMA DE DEFESA ATIVADO com botão [ ENTENDI ];
/// 2. Exibe o Canvas 2: CONTROLE DA ARMA / PREPARE-SE PARA A INTERCEPTAÇÃO com botão [ INICIAR DEFESA ];
/// 3. Bloqueia o combate até que o jogador clique em [ INICIAR DEFESA ];
/// 4. Inicia a defesa espacial e libera os disparos.
/// </summary>
public class IntroducaoDefesaVR : MonoBehaviour
{
    public static IntroducaoDefesaVR Instance { get; private set; }

    [Header("Canvases de Instrução (World Space)")]
    [SerializeField] private GameObject canvas1Protocolo;
    [SerializeField] private GameObject canvas2Arma;
    [SerializeField] private CanvasGroup group1;
    [SerializeField] private CanvasGroup group2;

    [Header("Arma Física do Cockpit")]
    [SerializeField] private GameObject physicalWeaponObj;

    private bool isIntroActive = true;
    private int currentStep = 1; // 1 = Canvas 1 (Protocolo), 2 = Canvas 2 (Arma), 0 = Concluído
    private float inputCooldown = 0.5f;
    private bool wasTriggerOrPrimaryPressed = false;
    private readonly List<UnityEngine.XR.InputDevice> xrControllers = new List<UnityEngine.XR.InputDevice>();

    public bool IsIntroActive => isIntroActive;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        BeginIntroSequence();
    }

    public void BeginIntroSequence()
    {
        isIntroActive = true;
        currentStep = 1;
        inputCooldown = 0.45f;

        // 1. Bloqueia disparos de combate durante os canvases de introdução
        ArmaVR.SetAllCanShoot(false);

        // 2. Garante a configuração da arma da cena e dos canvases de história
        EnsureWeaponInCockpit();
        EnsureInstructionCanvases();

        // 3. Oculta temporariamente o HUD de combate para foco total na história e instruções
        if (InterfaceVR.Instance != null)
        {
            InterfaceVR.Instance.HideGameOverPanel();
            InterfaceVR.Instance.SetCockpitHUDVisible(false);
            InterfaceVR.Instance.SetHUDRootElementsVisible(false);
        }

        // 4. Exibe o primeiro Canvas (Protocolo de Interceptação)
        ShowCanvas1();
    }

    private void Update()
    {
        if (!isIntroActive) return;

        if (inputCooldown > 0f)
        {
            inputCooldown -= Time.deltaTime;
            return;
        }

        // 1. Suporte a clique com o Mouse no Editor / Simulator diretamente sobre o botão ou Canvas
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null &&
            UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                Ray ray = cam.ScreenPointToRay(mousePos);
                if (TryRaycastInstructionButton(ray))
                {
                    return;
                }
            }
        }

        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                TriggerCurrentStepButton();
                return;
            }
        }
#endif

        // 2. Suporte a controles VR (Gatilho apontando para o botão/canvas ou Botão Primário A/X)
        bool pressedNow = false;
        xrControllers.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, xrControllers);
        for (int i = 0; i < xrControllers.Count; i++)
        {
            var dev = xrControllers[i];
            if (!dev.isValid) continue;

            if (dev.TryGetFeatureValue(CommonUsages.primaryButton, out bool prim) && prim)
                pressedNow = true;
            if (dev.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigBtn) && trigBtn)
                pressedNow = true;
            else if (dev.TryGetFeatureValue(CommonUsages.trigger, out float trigVal) && trigVal > 0.55f)
                pressedNow = true;
        }

        if (pressedNow && !wasTriggerOrPrimaryPressed)
        {
            wasTriggerOrPrimaryPressed = true;
            TriggerCurrentStepButton();
            return;
        }

        if (!pressedNow)
        {
            wasTriggerOrPrimaryPressed = false;
        }
    }

    private bool TryRaycastInstructionButton(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, 25f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;
            Button btn = hits[i].collider.GetComponentInParent<Button>();
            if (btn != null && btn.interactable &&
                ((canvas1Protocolo != null && canvas1Protocolo.activeInHierarchy && btn.transform.IsChildOf(canvas1Protocolo.transform)) ||
                 (canvas2Arma != null && canvas2Arma.activeInHierarchy && btn.transform.IsChildOf(canvas2Arma.transform))))
            {
                inputCooldown = 0.45f;
                btn.onClick.Invoke();
                return true;
            }
        }
        return false;
    }

    private void TriggerCurrentStepButton()
    {
        inputCooldown = 0.5f;
        if (currentStep == 1)
        {
            OnClickEntendiProtocolo();
        }
        else if (currentStep == 2)
        {
            OnClickIniciarDefesa();
        }
    }

    private void ShowCanvas1()
    {
        currentStep = 1;
        if (canvas1Protocolo != null) canvas1Protocolo.SetActive(true);
        if (canvas2Arma != null) canvas2Arma.SetActive(false);
        if (group1 != null)
        {
            group1.alpha = 1f;
            StartCoroutine(FadeRoutine(group1, 0f, 1f, 0.3f));
        }
    }

    public void OnClickEntendiProtocolo()
    {
        if (!isIntroActive || currentStep != 1) return;
        currentStep = 2;
        inputCooldown = 0.5f;

        if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
        StartCoroutine(TransitionToCanvas2Routine());
    }

    private IEnumerator TransitionToCanvas2Routine()
    {
        if (group1 != null) yield return StartCoroutine(FadeRoutine(group1, 1f, 0f, 0.2f));
        if (canvas1Protocolo != null) canvas1Protocolo.SetActive(false);

        if (canvas2Arma != null) canvas2Arma.SetActive(true);
        if (group2 != null)
        {
            group2.alpha = 0f;
            yield return StartCoroutine(FadeRoutine(group2, 0f, 1f, 0.3f));
        }
    }

    public void OnClickIniciarDefesa()
    {
        if (!isIntroActive || currentStep != 2) return;
        isIntroActive = false;
        currentStep = 0;
        inputCooldown = 0.5f;

        if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
        StartCoroutine(StartDefenseSequenceRoutine());
    }

    private IEnumerator StartDefenseSequenceRoutine()
    {
        if (group2 != null) yield return StartCoroutine(FadeRoutine(group2, 1f, 0f, 0.25f));
        if (canvas2Arma != null) canvas2Arma.SetActive(false);

        // 1. Exibe o HUD do Cockpit
        if (InterfaceVR.Instance != null)
        {
            InterfaceVR.Instance.SetHUDRootElementsVisible(true);
            InterfaceVR.Instance.SetCockpitHUDVisible(true);
        }

        // 2. Libera disparos e inicia a defesa no GerenteDefesa
        ArmaVR.SetAllCanShoot(true);
        if (GerenteDefesa.Instance != null)
        {
            GerenteDefesa.Instance.StartDefense();
        }
    }

    private IEnumerator FadeRoutine(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    private void EnsureWeaponInCockpit()
    {
        ArmaVR configuredWeapon = ArmaVR.EnsureScenePhysicalWeaponConfigured();
        if (configuredWeapon != null)
        {
            physicalWeaponObj = configuredWeapon.gameObject;
        }
    }

    /// <summary>
    /// Constrói os dois World Space Canvases de história e instruções em frente à cadeira do piloto,
    /// posicionados acima do painel de controle para que o botão fique 100% visível e clicável.
    /// </summary>
    private void EnsureInstructionCanvases()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        // Posicionado acima do MainControlPanel, na altura dos olhos do jogador sentado na cadeira
        Vector3 canvasPos = new Vector3(-0.18f, 1.85f, 1.18f);
        Quaternion canvasRot = Quaternion.Euler(2f, 0f, 0f);

        // === 1. AO CHEGAR À CENA DE DEFESA ===
        if (canvas1Protocolo == null)
        {
            string msgCanvas1 =
                "Energia restabelecida. O sistema de defesa da nave foi ativado, mas os danos causados pelo ataque dos piratas espaciais comprometeram o sistema de mira automática.\n\n" +
                "Os sensores identificaram detritos espaciais que podem atingir a embarcação. Sem a mira automática, você precisará controlar os disparos manualmente.\n\n" +
                "O computador de bordo fornecerá operações matemáticas para ajudar a identificar os alvos. Resolva cada operação e encontre o detrito que apresenta o resultado correto.\n\n" +
                "Neutralize as ameaças antes que os escudos da nave se esgotem.";

            canvas1Protocolo = CreateInstructionCanvas(
                "Canvas1_ProtocoloInterceptacao",
                canvasPos,
                canvasRot,
                "PROTOCOLO DE INTERCEPTAÇÃO",
                "SISTEMA DE DEFESA ATIVADO",
                msgCanvas1,
                "Cuidado: disparos incorretos e impactos dos detritos reduzem a energia dos escudos.",
                "[ ENTENDI ]",
                OnClickEntendiProtocolo,
                font,
                out group1
            );
        }

        // === 2. INSTRUÇÕES PARA UTILIZAR A ARMA ===
        if (canvas2Arma == null)
        {
            string msgCanvas2 =
                "A defesa será realizada manualmente. Utilize a arma laser disponível na cabine para destruir os detritos que ameaçam a nave.\n\n" +
                "<color=#38BDF8><b>COMO JOGAR</b></color>\n" +
                "1. Pegue a arma utilizando o controle VR.\n" +
                "2. Observe a operação matemática apresentada no painel.\n" +
                "3. Resolva a operação.\n" +
                "4. Localize o detrito identificado com o resultado correto.\n" +
                "5. Mire no alvo e pressione o gatilho para disparar.\n" +
                "6. Repita o processo para neutralizar as ameaças restantes.";

            canvas2Arma = CreateInstructionCanvas(
                "Canvas2_ControleDaArma",
                canvasPos,
                canvasRot,
                "CONTROLE DA ARMA",
                "PREPARE-SE PARA A INTERCEPTAÇÃO",
                msgCanvas2,
                "Errar o alvo ou permitir que um detrito atinja a nave reduz a energia dos escudos. Se ela chegar a zero, a missão será encerrada.",
                "[ INICIAR DEFESA ]",
                OnClickIniciarDefesa,
                font,
                out group2
            );
            canvas2Arma.SetActive(false);
        }
    }

    private GameObject CreateInstructionCanvas(
        string name,
        Vector3 pos,
        Quaternion rot,
        string title,
        string subtitle,
        string body,
        string warning,
        string buttonText,
        UnityEngine.Events.UnityAction buttonAction,
        TMP_FontAsset font,
        out CanvasGroup group)
    {
        GameObject cObj = new GameObject(name);
        cObj.transform.position = pos;
        cObj.transform.rotation = rot;

        Canvas canvas = cObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 150;
        canvas.worldCamera = Camera.main;

        cObj.AddComponent<GraphicRaycaster>();
        if (cObj.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
        {
            cObj.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        RectTransform rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(920f, 580f);
        rt.localScale = new Vector3(0.0019f, 0.0019f, 0.0019f);

        group = cObj.AddComponent<CanvasGroup>();
        group.alpha = 1f;

        // Fundo escuro translúcido do cockpit
        GameObject bgObj = new GameObject("Fundo");
        bgObj.transform.SetParent(cObj.transform, false);
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.02f, 0.05f, 0.13f, 0.96f);
        bgImg.raycastTarget = false;
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // Borda Superior luminosa
        GameObject borderTop = new GameObject("BordaTop");
        borderTop.transform.SetParent(cObj.transform, false);
        var bImg = borderTop.AddComponent<Image>();
        bImg.color = new Color(0.22f, 0.74f, 0.97f, 0.9f);
        bImg.raycastTarget = false;
        RectTransform bRt = borderTop.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.04f, 0.965f);
        bRt.anchorMax = new Vector2(0.96f, 0.975f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        // Título
        GameObject titleObj = new GameObject("Titulo");
        titleObj.transform.SetParent(cObj.transform, false);
        var tTxt = titleObj.AddComponent<TextMeshProUGUI>();
        if (font != null) tTxt.font = font;
        tTxt.fontSize = 30f;
        tTxt.fontStyle = FontStyles.Bold;
        tTxt.alignment = TextAlignmentOptions.Center;
        tTxt.color = new Color(0.22f, 0.78f, 1f);
        tTxt.raycastTarget = false;
        tTxt.text = title;
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.04f, 0.87f);
        tRt.anchorMax = new Vector2(0.96f, 0.955f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        // Subtítulo
        GameObject subObj = new GameObject("Subtitulo");
        subObj.transform.SetParent(cObj.transform, false);
        var sTxt = subObj.AddComponent<TextMeshProUGUI>();
        if (font != null) sTxt.font = font;
        sTxt.fontSize = 19f;
        sTxt.fontStyle = FontStyles.Bold;
        sTxt.alignment = TextAlignmentOptions.Center;
        sTxt.color = new Color(0.65f, 0.88f, 1f, 0.95f);
        sTxt.raycastTarget = false;
        sTxt.text = subtitle;
        RectTransform sRt = subObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.04f, 0.80f);
        sRt.anchorMax = new Vector2(0.96f, 0.87f);
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;

        // Corpo da Mensagem
        GameObject bodyObj = new GameObject("CorpoTexto");
        bodyObj.transform.SetParent(cObj.transform, false);
        var bTxt = bodyObj.AddComponent<TextMeshProUGUI>();
        if (font != null) bTxt.font = font;
        bTxt.fontSize = 17.5f;
        bTxt.lineSpacing = 4f;
        bTxt.alignment = TextAlignmentOptions.Center;
        bTxt.textWrappingMode = TextWrappingModes.Normal;
        bTxt.color = Color.white;
        bTxt.raycastTarget = false;
        bTxt.text = body;
        RectTransform bTextRt = bodyObj.GetComponent<RectTransform>();
        bTextRt.anchorMin = new Vector2(0.06f, 0.29f);
        bTextRt.anchorMax = new Vector2(0.94f, 0.79f);
        bTextRt.offsetMin = Vector2.zero;
        bTextRt.offsetMax = Vector2.zero;

        // Aviso
        GameObject warnObj = new GameObject("Aviso");
        warnObj.transform.SetParent(cObj.transform, false);
        var wTxt = warnObj.AddComponent<TextMeshProUGUI>();
        if (font != null) wTxt.font = font;
        wTxt.fontSize = 16.5f;
        wTxt.fontStyle = FontStyles.Bold;
        wTxt.alignment = TextAlignmentOptions.Center;
        wTxt.textWrappingMode = TextWrappingModes.Normal;
        wTxt.color = new Color(1f, 0.42f, 0.38f);
        wTxt.raycastTarget = false;
        wTxt.text = warning;
        RectTransform wRt = warnObj.GetComponent<RectTransform>();
        wRt.anchorMin = new Vector2(0.06f, 0.18f);
        wRt.anchorMax = new Vector2(0.94f, 0.28f);
        wRt.offsetMin = Vector2.zero;
        wRt.offsetMax = Vector2.zero;

        // Botão de ação ([ ENTENDI ] / [ INICIAR DEFESA ]) bem destacado dentro da parte inferior do Canvas
        GameObject btnObj = new GameObject("BotaoAcao");
        btnObj.transform.SetParent(cObj.transform, false);
        RectTransform btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.28f, 0.045f);
        btnRt.anchorMax = new Vector2(0.72f, 0.155f);
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;

        btnObj.AddComponent<CanvasRenderer>();
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.12f, 0.52f, 0.85f, 1f);
        btnImg.raycastTarget = true;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.12f, 0.52f, 0.85f, 1f);
        cb.highlightedColor = new Color(0.25f, 0.80f, 1.00f, 1.00f);
        cb.pressedColor = new Color(0.06f, 0.28f, 0.50f, 1.00f);
        cb.selectedColor = new Color(0.20f, 0.68f, 0.95f, 1.00f);
        btn.colors = cb;
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(buttonAction);

        BoxCollider col = btnObj.AddComponent<BoxCollider>();
        col.size = new Vector3(410f, 68f, 30f);
        col.isTrigger = false;

        // XRSimpleInteractable para permitir clique direto com o raio dos controles VR (Select / Activate)
        XRSimpleInteractable xrInteractable = btnObj.AddComponent<XRSimpleInteractable>();
        if (!xrInteractable.colliders.Contains(col))
        {
            xrInteractable.colliders.Add(col);
        }
        xrInteractable.selectEntered.AddListener(_ => buttonAction.Invoke());
        xrInteractable.activated.AddListener(_ => buttonAction.Invoke());

        GameObject btnTxtObj = new GameObject("Text");
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        RectTransform btRt = btnTxtObj.AddComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.offsetMin = Vector2.zero;
        btRt.offsetMax = Vector2.zero;

        var btnTmp = btnTxtObj.AddComponent<TextMeshProUGUI>();
        if (font != null) btnTmp.font = font;
        btnTmp.fontSize = 23f;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.alignment = TextAlignmentOptions.Center;
        btnTmp.color = Color.white;
        btnTmp.raycastTarget = false;
        btnTmp.text = buttonText;

        return cObj;
    }
}


