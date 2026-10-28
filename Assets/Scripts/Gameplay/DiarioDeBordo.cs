using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
#if ENABLE_INPUT_SYSTEM
using InputAction = UnityEngine.InputSystem.InputAction;
using InputActionAsset = UnityEngine.InputSystem.InputActionAsset;
using InputSystem = UnityEngine.InputSystem.InputSystem;
#endif


/// <summary>
/// Controla o diário de bordo com 3 páginas da narrativa inicial.
/// Compatível tanto com cliques via raios de VR (TrackedDeviceGraphicRaycaster + XRSimpleInteractable)
/// quanto com acionamento direto por botões do controle VR (Gatilho, Botão Primário A/X, Stick, Teclado).
/// </summary>
public class DiarioDeBordo : MonoBehaviour
{
    [SerializeField] TMP_Text texto;
    [SerializeField] TMP_Text contador;
    [SerializeField] GameObject botaoVoltar;
    [SerializeField] GameObject botaoAvancar;
    [SerializeField] GameObject botaoExplorar;

    [TextArea(2, 5)]
    public string[] paginas =
    {
        "<size=85%><color=#38BDF8><b>DIÁRIO DE BORDO</b></color></size>\n\n\n\n<size=80%>Você está a bordo de uma nave espacial em uma longa jornada pela galáxia. Sua missão é atravessar regiões desconhecidas do espaço e chegar ao seu próximo destino.</size>",
        "<size=85%><color=#38BDF8><b>DIÁRIO DE BORDO</b></color></size>\n\n\n\n<size=80%>Durante a viagem, a nave foi atacada por piratas espaciais. Embora você tenha conseguido sobreviver, alguns sistemas foram danificados e o funcionamento da embarcação ficou comprometido.</size>",
        "<size=85%><color=#38BDF8><b>DIÁRIO DE BORDO</b></color></size>\n\n\n\n<size=80%><color=#6EE7B7>Após o incidente, você desperta em seu quarto. É necessário verificar o estado da nave antes de continuar a viagem. Explore os compartimentos e procure descobrir quais sistemas precisam ser restaurados.</color></size>"
    };

    public UnityEvent aoTerminarDiario;
    int atual;

    private readonly List<UnityEngine.XR.InputDevice> controllers = new List<UnityEngine.XR.InputDevice>();
    private float cooldownTimer = 0f;
    private bool wasAdvancePressed = false;
    private bool wasBackPressed = false;
#if ENABLE_INPUT_SYSTEM
    private InputAction leftActivateAction;
    private InputAction rightActivateAction;
#endif

    private void Awake()
    {
        ConfigurarCanvasVR();
        ConfigurarBotoesVR();
#if ENABLE_INPUT_SYSTEM
        ResolveActivateActions();
#endif
    }

    private void Start()
    {
        ConfigurarCanvasVR();
        Mostrar(0);
    }

    private void ConfigurarCanvasVR()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                var camObj = GameObject.FindWithTag("MainCamera");
                if (camObj != null) mainCam = camObj.GetComponent<Camera>();
            }
            if (mainCam != null) canvas.worldCamera = mainCam;
        }

        // Garante TrackedDeviceGraphicRaycaster para permitir cliques com os raios dos controles VR
        var oldRaycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (oldRaycaster != null && oldRaycaster.GetType() == typeof(UnityEngine.UI.GraphicRaycaster))
        {
            Destroy(oldRaycaster);
        }

        var trackedRaycaster = GetComponent<TrackedDeviceGraphicRaycaster>();
        if (trackedRaycaster == null)
        {
            gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        // Garante que textos não interceptem raios de clique
        if (texto != null) texto.raycastTarget = false;
        if (contador != null) contador.raycastTarget = false;
    }

    private void ConfigurarBotoesVR()
    {
        ConfigurarBotaoIndividual(botaoAvancar, Avancar);
        ConfigurarBotaoIndividual(botaoVoltar, Voltar);
        ConfigurarBotaoIndividual(botaoExplorar, Explorar);
    }

    private void ConfigurarBotaoIndividual(GameObject btnObj, UnityAction acao)
    {
        if (btnObj == null) return;

        // Garante que a imagem do botão seja alvo de raycast
        var img = btnObj.GetComponent<UnityEngine.UI.Image>();
        if (img != null) img.raycastTarget = true;

        // Assegura BoxCollider no botão para interação física e de raio do XR
        var col = btnObj.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = btnObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var rt = btnObj.GetComponent<RectTransform>();
            if (rt != null) col.size = new Vector3(rt.rect.width, rt.rect.height, 15f);
        }

        // Adiciona XRSimpleInteractable como canal direto de interação XR
        var interactable = btnObj.GetComponent<XRSimpleInteractable>();
        if (interactable == null)
        {
            interactable = btnObj.AddComponent<XRSimpleInteractable>();
        }

        if (interactable != null)
        {
            interactable.selectEntered.AddListener(_ => acao());
            interactable.activated.AddListener(_ => acao());
        }
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        bool advancePressed = false;
        bool backPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (leftActivateAction == null || rightActivateAction == null)
            ResolveActivateActions();

        advancePressed |= leftActivateAction != null && leftActivateAction.IsPressed();
        advancePressed |= rightActivateAction != null && rightActivateAction.IsPressed();
