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
                if (GerenteDefesa.Instance != null) GerenteDefesa.Instance.ReturnToShip();
            });
        }

        if (btnRestart != null)
        {
            btnRestart.onClick.AddListener(() =>
            {
                if (GerenteDefesa.Instance != null) GerenteDefesa.Instance.RestartDefense();
            });
        }

        if (btnReturnToShip != null)
        {
            btnReturnToShip.onClick.AddListener(() =>
            {
                if (GerenteDefesa.Instance != null) GerenteDefesa.Instance.ReturnToShip();
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
            if (gameOverScoreText != null)
            {
                gameOverScoreText.text = $"DEFESA ENCERRADA\n\n" +
                                         $"PONTUAÇÃO: {finalScore}\n\n" +
                                         $"ACERTOS: {correct}\n" +
                                         $"ERROS: {wrong}\n" +
                                         $"PRECISÃO: {accuracy:F0}%\n" +
                                         $"RODADAS: {rounds}";
            }
            gameOverPanel.SetActive(true);
        }
    }

    public void HideGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
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

