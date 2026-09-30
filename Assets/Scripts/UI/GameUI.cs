using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gerencia a interface de usuário tanto na nave (exploração) quanto no sistema de defesa (matemática).
/// </summary>
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("UI da Espaçonave (Exploração)")]
    [SerializeField] private GameObject interactionPromptPanel;
    [SerializeField] private TMP_Text interactionPromptText;
    [SerializeField] private GameObject alarmAlertBanner;
    [SerializeField] private TMP_Text alarmAlertText;

    [Header("UI do Sistema de Defesa")]
    [SerializeField] private TMP_Text mathQuestionText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private Image energyFillImage;
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Painéis de Fim de Jogo e Vitória")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverScoreText;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TMP_Text victoryScoreText;
    [SerializeField] private Button btnRestartDefense;
    [SerializeField] private Button btnReturnToShip;

    private Coroutine feedbackRoutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        // Conecta botões se configurados
        if (btnRestartDefense != null)
        {
            btnRestartDefense.onClick.AddListener(() =>
            {
                if (DefenseManager.Instance != null) DefenseManager.Instance.RestartDefense();
            });
        }

        if (btnReturnToShip != null)
        {
            btnReturnToShip.onClick.AddListener(() =>
            {
                if (DefenseManager.Instance != null) DefenseManager.Instance.ReturnToShip();
            });
        }

        // Inscreve no ScoreManager se presente na cena
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged += UpdateScoreDisplay;
            ScoreManager.Instance.OnEnergyChanged += UpdateEnergyDisplay;
            UpdateScoreDisplay(ScoreManager.Instance.CurrentScore);
            UpdateEnergyDisplay(ScoreManager.Instance.CurrentEnergy, 100);
        }

        // Garante estados iniciais
        HideInteractionPrompt();
        HideAlarmAlert();
        HideResultPanels();
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged -= UpdateScoreDisplay;
            ScoreManager.Instance.OnEnergyChanged -= UpdateEnergyDisplay;
        }
    }

    #region Métodos de Exploração da Nave

    public void ShowInteractionPrompt(string message)
    {
        if (interactionPromptText != null) interactionPromptText.text = message;
        if (interactionPromptPanel != null) interactionPromptPanel.SetActive(true);
    }

    public void HideInteractionPrompt()
    {
        if (interactionPromptPanel != null) interactionPromptPanel.SetActive(false);
    }

    public void ShowAlarmAlert(string message)
    {
        if (alarmAlertText != null) alarmAlertText.text = message;
        if (alarmAlertBanner != null) alarmAlertBanner.SetActive(true);
    }

    public void HideAlarmAlert()
    {
        if (alarmAlertBanner != null) alarmAlertBanner.SetActive(false);
    }

    #endregion

    #region Métodos do Sistema de Defesa

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
            roundText.text = $"RODADA: {round}";
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
            // Muda cor dependendo do nível de energia
            energyFillImage.color = fill > 0.5f ? Color.green : (fill > 0.25f ? Color.yellow : Color.red);
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
        feedbackText.color = isCorrect ? new Color(0.2f, 1f, 0.3f, 1f) : new Color(1f, 0.25f, 0.25f, 1f);
        feedbackText.gameObject.SetActive(true);

        yield return new WaitForSeconds(1.2f);

        feedbackText.gameObject.SetActive(false);
    }

    public void ShowGameOver(int finalScore)
    {
        if (gameOverPanel != null)
        {
            if (gameOverScoreText != null)
            {
                gameOverScoreText.text = $"PONTUAÇÃO FINAL: {finalScore}";
            }
            gameOverPanel.SetActive(true);
        }
    }

    public void ShowVictory(int finalScore)
    {
        if (victoryPanel != null)
        {
            if (victoryScoreText != null)
            {
                victoryScoreText.text = $"SETOR DEFENDIDO!\nPONTOS: {finalScore}";
            }
            victoryPanel.SetActive(true);
        }
    }

    public void HideResultPanels()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    #endregion
}
