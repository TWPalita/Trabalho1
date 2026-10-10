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
    [TextArea(2, 4)]
    [SerializeField] private string storyMessage1 = "Você é um viajante espacial em uma longa jornada pela galáxia.";

    [TextArea(2, 4)]
    [SerializeField] private string storyMessage2 = "Depois de dias de viagem, sua nave segue tranquilamente pelo espaço.";

    [TextArea(2, 4)]
    [SerializeField] private string storyMessage3 = "Explore a nave e prepare-se para continuar sua jornada.";

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
    [SerializeField] private string textoBotaoAvancar = "Avançar";

    [Tooltip("Texto do botão Voltar nas páginas 2 e 3")]
    [SerializeField] private string textoBotaoVoltar = "Voltar";

    [Tooltip("Texto do botão na página 3 para iniciar a exploração")]
    [SerializeField] private string textoBotaoConcluir = "Explorar Nave";

    [Tooltip("Texto do botão durante o alerta de emergência para fechar o canvas")]
    [SerializeField] private string textoBotaoAlerta = "ENTENDIDO";

    private bool isAlarmActive = false;
    private bool isReadingDiary = true;
    private int currentStoryPage = 0;
    private Coroutine emergencyRoutine;
    private Coroutine transitionRoutine;
    private UnityEngine.UI.Image hologramBgImage;
    private UnityEngine.UI.Image hologramLineImage;
    private float lastPageChangeTime = -1f;
    private const float PageChangeCooldown = 0.3f;
    private bool isTransitioningPage = false;
    private bool wasPrimaryButtonPressed = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureEmergencyLightsController();
        EnsureWorldSpaceUI();
        EnsureButtonsUI();
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

        if (narrativeText != null)
        {
            RectTransform textRt = narrativeText.rectTransform;

            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.pivot = new Vector2(0.5f, 0.5f);

            textRt.anchoredPosition = new Vector2(0f, 53f);
            textRt.sizeDelta = new Vector2(740f, 250f);
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
                message =
                    $"<color=#6EE7B7>{storyMessage3}</color>";
                break;
        }

        SetMessageText(
            "<size=85%><color=#38BDF8><b>DIÁRIO DE BORDO</b></color></size>\n\n" +
            $"{message}\n\n" +
            $"<size=65%><color=#94A3B8>[ {currentStoryPage + 1} / 3 ]</color></size>"
        );

        if (btnVoltar != null)
        {
            btnVoltar.gameObject.SetActive(
                currentStoryPage > 0
            );

            if (txtBtnVoltar != null)
                txtBtnVoltar.text = textoBotaoVoltar;

            SetButtonListener(btnVoltar, VoltarPagina);
        }

        if (btnAvancar != null)
        {
            btnAvancar.gameObject.SetActive(true);

            RectTransform btnRt =
                btnAvancar.GetComponent<RectTransform>();

            if (btnRt != null)
            {
                btnRt.anchoredPosition =
                    new Vector2(190f, -135f);

                btnRt.sizeDelta =
                    new Vector2(230f, 52f);
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
    /// Chamado ao clicar no botão final 'Explorar Nave' da página 3.
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

        // DIÁRIO CONCLUÍDO: posição Y = 60
        if (narrativeText != null)
        {
            narrativeText.rectTransform.anchoredPosition =
                new Vector2(0f, 50f);
        }

        SetMessageText(
            "<size=90%><color=#38BDF8><b>DIÁRIO CONCLUÍDO</b></color></size>\n\n" +
            "<color=#6EE7B7>Boa jornada, viajante. Explore o corredor da nave.</color>"
        );

        yield return new WaitForSeconds(1.2f);

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
            isAlarmActive &&
            narrativeCanvasObject != null &&
            narrativeCanvasObject.activeSelf)
        {
            if (CheckAdvanceInput())
            {
                if (GerenteSom.Instance != null)
                    GerenteSom.Instance.PlayClick();

                StartCoroutine(
                    DismissHologramRoutine()
                );
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
        if (narrativeCanvasObject != null &&
            narrativeCanvasObject.activeSelf)
        {
            Camera cam = Camera.main;

            if (cam != null)
            {
                Vector3 toCam =
                    narrativeCanvasObject.transform.position -
                    cam.transform.position;

                if (toCam.sqrMagnitude > 0.05f)
                {
                    narrativeCanvasObject.transform.rotation =
                        Quaternion.LookRotation(toCam);
                }

                if (toCam.magnitude > 3.5f)
                {
                    Vector3 camFwd =
                        cam.transform.forward;

                    camFwd.y = 0f;

                    if (camFwd.sqrMagnitude > 0.05f)
                    {
                        camFwd.Normalize();

                        Vector3 desiredPos =
                            cam.transform.position +
                            camFwd * 1.8f;

                        desiredPos.y =
                            cam.transform.position.y + 0.05f;

                        narrativeCanvasObject.transform.position =
                            Vector3.Lerp(
                                narrativeCanvasObject.transform.position,
                                desiredPos,
                                Time.deltaTime * 3.5f
                            );
                    }
                }
            }
        }
    }

    public void IniciarContagemAlerta()
    {
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
        // 1. Sirene começa a tocar
        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.StartAlarm();
        }

        yield return new WaitForSeconds(0.8f);

        // 2. Luzes vermelhas começam a piscar
        if (emergencyLightsController != null)
        {
            emergencyLightsController.StartEmergencyLights();
        }

        yield return new WaitForSeconds(0.7f);

        // 3. Exibe alerta holográfico em vermelho com botão ENTENDIDO
        if (
            showWorldSpaceNarrativeUI &&
            narrativeCanvasObject != null)
        {
            AlignHologramWithPlayer();
            SetHologramTheme(true);

            narrativeCanvasObject.SetActive(true);

            if (narrativeCanvasGroup != null)
                narrativeCanvasGroup.alpha = 0f;

            // ALERTA: posição Y = 60
            if (narrativeText != null)
            {
                narrativeText.rectTransform.anchoredPosition =
                    new Vector2(0f, 35f);
            }

            SetMessageText(
                $"<size=125%><color=#EF4444><b>{alertTitle}</b></color></size>\n\n" +
                $"<size=105%><color=#FCA5A5>{alertMessage}</color></size>\n\n"
            );

            if (btnVoltar != null)
                btnVoltar.gameObject.SetActive(false);

            if (btnAvancar != null)
            {
                btnAvancar.gameObject.SetActive(true);

                RectTransform btnRt =
                    btnAvancar.GetComponent<RectTransform>();

                if (btnRt != null)
                {
                    btnRt.anchoredPosition =
                        new Vector2(0f, -135f);

                    btnRt.sizeDelta =
                        new Vector2(250f, 52f);
                }

                if (txtBtnAvancar != null)
                {
                    txtBtnAvancar.text =
                        textoBotaoAlerta;
                }

                SetButtonListener(btnAvancar, () =>
                {
                    if (GerenteSom.Instance != null)
                        GerenteSom.Instance.PlayClick();

                    StartCoroutine(
                        DismissHologramRoutine()
                    );
                });
            }

            yield return StartCoroutine(
                FadeCanvasGroup(1f, 0.35f)
            );
        }
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

        if (narrativeCanvasObject != null)
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
    }

    public void MostrarObjetivo()
    {
        if (narrativeCanvasObject == null || narrativeText == null)
        {
            Debug.LogWarning(
                "[GerenteAlarme] Canvas ou texto não configurado."
            );
            return;
        }

        narrativeCanvasObject.SetActive(true);

        if (narrativeCanvasGroup != null)
            narrativeCanvasGroup.alpha = 1f;

        SetMessageText(
            $"<size=125%><color=#38BDF8><b>{objectiveTitle}</b></color></size>\n\n" +
            $"<size=105%>{objectiveMessage}</size>\n\n" +
            $"<size=70%><color=#94A3B8>{objectiveHint}</color></size>"
        );

        if (btnAvancar != null)
            btnAvancar.gameObject.SetActive(false);

        if (btnVoltar != null)
            btnVoltar.gameObject.SetActive(false);
    }
}

public class AlarmManager : GerenteAlarme
{
}