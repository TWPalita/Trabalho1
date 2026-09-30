using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Sistema de detecção e interação do jogador com objetos no cenário (como o Painel de Comando).
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Configuração de Interação")]
    [SerializeField] private float maxDistance = 3.5f;
    [SerializeField] private LayerMask interactableLayer = ~0;
    [SerializeField] private Transform cameraTransform;

    private DefenseConsoleTrigger currentTrigger;

    private void Awake()
    {
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }
    }

    private void Update()
    {
        CheckInteractionRaycast();
        CheckInput();
    }

    private void CheckInteractionRaycast()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        DefenseConsoleTrigger detectedTrigger = null;

        // 1. Tenta por Raycast na direção da mira
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableLayer, QueryTriggerInteraction.Collide))
        {
            detectedTrigger = hit.collider.GetComponentInParent<DefenseConsoleTrigger>();
        }

        // 2. Fallback de proximidade esférica caso o jogador esteja bem perto
        if (detectedTrigger == null)
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, 2.0f, interactableLayer, QueryTriggerInteraction.Collide);
            foreach (var col in colliders)
            {
                var trigger = col.GetComponentInParent<DefenseConsoleTrigger>();
                if (trigger != null)
                {
                    detectedTrigger = trigger;
                    break;
                }
            }
        }

        if (detectedTrigger != currentTrigger)
        {
            currentTrigger = detectedTrigger;
            UpdateUIPrompt();
        }
    }

    private void UpdateUIPrompt()
    {
        if (GameUI.Instance != null)
        {
            if (currentTrigger != null && currentTrigger.CanInteract)
            {
                GameUI.Instance.ShowInteractionPrompt("Pressione E para ativar o sistema de defesa");
            }
            else
            {
                GameUI.Instance.HideInteractionPrompt();
            }
        }
    }

    private void CheckInput()
    {
        if (currentTrigger == null || !currentTrigger.CanInteract) return;

        bool interactPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            interactPressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.E))
        {
            interactPressed = true;
        }
#endif

        if (interactPressed)
        {
            currentTrigger.Interact();
            if (GameUI.Instance != null)
            {
                GameUI.Instance.HideInteractionPrompt();
            }
        }
    }
}
