using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gerencia as interfaces tridimensionais em World Space adaptadas para conforto, nitidez e fluidez em VR.
/// Organiza o visor matemático, o painel de status (pontos/energia), o feedback tático de tiro e a tela de encerramento.
/// </summary>
public class InterfaceVR : MonoBehaviour
{
    public static InterfaceVR Instance { get; private set; }

    [Header("Cockpit HUD (World Space)")]
    [SerializeField] private GameObject cockpitHUDParent;
    [SerializeField] private TMP_Text mathQuestionText;
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private Image energyFillImage;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Painel de Controle - Console Cockpit (World Space)")]
    [SerializeField] private TMP_Text energySegmentedText; // "████████"
    [SerializeField] private Button btnConsoleNewRound;
    [SerializeField] private Button btnConsoleReturnShip;

    [Header("Painel de Fim de Jogo VR")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverScoreText;
    [SerializeField] private Button btnRestart;
    [SerializeField] private Button btnReturnToShip;

    [Header("Alertas na Espaçonave (World Space)")]
    [SerializeField] private GameObject shipAlarmHologram;
    [SerializeField] private TMP_Text shipAlarmText;
    [SerializeField] private GameObject panelHologramLabel;

    private Coroutine feedbackRoutine;
    private Coroutine questionPopRoutine;
    private Coroutine endPanelAnimRoutine;

    private Material controlPanelHoloMat;
    private Material endPanelHoloMat;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        ConfigureGameOverLayout();
    }

