using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gerencia as rodadas do Sistema de Defesa Espacial em Realidade Virtual:
/// geração de alvos numerados no espaço, validação de tiro por raio VR,
/// pontuação, integridade e interface em World Space.
/// </summary>
public class DefenseManager : MonoBehaviour
{
    public static DefenseManager Instance { get; private set; }

    [Header("Referências Centrais")]
    [SerializeField] private MathManager mathManager;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private VRWeaponController weaponController;
    [SerializeField] private GameObject debrisPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Configurações das Rodadas")]
    [SerializeField] private float delayBetweenRounds = 1.5f;
    [SerializeField] private Transform shipExplorationSpawn;
    [SerializeField] private string shipSceneName = "NaveScene";
    [SerializeField] private bool autoStartOnAwake = false;

    private List<Debris> activeDebris = new List<Debris>();
    private List<LixoEspacial> activeLegacyLixos = new List<LixoEspacial>();
    private MathProblem currentProblem;
    private int currentRound = 1;
    private bool isRoundTransitioning = false;
    private bool isDefenseActive = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        if (mathManager == null) mathManager = FindAnyObjectByType<MathManager>();
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
        if (weaponController == null) weaponController = FindAnyObjectByType<VRWeaponController>();
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

        if (VRWorldSpaceUI.Instance != null)
        {
            VRWorldSpaceUI.Instance.HideGameOverPanel();
            VRWorldSpaceUI.Instance.SetCockpitHUDVisible(true);
        }

        StartNewRound();
    }

    public void StartNewRound()
    {
        if (mathManager == null || !isDefenseActive) return;
        isRoundTransitioning = false;

        ClearAllDebris();

        // 1. Gera novo problema matemático
        currentProblem = mathManager.GenerateNewProblem();

        // 2. Atualiza UI World Space no Cockpit
        if (VRWorldSpaceUI.Instance != null)
        {
            VRWorldSpaceUI.Instance.UpdateMathQuestion(currentProblem.QuestionText);
            VRWorldSpaceUI.Instance.UpdateRound(currentRound);
        }

        // 3. Spawna os objetos espaciais com as opções de resposta no campo de visão
        SpawnDebris(currentProblem.Options);

        VRWeaponController.SetAllCanShoot(true);
        if (weaponController != null)
        {
            weaponController.SetCanShoot(true);
        }
    }

    private void SpawnDebris(int[] options)
    {
        if (debrisPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[DefenseManager] Prefab de detrito ou pontos de spawn não configurados!");
            return;
        }

        int count = Mathf.Min(options.Length, spawnPoints.Length);
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(debrisPrefab, spawnPoints[i].position, spawnPoints[i].rotation);

            Debris deb = obj.GetComponent<Debris>();
            if (deb != null)
            {
                deb.Init(options[i], this);
                activeDebris.Add(deb);
            }
            else
            {
                LixoEspacial leg = obj.GetComponent<LixoEspacial>();
                if (leg != null)
                {
                    leg.ConfigurarLixo(options[i]);
                    activeLegacyLixos.Add(leg);
                }
            }
        }
    }

    public void OnDebrisHit(Debris hitDebris)
    {
        if (isRoundTransitioning || hitDebris == null || !isDefenseActive) return;

        if (hitDebris.Number == currentProblem.CorrectAnswer)
        {
            // === ACERTO ===
            isRoundTransitioning = true;
            VRWeaponController.SetAllCanShoot(false);
            if (weaponController != null) weaponController.SetCanShoot(false);

            if (scoreManager != null) scoreManager.AddCorrectHit();
            if (SoundEffectsManager.Instance != null) SoundEffectsManager.Instance.PlayCorrect();

            if (VRWorldSpaceUI.Instance != null)
            {
                VRWorldSpaceUI.Instance.ShowFeedback(true, "CORRETO!\n+100 PONTOS");
            }

            activeDebris.Remove(hitDebris);
            hitDebris.Explode(true);

            StartCoroutine(NextRoundRoutine());
        }
        else
        {
            // === ERRO ===
            if (scoreManager != null) scoreManager.AddWrongHit();
            if (SoundEffectsManager.Instance != null) SoundEffectsManager.Instance.PlayError();

            if (VRWorldSpaceUI.Instance != null)
            {
                VRWorldSpaceUI.Instance.ShowFeedback(false, "ERRADO!\n-20 ENERGIA");
            }

            activeDebris.Remove(hitDebris);
            hitDebris.Explode(false);
        }
    }

    public void OnLegacyLixoHit(LixoEspacial hitLixo)
    {
        if (isRoundTransitioning || hitLixo == null || !isDefenseActive) return;

        if (hitLixo.valorDesteLixo == currentProblem.CorrectAnswer)
        {
            isRoundTransitioning = true;
            VRWeaponController.SetAllCanShoot(false);
            if (weaponController != null) weaponController.SetCanShoot(false);

            if (scoreManager != null) scoreManager.AddCorrectHit();
            if (SoundEffectsManager.Instance != null) SoundEffectsManager.Instance.PlayCorrect();

            if (VRWorldSpaceUI.Instance != null)
            {
                VRWorldSpaceUI.Instance.ShowFeedback(true, "CORRETO!\n+100 PONTOS");
            }

            activeLegacyLixos.Remove(hitLixo);
            Destroy(hitLixo.gameObject);

            StartCoroutine(NextRoundRoutine());
        }
        else
        {
            if (scoreManager != null) scoreManager.AddWrongHit();
            if (SoundEffectsManager.Instance != null) SoundEffectsManager.Instance.PlayError();

            if (VRWorldSpaceUI.Instance != null)
            {
                VRWorldSpaceUI.Instance.ShowFeedback(false, "ERRADO!\n-20 ENERGIA");
            }

            activeLegacyLixos.Remove(hitLixo);
            Destroy(hitLixo.gameObject);
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

        foreach (var leg in activeLegacyLixos)
        {
            if (leg != null) Destroy(leg.gameObject);
        }
        activeLegacyLixos.Clear();
    }

    private void HandleGameOver()
    {
        isRoundTransitioning = true;
        isDefenseActive = false;
        VRWeaponController.SetAllCanShoot(false);
        if (weaponController != null) weaponController.SetCanShoot(false);
        ClearAllDebris();

        if (VRWorldSpaceUI.Instance != null)
        {
            int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;
            VRWorldSpaceUI.Instance.ShowGameOver(finalScore);
        }
    }

    public void RestartDefense()
    {
        StartDefense();
    }

    public void ReturnToShip()
    {
        isDefenseActive = false;
        ClearAllDebris();

        if (VRWorldSpaceUI.Instance != null)
        {
            VRWorldSpaceUI.Instance.HideGameOverPanel();
            VRWorldSpaceUI.Instance.SetCockpitHUDVisible(false);
        }

        if (VRPlayerController.Instance != null && shipExplorationSpawn != null)
        {
            VRPlayerController.Instance.TeleportTo(shipExplorationSpawn, () =>
            {
                if (AlarmManager.Instance != null)
                {
                    AlarmManager.Instance.TriggerAlarm();
                }
            });
        }
        else if (Application.CanStreamedLevelBeLoaded(shipSceneName))
        {
            SceneManager.LoadScene(shipSceneName);
        }
    }
}
