using System.Collections;
using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controla o fluxo de introdução narrativa, exploração livre e ativação de emergência na espaçonave (SampleScene).
/// Apresenta a história inicial em uma interface holográfica World Space imersiva em VR,
/// dá tempo para exploração livre e aciona o alarme sonoro e pisca-pisca vermelho.
/// </summary>
public class GerenteAlarme : MonoBehaviour
{
    public static GerenteAlarme Instance { get; private set; }

    [Header("Configurações da Narrativa")]
    [Tooltip("Tempo em segundos antes da primeira mensagem")]
    [SerializeField] private float initialMessageDelay = 0.8f;

    [Tooltip("Duração de exibição de cada frase da história (em segundos)")]
    [SerializeField] private float messageDuration = 7.0f;

    [Tooltip("Tempo em segundos que o jogador tem para explorar a nave livremente após a história")]
    [SerializeField] private float explorationDelay = 10.0f;

    [Header("Textos da História Inicial")]
    [TextArea(2, 4)]
    [SerializeField] private string storyMessage1 = "Você é um viajante espacial em uma longa jornada pela galáxia.";
    [TextArea(2, 4)]
    [SerializeField] private string storyMessage2 = "Depois de dias de viagem, sua nave segue tranquilamente pelo espaço.";
    [TextArea(2, 4)]
    [SerializeField] private string storyMessage3 = "Explore a nave e prepare-se para continuar sua jornada.";

    [Header("Textos do Alerta de Emergência")]
    [Tooltip("Tempo em segundos que o alerta de emergência fica visível antes de mostrar o objetivo")]
    [SerializeField] private float alertDuration = 5.0f;

    [SerializeField] private string alertTitle = "⚠ ALERTA ⚠";

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

    private bool isAlarmActive = false;
    private Coroutine introRoutine;
    private Coroutine emergencyRoutine;
    private UnityEngine.UI.Image hologramBgImage;
    private UnityEngine.UI.Image hologramLineImage;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        EnsureEmergencyLightsController();
        EnsureWorldSpaceUI();
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

        // Fallback procedural completo com suporte total a TextMeshPro e URP
        GameObject canvasObj = new GameObject("ShipWorldSpaceNarrativeCanvas");
        canvasObj.transform.SetParent(null); // Objeto raiz no mundo

        canvasObj.transform.position = new Vector3(-3.57f, 1.40f, -6.80f);
        canvasObj.transform.rotation = Quaternion.identity;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        canvas.worldCamera = Camera.main;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                                          AdditionalCanvasShaderChannels.Normal |
                                          AdditionalCanvasShaderChannels.Tangent;

        RectTransform rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(850f, 400f);
        rt.localScale = new Vector3(0.0025f, 0.0025f, 0.0025f);

        narrativeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        narrativeCanvasGroup.alpha = 1f;

        // Fundo holográfico translúcido com textura branca sólida para compatibilidade com URP
        GameObject bgObj = new GameObject("HologramBackground");
        bgObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
        Texture2D whiteTex = Texture2D.whiteTexture;
        bgImage.sprite = Sprite.Create(whiteTex, new Rect(0, 0, whiteTex.width, whiteTex.height), new Vector2(0.5f, 0.5f));
        bgImage.color = new Color(0.03f, 0.07f, 0.16f, 0.94f);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // Linha ciano decorativa de HUD holográfico
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