    public void ConfigureGameOverLayout()
    {
        Canvas rootCanvas = GetComponent<Canvas>();
        if (rootCanvas != null && rootCanvas.worldCamera == null)
        {
            rootCanvas.worldCamera = Camera.main;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }
        scaler.dynamicPixelsPerUnit = 2.5f;

        RectTransform hudRootRt = GetComponent<RectTransform>();
        if (hudRootRt != null && gameObject.name.IndexOf("Cockpit_WorldSpace_HUD", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            hudRootRt.position = new Vector3(-0.18f, 1.44f, 1.34f);
            hudRootRt.localScale = new Vector3(0.0024f, 0.0024f, 0.0024f);
        }

        Shader holoVisorShader = Shader.Find("Custom/CockpitHologramVisor");

        // 1. Visor Holográfico da Rodada e Conta (MathVisor_Border) posicionado mais baixo sobre o console
        Transform mathVisorTr = transform.Find("MathVisor_Border");
        if (mathVisorTr != null)
        {
            RectTransform mathVisorRt = mathVisorTr.GetComponent<RectTransform>();
            if (mathVisorRt != null)
            {
                mathVisorRt.anchoredPosition = new Vector2(0f, -15f);
                mathVisorRt.sizeDelta = new Vector2(1120f, 145f);
            }

            Image borderBg = mathVisorTr.GetComponent<Image>();
            if (borderBg != null)
            {
                borderBg.color = new Color(1f, 1f, 1f, 0f);
                borderBg.raycastTarget = false;
            }

            Transform innerTr = mathVisorTr.Find("MathVisor_Inner");
            if (innerTr != null)
            {
                RectTransform innerRt = innerTr.GetComponent<RectTransform>();
                if (innerRt != null)
                {
                    innerRt.sizeDelta = new Vector2(500f, 126f);
                }

                HologramVisorEffect visorFx = innerTr.GetComponent<HologramVisorEffect>();
                if (visorFx != null)
                {
                    visorFx.ApplyHologramShaderToVisor();
                }
            }
        }

        if (roundText != null)
        {
            RectTransform rRt = roundText.GetComponent<RectTransform>();
            if (rRt != null)
            {
                rRt.anchoredPosition = new Vector2(0f, 36f);
                rRt.sizeDelta = new Vector2(460f, 30f);
            }
            roundText.fontSize = 18.5f;
            roundText.fontStyle = FontStyles.Bold;
            roundText.characterSpacing = 4.5f;
            roundText.alignment = TextAlignmentOptions.Center;
            roundText.color = Color.white;
            roundText.enableVertexGradient = true;
            roundText.colorGradient = new VertexGradient(
                new Color(0.65f, 0.99f, 1f, 1f),
                new Color(0.65f, 0.99f, 1f, 1f),
                new Color(0.18f, 0.85f, 1f, 1f),
                new Color(0.18f, 0.85f, 1f, 1f)
            );
            roundText.raycastTarget = false;
        }

        if (mathQuestionText != null)
        {
            RectTransform qRt = mathQuestionText.GetComponent<RectTransform>();
            if (qRt != null)
            {
                qRt.anchoredPosition = new Vector2(0f, -12f);
                qRt.sizeDelta = new Vector2(465f, 70f);
            }
            mathQuestionText.fontSize = 50f;
            mathQuestionText.fontStyle = FontStyles.Bold;
            mathQuestionText.alignment = TextAlignmentOptions.Center;
            mathQuestionText.color = Color.white;
            mathQuestionText.enableVertexGradient = true;
            mathQuestionText.colorGradient = new VertexGradient(
                new Color(1f, 0.99f, 0.82f, 1f),
                new Color(1f, 0.99f, 0.82f, 1f),
                new Color(0.35f, 0.96f, 1f, 1f),
                new Color(0.35f, 0.96f, 1f, 1f)
            );
            mathQuestionText.raycastTarget = false;
        }

        // 2. Conteúdo de status integrado ao MathVisor_Border
        if (mathVisorTr != null)
        {
<<<<<<< Updated upstream
            controlBorderTr.SetAsFirstSibling();

            RectTransform controlBorderRt = controlBorderTr.GetComponent<RectTransform>();
            if (controlBorderRt != null)
            {
                controlBorderRt.anchoredPosition = new Vector2(0f, -15f);
                controlBorderRt.sizeDelta = new Vector2(1120f, 130f);
            }

            Image ctrlBorderImg = controlBorderTr.GetComponent<Image>();
            if (ctrlBorderImg != null && holoVisorShader != null)
            {
                ctrlBorderImg.color = new Color(1f, 1f, 1f, 0f);
                ctrlBorderImg.raycastTarget = false;
            }

            Transform controlInnerTr = controlBorderTr.Find("ControlPanel_Inner");
=======
            Transform controlInnerTr = mathVisorTr.Find("ControlPanel_Inner");
>>>>>>> Stashed changes
            if (controlInnerTr != null)
            {
                RectTransform innerRt = controlInnerTr.GetComponent<RectTransform>();
                if (innerRt != null)
                {
<<<<<<< Updated upstream
                    innerRt.sizeDelta = new Vector2(1120f, 130f);
=======
                    innerRt.sizeDelta = new Vector2(1120f, 145f);
>>>>>>> Stashed changes
                }

                Image ctrlInnerImg = controlInnerTr.GetComponent<Image>();
                if (ctrlInnerImg != null && holoVisorShader != null)
                {
                    if (controlPanelHoloMat == null)
                    {
                        controlPanelHoloMat = new Material(holoVisorShader) { name = "ControlPanel_HoloSDF_Mat" };
                    }
                    controlPanelHoloMat.SetFloat("_VisorMode", 4f);
                    controlPanelHoloMat.SetColor("_PrimaryColor", new Color(0.18f, 0.88f, 1.0f, 1f));
                    controlPanelHoloMat.SetColor("_SecondaryColor", new Color(0.08f, 0.42f, 0.92f, 1f));
                    controlPanelHoloMat.SetColor("_BgColor", new Color(0.01f, 0.05f, 0.13f, 0.68f));

                    ctrlInnerImg.material = controlPanelHoloMat;
                    ctrlInnerImg.color = Color.white;
                    ctrlInnerImg.raycastTarget = false;
                }
            }
        }

        // 3. Pontos (Esquerda) e Energia (Direita) com largura suficiente para nunca cortar texto
        if (scoreText != null)
        {
            RectTransform scoreValRt = scoreText.GetComponent<RectTransform>();
            if (scoreValRt != null)
            {
                scoreValRt.anchoredPosition = new Vector2(-410f, 6f);
<<<<<<< Updated upstream
                scoreValRt.sizeDelta = new Vector2(300f, 44f);
=======
                scoreValRt.sizeDelta = new Vector2(280f, 44f);
>>>>>>> Stashed changes
            }
            scoreText.fontSize = 27f;
            scoreText.fontStyle = FontStyles.Bold;
            scoreText.alignment = TextAlignmentOptions.Center;
            scoreText.textWrappingMode = TextWrappingModes.NoWrap;
            scoreText.raycastTarget = false;
        }

        if (energySegmentedText != null)
        {
            RectTransform energySegRt = energySegmentedText.GetComponent<RectTransform>();
            if (energySegRt != null)
            {
                energySegRt.anchoredPosition = new Vector2(410f, 18f);
<<<<<<< Updated upstream
                energySegRt.sizeDelta = new Vector2(300f, 34f);
=======
                energySegRt.sizeDelta = new Vector2(280f, 34f);
>>>>>>> Stashed changes
            }
            energySegmentedText.fontSize = 21f;
            energySegmentedText.alignment = TextAlignmentOptions.Center;
            energySegmentedText.textWrappingMode = TextWrappingModes.NoWrap;
            energySegmentedText.raycastTarget = false;
        }

        if (energyText != null)
        {
            RectTransform energyPctRt = energyText.GetComponent<RectTransform>();
            if (energyPctRt != null)
            {
                energyPctRt.anchoredPosition = new Vector2(410f, -16f);
<<<<<<< Updated upstream
                energyPctRt.sizeDelta = new Vector2(300f, 30f);
=======
                energyPctRt.sizeDelta = new Vector2(280f, 30f);
>>>>>>> Stashed changes
            }
            energyText.fontSize = 18f;
            energyText.fontStyle = FontStyles.Bold;
            energyText.alignment = TextAlignmentOptions.Center;
            energyText.textWrappingMode = TextWrappingModes.NoWrap;
            energyText.raycastTarget = false;
        }

        // 4. Banner Central de Feedback Tático (Acerto / Erro)
        if (feedbackText != null)
        {
            RectTransform fbRt = feedbackText.GetComponent<RectTransform>();
            if (fbRt != null)
            {
                fbRt.anchoredPosition = new Vector2(0f, 4f);
                fbRt.sizeDelta = new Vector2(310f, 70f);
            }
            feedbackText.fontSize = 20f;
            feedbackText.fontStyle = FontStyles.Bold;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.textWrappingMode = TextWrappingModes.Normal;
            feedbackText.raycastTarget = false;
        }

        if (gameOverPanel == null) return;

        // 5. Painel de Fim de Jogo / Vitória (posicionado mais alto no para-brisa para leitura confortável)
        RectTransform panelRt = gameOverPanel.GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.sizeDelta = new Vector2(880f, 620f);
            panelRt.anchoredPosition = new Vector2(0f, 140f);
            panelRt.localPosition = new Vector3(0f, 140f, -45f);
            panelRt.localScale = new Vector3(0.92f, 0.92f, 1f);
        }

        Image panelBg = gameOverPanel.GetComponent<Image>();
        if (panelBg != null)
        {
            if (holoVisorShader != null)
            {
                if (endPanelHoloMat == null)
                {
                    endPanelHoloMat = new Material(holoVisorShader) { name = "EndPanel_HoloSDF_Mat" };
                }
                endPanelHoloMat.SetFloat("_VisorMode", 3f);
                endPanelHoloMat.SetColor("_PrimaryColor", new Color(0.20f, 1.00f, 0.72f, 1f));
                endPanelHoloMat.SetColor("_SecondaryColor", new Color(0.12f, 0.75f, 1.00f, 1f));
                endPanelHoloMat.SetColor("_BgColor", new Color(0.01f, 0.06f, 0.14f, 0.80f));

                panelBg.material = endPanelHoloMat;
                panelBg.color = Color.white;
            }
            else
            {
                panelBg.color = new Color(0.02f, 0.05f, 0.13f, 0.94f);
            }
            panelBg.raycastTarget = false;
        }

        Transform titleTr = gameOverPanel.transform.Find("Title");
        TMP_FontAsset sharedFont = gameOverScoreText != null ? gameOverScoreText.font : null;
        if (titleTr != null)
        {
            RectTransform titleRt = titleTr.GetComponent<RectTransform>();
            if (titleRt != null)
            {
                titleRt.anchoredPosition = new Vector2(0f, 245f);
                titleRt.sizeDelta = new Vector2(800f, 54f);
            }
            TMP_Text titleTxt = titleTr.GetComponent<TMP_Text>();
            if (titleTxt != null)
            {
                titleTxt.fontSize = 36f;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.alignment = TextAlignmentOptions.Center;
                titleTxt.raycastTarget = false;
                if (sharedFont == null) sharedFont = titleTxt.font;
            }
        }

        Transform subTr = gameOverPanel.transform.Find("Subtitle");
        if (subTr == null)
        {
            GameObject subObj = new GameObject("Subtitle", typeof(RectTransform));
            subObj.transform.SetParent(gameOverPanel.transform, false);
            subTr = subObj.transform;
        }
        RectTransform subRt = subTr.GetComponent<RectTransform>();
        if (subRt != null)
        {
            subRt.anchorMin = new Vector2(0.5f, 0.5f);
            subRt.anchorMax = new Vector2(0.5f, 0.5f);
            subRt.pivot = new Vector2(0.5f, 0.5f);
            subRt.anchoredPosition = new Vector2(0f, 195f);
            subRt.sizeDelta = new Vector2(800f, 36f);
        }
        TMP_Text subTxt = subTr.GetComponent<TMP_Text>();
        if (subTxt == null)
        {
            subTxt = subTr.gameObject.AddComponent<TextMeshProUGUI>();
        }
        if (sharedFont != null) subTxt.font = sharedFont;
        subTxt.fontSize = 21f;
        subTxt.fontStyle = FontStyles.Bold;
        subTxt.alignment = TextAlignmentOptions.Center;
        subTxt.raycastTarget = false;

        if (gameOverScoreText != null)
        {
            RectTransform scoreRt = gameOverScoreText.GetComponent<RectTransform>();
            if (scoreRt != null)
            {
                scoreRt.anchoredPosition = new Vector2(0f, 22f);
                scoreRt.sizeDelta = new Vector2(780f, 260f);
            }
            gameOverScoreText.fontSize = 22f;
            gameOverScoreText.lineSpacing = 8f;
            gameOverScoreText.alignment = TextAlignmentOptions.Center;
            gameOverScoreText.textWrappingMode = TextWrappingModes.Normal;
            gameOverScoreText.raycastTarget = false;
        }

        ConfigureEndButton(btnRestart, new Vector2(0f, -175f), new Vector2(440f, 64f), new Color(0.12f, 0.52f, 0.84f, 0.96f));

        // Oculta a opção de voltar para a nave conforme solicitado
        if (btnReturnToShip != null)
        {
            btnReturnToShip.gameObject.SetActive(false);
        }
        if (btnConsoleReturnShip != null)
        {
            btnConsoleReturnShip.gameObject.SetActive(false);
        }
    }