#endif

        // 1. Controles VR via UnityEngine.XR.InputDevices (Oculus/Meta Quest, OpenXR, PCVR)
        controllers.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, controllers);
        for (int i = 0; i < controllers.Count; i++)
        {
            UnityEngine.XR.InputDevice dev = controllers[i];
            if (!dev.isValid) continue;

            // Botão Primário (A no controle direito, X no controle esquerdo)
            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool prim) && prim)
                advancePressed = true;

            // Gatilho (Trigger button ou pressão > 0.5f)
            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trigBtn) && trigBtn)
                advancePressed = true;
            else if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float trigVal) && trigVal > 0.5f)
                advancePressed = true;

            // Botão Secundário (B no controle direito, Y no controle esquerdo)
            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool sec) && sec)
                backPressed = true;

            // Grip (Grip button ou pressão > 0.5f)
            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gripBtn) && gripBtn)
                backPressed = true;
            else if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float gripVal) && gripVal > 0.5f)
                backPressed = true;

            // Direcional / Thumbstick (flick horizontal para avançar ou retroceder)
            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 stick))
            {
                if (stick.x > 0.55f) advancePressed = true;
                else if (stick.x < -0.55f) backPressed = true;
            }
        }

        // 2. Novo Input System (Gamepad e Teclado / XR Simulator)
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                advancePressed = true;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                backPressed = true;
        }

        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad.buttonSouth.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame)
                advancePressed = true;
            if (pad.buttonEast.wasPressedThisFrame || pad.leftTrigger.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame)
                backPressed = true;
        }
#endif

        // 3. Fallback legado (Input Manager) se ativo
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
            advancePressed = true;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
            backPressed = true;
#endif

        // Disparo com debounce e detecção de borda de subida
        if (advancePressed && !wasAdvancePressed && cooldownTimer <= 0f)
        {
            cooldownTimer = 0.28f;
            if (atual < paginas.Length - 1)
            {
                Avancar();
            }
            else
            {
                Explorar();
            }
        }
        else if (backPressed && !wasBackPressed && cooldownTimer <= 0f)
        {
            cooldownTimer = 0.28f;
            if (atual > 0)
            {
                Voltar();
            }
        }

        wasAdvancePressed = advancePressed;
        wasBackPressed = backPressed;
    }

#if ENABLE_INPUT_SYSTEM
    private void ResolveActivateActions()
    {
        leftActivateAction = FindActivateAction("XRI Left Interaction", "XRI LeftHand Interaction");
        rightActivateAction = FindActivateAction("XRI Right Interaction", "XRI RightHand Interaction");
    }

    private InputAction FindActivateAction(string mapName, string fallbackMapName)
    {
        InputAction action = InputSystem.actions?.FindActionMap(mapName)?.FindAction("Activate");
        if (action == null)
            action = InputSystem.actions?.FindActionMap(fallbackMapName)?.FindAction("Activate");

        if (action == null)
        {
            InputActionAsset[] assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            foreach (InputActionAsset asset in assets)
            {
                action = asset.FindActionMap(mapName)?.FindAction("Activate")
                    ?? asset.FindActionMap(fallbackMapName)?.FindAction("Activate");
                if (action != null) break;
            }
        }

        if (action != null && !action.enabled)
            action.Enable();

        return action;
    }
#endif

    public void Avancar() => Mostrar(atual + 1);
    public void Voltar() => Mostrar(atual - 1);

    public void Explorar()
    {
        aoTerminarDiario.Invoke();
        gameObject.SetActive(false);
    }

    // Métodos auxiliares para UnityEvents do XR (SelectEnterEventArgs)
    public void AvancarXR(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args = null) => Avancar();
    public void VoltarXR(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args = null) => Voltar();
    public void ExplorarXR(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args = null) => Explorar();

    void Mostrar(int i)
    {
        atual = Mathf.Clamp(i, 0, paginas.Length - 1);
        if (texto) texto.text = paginas[atual];
        if (contador) contador.text = $"[{atual + 1}/{paginas.Length}]";
        bool ultima = atual == paginas.Length - 1;
        if (botaoVoltar) botaoVoltar.SetActive(atual > 0);
        if (botaoAvancar) botaoAvancar.SetActive(!ultima);
        if (botaoExplorar) botaoExplorar.SetActive(ultima);
        if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
    }
}
