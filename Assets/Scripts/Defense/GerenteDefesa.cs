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

    [Header("Modo de Jogo")]
    [Tooltip("Se falso, executa o modo base de defesa e tiro ao alvo (sem matemática, sem pontuação punitiva)")]
    [SerializeField] private bool enableMath = true;

    [Header("Configurações das Rodadas")]
    [SerializeField] private float delayBetweenRounds = 1.5f;
    [SerializeField] private Transform shipExplorationSpawn;
    [SerializeField] private string shipSceneName = "SampleScene";
    [SerializeField] private bool autoStartOnAwake = false;

    private List<Detrito> activeDebris = new List<Detrito>();
    private MathProblem currentProblem;
    private int currentRound = 1;
    private bool isRoundTransitioning = false;
    private bool roundResolved = false;
    private bool isDefenseActive = false;

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

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame ||
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReturnToShip();
            }
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
                InterfaceVR.Instance.UpdateRound(currentRound);
            }

            // 3. Spawna os objetos espaciais com as opções de resposta no campo de visão
            SpawnDebris(currentProblem.Options);
        }
        else
        {
            // === MODO BASE DE DEFESA (SEM MATEMÁTICA) ===
            // 5 detritos no espaço em movimento em direção à nave
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
        // Sobe os detritos para ficarem mais altos no campo de visão do para-brisa
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
        GameObject obj = Instantiate(debrisPrefab, spawnPos, spawnPoints[spawnIndex].rotation);
        obj.name = $"Detrito_{spawnIndex + 1}";

        Detrito deb = obj.GetComponent<Detrito>();
        if (deb != null)
        {
            deb.Init(0, this);
            deb.SetShowNumberLabel(false);
            activeDebris.Add(deb);
        }

        // Garante que o DebrisMovement esteja presente e inicializado no ponto de spawn
        DebrisMovement mov = obj.GetComponent<DebrisMovement>();
        if (mov == null) mov = obj.AddComponent<DebrisMovement>();
        mov.SetSpawnPoint(spawnPos, spawnIndex);
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
            GameObject obj = Instantiate(debrisPrefab, spawnPos, spawnPoints[i].rotation);

            Detrito deb = obj.GetComponent<Detrito>();
            if (deb != null)
            {
                deb.Init(options[i], this);
                deb.SetShowNumberLabel(true);
                activeDebris.Add(deb);
            }

            DebrisMovement mov = obj.GetComponent<DebrisMovement>();
            if (mov == null) mov = obj.AddComponent<DebrisMovement>();
            mov.SetSpawnPoint(spawnPos, i);
        }
    }

    public void OnDebrisHit(Detrito hitDebris)
    {
        if (hitDebris == null || !isDefenseActive || hitDebris.IsHit) return;

        if (!enableMath)
        {
            // === MODO BASE: IMPACTO E DESTRUIÇÃO/FEEDBACK IMEDIATO ===
            hitDebris.MarkHit();

            // 1. Áudio de impacto
            if (GerenteSom.Instance != null)
            {
                GerenteSom.Instance.PlayHit();
            }

            // 2. Vibração tátil no controlador VR
            ArmaVR.TriggerAllHaptics(0.8f, 0.18f);

            // 3. Feedback visual no HUD do Cockpit
            if (InterfaceVR.Instance != null)
            {
                InterfaceVR.Instance.ShowFeedback(true, "DETRITO ELIMINADO!");
            }

            // 4. Identifica ponto de spawn original mais próximo para respawn
            int spIndex = FindClosestSpawnIndex(hitDebris.transform.position);

            activeDebris.Remove(hitDebris);
            hitDebris.Explode(true);

            // 5. Respawn automático após intervalo para manter continuamente alvos no espaço
            StartCoroutine(RespawnSingleDebrisRoutine(spIndex, 1.8f));
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
            // === RESPOSTA INCORRETA ===
            // Subtrai energia/vida e pontos. NÃO explode o detrito, NÃO avança a rodada.
            ArmaVR.TriggerAllHaptics(0.6f, 0.25f);

            if (scoreManager != null) scoreManager.AddWrongHit();

            // Se a perda de vida causou Game Over, o painel de fim de jogo já foi acionado e os detritos limpos
            if (scoreManager != null && scoreManager.IsGameOver)
            {
                return;
            }

            if (GerenteSom.Instance != null) GerenteSom.Instance.PlayError();

            if (InterfaceVR.Instance != null)
            {
                int currentEnergy = scoreManager != null ? scoreManager.CurrentEnergy : 0;
                InterfaceVR.Instance.ShowFeedback(false, $"RESPOSTA INCORRETA!\n-20% ENERGIA ({currentEnergy}%)");
            }

            // Feedback visual no próprio detrito (flash vermelho temporário)
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

        string targetScene = !string.IsNullOrEmpty(shipSceneName) ? shipSceneName : "SampleScene";
        Debug.Log($"[GerenteDefesa] Retornando imediatamente para a cena: '{targetScene}'");
        SceneManager.LoadScene(targetScene);
    }
}

public class DefenseManager : GerenteDefesa { }

