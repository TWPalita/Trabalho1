using System;
using UnityEngine;

/// <summary>
/// Gerencia a pontuação do jogador e os pontos de energia / integridade da nave.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Configurações")]
    [SerializeField] private int initialEnergy = 100;
    [SerializeField] private int maxEnergy = 100;
    [SerializeField] private int pointsPerCorrect = 100;
    [SerializeField] private int energyLossPerWrong = 20;
    [SerializeField] private int pointsLossPerWrong = 20;

    public int CurrentScore { get; private set; } = 0;
    public int CurrentEnergy { get; private set; } = 100;
    public bool IsGameOver => CurrentEnergy <= 0;

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
        OnScoreChanged?.Invoke(CurrentScore);
        OnEnergyChanged?.Invoke(CurrentEnergy, maxEnergy);
    }

    public void AddCorrectHit()
    {
        CurrentScore += pointsPerCorrect;
        OnScoreChanged?.Invoke(CurrentScore);
    }

    public void AddWrongHit()
    {
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
