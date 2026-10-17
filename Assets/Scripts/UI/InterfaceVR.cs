using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gerencia as interfaces tridimensionais em World Space adaptadas para conforto e clareza em VR.
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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        ConfigureGameOverLayout();
    }

    public void ConfigureGameOverLayout()
    {
        RectTransform hudRootRt = GetComponent<RectTransform>();
        if (hudRootRt != null && gameObject.name.IndexOf("Cockpit_WorldSpace_HUD", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            hudRootRt.position = new Vector3(-0.18f, 1.48f, 1.35f);
            hudRootRt.localScale = new Vector3(0.0026f, 0.0026f, 0.0026f);
        }

        // Posiciona o canva da conta (MathVisor_Border) e os pontos/energia (ControlPanel_Border) mais abaixo, próximos ao painel
        Transform mathVisorTr = transform.Find("MathVisor_Border");
        if (mathVisorTr != null)
        {
            RectTransform mathVisorRt = mathVisorTr.GetComponent<RectTransform>();
            if (mathVisorRt != null)
            {
                mathVisorRt.anchoredPosition = new Vector2(0f, -20f);
            }
        }

        Transform controlBorderTr = transform.Find("ControlPanel_Border");
        if (controlBorderTr != null)
        {
            RectTransform controlBorderRt = controlBorderTr.GetComponent<RectTransform>();
            if (controlBorderRt != null)
            {
                controlBorderRt.anchoredPosition = new Vector2(0f, -40f);
            }
        }

        // Sobe somente os pontos (ScoreValue) e a energia com a barra (EnergySegments e EnergyPercent)
        if (scoreText != null)
        {
            RectTransform scoreValRt = scoreText.GetComponent<RectTransform>();
            if (scoreValRt != null)
            {
                scoreValRt.anchoredPosition = new Vector2(-300f, -5f);
            }
        }

        if (energySegmentedText != null)
        {
            RectTransform energySegRt = energySegmentedText.GetComponent<RectTransform>();
            if (energySegRt != null)
            {
                energySegRt.anchoredPosition = new Vector2(300f, 2f);
            }
        }

        if (energyText != null)
        {
            RectTransform energyPctRt = energyText.GetComponent<RectTransform>();
            if (energyPctRt != null)
            {
                energyPctRt.anchoredPosition = new Vector2(300f, -32f);
            }
        }

        if (feedbackText != null)
        {
            RectTransform fbRt = feedbackText.GetComponent<RectTransform>();
            if (fbRt != null)
            {
                fbRt.anchoredPosition = new Vector2(0f, 72f);
                fbRt.sizeDelta = new Vector2(860f, 110f);
            }
            feedbackText.fontSize = 22f;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.textWrappingMode = TextWrappingModes.Normal;
        }

        if (gameOverPanel == null) return;

        RectTransform panelRt = gameOverPanel.GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.sizeDelta = new Vector2(880f, 650f);
            panelRt.anchoredPosition = new Vector2(0f, 140f);
            panelRt.localPosition = new Vector3(0f, 140f, -60f);
            panelRt.localScale = new Vector3(0.90f, 0.90f, 1f);
        }

        // 1. Título
        Transform titleTr = gameOverPanel.transform.Find("Title");
        TMP_FontAsset sharedFont = gameOverScoreText != null ? gameOverScoreText.font : null;
        if (titleTr != null)
        {
            RectTransform titleRt = titleTr.GetComponent<RectTransform>();
            if (titleRt != null)
            {
                titleRt.anchoredPosition = new Vector2(0f, 255f);
                titleRt.sizeDelta = new Vector2(800f, 56f);
            }
            TMP_Text titleTxt = titleTr.GetComponent<TMP_Text>();
            if (titleTxt != null)
            {
                titleTxt.fontSize = 38f;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.alignment = TextAlignmentOptions.Center;
                if (sharedFont == null) sharedFont = titleTxt.font;
            }
        }

        // 1b. Subtítulo ("Subtitle")
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
            subRt.anchoredPosition = new Vector2(0f, 202f);
            subRt.sizeDelta = new Vector2(800f, 40f);
        }
        TMP_Text subTxt = subTr.GetComponent<TMP_Text>();
        if (subTxt == null)
        {
            subTxt = subTr.gameObject.AddComponent<TextMeshProUGUI>();
        }
        if (sharedFont != null) subTxt.font = sharedFont;
        subTxt.fontSize = 24f;
        subTxt.fontStyle = FontStyles.Bold;
        subTxt.alignment = TextAlignmentOptions.Center;
        subTxt.raycastTarget = false;

        // 2. Texto de mensagem ("FinalScore")
        if (gameOverScoreText != null)
        {
            RectTransform scoreRt = gameOverScoreText.GetComponent<RectTransform>();
            if (scoreRt != null)
            {
                scoreRt.anchoredPosition = new Vector2(0f, 45f);
                scoreRt.sizeDelta = new Vector2(780f, 235f);
            }
            gameOverScoreText.fontSize = 23f;
            gameOverScoreText.lineSpacing = 12f;
            gameOverScoreText.alignment = TextAlignmentOptions.Center;
            gameOverScoreText.textWrappingMode = TextWrappingModes.Normal;
        }

        // 3. Botão Recomeçar ("BtnRestart")
        if (btnRestart != null)
        {
            RectTransform restartRt = btnRestart.GetComponent<RectTransform>();
            if (restartRt != null)
            {
                restartRt.anchoredPosition = new Vector2(0f, -145f);
                restartRt.sizeDelta = new Vector2(420f, 62f);
            }
            BoxCollider col = btnRestart.GetComponent<BoxCollider>();
            if (col == null) col = btnRestart.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(420f, 62f, 20f);
            col.isTrigger = false;

            var xrInt = btnRestart.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (xrInt == null)
            {
                xrInt = btnRestart.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            }
            if (!xrInt.colliders.Contains(col)) xrInt.colliders.Add(col);
        }

        // 4. Botão Voltar ao Início ("BtnReturnShip")
        if (btnReturnToShip != null)
        {
            RectTransform returnRt = btnReturnToShip.GetComponent<RectTransform>();
            if (returnRt != null)
            {
                returnRt.anchoredPosition = new Vector2(0f, -230f);
                returnRt.sizeDelta = new Vector2(420f, 62f);
            }
            BoxCollider col = btnReturnToShip.GetComponent<BoxCollider>();
            if (col == null) col = btnReturnToShip.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(420f, 62f, 20f);
            col.isTrigger = false;

            var xrInt = btnReturnToShip.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (xrInt == null)
            {
                xrInt = btnReturnToShip.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            }
            if (!xrInt.colliders.Contains(col)) xrInt.colliders.Add(col);
        }
    }

    public void SetHUDRootElementsVisible(bool visible)
    {
        Transform mathVisorTr = transform.Find("MathVisor_Border");
        if (mathVisorTr != null) mathVisorTr.gameObject.SetActive(visible);

        Transform controlBorderTr = transform.Find("ControlPanel_Border");
        if (controlBorderTr != null) controlBorderTr.gameObject.SetActive(visible);
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
            btnConsoleReturnShip.onClick.AddListener(() =>
            {
                if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
                GerenteDefesa def = GerenteDefesa.Instance != null ? GerenteDefesa.Instance : FindAnyObjectByType<GerenteDefesa>();
                if (def != null) def.ReturnToShip();
                else UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
            });
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
            UnityEngine.Events.UnityAction returnAction = () =>
            {
                if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
                GerenteDefesa def = GerenteDefesa.Instance != null ? GerenteDefesa.Instance : FindAnyObjectByType<GerenteDefesa>();
                if (def != null) def.ReturnToShip();
                else UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
            };
            btnReturnToShip.onClick.AddListener(returnAction);

            var xrInt = btnReturnToShip.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (xrInt != null)
            {
                xrInt.selectEntered.AddListener(_ => returnAction());
                xrInt.activated.AddListener(_ => returnAction());
            }
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

    private void OnDestroy()
    {
        if (GerentePontuacao.Instance != null)
        {
            GerentePontuacao.Instance.OnScoreChanged -= UpdateScoreDisplay;
            GerentePontuacao.Instance.OnEnergyChanged -= UpdateEnergyDisplay;
        }
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
            mathQuestionText.text = question;
        }
    }

    public void UpdateRound(int round)
    {
        if (roundText != null)
        {
            roundText.text = $"RODADA {round}";
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
            energyText.text = $"ENERGIA: {current}%";
        }
        if (energyFillImage != null)
        {
            float fill = (float)current / Mathf.Max(1, max);
            energyFillImage.fillAmount = fill;
            energyFillImage.color = fill > 0.5f ? Color.green : (fill > 0.25f ? Color.yellow : Color.red);
        }
        if (energySegmentedText != null)
        {
            int totalBlocks = 8;
            int activeBlocks = Mathf.Clamp(Mathf.CeilToInt((float)current / Mathf.Max(1, max) * totalBlocks), 0, totalBlocks);
            string blocks = new string('█', activeBlocks).PadRight(totalBlocks, '░');
            energySegmentedText.text = blocks;

            if (current > 50) energySegmentedText.color = new Color(0.2f, 1f, 0.4f, 1f);
            else if (current > 20) energySegmentedText.color = new Color(1f, 0.85f, 0.2f, 1f);
            else energySegmentedText.color = new Color(1f, 0.25f, 0.25f, 1f);
        }
    }

    public void ShowFeedback(bool isCorrect, string message)
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
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
            TMP_Text returnTxt = btnReturnToShip.GetComponentInChildren<TMP_Text>(true);
            if (returnTxt != null) returnTxt.text = returnBtnLabel;
        }
    }

    public void ShowVictory(int finalScore = 0, int correct = 0, int wrong = 0, float accuracy = 0f, int rounds = 1)
    {
        if (gameOverPanel != null)
        {
            if (feedbackText != null) feedbackText.gameObject.SetActive(false);
            ConfigureGameOverLayout();
            SetBackgroundHUDPanelsVisible(false);

            SetEndScreenTexts(
                "DEFESA BEM-SUCEDIDA",
                new Color(0.25f, 1f, 0.55f, 1f),
                "ROTA SEGURA",
                new Color(0.35f, 0.9f, 1f, 1f),
                "Todos os detritos previstos para esta interceptação foram neutralizados.\n" +
                "A nave está protegida e a rota foi liberada. Mesmo após o ataque dos piratas espaciais, você conseguiu restaurar a energia e defender a embarcação.\n" +
                "Graças à sua atenção e aos seus cálculos, a jornada pode continuar.\n" +
                "Missão concluída. Boa viagem, viajante!",
                "[ JOGAR NOVAMENTE ]",
                "[ VOLTAR AO INÍCIO ]"
            );

            gameOverPanel.SetActive(true);
        }
    }

    public void ShowGameOver(int finalScore)
    {
        ShowGameOver(finalScore, 0, 0, 0f, 1);
    }

    public void ShowGameOver(int finalScore, int correct, int wrong, float accuracy, int rounds)
    {
        if (gameOverPanel != null)
        {
            if (feedbackText != null) feedbackText.gameObject.SetActive(false);
            ConfigureGameOverLayout();
            SetBackgroundHUDPanelsVisible(false);

            SetEndScreenTexts(
                "NAVE DESTRUÍDA",
                new Color(1f, 0.3f, 0.3f, 1f),
                "OS ESCUDOS FALHARAM",
                new Color(1f, 0.78f, 0.25f, 1f),
                "Os impactos dos detritos comprometeram os escudos da nave, e a energia chegou a zero.\n" +
                "A missão de defesa falhou. A embarcação não conseguiu resistir aos danos.\n" +
                "Você pode tentar novamente, resolver as operações com atenção e neutralizar os alvos antes que seja tarde demais.",
                "[ TENTAR NOVAMENTE ]",
                "[ VOLTAR AO INÍCIO ]"
            );

            gameOverPanel.SetActive(true);
        }
    }

    public void HideGameOverPanel()
    {
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


