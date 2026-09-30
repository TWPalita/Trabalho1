using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Botão físico 3D interativo no painel de comando para VR.
/// Responde ao apontar o controlador VR (hover) e ao pressionar o gatilho/poke (select).
/// Ao ser acionado, ativa o sistema de defesa e transporta o jogador para o posto de combate.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class VRDefenseButton : MonoBehaviour
{
    [Header("Componentes Visuais")]
    [SerializeField] private Transform movingButtonMesh;
    [SerializeField] private Renderer buttonRenderer;
    [SerializeField] private Color normalColor = new Color(0.9f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color hoverColor = new Color(0.2f, 1f, 0.8f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.1f, 0.8f, 0.2f, 1f);

    [Header("Animação de Pressionamento")]
    [SerializeField] private Vector3 pressDirection = new Vector3(0f, -0.025f, 0f);
    [SerializeField] private float pressDuration = 0.15f;

    [Header("Destino do Transporte")]
    [SerializeField] private Transform defenseSeatDestination;
    [SerializeField] private string defenseSceneName = "DefenseScene";

    private XRSimpleInteractable interactable;
    private Material buttonMaterial;
    private Vector3 initialLocalPos;
    private bool isPressed = false;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        if (movingButtonMesh == null)
        {
            movingButtonMesh = transform;
        }

        initialLocalPos = movingButtonMesh.localPosition;

        if (buttonRenderer == null)
        {
            buttonRenderer = movingButtonMesh.GetComponentInChildren<Renderer>();
            if (buttonRenderer == null)
            {
                buttonRenderer = GetComponentInChildren<Renderer>();
            }
        }

        if (buttonRenderer != null)
        {
            buttonMaterial = buttonRenderer.material;
            SetButtonColor(normalColor);
        }
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.firstHoverEntered.AddListener(OnHoverEntered);
            interactable.lastHoverExited.AddListener(OnHoverExited);
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.activated.AddListener(OnActivated);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.firstHoverEntered.RemoveListener(OnHoverEntered);
            interactable.lastHoverExited.RemoveListener(OnHoverExited);
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.activated.RemoveListener(OnActivated);
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (isPressed) return;
        SetButtonColor(hoverColor);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (isPressed) return;
        SetButtonColor(normalColor);
    }

    public void SetHoverState(bool hover)
    {
        if (isPressed) return;
        SetButtonColor(hover ? hoverColor : normalColor);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Aciona quando a mão ou controlador VR toca fisicamente no botão
        PressButton();
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        PressButton();
    }

    private void OnActivated(ActivateEventArgs args)
    {
        PressButton();
    }

    public void PressButton()
    {
        if (isPressed) return;
        isPressed = true;

        Debug.Log("[VRDefenseButton] Botão do painel pressionado pelo controlador VR!");

        SetButtonColor(pressedColor);

        // 1. Toca som de clique físico
        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayClick();
            SoundEffectsManager.Instance.StopAlarm();
        }

        // 2. Animação de pressionamento físico
        StartCoroutine(ButtonPressRoutine());
    }

    private IEnumerator ButtonPressRoutine()
    {
        Vector3 targetPos = initialLocalPos + pressDirection;
        float elapsed = 0f;

        while (elapsed < pressDuration)
        {
            elapsed += Time.deltaTime;
            movingButtonMesh.localPosition = Vector3.Lerp(initialLocalPos, targetPos, elapsed / pressDuration);
            yield return null;
        }

        movingButtonMesh.localPosition = targetPos;

        yield return new WaitForSeconds(0.15f);

        // 3. Transporta o jogador VR para a cabine de defesa e inicia o jogo de matemática
        if (VRPlayerController.Instance != null && defenseSeatDestination != null)
        {
            VRPlayerController.Instance.TeleportTo(defenseSeatDestination, () =>
            {
                if (DefenseManager.Instance != null)
                {
                    DefenseManager.Instance.StartDefense();
                }
            });
        }
        else if (DefenseManager.Instance != null)
        {
            DefenseManager.Instance.StartDefense();
        }
        else if (!string.IsNullOrEmpty(defenseSceneName) && UnityEngine.Application.CanStreamedLevelBeLoaded(defenseSceneName))
        {
            if (SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeOut(0.5f, () =>
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(defenseSceneName);
                });
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(defenseSceneName);
            }
        }

        // Retorna botão suavemente
        elapsed = 0f;
        while (elapsed < pressDuration)
        {
            elapsed += Time.deltaTime;
            movingButtonMesh.localPosition = Vector3.Lerp(targetPos, initialLocalPos, elapsed / pressDuration);
            yield return null;
        }

        movingButtonMesh.localPosition = initialLocalPos;
        isPressed = false;
        SetButtonColor(normalColor);
    }

    private void SetButtonColor(Color color)
    {
        if (buttonMaterial != null)
        {
            buttonMaterial.color = color;
            if (buttonMaterial.HasProperty("_EmissionColor"))
            {
                buttonMaterial.SetColor("_EmissionColor", color * 0.7f);
                buttonMaterial.EnableKeyword("_EMISSION");
            }
        }
    }
}
