using System;
using System.Collections;
using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// Gerencia a posição, transição e configuração do jogador no espaço VR.
/// Localiza o XROrigin e realiza o teleporte confortável entre a nave e o posto de defesa.
/// Toda a locomoção e rotação são controladas exclusivamente pelos controladores VR via XR Interaction Toolkit.
/// </summary>
public class VRPlayerController : MonoBehaviour
{
    public static VRPlayerController Instance { get; private set; }

    [Header("Referências")]
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private float fadeDuration = 0.5f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        if (xrOrigin == null)
        {
            xrOrigin = GetComponent<XROrigin>() ?? FindAnyObjectByType<XROrigin>();
        }

        // Garante que o modo de rastreamento do XR Origin seja baseado no chão (Floor)
        if (xrOrigin != null)
        {
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        }
    }

    /// <summary>
    /// Teleporta o XR Origin mantendo a câmera alinhada com o destino especificado.
    /// Utiliza Fade para conforto em VR.
    /// </summary>
    public void TeleportTo(Transform destination, Action onComplete = null)
    {
        if (destination == null)
        {
            Debug.LogError("[VRPlayerController] Destino nulo!");
            onComplete?.Invoke();
            return;
        }

        if (xrOrigin == null)
        {
            xrOrigin = GetComponent<XROrigin>() ?? FindAnyObjectByType<XROrigin>();
        }

        if (xrOrigin == null)
        {
            Debug.LogError("[VRPlayerController] XROrigin não encontrado na cena!");
            onComplete?.Invoke();
            return;
        }

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeOut(fadeDuration, () =>
            {
                ExecuteTeleport(destination);
                SceneFader.Instance.FadeIn(fadeDuration, () =>
                {
                    onComplete?.Invoke();
                });
            });
        }
        else
        {
            ExecuteTeleport(destination);
            onComplete?.Invoke();
        }
    }

    private void ExecuteTeleport(Transform destination)
    {
        // 1. Alinha a rotação Y
        Quaternion rotDestino = Quaternion.Euler(0f, destination.eulerAngles.y, 0f);
        xrOrigin.transform.rotation = rotDestino;

        // 2. Calcula a compensação de posição da câmera física do headset
        Vector3 camPos = xrOrigin.Camera != null ? xrOrigin.Camera.transform.position : xrOrigin.transform.position;
        Vector3 delta = destination.position - camPos;

        // 3. Move o XR Origin para que a cabeça fique exatamente no ponto do assento/destino
        xrOrigin.transform.position += delta;

        Debug.Log($"[VRPlayerController] Teleporte VR concluído para: {destination.name} em {destination.position}");
    }
}
