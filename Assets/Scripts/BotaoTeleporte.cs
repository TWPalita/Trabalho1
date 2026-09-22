using UnityEngine;
using Unity.XR.CoreUtils;

public class TeleportButton : MonoBehaviour
{
    public Transform destino;

    public void Teleportar()
    {
        XROrigin xrOrigin = FindFirstObjectByType<XROrigin>();

        if (xrOrigin == null || destino == null)
        {
            Debug.LogError("XR Origin ou destino não encontrado!");
            return;
        }

        // Rotaciona apenas no eixo Y
        Quaternion rotacaoDestino = Quaternion.Euler(
            0f,
            destino.eulerAngles.y,
            0f
        );

        xrOrigin.transform.rotation = rotacaoDestino;

        // Obtém a posição atual da câmera
        Vector3 posicaoCamera = xrOrigin.Camera.transform.position;

        // Calcula a diferença entre o destino e a câmera
        Vector3 deslocamento = destino.position - posicaoCamera;

        // Move o XR Origin para corrigir essa diferença
        xrOrigin.transform.position += deslocamento;
    }
}
