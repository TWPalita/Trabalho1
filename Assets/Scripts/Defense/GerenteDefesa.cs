using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gerencia as rodadas do Sistema de Defesa Espacial em Realidade Virtual:
/// geração de alvos numerados no espaço, validação de tiro por raio VR,
/// pontuação, integridade, feedback tático imediato e interface em World Space.
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

    [Header("Modo de Jogo")]
    [Tooltip("Se falso, executa o modo base de defesa e tiro ao alvo (sem matemática, sem pontuação punitiva)")]
    [SerializeField] private bool enableMath = true;

    [Header("Configurações das Rodadas")]
    [SerializeField] private float delayBetweenRounds = 1.2f;
    [SerializeField] private int roundsToWin = 10;
    [SerializeField] private Transform shipExplorationSpawn;
    [SerializeField] private string shipSceneName = "SampleScene";
    [SerializeField] private bool autoStartOnAwake = false;

    private List<Detrito> activeDebris = new List<Detrito>();
    private readonly List<Detrito> debrisPool = new List<Detrito>();
    private MathProblem currentProblem;
    private int currentRound = 1;
    private bool isRoundTransitioning = false;
    private bool roundResolved = false;
    private bool isDefenseActive = false;

    public bool IsDefenseActive => isDefenseActive;
    public int CurrentRound => currentRound;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        ArmaVR physicalWeapon = ArmaVR.EnsureScenePhysicalWeaponConfigured();
        if (physicalWeapon != null)
        {
            weaponController = physicalWeapon;
        }

        if (mathManager == null) mathManager = FindAnyObjectByType<GeradorMatematica>();
        if (scoreManager == null) scoreManager = FindAnyObjectByType<GerentePontuacao>();
        if (weaponController == null) weaponController = FindAnyObjectByType<ArmaVR>();

        if (roundsToWin == 5)
        {
            roundsToWin = 10;
        }
    }

    private void Start()
    {
        if (scoreManager != null)
        {
            scoreManager.OnGameOver += HandleGameOver;
        }

        // Pré-aquece o pool de explosões e o pool de meteoros no carregamento da cena (zero travadas durante o jogo)
        DebrisShatterEffect.PrewarmPool();
        EnsureDebrisPoolPrewarmed();

        // Garante que a sequência narrativa de introdução seja exibida ao chegar na cena
        IntroducaoDefesaVR intro = FindAnyObjectByType<IntroducaoDefesaVR>();
        if (intro == null)
        {
            GameObject introObj = new GameObject("IntroducaoDefesaVR_Controller");
            intro = introObj.AddComponent<IntroducaoDefesaVR>();
        }
    }

    private void EnsureDebrisPoolPrewarmed()
    {
        if (debrisPrefab == null || spawnPoints == null) return;

        int targetPoolSize = Mathf.Max(4, spawnPoints.Length);
        while (debrisPool.Count < targetPoolSize)
        {
            int idx = debrisPool.Count;
            Vector3 pos = (idx < spawnPoints.Length && spawnPoints[idx] != null)
                ? GetElevatedSpawnPosition(idx)
                : Vector3.zero;
            Quaternion rot = (idx < spawnPoints.Length && spawnPoints[idx] != null)
                ? spawnPoints[idx].rotation
                : Quaternion.identity;

            GameObject obj = Instantiate(debrisPrefab, pos, rot);
            obj.name = $"Detrito_Pooled_{idx + 1}";
            Detrito deb = obj.GetComponent<Detrito>();
            if (deb == null) deb = obj.AddComponent<Detrito>();
            if (obj.GetComponent<DebrisMovement>() == null) obj.AddComponent<DebrisMovement>();

            deb.DeactivateToPool();
            debrisPool.Add(deb);
        }
    }

    private Detrito GetOrCreatePooledDebris(int slotIndex, Vector3 spawnPos, Quaternion spawnRot)
    {
        EnsureDebrisPoolPrewarmed();

        if (slotIndex >= 0 && slotIndex < debrisPool.Count && debrisPool[slotIndex] != null && !debrisPool[slotIndex].gameObject.activeSelf)
        {
            Detrito slotDeb = debrisPool[slotIndex];
            slotDeb.transform.SetPositionAndRotation(spawnPos, spawnRot);
            slotDeb.gameObject.SetActive(true);
            return slotDeb;
        }

        for (int i = 0; i < debrisPool.Count; i++)
        {
            if (debrisPool[i] != null && !debrisPool[i].gameObject.activeSelf)
            {
                Detrito freeDeb = debrisPool[i];
                freeDeb.transform.SetPositionAndRotation(spawnPos, spawnRot);
                freeDeb.gameObject.SetActive(true);
                return freeDeb;
            }
        }

        GameObject obj = Instantiate(debrisPrefab, spawnPos, spawnRot);
        obj.name = $"Detrito_Pooled_{debrisPool.Count + 1}";
        Detrito newDeb = obj.GetComponent<Detrito>();
        if (newDeb == null) newDeb = obj.AddComponent<Detrito>();
        debrisPool.Add(newDeb);
        return newDeb;
    }

    private void Update()
    {
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

        if (mathManager != null)
        {
            mathManager.ResetHistory();
        }

        if (scoreManager != null)
        {
            scoreManager.ResetStats();
        }

        if (InterfaceVR.Instance != null)
        {
            InterfaceVR.Instance.HideGameOverPanel();
            InterfaceVR.Instance.SetCockpitHUDVisible(true);
            InterfaceVR.Instance.SetHUDRootElementsVisible(true);
        }

        StartNewRound();
    }

    public void StartNewRound()
    {
        if (!isDefenseActive) return;
        isRoundTransitioning = false;
        roundResolved = false;

        ClearAllDebris();

        if (enableMath && mathManager != null)
        {
            // 1. Gera novo problema matemático com base na progressão da rodada
            currentProblem = mathManager.GenerateNewProblem(currentRound);

            // 2. Atualiza UI World Space no Cockpit
            if (InterfaceVR.Instance != null)
            {
                InterfaceVR.Instance.UpdateMathQuestion(currentProblem.QuestionText);
                InterfaceVR.Instance.UpdateRound(currentRound, roundsToWin);
            }

            // 3. Spawna os objetos espaciais com as opções de resposta no campo de visão
            SpawnDebris(currentProblem.Options);
        }
        else
        {
            // === MODO BASE DE DEFESA (SEM MATEMÁTICA) ===
            if (InterfaceVR.Instance != null)
            {
                InterfaceVR.Instance.UpdateMathQuestion("DEFESA DO COCKPIT\nATINJA OS DETRITOS");
                InterfaceVR.Instance.UpdateRound(1);
            }

            SpawnAllDebrisBase();
        }

        ArmaVR.SetAllCanShoot(true);
        if (weaponController != null)
        {
            weaponController.SetCanShoot(true);
        }
    }

    private void SpawnAllDebrisBase()
    {
        if (debrisPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[GerenteDefesa] Prefab de detrito ou pontos de spawn não configurados!");
            return;
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null) continue;
            SpawnSingleDebris(i);
        }
    }

    private Vector3 GetElevatedSpawnPosition(int spawnIndex)
    {
        if (spawnPoints == null || spawnIndex < 0 || spawnIndex >= spawnPoints.Length || spawnPoints[spawnIndex] == null)
            return Vector3.zero;

        Vector3 pos = spawnPoints[spawnIndex].position;
        if (pos.y < 6.0f)
        {
            pos.y += 1.8f;
        }
        return pos;
    }

    private void SpawnSingleDebris(int spawnIndex)
    {
        if (spawnIndex < 0 || spawnIndex >= spawnPoints.Length || spawnPoints[spawnIndex] == null) return;

        Vector3 spawnPos = GetElevatedSpawnPosition(spawnIndex);
        Detrito deb = GetOrCreatePooledDebris(spawnIndex, spawnPos, spawnPoints[spawnIndex].rotation);
        if (deb != null)
        {
            deb.Init(0, this);
            deb.SetShowNumberLabel(false);
            activeDebris.Add(deb);

            DebrisMovement mov = deb.GetComponent<DebrisMovement>();
            if (mov == null) mov = deb.gameObject.AddComponent<DebrisMovement>();
            mov.SetSpawnPoint(spawnPos, spawnIndex);
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
            Vector3 spawnPos = GetElevatedSpawnPosition(i);
            Detrito deb = GetOrCreatePooledDebris(i, spawnPos, spawnPoints[i].rotation);
            if (deb != null)
            {
                deb.Init(options[i], this);
                deb.SetShowNumberLabel(true);
                activeDebris.Add(deb);

                DebrisMovement mov = deb.GetComponent<DebrisMovement>();
                if (mov == null) mov = deb.gameObject.AddComponent<DebrisMovement>();
                mov.SetSpawnPoint(spawnPos, i);
            }
        }
    }

    public void OnDebrisHit(Detrito hitDebris)
    {
        if (hitDebris == null || !isDefenseActive || hitDebris.IsHit) return;

        if (!enableMath)
        {
            hitDebris.MarkHit();

            if (GerenteSom.Instance != null)
            {
                GerenteSom.Instance.PlayHit();
            }

            ArmaVR.TriggerAllHaptics(0.8f, 0.18f);

            int spIndex = FindClosestSpawnIndex(hitDebris.transform.position);

            activeDebris.Remove(hitDebris);
            hitDebris.Explode(true);

            StartCoroutine(RespawnSingleDebrisRoutine(spIndex, 1.4f));
            return;
        }

        // === MODO COM MATEMÁTICA ===
        if (roundResolved || isRoundTransitioning) return;

        if (hitDebris.Number == currentProblem.CorrectAnswer)
        {
            // === ACERTO CORRETO ===
            roundResolved = true;
            isRoundTransitioning = true;
            hitDebris.MarkHit();

            ArmaVR.SetAllCanShoot(false);
            if (weaponController != null) weaponController.SetCanShoot(false);

            ArmaVR.TriggerAllHaptics(0.85f, 0.2f);

            if (scoreManager != null) scoreManager.AddCorrectHit();
            if (GerenteSom.Instance != null) GerenteSom.Instance.PlayCorrect();

            activeDebris.Remove(hitDebris);
            hitDebris.Explode(true);

            // Após acertar a primeira conta (Rodada 1), faz a tela de objetivo sumir como um holograma se desligando
            if (currentRound == 1 && IntroducaoDefesaVR.Instance != null)
            {
                IntroducaoDefesaVR.Instance.DismissObjectiveHologramAfterFirstQuestion();
            }

            StartCoroutine(NextRoundRoutine());
        }
        else
        {
            // === RESPOSTA INCORRETA ===
            ArmaVR.TriggerAllHaptics(0.6f, 0.25f);

            if (scoreManager != null) scoreManager.AddWrongHit();

            if (scoreManager != null && scoreManager.IsGameOver)
            {
                return;
            }

            if (GerenteSom.Instance != null) GerenteSom.Instance.PlayError();

            if (hitDebris != null) hitDebris.FlashError();
        }
    }

    private IEnumerator RespawnSingleDebrisRoutine(int spawnIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (isDefenseActive && !enableMath)
        {
            SpawnSingleDebris(spawnIndex);
        }
    }

    private int FindClosestSpawnIndex(Vector3 pos)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return 0;
        int closest = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null) continue;
            float d = Vector3.Distance(pos, spawnPoints[i].position);
            if (d < minDist)
            {
                minDist = d;
                closest = i;
            }
        }
        return closest;
    }

    private IEnumerator NextRoundRoutine()
    {
        yield return new WaitForSeconds(0.28f);
        ClearAllDebris();

        if (!isDefenseActive) yield break;

        if (roundsToWin > 0 && currentRound >= roundsToWin)
        {
            if (InterfaceVR.Instance != null)
            {
                InterfaceVR.Instance.SetHUDRootElementsVisible(false);
            }
            HologramVisorEffect.SetSpaceTravelActive(true);
            yield return new WaitForSeconds(0.65f);
            HandleVictory();
            yield break;
        }

        float roundWait = (currentRound == 1)
            ? 0.92f
            : Mathf.Clamp(delayBetweenRounds * 0.45f, 0.55f, 0.85f);
        yield return new WaitForSeconds(roundWait);

        if (!isDefenseActive) yield break;

        currentRound++;
        StartNewRound();
    }

    private void ClearAllDebris()
    {
        for (int i = 0; i < activeDebris.Count; i++)
        {
            Detrito deb = activeDebris[i];
            if (deb != null)
            {
                deb.DeactivateToPool();
            }
        }
        activeDebris.Clear();
    }

    private void HandleVictory()
    {
        isRoundTransitioning = true;
        isDefenseActive = false;
        ArmaVR.SetAllCanShoot(false);
        if (weaponController != null) weaponController.SetCanShoot(false);
        ClearAllDebris();

        if (InterfaceVR.Instance != null && scoreManager != null)
        {
            InterfaceVR.Instance.ShowVictory(
                scoreManager.CurrentScore,
                scoreManager.CorrectAnswers,
                scoreManager.WrongAnswers,
                scoreManager.AccuracyPercentage,
                scoreManager.CompletedRounds
            );
        }
        else if (InterfaceVR.Instance != null)
        {
            InterfaceVR.Instance.ShowVictory();
        }
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
        if (IntroducaoDefesaVR.Instance != null)
        {
            IntroducaoDefesaVR.Instance.OnDefenseRestarted();
        }
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

        string targetScene = !string.IsNullOrEmpty(shipSceneName) ? shipSceneName : "SampleScene";
        Debug.Log($"[GerenteDefesa] Retornando para a cena: '{targetScene}'");
        if (TransicaoTela.Instance != null)
        {
            TransicaoTela.Instance.FadeOut(0.30f, () => SceneManager.LoadScene(targetScene));
        }
        else
        {
            SceneManager.LoadScene(targetScene);
        }
    }
}

public class DefenseManager : GerenteDefesa { }
