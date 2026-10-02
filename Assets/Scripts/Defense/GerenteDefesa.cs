using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gerencia as rodadas do Sistema de Defesa Espacial em Realidade Virtual:
/// geração de alvos numerados no espaço, validação de tiro por raio VR,
/// pontuação, integridade e interface em World Space.
/// </summary>
public class GerenteDefesa : MonoBehaviour
{
    public static GerenteDefesa Instance { get; private set; }

    [Header("Referências Centrais")]
    [SerializeField] private GeradorMatematica mathManager;
    [SerializeField] private GerentePontuacao scoreManager;
    [SerializeField] private ArmaVR weaponController;
    [SerializeField] private GameObject debrisPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Configurações das Rodadas")]
    [SerializeField] private float delayBetweenRounds = 1.5f;
    [SerializeField] private Transform shipExplorationSpawn;
    [SerializeField] private string shipSceneName = "SampleScene";
    [SerializeField] private bool autoStartOnAwake = false;

    private List<Detrito> activeDebris = new List<Detrito>();
    private MathProblem currentProblem;
    private int currentRound = 1;
    private bool isRoundTransitioning = false;
    private bool isDefenseActive = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        if (mathManager == null) mathManager = FindAnyObjectByType<GeradorMatematica>();
        if (scoreManager == null) scoreManager = FindAnyObjectByType<GerentePontuacao>();
        if (weaponController == null) weaponController = FindAnyObjectByType<ArmaVR>();
    }

    private void Start()
    {
        if (scoreManager != null)
        {
            scoreManager.OnGameOver += HandleGameOver;
        }

        if (autoStartOnAwake)
        {
            StartDefense();
        }
    }

    private void OnDestroy()
    {
        if (scoreManager != null)
        {
            scoreManager.OnGameOver -= HandleGameOver;
        }
    }

    public void StartDefense()
    {
        isDefenseActive = true;
        currentRound = 1;

        if (scoreManager != null)
        {
            scoreManager.ResetStats();
        }

        if (InterfaceVR.Instance != null)
        {
            InterfaceVR.Instance.HideGameOverPanel();
            InterfaceVR.Instance.SetCockpitHUDVisible(true);
        }

        StartNewRound();
    }

    public void StartNewRound()
    {
        if (mathManager == null || !isDefenseActive) return;
        isRoundTransitioning = false;

        ClearAllDebris();

        // 1. Gera novo problema matemático com base na progressão da rodada
        currentProblem = mathManager.GenerateNewProblem(currentRound);

        // 2. Atualiza UI World Space no Cockpit
        if (InterfaceVR.Instance != null)
        {
            InterfaceVR.Instance.UpdateMathQuestion(currentProblem.QuestionText);
            InterfaceVR.Instance.UpdateRound(currentRound);
        }

        // 3. Spawna os objetos espaciais com as opções de resposta no campo de visão
        SpawnDebris(currentProblem.Options);

        ArmaVR.SetAllCanShoot(true);
        if (weaponController != null)
        {
            weaponController.SetCanShoot(true);
        }
    }

    private void SpawnDebris(int[] options)
    {
        if (debrisPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[GerenteDefesa] Prefab de detrito ou pontos de spawn não configurados!");
            return;
        }

        int count = Mathf.Min(options.Length, spawnPoints.Length);
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(debrisPrefab, spawnPoints[i].position, spawnPoints[i].rotation);

            Detrito deb = obj.GetComponent<Detrito>();
            if (deb != null)
            {
                deb.Init(options[i], this);
                activeDebris.Add(deb);
            }
        }
    }

    public void OnDebrisHit(Detrito hitDebris)
    {
        if (isRoundTransitioning || hitDebris == null || !isDefenseActive || hitDebris.IsHit) return;
        hitDebris.MarkHit();

        if (hitDebris.Number == currentProblem.CorrectAnswer)
        {
            // === ACERTO ===
            isRoundTransitioning = true;
            ArmaVR.SetAllCanShoot(false);
            if (weaponController != null) weaponController.SetCanShoot(false);

            ArmaVR.TriggerAllHaptics(0.85f, 0.2f);

            if (scoreManager != null) scoreManager.AddCorrectHit();
            if (GerenteSom.Instance != null) GerenteSom.Instance.PlayCorrect();

            if (InterfaceVR.Instance != null)
            {
                InterfaceVR.Instance.ShowFeedback(true, "CORRETO!\n+100 PONTOS");
            }

            activeDebris.Remove(hitDebris);
            hitDebris.Explode(true);

            StartCoroutine(NextRoundRoutine());
        }
        else
        {
            // === ERRO ===
            ArmaVR.TriggerAllHaptics(1.0f, 0.35f);

            if (scoreManager != null) scoreManager.AddWrongHit();
            if (GerenteSom.Instance != null) GerenteSom.Instance.PlayError();

            if (InterfaceVR.Instance != null)
            {
                InterfaceVR.Instance.ShowFeedback(false, "ERRADO!\n-20 ENERGIA");
            }

            activeDebris.Remove(hitDebris);
            hitDebris.Explode(false);
        }
    }

    private IEnumerator NextRoundRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        ClearAllDebris();

        yield return new WaitForSeconds(delayBetweenRounds - 0.4f);

        currentRound++;
        StartNewRound();
    }

    private void ClearAllDebris()
    {
        foreach (var deb in activeDebris)
        {
            if (deb != null) Destroy(deb.gameObject);
        }
        activeDebris.Clear();
    }

    private void HandleGameOver()
    {
        isRoundTransitioning = true;
        isDefenseActive = false;
        ArmaVR.SetAllCanShoot(false);
        if (weaponController != null) weaponController.SetCanShoot(false);
        ClearAllDebris();

        if (InterfaceVR.Instance != null && scoreManager != null)
        {
            InterfaceVR.Instance.ShowGameOver(
                scoreManager.CurrentScore,
                scoreManager.CorrectAnswers,
                scoreManager.WrongAnswers,
                scoreManager.AccuracyPercentage,
                scoreManager.CompletedRounds
            );
        }
        else if (InterfaceVR.Instance != null)
        {
            int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;
            InterfaceVR.Instance.ShowGameOver(finalScore);
        }
    }

    public void RestartDefense()
    {
        StartDefense();
    }

    public void ReturnToShip()
    {
        isDefenseActive = false;
        isRoundTransitioning = true;
        ArmaVR.SetAllCanShoot(false);
        ClearAllDebris();

        if (InterfaceVR.Instance != null)
        {
            InterfaceVR.Instance.HideGameOverPanel();
            InterfaceVR.Instance.SetCockpitHUDVisible(false);
        }

        if (TransicaoTela.Instance != null)
        {
            TransicaoTela.Instance.FadeOut(0.4f, () =>
            {
                SceneManager.LoadScene(shipSceneName);
            });
        }
        else if (Application.CanStreamedLevelBeLoaded(shipSceneName))
        {
            SceneManager.LoadScene(shipSceneName);
        }
    }
}

public class DefenseManager : GerenteDefesa { }

