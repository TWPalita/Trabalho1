using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Componente anexado ao Painel de Comando da espaçonave.
/// Aciona a transição para o Sistema de Defesa quando o jogador interage.
/// </summary>
public class DefenseConsoleTrigger : MonoBehaviour
{
    [Header("Configurações")]
    [SerializeField] private string defenseSceneName = "DefenseScene";
    [SerializeField] private float fadeDuration = 0.8f;
    [SerializeField] private Light panelHighlightLight;

    public bool CanInteract { get; private set; } = true;

    private void Start()
    {
        if (panelHighlightLight != null)
        {
            panelHighlightLight.enabled = true;
        }
    }

    public void Interact()
    {
        if (!CanInteract) return;
        CanInteract = false;

        Debug.Log("[DefenseConsole] Sistema de defesa ativado pelo jogador!");

        // 1. Toca som de ativação
        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayClick();
            SoundEffectsManager.Instance.StopAlarm();
        }

        // 2. Desativa avisos de alarme na UI
        if (GameUI.Instance != null)
        {
            GameUI.Instance.HideAlarmAlert();
            GameUI.Instance.HideInteractionPrompt();
        }

        // 3. Transição de tela escurecendo (Fade Out)
        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeOut(fadeDuration, () =>
            {
                LoadDefense();
            });
        }
        else
        {
            LoadDefense();
        }
    }

    private void LoadDefense()
    {
        // Se a cena estiver configurada no Build Settings, carrega a cena de defesa
        if (Application.CanStreamedLevelBeLoaded(defenseSceneName))
        {
            SceneManager.LoadScene(defenseSceneName);
        }
        else
        {
            Debug.LogWarning($"[DefenseConsole] Cena '{defenseSceneName}' não encontrada no Build Settings. Carregando pelo nome diretamente.");
            SceneManager.LoadScene(defenseSceneName);
        }
    }
}
