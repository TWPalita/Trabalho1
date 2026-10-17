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

        if (gameOverPanel == null) return;

        RectTransform panelRt = gameOverPanel.GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.sizeDelta = new Vector2(860f, 600f);
            panelRt.anchoredPosition = new Vector2(0f, 140f);
            panelRt.localPosition = new Vector3(0f, 140f, -60f);
            panelRt.localScale = new Vector3(0.90f, 0.90f, 1f);
        }

        // 1. Título ("FIM DE JOGO")
        Transform titleTr = gameOverPanel.transform.Find("Title");
        if (titleTr != null)
        {
            RectTransform titleRt = titleTr.GetComponent<RectTransform>();
            if (titleRt != null)
            {
                titleRt.anchoredPosition = new Vector2(0f, 230f);
                titleRt.sizeDelta = new Vector2(760f, 70f);
            }
            TMP_Text titleTxt = titleTr.GetComponent<TMP_Text>();
            if (titleTxt != null)
            {
                titleTxt.fontSize = 44f;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.alignment = TextAlignmentOptions.Center;
            }
        }

        // 2. Texto de pontuação e mensagem ("FinalScore")
        if (gameOverScoreText != null)
        {
            RectTransform scoreRt = gameOverScoreText.GetComponent<RectTransform>();
            if (scoreRt != null)
            {
                scoreRt.anchoredPosition = new Vector2(0f, 60f);
                scoreRt.sizeDelta = new Vector2(760f, 210f);
            }
            gameOverScoreText.fontSize = 25f;
            gameOverScoreText.lineSpacing = 18f;
            gameOverScoreText.alignment = TextAlignmentOptions.Center;
        }

        // 3. Botão Recomeçar ("BtnRestart")
        if (btnRestart != null)
        {
            RectTransform restartRt = btnRestart.GetComponent<RectTransform>();
            if (restartRt != null)
            {
                restartRt.anchoredPosition = new Vector2(0f, -120f);
                restartRt.sizeDelta = new Vector2(380f, 62f);
            }
            BoxCollider col = btnRestart.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.size = new Vector3(380f, 62f, 20f);
            }
        }

        // 4. Botão Voltar para a Nave ("BtnReturnShip")
        if (btnReturnToShip != null)
        {
            RectTransform returnRt = btnReturnToShip.GetComponent<RectTransform>();
            if (returnRt != null)
            {
                returnRt.anchoredPosition = new Vector2(0f, -215f);
                returnRt.sizeDelta = new Vector2(380f, 62f);
            }
            BoxCollider col = btnReturnToShip.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.size = new Vector3(380f, 62f, 20f);
            }
        }
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
            btnRestart.onClick.AddListener(() =>
            {
                if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
                GerenteDefesa def = GerenteDefesa.Instance != null ? GerenteDefesa.Instance : FindAnyObjectByType<GerenteDefesa>();
                if (def != null) def.RestartDefense();
            });
        }

        if (btnReturnToShip != null)
        {
            btnReturnToShip.onClick.AddListener(() =>
            {
                if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
                GerenteDefesa def = GerenteDefesa.Instance != null ? GerenteDefesa.Instance : FindAnyObjectByType<GerenteDefesa>();
                if (def != null) def.ReturnToShip();
                else UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
            });
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
        if (feedbackText == null) return;

        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(FeedbackRoutine(isCorrect, message));
    }

    private IEnumerator FeedbackRoutine(bool isCorrect, string message)
    {
        feedbackText.text = message;
        feedbackText.color = isCorrect ? new Color(0.2f, 1f, 0.4f, 1f) : new Color(1f, 0.25f, 0.25f, 1f);
        feedbackText.gameObject.SetActive(true);

        yield return new WaitForSeconds(1.3f);

        feedbackText.gameObject.SetActive(false);
    }

    public void ShowGameOver(int finalScore)
    {
        ShowGameOver(finalScore, 0, 0, 0f, 1);
    }

    public void ShowGameOver(int finalScore, int correct, int wrong, float accuracy, int rounds)
    {
        if (gameOverPanel != null)
        {
            ConfigureGameOverLayout();
            SetBackgroundHUDPanelsVisible(false);

            if (gameOverScoreText != null)
            {
                gameOverScoreText.text =
                    $"<color=#FF5555><b>ENERGIA DA NAVE ESGOTADA!</b></color>\n\n" +
                    $"Pontuação: <b>{finalScore}</b>     |     Acertos: <b>{correct}</b>     |     Erros: <b>{wrong}</b>\n\n" +
                    $"Deseja <b>recomeçar a defesa</b> ou <b>voltar para a nave</b>?";
            }
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