    private void ConfigureEndButton(Button btn, Vector2 pos, Vector2 size, Color normalColor)
    {
        if (btn == null) return;

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = normalColor;
            img.raycastTarget = true;
        }

        ColorBlock cb = btn.colors;
        cb.normalColor = normalColor;
        cb.highlightedColor = new Color(Mathf.Min(1f, normalColor.r + 0.18f), Mathf.Min(1f, normalColor.g + 0.25f), 1f, 1f);
        cb.pressedColor = normalColor * 0.65f;
        cb.selectedColor = cb.highlightedColor;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        BoxCollider col = btn.GetComponent<BoxCollider>();
        if (col == null) col = btn.gameObject.AddComponent<BoxCollider>();
        col.size = new Vector3(size.x, size.y, 24f);
        col.isTrigger = false;

        var xrInt = btn.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (xrInt == null)
        {
            xrInt = btn.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        }
        if (!xrInt.colliders.Contains(col)) xrInt.colliders.Add(col);
    }

    public void SetHUDRootElementsVisible(bool visible)
    {
        Transform mathVisorTr = transform.Find("MathVisor_Border");
        if (mathVisorTr != null) mathVisorTr.gameObject.SetActive(visible);
    }

    private void SetBackgroundHUDPanelsVisible(bool visible)
    {
        if (gameOverPanel == null || gameOverPanel.transform.parent == null) return;

        Transform canvasParent = gameOverPanel.transform.parent;
        for (int i = 0; i < canvasParent.childCount; i++)
        {
            Transform child = canvasParent.GetChild(i);
            if (child == null || child.gameObject == gameOverPanel) continue;
            child.gameObject.SetActive(visible);
        }
    }

    private void Start()
    {
        var trackedRaycaster = GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        if (trackedRaycaster == null)
        {
            gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        }

        if (btnConsoleNewRound != null)
        {
            btnConsoleNewRound.onClick.AddListener(() =>
            {
                if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
                if (GerenteDefesa.Instance != null) GerenteDefesa.Instance.StartNewRound();
            });
        }

        if (btnConsoleReturnShip != null)
        {
            btnConsoleReturnShip.gameObject.SetActive(false);
        }

        if (btnRestart != null)
        {
            UnityEngine.Events.UnityAction restartAction = () =>
            {
                if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
                GerenteDefesa def = GerenteDefesa.Instance != null ? GerenteDefesa.Instance : FindAnyObjectByType<GerenteDefesa>();
                if (def != null) def.RestartDefense();
            };
            btnRestart.onClick.AddListener(restartAction);

            var xrInt = btnRestart.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (xrInt != null)
            {
                xrInt.selectEntered.AddListener(_ => restartAction());
                xrInt.activated.AddListener(_ => restartAction());
            }
        }

        if (btnReturnToShip != null)
        {
            btnReturnToShip.gameObject.SetActive(false);
        }

        if (GerentePontuacao.Instance != null)
        {
            GerentePontuacao.Instance.OnScoreChanged += UpdateScoreDisplay;
            GerentePontuacao.Instance.OnEnergyChanged += UpdateEnergyDisplay;
            UpdateScoreDisplay(GerentePontuacao.Instance.CurrentScore);
            UpdateEnergyDisplay(GerentePontuacao.Instance.CurrentEnergy, 100);
        }

        HideGameOverPanel();
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Permite clicar nos botões da tela de Vitória/Derrota com o mouse no Editor / Simulador XR
#if ENABLE_INPUT_SYSTEM
        if (gameOverPanel != null && gameOverPanel.activeInHierarchy &&
            UnityEngine.InputSystem.Mouse.current != null &&
            UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
                RaycastHit[] hits = Physics.RaycastAll(ray, 25f, ~0, QueryTriggerInteraction.Collide);
                if (hits != null && hits.Length > 0)
                {
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    for (int i = 0; i < hits.Length; i++)
                    {
                        if (hits[i].collider == null) continue;
                        Button b = hits[i].collider.GetComponentInParent<Button>();
                        if (b != null && b.interactable && b.transform.IsChildOf(gameOverPanel.transform))
                        {
                            b.onClick.Invoke();
                            break;
                        }
                    }
                }
            }
        }