        // Texto com suporte a TextMeshPro
        GameObject textObj = new GameObject("NarrativeText");
        textObj.transform.SetParent(canvasObj.transform, false);
        narrativeText = textObj.AddComponent<TextMeshProUGUI>();

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null)
        {
            narrativeText.font = font;
        }

        narrativeText.fontSize = 32f;
        narrativeText.alignment = TextAlignmentOptions.Center;
        narrativeText.textWrappingMode = TextWrappingModes.Normal;
        narrativeText.color = Color.white;

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(40f, 20f);
        textRt.offsetMax = new Vector2(-40f, -20f);

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
            if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 |
                                               AdditionalCanvasShaderChannels.Normal |
                                               AdditionalCanvasShaderChannels.Tangent;
        }

        if (narrativeCanvasGroup == null)
        {
            narrativeCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
        }
        if (narrativeText == null)
        {
            narrativeText = canvasObj.GetComponentInChildren<TMP_Text>();
        }

        UnityEngine.UI.Image[] images = canvasObj.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach (var img in images)
        {
            if (img.gameObject.name.Contains("Background") || img.gameObject.name.Contains("Fundo")) hologramBgImage = img;
            else if (img.gameObject.name.Contains("Line") || img.gameObject.name.Contains("Header") || img.gameObject.name.Contains("Linha")) hologramLineImage = img;
        }
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
    }

    private void Start()
    {
        SetHologramTheme(false);
        // Alinha a posição e rotação inicial do holograma diretamente à frente da visão do jogador em VR
        AlignHologramWithPlayer();
        introRoutine = StartCoroutine(NarrativeSequenceRoutine());
    }

    private void AlignHologramWithPlayer()
    {
        if (narrativeCanvasObject == null) return;

        Camera cam = Camera.main;
        if (cam != null && cam.isActiveAndEnabled)
        {
            Vector3 camFwd = cam.transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.05f)
            {
                camFwd.Normalize();
                // Coloca o painel 1.8 metros à frente na linha dos olhos
                Vector3 targetPos = cam.transform.position + camFwd * 1.8f;
                targetPos.y = cam.transform.position.y + 0.05f;
                narrativeCanvasObject.transform.position = targetPos;
                narrativeCanvasObject.transform.rotation = Quaternion.LookRotation(camFwd);
            }
        }
    }

    /// <summary>
    /// Sequência narrativa completa:
    /// Início sereno -> Apresentação da história em World Space -> Desaparecimento da interface ->
    /// Exploração livre pela nave -> Ativação do Alarme e Luzes de Emergência.
    /// </summary>
    private IEnumerator NarrativeSequenceRoutine()
    {
        // 1. INÍCIO SERENO: Alarme desligado, iluminação normal
        if (emergencyLightsController != null) emergencyLightsController.ResetToNormal();

        if (showWorldSpaceNarrativeUI && narrativeCanvasObject != null)
        {
            narrativeCanvasObject.SetActive(true);
            if (narrativeCanvasGroup != null) narrativeCanvasGroup.alpha = 0f;

            // Aguarda o fade-in inicial da cena terminar para o jogador ver a primeira mensagem com clareza
            yield return new WaitForSeconds(initialMessageDelay);

            // MENSAGEM 1: Introdução do viajante
            SetMessageText("<size=80%><color=#38BDF8><b>DIÁRIO DE BORDO</b></color></size>\n\n" +
                           storyMessage1 + "\n\n" +
                           "<size=60%><color=#94A3B8>[ 1 / 3 ]</color></size>");
            yield return StartCoroutine(FadeCanvasGroup(1f, 0.35f));
            yield return StartCoroutine(WaitForMessageDurationOrInput(messageDuration));

            // MENSAGEM 2: Situação da viagem
            yield return StartCoroutine(FadeCanvasGroup(0f, 0.25f));
            SetMessageText("<size=80%><color=#38BDF8><b>DIÁRIO DE BORDO</b></color></size>\n\n" +
                           storyMessage2 + "\n\n" +
                           "<size=60%><color=#94A3B8>[ 2 / 3 ]</color></size>");
            yield return StartCoroutine(FadeCanvasGroup(1f, 0.25f));
            yield return StartCoroutine(WaitForMessageDurationOrInput(messageDuration));

            // MENSAGEM 3: Convite à exploração
            yield return StartCoroutine(FadeCanvasGroup(0f, 0.25f));
            SetMessageText("<size=80%><color=#38BDF8><b>DIÁRIO DE BORDO</b></color></size>\n\n" +
                           $"<color=#6EE7B7>{storyMessage3}</color>\n\n" +
                           "<size=60%><color=#94A3B8>[ 3 / 3 ]</color></size>");
            yield return StartCoroutine(FadeCanvasGroup(1f, 0.25f));
            yield return StartCoroutine(WaitForMessageDurationOrInput(messageDuration));

            // Fade out final e desativação total do holograma para liberar a visão
            yield return StartCoroutine(FadeCanvasGroup(0f, 0.4f));

            narrativeCanvasObject.SetActive(false);
        }

        // 2. PERÍODO LIVRE DE EXPLORAÇÃO
        Debug.Log($"[AlarmManager] Período de exploração livre iniciado ({explorationDelay}s)...");
        yield return new WaitForSeconds(explorationDelay);

        // 3. INÍCIO DO ALERTA E EMERGÊNCIA
        TriggerAlarm();
    }

    private IEnumerator WaitForMessageDurationOrInput(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // Só permite avançar por clique/gatilho após pelo menos 1.5s de leitura, evitando pulos acidentais
            if (elapsed > 1.5f && CheckAdvanceInput()) yield break;
            yield return null;
        }
    }

    private bool CheckAdvanceInput()
    {
        try
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
#endif
            var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.Controller, devices);

            foreach (var device in devices)
            {
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool tb) && tb) return true;
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pb) && pb) return true;
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool sb) && sb) return true;
            }
        }
        catch
        {
            // Fallback seguro: se houver qualquer divergência de hardware/input, a contagem de tempo segue normalmente
        }

        return false;
    }

    private void SetMessageText(string text)
    {
        if (narrativeText != null)
        {
            narrativeText.text = text;
        }
    }

    private IEnumerator FadeCanvasGroup(float targetAlpha, float duration)
    {
        if (narrativeCanvasGroup == null) yield break;

        float startAlpha = narrativeCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            narrativeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        narrativeCanvasGroup.alpha = targetAlpha;
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Atalho de teste rápido no Editor para disparar a emergência imediatamente sem esperar
        if (UnityEngine.Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("[GerenteAlarme] Tecla T pressionada: disparando emergência imediatamente.");
            TriggerAlarm();
        }
#endif
    }

    private void LateUpdate()
    {
        // Enquanto o alarme/objetivo estiver ativo, mantém o holograma sempre voltado para a visão do jogador
        if (isAlarmActive && narrativeCanvasObject != null && narrativeCanvasObject.activeSelf)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = narrativeCanvasObject.transform.position - cam.transform.position;
                if (toCam.sqrMagnitude > 0.05f)
                {
                    narrativeCanvasObject.transform.rotation = Quaternion.LookRotation(toCam);
                }

                // Se o jogador correr para longe do holograma, traz o holograma suavemente à frente da visão dele
                if (toCam.magnitude > 3.5f)
                {
                    Vector3 camFwd = cam.transform.forward;
                    camFwd.y = 0f;
                    if (camFwd.sqrMagnitude > 0.05f)
                    {
                        camFwd.Normalize();
                        Vector3 desiredPos = cam.transform.position + camFwd * 1.8f;
                        desiredPos.y = cam.transform.position.y + 0.05f;
                        narrativeCanvasObject.transform.position = Vector3.Lerp(
                            narrativeCanvasObject.transform.position, desiredPos, Time.deltaTime * 3.5f);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Aciona a sequência completa de emergência:
    /// 1. alarme começa a tocar;
    /// 2. LEDs/luzes vermelhas da nave começam a piscar;
    /// 3. aparece uma mensagem de alerta;
    /// 4. aparece o objetivo que o jogador deve cumprir.
    /// </summary>
    public void TriggerAlarm()
    {
        if (isAlarmActive) return;
        isAlarmActive = true;

        Debug.Log("[GerenteAlarme] Sequência de emergência ativada!");

        if (emergencyRoutine != null) StopCoroutine(emergencyRoutine);
        emergencyRoutine = StartCoroutine(EmergencySequenceRoutine());
    }

    private IEnumerator EmergencySequenceRoutine()
    {
        // ========================================================
        // 1. O alarme começa a tocar
        // ========================================================
        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.StartAlarm();
        }

        // Intervalo de impacto sonoro: o alarme ecoa pela nave
        yield return new WaitForSeconds(0.8f);

        // ========================================================
        // 2. LEDs/luzes vermelhas da nave começam a piscar
        // ========================================================
        if (emergencyLightsController != null)
        {
            emergencyLightsController.StartEmergencyLights();
        }

        // Intervalo de impacto visual: o ambiente se transforma em emergência
        yield return new WaitForSeconds(0.7f);

        // ========================================================
        // 3. Aparece a mensagem de alerta
        // ========================================================
        if (showWorldSpaceNarrativeUI && narrativeCanvasObject != null)
        {
            AlignHologramWithPlayer();
            SetHologramTheme(true);
            narrativeCanvasObject.SetActive(true);
            if (narrativeCanvasGroup != null) narrativeCanvasGroup.alpha = 0f;

            SetMessageText($"<size=125%><color=#EF4444><b>{alertTitle}</b></color></size>\n\n" +
                           $"<size=110%><color=#FCA5A5>{alertMessage}</color></size>");

            yield return StartCoroutine(FadeCanvasGroup(1f, 0.35f));

            // Permite ao jogador ler com calma a mensagem de alerta
            yield return new WaitForSeconds(alertDuration);

            // ========================================================
            // 4. Aparece o objetivo que o jogador deve cumprir
            // ========================================================
            yield return StartCoroutine(FadeCanvasGroup(0f, 0.25f));

            SetMessageText($"<size=125%><color=#FCD34D><b>{objectiveTitle}</b></color></size>\n\n" +
                           $"<size=115%><color=#FFFFFF><b>{objectiveMessage}</b></color></size>\n\n" +
                           $"<size=70%><color=#94A3B8>{objectiveHint}</color></size>");

            yield return StartCoroutine(FadeCanvasGroup(1f, 0.35f));

            // O objetivo permanece visível guiando o jogador até o botão ser pressionado
        }
    }

    /// <summary>
    /// Interrompe o alarme e desliga as luzes vermelhas.
    /// Chamado quando o jogador aperta o botão físico 3D na mesa do cockpit.
    /// </summary>
    public void StopAlarm()
    {
        if (!isAlarmActive) return;
        isAlarmActive = false;

        Debug.Log("[GerenteAlarme] Alarme e emergência desligados pelo botão físico da mesa.");

        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        if (emergencyRoutine != null)
        {
            StopCoroutine(emergencyRoutine);
            emergencyRoutine = null;
        }

        // Para a sirene
        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.StopAlarm();
        }

        // Para as luzes de emergência
        if (emergencyLightsController != null)
        {
            emergencyLightsController.StopEmergencyLights();
        }

        // Desativa o holograma com fade-out suave
        if (narrativeCanvasObject != null)
        {
            StartCoroutine(DismissHologramRoutine());
        }
    }

    private IEnumerator DismissHologramRoutine()
    {
        yield return StartCoroutine(FadeCanvasGroup(0f, 0.3f));
        if (narrativeCanvasObject != null)
        {
            narrativeCanvasObject.SetActive(false);
        }
    }
}

public class AlarmManager : GerenteAlarme { }

