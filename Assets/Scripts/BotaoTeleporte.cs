using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

/// <summary>
/// Controla o botão de teleporte para a área de defesa/matemática.
/// Compatível com as chamadas de evento existentes do Push Button no projeto.
/// </summary>
public class BotaoTeleporte : MonoBehaviour
{
    [Tooltip("Nome da cena de destino")]
    public string cenaDestino = "DefenseScene";
    public Transform destino;

    public void Teleportar()
    {
        Debug.Log("[BotaoTeleporte] Acionando transição para a DefenseScene...");

        // 1. Toca som de ativação do painel
        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayClick();
            GerenteSom.Instance.StopAlarm();
        }

        // 2. Para o alarme da nave
        if (GerenteAlarme.Instance != null)
        {
            GerenteAlarme.Instance.StopAlarm();
        }

        // 3. Transição direta para a cena DefenseScene
        if (!string.IsNullOrEmpty(cenaDestino))
        {
            if (TransicaoTela.Instance != null)
            {
                TransicaoTela.Instance.FadeOut(0.4f, () =>
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
            Debug.LogError("[BotaoTeleporte] XR Origin ou destino não encontrado!");
            return;
        }

        if (TransicaoTela.Instance != null)
        {
            TransicaoTela.Instance.FadeOut(0.4f, () =>
            {
                ExecutarMovimento(xrOrigin);
                TransicaoTela.Instance.FadeIn(0.4f, () =>
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
        if (GerenteDefesa.Instance != null)
        {
            GerenteDefesa.Instance.StartDefense();
        }
    }
}

public class TeleportButton : BotaoTeleporte { }