#endif
    }

    private void OnDestroy()
    {
        if (GerentePontuacao.Instance != null)
        {
            GerentePontuacao.Instance.OnScoreChanged -= UpdateScoreDisplay;
            GerentePontuacao.Instance.OnEnergyChanged -= UpdateEnergyDisplay;
        }
        if (controlPanelHoloMat != null) Destroy(controlPanelHoloMat);
        if (endPanelHoloMat != null) Destroy(endPanelHoloMat);
    }

    #region HUD do Cockpit

    public void SetCockpitHUDVisible(bool visible)
    {
        if (cockpitHUDParent != null)
        {
            cockpitHUDParent.SetActive(visible);
        }
    }

    public void UpdateMathQuestion(string question)
    {
        if (mathQuestionText != null)
        {
            int len = string.IsNullOrEmpty(question) ? 0 : question.Length;
            bool isTextPrompt = !string.IsNullOrEmpty(question) && !question.Contains("=") && !question.Contains("?");
            if (isTextPrompt)
            {
                mathQuestionText.fontSize = 34f;
            }
            else if (len >= 15)
            {
                mathQuestionText.fontSize = 37f;
            }
            else if (len >= 12)
            {
                mathQuestionText.fontSize = 42f;
            }
            else
            {
                mathQuestionText.fontSize = 48f;
            }
            mathQuestionText.textWrappingMode = TextWrappingModes.NoWrap;
            mathQuestionText.text = question;
            if (gameObject.activeInHierarchy)
            {
                if (questionPopRoutine != null) StopCoroutine(questionPopRoutine);
                questionPopRoutine = StartCoroutine(PopTextScaleRoutine(mathQuestionText.transform, 1.16f, 0.18f));
            }
        }
    }

    public void UpdateRound(int round, int totalRounds = 10)
    {
        if (roundText != null)
        {
            roundText.text = totalRounds > 0
                ? $"[  •  RODADA {round} / {totalRounds}  •  ]"
                : $"[  •  RODADA {round}  •  ]";
        }
        if (HologramVisorEffect.Instance != null)
        {
            HologramVisorEffect.Instance.TriggerNewRoundPulse();
        }
    }

    public void UpdateScoreDisplay(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = $"PONTOS: {score:D4}";
        }
    }

    public void UpdateEnergyDisplay(int current, int max)
    {
        if (energyText != null)
        {
            energyText.text = $"ESCUDOS: {current}%";
        }
        if (energyFillImage != null)
        {
            float fill = (float)current / Mathf.Max(1, max);
            energyFillImage.fillAmount = fill;
            energyFillImage.color = fill > 0.5f ? new Color(0.2f, 0.95f, 0.45f) : (fill > 0.25f ? new Color(1f, 0.82f, 0.2f) : new Color(1f, 0.25f, 0.25f));
        }
        if (energySegmentedText != null)
        {
            int totalBlocks = 8;
            int activeBlocks = Mathf.Clamp(Mathf.CeilToInt((float)current / Mathf.Max(1, max) * totalBlocks), 0, totalBlocks);
            string blocks = new string('█', activeBlocks).PadRight(totalBlocks, '░');
            energySegmentedText.text = blocks;

            if (current > 50) energySegmentedText.color = new Color(0.2f, 1f, 0.45f, 1f);
            else if (current > 20) energySegmentedText.color = new Color(1f, 0.85f, 0.2f, 1f);
            else energySegmentedText.color = new Color(1f, 0.28f, 0.28f, 1f);
        }
    }

    public void ShowFeedback(bool isCorrect, string message)
    {
        if (feedbackText == null || !gameObject.activeInHierarchy) return;

        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }
        feedbackRoutine = StartCoroutine(ShowFeedbackRoutine(isCorrect, message));
    }

    private IEnumerator ShowFeedbackRoutine(bool isCorrect, string message)
    {
        feedbackText.gameObject.SetActive(true);
        feedbackText.color = isCorrect
            ? new Color(0.22f, 1f, 0.55f, 1f)
            : new Color(1f, 0.35f, 0.32f, 1f);
        feedbackText.text = message;

        Transform fbTr = feedbackText.transform;
        float popElapsed = 0f;
        while (popElapsed < 0.14f)
        {
            popElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(popElapsed / 0.14f);
            float s = Mathf.Lerp(1.22f, 1.0f, t);
            fbTr.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        fbTr.localScale = Vector3.one;

        yield return new WaitForSeconds(0.75f);

        Color baseCol = feedbackText.color;
        float fadeElapsed = 0f;
        while (fadeElapsed < 0.22f)
        {
            fadeElapsed += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, fadeElapsed / 0.22f);
            feedbackText.color = new Color(baseCol.r, baseCol.g, baseCol.b, a);
            yield return null;
        }

        feedbackText.gameObject.SetActive(false);
        feedbackRoutine = null;
    }

    private IEnumerator PopTextScaleRoutine(Transform tr, float peakScale, float duration)
    {
        if (tr == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = Mathf.Lerp(peakScale, 1f, 1f - Mathf.Pow(1f - t, 2f));
            tr.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        tr.localScale = Vector3.one;
    }

    private void SetEndScreenTexts(
        string title,
        Color titleColor,
        string subtitle,
        Color subtitleColor,
        string message,
        string restartBtnLabel,
        string returnBtnLabel)
    {
        if (gameOverPanel == null) return;

        Transform titleTr = gameOverPanel.transform.Find("Title");
        if (titleTr != null)
        {
            TMP_Text titleTxt = titleTr.GetComponent<TMP_Text>();
            if (titleTxt != null)
            {
                titleTxt.text = title;
                titleTxt.color = titleColor;
            }
        }

        Transform subTr = gameOverPanel.transform.Find("Subtitle");
        if (subTr != null)
        {
            TMP_Text subTxt = subTr.GetComponent<TMP_Text>();
            if (subTxt != null)
            {
                subTxt.text = subtitle;
                subTxt.color = subtitleColor;
            }
        }

        if (gameOverScoreText != null)
        {
            gameOverScoreText.text = message;
            gameOverScoreText.color = new Color(0.92f, 0.96f, 1f, 1f);
        }

        if (btnRestart != null)
        {
            TMP_Text restartTxt = btnRestart.GetComponentInChildren<TMP_Text>(true);
            if (restartTxt != null) restartTxt.text = restartBtnLabel;
        }

        if (btnReturnToShip != null)
        {
            btnReturnToShip.gameObject.SetActive(false);
        }
    }

    public void ShowVictory(int finalScore = 0, int correct = 0, int wrong = 0, float accuracy = 0f, int rounds = 1)
    {
        HologramVisorEffect.SetSpaceTravelActive(true);

        if (gameOverPanel != null)
        {
            if (feedbackText != null) feedbackText.gameObject.SetActive(false);
            ConfigureGameOverLayout();
            SetBackgroundHUDPanelsVisible(false);

            if (endPanelHoloMat != null)
            {
                endPanelHoloMat.SetColor("_PrimaryColor", new Color(0.20f, 1.00f, 0.72f, 1f));
                endPanelHoloMat.SetColor("_SecondaryColor", new Color(0.14f, 0.80f, 1.00f, 1f));
                endPanelHoloMat.SetColor("_BgColor", new Color(0.01f, 0.06f, 0.14f, 0.76f));
            }

            string statsLine = $"<size=95%><color=#38BDF8><b>PONTOS: {finalScore:D4}   |   PRECISÃO: {accuracy:F0}%   |   RODADAS: {rounds}/10</b></color></size>\n\n";

            SetEndScreenTexts(
                "PARABÉNS, COMANDANTE!",
                new Color(0.25f, 1f, 0.62f, 1f),
                "•  DEFESA CONCLUÍDA  |  VIAGEM ESPACIAL EM CURSO  •",
                new Color(0.38f, 0.94f, 1f, 1f),
                statsLine +
                "Você neutralizou todos os meteoros e salvou a espaçonave!\n" +
                "Graças à sua agilidade e precisão nos cálculos, a rota foi liberada, os propulsores de dobra foram acionados e a nave já está viajando em segurança pelo espaço.\n" +
                "<color=#6EE7B7><b>Excelente trabalho! Boa viagem pelas estrelas!</b></color>",
                "JOGAR NOVAMENTE  >>",
                ""
            );

            gameOverPanel.SetActive(true);
            AnimateEndPanelIn();
        }
    }

    public void ShowGameOver(int finalScore)
    {
        ShowGameOver(finalScore, 0, 0, 0f, 1);
    }

    public void ShowGameOver(int finalScore, int correct, int wrong, float accuracy, int rounds)
    {
        HologramVisorEffect.SetSpaceTravelActive(false);

        if (gameOverPanel != null)
        {
            if (feedbackText != null) feedbackText.gameObject.SetActive(false);
            ConfigureGameOverLayout();
            SetBackgroundHUDPanelsVisible(false);

            if (endPanelHoloMat != null)
            {
                endPanelHoloMat.SetColor("_PrimaryColor", new Color(1.00f, 0.32f, 0.28f, 1f));
                endPanelHoloMat.SetColor("_SecondaryColor", new Color(1.00f, 0.68f, 0.18f, 1f));
                endPanelHoloMat.SetColor("_BgColor", new Color(0.11f, 0.02f, 0.04f, 0.84f));
            }

            string statsLine = $"<size=92%><color=#FBBF24><b>PONTOS: {finalScore:D4}   |   ACERTOS: {correct}   |   PRECISÃO: {accuracy:F0}%</b></color></size>\n\n";

            SetEndScreenTexts(
                "NAVE DESTRUÍDA",
                new Color(1f, 0.32f, 0.32f, 1f),
                "•  OS ESCUDOS FALHARAM  •",
                new Color(1f, 0.78f, 0.25f, 1f),
                statsLine +
                "Os impactos comprometeram os escudos da nave e a energia chegou a zero.\n" +
                "Você pode tentar novamente agora mesmo: resolva as operações no visor e neutralize os detritos corretos para proteger a embarcação!",
                "TENTAR NOVAMENTE  >>",
                ""
            );

            gameOverPanel.SetActive(true);
            AnimateEndPanelIn();
        }
    }

    private void AnimateEndPanelIn()
    {
        if (!gameObject.activeInHierarchy || gameOverPanel == null) return;
        if (endPanelAnimRoutine != null) StopCoroutine(endPanelAnimRoutine);
        endPanelAnimRoutine = StartCoroutine(AnimateEndPanelRoutine());
    }

    private IEnumerator AnimateEndPanelRoutine()
    {
        Transform tr = gameOverPanel.transform;
        Vector3 startScale = new Vector3(0.84f, 0.84f, 1f);
        Vector3 endScale = new Vector3(0.92f, 0.92f, 1f);
        float elapsed = 0f;
        float duration = 0.28f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
            tr.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
            yield return null;
        }
        tr.localScale = endScale;
    }

    public void HideGameOverPanel()
    {
        HologramVisorEffect.SetSpaceTravelActive(false);

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
            SetBackgroundHUDPanelsVisible(true);
        }
    }

    #endregion

    #region Alertas da Nave

    public void ShowShipAlarm(string msg)
    {
        if (shipAlarmText != null) shipAlarmText.text = msg;
        if (shipAlarmHologram != null) shipAlarmHologram.SetActive(true);
    }

    public void HideShipAlarm()
    {
        if (shipAlarmHologram != null) shipAlarmHologram.SetActive(false);
    }

    public void SetPanelLabelVisible(bool visible)
    {
        if (panelHologramLabel != null) panelHologramLabel.SetActive(visible);
    }

    #endregion
}

public class VRWorldSpaceUI : InterfaceVR { }

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
public static class InterfaceVREditorAutoSetup
{
    static InterfaceVREditorAutoSetup()
    {
        UnityEditor.EditorApplication.delayCall += TrySetupGameOverInActiveScene;
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, mode) => TrySetupGameOverInActiveScene();
    }

    private static void TrySetupGameOverInActiveScene()
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

        var ui = Object.FindAnyObjectByType<InterfaceVR>();
        if (ui != null)
        {
            ui.ConfigureGameOverLayout();
        }
    }
}
#endif
