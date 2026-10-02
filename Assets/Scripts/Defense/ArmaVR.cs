using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Controla o disparo de laser em Realidade Virtual no Sistema de Defesa Espacial.
/// Suporta controladores esquerdo e direito (Dual-Wielding) com disparo acionado por
/// gatilho (Trigger), pegada (Grip) ou qualquer botão de ação dos controladores VR.
/// Implementa redundância de entrada em múltiplas camadas (XRI Action-Based, Interactor,
/// Novo Input System direto e fallback OpenXR/Legacy), mira por raio do interactor e
/// assistência de pontaria por SphereCast generoso.
/// </summary>
public class ArmaVR : MonoBehaviour
{
    public static ArmaVR Instance { get; private set; }
    public static List<ArmaVR> ActiveControllers { get; } = new List<ArmaVR>();

    [Header("Mão do Controlador")]
    [SerializeField] private UnityEngine.XR.XRNode controllerNode = UnityEngine.XR.XRNode.RightHand;

    [Header("Configuração de Disparo")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private float maxDistance = 150f;
    [SerializeField] private float fireRate = 0.25f;
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Input Actions (XRI)")]
    [SerializeField] private InputActionProperty triggerAction;
    [SerializeField] private InputActionProperty selectAction;

    [Header("Efeito Visual do Laser")]
    [SerializeField] private Color laserColor = new Color(0.2f, 1f, 0.4f, 1f);
    [SerializeField] private float laserBeamDuration = 0.12f;

    private float nextFireTime = 0f;
    private bool canShoot = true;
    private LineRenderer pooledLine;
    private Material laserMat;
    private Coroutine beamRoutine;

    private XRBaseInputInteractor cachedInteractor;
    private IXRRayProvider cachedRayProvider;

    public UnityEngine.XR.XRNode ControllerNode => controllerNode;
    public bool CanShoot => canShoot;

    private void Awake()
    {
        if (Instance == null && controllerNode == UnityEngine.XR.XRNode.RightHand)
        {
            Instance = this;
        }
        if (!ActiveControllers.Contains(this))
        {
            ActiveControllers.Add(this);
        }

        cachedInteractor = GetComponentInChildren<XRBaseInputInteractor>();
        cachedRayProvider = GetComponentInChildren<IXRRayProvider>();

        SetupMuzzlePoint();
        SetupLaserRenderer();
    }

    private void Start()
    {
        // Revalida interactor e ray provider caso tenham sido inicializados depois
        if (cachedInteractor == null) cachedInteractor = GetComponentInChildren<XRBaseInputInteractor>();
        if (cachedRayProvider == null) cachedRayProvider = GetComponentInChildren<IXRRayProvider>();

        SetupMuzzlePoint();
        ResolveInputActions();
    }

    private void SetupMuzzlePoint()
    {
        if (muzzlePoint == null && cachedRayProvider != null)
        {
            muzzlePoint = cachedRayProvider.GetOrCreateRayOrigin();
        }

        if (muzzlePoint == null)
        {
            Transform[] children = GetComponentsInChildren<Transform>();
            foreach (var ch in children)
            {
                string n = ch.name.ToLower();
                if (n.Contains("attach") || n.Contains("origin") || n.Contains("ray") || n.Contains("muzzle"))
                {
                    muzzlePoint = ch;
                    break;
                }
            }
        }

        if (muzzlePoint == null)
        {
            muzzlePoint = transform;
        }
    }

    private void SetupLaserRenderer()
    {
        GameObject beamObj = new GameObject("VRLaserBeam_Pooled");
        beamObj.transform.SetParent(transform, false);
        pooledLine = beamObj.AddComponent<LineRenderer>();
        pooledLine.startWidth = 0.045f;
        pooledLine.endWidth = 0.045f;
        pooledLine.positionCount = 2;
        pooledLine.useWorldSpace = true;
        pooledLine.numCapVertices = 4;
        pooledLine.numCornerVertices = 4;
        pooledLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pooledLine.receiveShadows = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        laserMat = new Material(shader);
        if (laserMat.HasProperty("_BaseColor")) laserMat.SetColor("_BaseColor", laserColor);
        if (laserMat.HasProperty("_Color")) laserMat.SetColor("_Color", laserColor);
        laserMat.color = laserColor;

        pooledLine.material = laserMat;
        pooledLine.enabled = false;
    }

    private void ResolveInputActions()
    {
        bool isRight = (controllerNode == UnityEngine.XR.XRNode.RightHand);
        string mapName = isRight ? "XRI Right Interaction" : "XRI Left Interaction";

        if (triggerAction.action == null)
        {
            var action = InputSystem.actions?.FindActionMap(mapName)?.FindAction("Activate");
            if (action == null)
            {
                var assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
                foreach (var asset in assets)
                {
                    var map = asset.FindActionMap(mapName) 
                              ?? asset.FindActionMap(isRight ? "XRI RightHand Interaction" : "XRI LeftHand Interaction");
                    if (map != null)
                    {
                        action = map.FindAction("Activate");
                        if (action != null) break;
                    }
                }
            }

            if (action != null)
            {
                triggerAction = new InputActionProperty(action);
            }
        }

        if (selectAction.action == null)
        {
            var action = InputSystem.actions?.FindActionMap(mapName)?.FindAction("Select");
            if (action == null)
            {
                var assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
                foreach (var asset in assets)
                {
                    var map = asset.FindActionMap(mapName) 
                              ?? asset.FindActionMap(isRight ? "XRI RightHand Interaction" : "XRI LeftHand Interaction");
                    if (map != null)
                    {
                        action = map.FindAction("Select");
                        if (action != null) break;
                    }
                }
            }

            if (action != null)
            {
                selectAction = new InputActionProperty(action);
            }
        }

        EnableActions();
    }

    private void EnableActions()
    {
        if (triggerAction.action != null && !triggerAction.action.enabled)
        {
            triggerAction.action.Enable();
        }
        if (selectAction.action != null && !selectAction.action.enabled)
        {
            selectAction.action.Enable();
        }
    }

    private void DisableActions()
    {
        if (triggerAction.action != null && triggerAction.action.enabled)
        {
            triggerAction.action.Disable();
        }
        if (selectAction.action != null && selectAction.action.enabled)
        {
            selectAction.action.Disable();
        }
    }

    private void OnEnable()
    {
        EnableActions();
    }

    private void OnDisable()
    {
        DisableActions();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ActiveControllers.Remove(this);
        if (laserMat != null) Destroy(laserMat);
    }

    public static void SetAllCanShoot(bool enable)
    {
        foreach (var c in ActiveControllers)
        {
            if (c != null) c.SetCanShoot(enable);
        }
    }

    public static void TriggerAllHaptics(float amplitude, float duration)
    {
        for (int i = 0; i < ActiveControllers.Count; i++)
        {
            if (ActiveControllers[i] != null)
            {
                ActiveControllers[i].TriggerHaptics(amplitude, duration);
            }
        }
    }

    public void SetCanShoot(bool enable)
    {
        canShoot = enable;
    }

    private void Update()
    {
        if (!canShoot || Time.time < nextFireTime) return;

        if (CheckFireInput())
        {
            Fire();
        }
    }

    /// <summary>
    /// Avalia múltiplos canais de entrada dos controladores VR para detectar disparo imediato.
    /// Funciona nativamente com Trigger, Grip e botões de ação em todos os headsets VR e no Simulador.
    /// </summary>
    private bool CheckFireInput()
    {
        // 1. Checa as Input Actions oficiais configuradas (Activate / Trigger e Select / Grip)
        if (triggerAction.action != null)
        {
            if (triggerAction.action.WasPressedThisFrame() || triggerAction.action.ReadValue<float>() > 0.45f)
            {
                return true;
            }
        }

        if (selectAction.action != null)
        {
            if (selectAction.action.WasPressedThisFrame() || selectAction.action.ReadValue<float>() > 0.45f)
            {
                return true;
            }
        }

        // 2. Checa diretamente o XRBaseInputInteractor (NearFarInteractor ou RayInteractor)
        if (cachedInteractor != null)
        {
            if (cachedInteractor.activateInput != null)
            {
                if (cachedInteractor.activateInput.ReadWasPerformedThisFrame() || 
                    cachedInteractor.activateInput.ReadIsPerformed() || 
                    cachedInteractor.activateInput.ReadValue() > 0.45f)
                {
                    return true;
                }
            }

            if (cachedInteractor.selectInput != null)
            {
                if (cachedInteractor.selectInput.ReadWasPerformedThisFrame() || 
                    cachedInteractor.selectInput.ReadIsPerformed() || 
                    cachedInteractor.selectInput.ReadValue() > 0.45f)
                {
                    return true;
                }
            }
        }

        // 3. Checa diretamente dispositivos do Novo Input System (InputSystem.devices)
        bool isRightHand = (controllerNode == UnityEngine.XR.XRNode.RightHand);
        foreach (var device in InputSystem.devices)
        {
            if (!device.added) continue;

            if (!DeviceMatchesHand(device, isRightHand)) continue;

            if (IsControlActive(device, "trigger") ||
                IsControlActive(device, "triggerButton") ||
                IsControlActive(device, "activate") ||
                IsControlActive(device, "grip") ||
                IsControlActive(device, "gripButton") ||
                IsControlActive(device, "select") ||
                IsControlActive(device, "primaryButton") ||
                IsControlActive(device, "secondaryButton"))
            {
                return true;
            }
        }

        // 4. Fallback legado para dispositivos UnityEngine.XR
        var xrDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(controllerNode);
        if (xrDevice.isValid)
        {
            if (xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool tb) && tb) return true;
            if (xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float tv) && tv > 0.45f) return true;
            if (xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gb) && gb) return true;
            if (xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float gv) && gv > 0.45f) return true;
            if (xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pb) && pb) return true;
            if (xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool sb) && sb) return true;
        }

