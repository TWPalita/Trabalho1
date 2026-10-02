using System;
using UnityEngine;

/// <summary>
/// Gerencia a pontuação do jogador e os pontos de energia / integridade da nave.
/// </summary>
public class GerentePontuacao : MonoBehaviour
{
    public static GerentePontuacao Instance { get; private set; }

    [Header("Configurações")]
    [SerializeField] private int initialEnergy = 100;
    [SerializeField] private int maxEnergy = 100;
    [SerializeField] private int pointsPerCorrect = 100;
    [SerializeField] private int energyLossPerWrong = 20;
    [SerializeField] private int pointsLossPerWrong = 20;

    public int CurrentScore { get; private set; } = 0;
    public int CurrentEnergy { get; private set; } = 100;
    public bool IsGameOver => CurrentEnergy <= 0;

    // Estatísticas da Partida
    public int CorrectAnswers { get; private set; } = 0;
    public int WrongAnswers { get; private set; } = 0;
    public int CompletedRounds { get; private set; } = 0;
    public int TotalShots { get; private set; } = 0;

    public float AccuracyPercentage
    {
        get
        {
            int total = CorrectAnswers + WrongAnswers;
            if (total == 0) return 0f;
            return ((float)CorrectAnswers / total) * 100f;
        }
    }

    public event Action<int> OnScoreChanged;
    public event Action<int, int> OnEnergyChanged; // (current, max)
    public event Action OnGameOver;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        ResetStats();
    }

    public void ResetStats()
    {
        CurrentScore = 0;
        CurrentEnergy = initialEnergy;
        CorrectAnswers = 0;
        WrongAnswers = 0;
        CompletedRounds = 0;
        TotalShots = 0;

        OnScoreChanged?.Invoke(CurrentScore);
        OnEnergyChanged?.Invoke(CurrentEnergy, maxEnergy);
    }

    public void AddCorrectHit()
    {
        CorrectAnswers++;
        CompletedRounds++;
        TotalShots++;
        CurrentScore += pointsPerCorrect;
        OnScoreChanged?.Invoke(CurrentScore);
    }

    public void AddWrongHit()
    {
        WrongAnswers++;
        TotalShots++;
        CurrentEnergy = Mathf.Max(0, CurrentEnergy - energyLossPerWrong);
        CurrentScore = Mathf.Max(0, CurrentScore - pointsLossPerWrong);

        OnScoreChanged?.Invoke(CurrentScore);
        OnEnergyChanged?.Invoke(CurrentEnergy, maxEnergy);

        if (IsGameOver)
        {
            OnGameOver?.Invoke();
        }
    }
}

public class ScoreManager : GerentePontuacao { }

