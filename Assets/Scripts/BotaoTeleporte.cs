using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

/// <summary>
/// Controla o botão de teleporte para a área de defesa/matemática.
/// Compatível com as chamadas de evento existentes do Push Button no projeto.
/// </summary>
public class TeleportButton : MonoBehaviour
{
    [Tooltip("Nome da cena de destino")]
    public string cenaDestino = "DefenseScene";
    public Transform destino;

    public void Teleportar()
    {
        Debug.Log("[TeleportButton] Acionando transição para a DefenseScene...");

        // 1. Toca som de ativação do painel
        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayClick();
            SoundEffectsManager.Instance.StopAlarm();
        }

        // 2. Para o alarme da nave
        if (AlarmManager.Instance != null)
        {
            AlarmManager.Instance.StopAlarm();
        }

        // 3. Transição direta para a cena DefenseScene
        if (!string.IsNullOrEmpty(cenaDestino))
        {
            if (SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeOut(0.4f, () =>
                {
                    SceneManager.LoadScene(cenaDestino);
                });
            }
            else
            {
                SceneManager.LoadScene(cenaDestino);
            }
            return;
        }

        // Fallback de teleporte local se não houver cena configurada
        XROrigin xrOrigin = FindAnyObjectByType<XROrigin>();
        if (xrOrigin == null || destino == null)
        {
            Debug.LogError("[TeleportButton] XR Origin ou destino não encontrado!");
            return;
        }

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeOut(0.4f, () =>
            {
                ExecutarMovimento(xrOrigin);
                SceneFader.Instance.FadeIn(0.4f, () =>
                {
                    AtivarFaseDefesa();
                });
            });
        }
        else
        {
            ExecutarMovimento(xrOrigin);
            AtivarFaseDefesa();
        }
    }

    private void ExecutarMovimento(XROrigin xrOrigin)
    {
        // Rotaciona apenas no eixo Y
        Quaternion rotacaoDestino = Quaternion.Euler(
            0f,
            destino.eulerAngles.y,
            0f
        );

        xrOrigin.transform.rotation = rotacaoDestino;

        // Obtém a posição atual da câmera
        Vector3 posicaoCamera = xrOrigin.Camera != null ? xrOrigin.Camera.transform.position : xrOrigin.transform.position;

        // Calcula a diferença entre o destino e a câmera
        Vector3 deslocamento = destino.position - posicaoCamera;

        // Move o XR Origin para corrigir essa diferença
        xrOrigin.transform.position += deslocamento;
    }

    private void AtivarFaseDefesa()
    {
        if (GerenteMatematica.Instance != null)
        {
            GerenteMatematica.Instance.CriarNovaConta();
        }
        if (DefenseManager.Instance != null)
        {
            DefenseManager.Instance.StartDefense();
        }
    }
}