#if UNITY_EDITOR
        // Suporte para testes rápidos no Unity Editor através do XR Device Simulator / cliques do mouse
        if (isRightHand && Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasPressedThisFrame))
        {
            return true;
        }
        else if (!isRightHand && Mouse.current != null && (Mouse.current.rightButton.isPressed || Mouse.current.rightButton.wasPressedThisFrame))
        {
            return true;
        }
#endif

        return false;
    }

    private bool DeviceMatchesHand(InputDevice device, bool isRightHand)
    {
        string targetUsage = isRightHand ? "RightHand" : "LeftHand";
        string targetName = isRightHand ? "right" : "left";

        foreach (var usage in device.usages)
        {
            if (usage.ToString().Equals(targetUsage, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (device.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    private bool IsControlActive(InputDevice device, string controlName)
    {
        var control = device.GetChildControl(controlName);
        if (control == null) return false;

        if (control is ButtonControl btn)
        {
            return btn.isPressed || btn.wasPressedThisFrame;
        }
        if (control is AxisControl axis)
        {
            return axis.ReadValue() > 0.45f;
        }
        return false;
    }

    /// <summary>
    /// Executa o disparo do laser, gera áudio, raio luminoso, vibração tátil (haptics)
    /// e valida impacto com alvos no espaço 3D.
    /// </summary>
    public void Fire()
    {
        nextFireTime = Time.time + fireRate;

        // 1. Efeito sonoro do disparo
        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayLaser();
        }

        // 2. Origem e direção do disparo alinhados ao raio do controlador VR
        Vector3 origin;
        Vector3 forward;

        if (cachedRayProvider != null)
        {
            Transform rayOrigin = cachedRayProvider.GetOrCreateRayOrigin();
            origin = rayOrigin != null ? rayOrigin.position : transform.position;
            forward = rayOrigin != null ? rayOrigin.forward : transform.forward;
        }
        else if (muzzlePoint != null)
        {
            origin = muzzlePoint.position;
            forward = muzzlePoint.forward;
        }
        else
        {
            origin = transform.position;
            forward = transform.forward;
        }

        // 3. Detecção de Colisão: Raycast direto + SphereCast generoso (Aim Assist para VR)
        Vector3 targetPoint = origin + forward * maxDistance;
        GameObject hitObject = null;

        RaycastHit hit;
        bool hasHit = Physics.Raycast(origin, forward, out hit, maxDistance, hitLayers, QueryTriggerInteraction.Collide);

        if (!hasHit)
        {
            // Aim assist generoso com esfera de raio 0.45m para conforto em mira VR à distância
            hasHit = Physics.SphereCast(origin, 0.45f, forward, out hit, maxDistance, hitLayers, QueryTriggerInteraction.Collide);
        }

        if (hasHit)
        {
            targetPoint = hit.point;
            hitObject = hit.collider.gameObject;
        }

        // 4. Renderiza o raio laser luminoso no espaço 3D
        if (beamRoutine != null) StopCoroutine(beamRoutine);
        beamRoutine = StartCoroutine(LaserBeamRoutine(origin, targetPoint));

        // 5. Vibração tátil no controlador VR (Haptics)
        TriggerHaptics(0.65f, 0.12f);

        // 6. Avalia acerto com o alvo espacial
        if (hitObject != null && GerenteDefesa.Instance != null)
        {
            Detrito detrito = hitObject.GetComponentInParent<Detrito>();
            if (detrito != null)
            {
                GerenteDefesa.Instance.OnDebrisHit(detrito);
                return;
            }
        }
    }

    private void TriggerHaptics(float amplitude, float duration)
    {
        if (cachedInteractor != null)
        {
            cachedInteractor.SendHapticImpulse(amplitude, duration);
        }

        var xrDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(controllerNode);
        if (xrDevice.isValid)
        {
            xrDevice.SendHapticImpulse(0u, amplitude, duration);
        }
    }

    private IEnumerator LaserBeamRoutine(Vector3 initialStart, Vector3 end)
    {
        if (pooledLine == null) yield break;

        pooledLine.SetPosition(0, initialStart);
        pooledLine.SetPosition(1, end);
        pooledLine.enabled = true;

        float elapsed = 0f;
        while (elapsed < laserBeamDuration)
        {
            elapsed += Time.deltaTime;
            Vector3 currentOrigin = (muzzlePoint != null) ? muzzlePoint.position : transform.position;
            pooledLine.SetPosition(0, currentOrigin);
            yield return null;
        }

        pooledLine.enabled = false;
    }
}

public class VRWeaponController : ArmaVR { }
