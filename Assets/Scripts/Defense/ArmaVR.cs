using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Controla o disparo de laser em Realidade Virtual no Sistema de Defesa Espacial.
/// Suporta armas empunhadas pelo jogador (XRGrabInteractable) e controladores VR diretos (Dual-Wielding)
/// com disparo acionado por gatilho (Trigger), pegada (Grip) ou qualquer botão de ação dos controladores.
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

    // Sistema de Mira Laser contínua + Retícula no alvo
    private LineRenderer aimLine;
    private Material aimLineMat;
    private GameObject aimReticleObj;
    private Material aimReticleMat;
    private readonly Color aimIdleColor = new Color(0.22f, 0.85f, 1f, 0.65f);
    private readonly Color aimLockedColor = new Color(0.2f, 1f, 0.45f, 0.95f);

    private XRBaseInputInteractor cachedInteractor;
    private XRBaseInputInteractor holdingInputInteractor;
    private IXRRayProvider cachedRayProvider;
    private XRGrabInteractable cachedGrabInteractable;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor currentHoldingInteractor;

    private Vector3 initialRestPosition;
    private Quaternion initialRestRotation;
    private Vector3 initialRestScale = new Vector3(0.03f, 0.03f, 0.03f);
    private bool isEquippedInHand = false;
    private Transform equippedControllerRoot;
    private float lastGrabTime = -10f;

    public UnityEngine.XR.XRNode ControllerNode => controllerNode;
    public bool CanShoot => canShoot;
    public bool IsGrabbed => isEquippedInHand || currentHoldingInteractor != null;
    public bool IsGrabbableWeapon => cachedGrabInteractable != null || isEquippedInHand;

    /// <summary>
    /// Procura o objeto 'arma' na cena e garante que possua BoxCollider, Rigidbody,
    /// XRGrabInteractable, AttachPoint, PontoDisparo e ArmaVR configurados.
    /// </summary>
    public static ArmaVR EnsureScenePhysicalWeaponConfigured()
    {
        GameObject armaObj = GameObject.Find("arma");
        if (armaObj == null)
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.Equals("arma", System.StringComparison.OrdinalIgnoreCase) ||
                    root.name.IndexOf("arma", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    armaObj = root;
                    break;
                }
            }
        }

        if (armaObj == null) return null;
        return ConfigureWeaponObject(armaObj);
    }

    public static ArmaVR ConfigureWeaponObject(GameObject armaObj)
    {
        if (armaObj == null) return null;

        // Ajusta o tamanho da arma sobre o painel para ficar bem visível a partir da cadeira
        if (armaObj.transform.localScale.x < 0.035f)
        {
            armaObj.transform.localScale = new Vector3(0.036f, 0.036f, 0.036f);
        }

        // 1. BoxCollider ajustado ao modelo 3D da arma (com margem ampla para pegada a partir da cadeira)
        BoxCollider box = armaObj.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = armaObj.AddComponent<BoxCollider>();
        }
        MeshFilter mf = armaObj.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            box.center = b.center;
            box.size = new Vector3(
                Mathf.Max(b.size.x * 2.2f, 5.5f),
                Mathf.Max(b.size.y * 1.8f, 5.5f),
                Mathf.Max(b.size.z * 1.5f, 8.0f)
            );
        }
        else
        {
            box.center = new Vector3(0f, 2.08f, 0.70f);
            box.size = new Vector3(5.5f, 5.5f, 8.0f);
        }
        box.isTrigger = false;

        // 2. Rigidbody (cinemático e sem gravidade enquanto repousa no painel do cockpit)
        Rigidbody rb = armaObj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = armaObj.AddComponent<Rigidbody>();
        }
        rb.mass = 1.5f;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // 3. Ponto de encaixe da mão (AttachPoint no cabo da arma, apontando o cano para +Z)
        Transform attachPoint = armaObj.transform.Find("AttachPoint");
        if (attachPoint == null)
        {
            GameObject attachObj = new GameObject("AttachPoint");
            attachObj.transform.SetParent(armaObj.transform, false);
            attachPoint = attachObj.transform;
        }
        attachPoint.localPosition = new Vector3(0f, 1.35f, -0.85f);
        attachPoint.localRotation = Quaternion.identity;

        // 4. Ponto de disparo do laser na ponta do cano da arma (PontoDisparo)
        Transform muzzle = armaObj.transform.Find("PontoDisparo");
        if (muzzle == null)
        {
            GameObject muzzleObj = new GameObject("PontoDisparo");
            muzzleObj.transform.SetParent(armaObj.transform, false);
            muzzle = muzzleObj.transform;
        }
        muzzle.localPosition = new Vector3(0f, 2.53f, 3.35f);
        muzzle.localRotation = Quaternion.identity;

        // 5. XRGrabInteractable para pegar a arma com a mão no VR
        XRGrabInteractable grab = armaObj.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            grab = armaObj.AddComponent<XRGrabInteractable>();
        }
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.useDynamicAttach = false;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;
        grab.attachTransform = attachPoint;
        grab.farAttachMode = UnityEngine.XR.Interaction.Toolkit.Attachment.InteractableFarAttachMode.Near;
        grab.throwOnDetach = false;
        grab.forceGravityOnDetach = false;
        grab.retainTransformParent = false;
        if (!grab.colliders.Contains(box))
        {
            grab.colliders.Add(box);
        }

        // 6. Componente ArmaVR na própria arma
        ArmaVR armaScript = armaObj.GetComponent<ArmaVR>();
        if (armaScript == null)
        {
            armaScript = armaObj.AddComponent<ArmaVR>();
        }
        armaScript.muzzlePoint = muzzle;
        armaScript.cachedGrabInteractable = grab;

        return armaScript;
    }

    private void Awake()
    {
        if (gameObject.name.Equals("arma", System.StringComparison.OrdinalIgnoreCase) && GetComponent<XRGrabInteractable>() == null)
        {
            ConfigureWeaponObject(gameObject);
        }
        else
        {
            EnsureScenePhysicalWeaponConfigured();
        }

        if (Instance == null && (GetComponent<XRGrabInteractable>() != null || controllerNode == UnityEngine.XR.XRNode.RightHand))
        {
            Instance = this;
        }
        if (GetComponent<XRGrabInteractable>() != null)
        {
            Instance = this;
        }
        if (!ActiveControllers.Contains(this))
        {
            ActiveControllers.Add(this);
        }

        cachedInteractor = GetComponentInChildren<XRBaseInputInteractor>();
        cachedRayProvider = GetComponentInChildren<IXRRayProvider>();
        cachedGrabInteractable = GetComponent<XRGrabInteractable>();
        if (cachedGrabInteractable != null)
        {
            initialRestPosition = transform.position;
            initialRestRotation = transform.rotation;
            initialRestScale = transform.localScale;

            cachedGrabInteractable.selectEntered.RemoveListener(OnGrabSelected);
            cachedGrabInteractable.selectExited.RemoveListener(OnGrabDeselected);
            cachedGrabInteractable.activated.RemoveListener(OnGrabActivated);

            cachedGrabInteractable.selectEntered.AddListener(OnGrabSelected);
            cachedGrabInteractable.selectExited.AddListener(OnGrabDeselected);
            cachedGrabInteractable.activated.AddListener(OnGrabActivated);
        }

        SetupMuzzlePoint();
        SetupLaserRenderer();
        SetupAimSight();
    }

    private void OnGrabSelected(SelectEnterEventArgs args)
    {
        if (args.interactorObject != null)
        {
            EquipToControllerHand(args.interactorObject);
        }
    }

    /// <summary>
    /// Faz a arma assumir o lugar do controle VR na mão que a pegou,
    /// ocultando o modelo visual do controle e fixando a arma diretamente na mão.
    /// </summary>
    private void EquipToControllerHand(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor)
    {
        if (isEquippedInHand || interactor == null) return;

        isEquippedInHand = true;
        currentHoldingInteractor = interactor as UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor;
        lastGrabTime = Time.time;

        // 1. Localiza o Transform raiz do controlador da mão (Right Controller ou Left Controller)
        Transform interactorTr = interactor.transform;
        holdingInputInteractor = interactor as XRBaseInputInteractor;
        if (holdingInputInteractor == null && interactorTr != null)
        {
            holdingInputInteractor = interactorTr.GetComponentInParent<XRBaseInputInteractor>();
        }

        Transform controllerRoot = interactorTr;
        ArmaVR handControllerWeapon = null;
        if (interactorTr != null)
        {
            Transform curr = interactorTr;
            while (curr != null)
            {
                ArmaVR cw = curr.GetComponent<ArmaVR>();
                if (cw != null && cw != this)
                {
                    handControllerWeapon = cw;
                    controllerRoot = curr;
                    break;
                }
                string n = curr.name.ToLower();
                if (n.Contains("right controller") || n.Contains("left controller"))
                {
                    controllerRoot = curr;
                    handControllerWeapon = curr.GetComponent<ArmaVR>();
                    break;
                }
                curr = curr.parent;
            }
        }

        equippedControllerRoot = controllerRoot;

        // 2. Define a mão (LeftHand / RightHand) e sincroniza as InputActions daquele controle
        string fullHierarchyName = (controllerRoot != null ? controllerRoot.name : "") + " " + (interactorTr != null ? interactorTr.name : "");
        fullHierarchyName = fullHierarchyName.ToLower();
        if (handControllerWeapon != null)
        {
            controllerNode = handControllerWeapon.ControllerNode;
            if (handControllerWeapon.triggerAction.action != null) triggerAction = handControllerWeapon.triggerAction;
            if (handControllerWeapon.selectAction.action != null) selectAction = handControllerWeapon.selectAction;
        }
        else
        {
            if (fullHierarchyName.Contains("left")) controllerNode = UnityEngine.XR.XRNode.LeftHand;
            else if (fullHierarchyName.Contains("right")) controllerNode = UnityEngine.XR.XRNode.RightHand;

            foreach (var c in ActiveControllers)
            {
                if (c != null && c != this && !c.IsGrabbableWeapon && c.ControllerNode == controllerNode)
                {
                    if (c.triggerAction.action != null) triggerAction = c.triggerAction;
                    if (c.selectAction.action != null) selectAction = c.selectAction;
                    break;
                }
            }
        }
        ResolveInputActions();

        // 3. Desativa o XRGrabInteractable e o BoxCollider da arma para que ela fique fixa na mão no lugar do controle
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null) col.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (cachedGrabInteractable != null)
        {
            cachedGrabInteractable.enabled = false;
        }

        // 4. Oculta o modelo visual do controle VR daquela mão para a arma assumir o lugar dele
        if (controllerRoot != null)
        {
            HideControllerVisuals(controllerRoot);

            // 5. Posiciona a arma diretamente como filha do controle, encaixando o cabo na mão e apontando para frente
            Vector3 targetScale = initialRestScale.sqrMagnitude > 0.00001f ? initialRestScale : new Vector3(0.03f, 0.03f, 0.03f);
            transform.SetParent(controllerRoot, false);
            transform.localScale = targetScale;
            transform.localRotation = Quaternion.identity;
            transform.localPosition = new Vector3(0f, -1.35f * targetScale.y, 0.85f * targetScale.z);
        }

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayClick();
        }
    }

    private void HideControllerVisuals(Transform controllerRoot)
    {
        if (controllerRoot == null) return;

        // Desativa objetos filhos de visualização do controle (ex: "Right Controller Visual" / "Left Controller Visual")
        for (int i = 0; i < controllerRoot.childCount; i++)
        {
            Transform child = controllerRoot.GetChild(i);
            if (child == transform) continue;

            string childName = child.name.ToLower();
            if (childName.Contains("visual") || childName.Contains("model"))
            {
                child.gameObject.SetActive(false);
            }
        }

        // Oculta também a linha padrão do controle para usar apenas a mira laser do cano da arma
        var lineVisuals = controllerRoot.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>(true);
        foreach (var lv in lineVisuals)
        {
            if (lv != null) lv.enabled = false;
        }

        // Garante que qualquer MeshRenderer / SkinnedMeshRenderer / LineRenderer do controle fique oculto, preservando apenas a arma
        Renderer[] renderers = controllerRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (r.transform.IsChildOf(transform)) continue;

            if (r is MeshRenderer || r is SkinnedMeshRenderer || r is LineRenderer)
            {
                r.enabled = false;
            }
        }
    }

    private void OnGrabDeselected(SelectExitEventArgs args)
    {
        // Se a arma já assumiu o lugar do controle na mão, permanece equipada na mão
        if (isEquippedInHand && equippedControllerRoot != null)
        {
            Vector3 targetScale = initialRestScale.sqrMagnitude > 0.00001f ? initialRestScale : new Vector3(0.03f, 0.03f, 0.03f);
            if (transform.parent != equippedControllerRoot)
            {
                transform.SetParent(equippedControllerRoot, false);
            }
            transform.localScale = targetScale;
            transform.localRotation = Quaternion.identity;
            transform.localPosition = new Vector3(0f, -1.35f * targetScale.y, 0.85f * targetScale.z);
            return;
        }

        currentHoldingInteractor = null;
        holdingInputInteractor = null;
    }

    private void OnGrabActivated(ActivateEventArgs args)
    {
        if (!isEquippedInHand && args.interactorObject != null)
        {
            EquipToControllerHand(args.interactorObject);
            return;
        }

        if (canShoot && Time.time >= nextFireTime)
        {
            Fire();
        }
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
        if (muzzlePoint != null) return;

        Transform customMuzzle = transform.Find("PontoDisparo");
        if (customMuzzle != null)
        {
            muzzlePoint = customMuzzle;
            return;
        }

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
                if (n.Contains("ponto") || n.Contains("muzzle") || n.Contains("origin") || n.Contains("ray"))
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

    /// <summary>
    /// Configura a mira laser contínua (feixe fino + ponto luminoso de retícula) saindo do cano da arma.
    /// </summary>
    private void SetupAimSight()
    {
        if (!IsGrabbableWeapon && !gameObject.name.Equals("arma", System.StringComparison.OrdinalIgnoreCase))
            return;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");

        // 1. Linha fina da mira laser
        GameObject aimObj = new GameObject("MiraLaser_Line");
        aimObj.transform.SetParent(transform, false);
        aimLine = aimObj.AddComponent<LineRenderer>();
        aimLine.startWidth = 0.008f;
        aimLine.endWidth = 0.003f;
        aimLine.positionCount = 2;
        aimLine.useWorldSpace = true;
        aimLine.numCapVertices = 4;
        aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        aimLine.receiveShadows = false;

        aimLineMat = new Material(shader);
        SetMaterialColor(aimLineMat, aimIdleColor);
        aimLine.material = aimLineMat;
        aimLine.enabled = false;

        // 2. Ponto/Retícula 3D onde a mira está apontando
        aimReticleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        aimReticleObj.name = "PontoMira_Reticle";
        aimReticleObj.transform.SetParent(transform, false);
        Collider retCol = aimReticleObj.GetComponent<Collider>();
        if (retCol != null) Destroy(retCol);

        Renderer retRenderer = aimReticleObj.GetComponent<Renderer>();
        if (retRenderer != null)
        {
            retRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            retRenderer.receiveShadows = false;
            aimReticleMat = new Material(shader);
            SetMaterialColor(aimReticleMat, aimIdleColor);
            retRenderer.sharedMaterial = aimReticleMat;
        }

        aimReticleObj.SetActive(false);
    }

    private void SetMaterialColor(Material mat, Color col)
    {
        if (mat == null) return;
        mat.color = col;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
    }

    private void UpdateAimSight()
    {
        if (aimLine == null || aimReticleObj == null) return;

        if (!isEquippedInHand)
        {
            if (aimLine.enabled) aimLine.enabled = false;
            if (aimReticleObj.activeSelf) aimReticleObj.SetActive(false);
            return;
        }

        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position;
        Vector3 forward = muzzlePoint != null ? muzzlePoint.forward : transform.forward;

        bool lockedOnTarget = false;
        float aimDistance = 40f;

        if (TryFindValidTarget(origin, forward, 0f, out RaycastHit hit) ||
            TryFindValidTarget(origin, forward, 0.45f, out hit))
        {
            lockedOnTarget = true;
            aimDistance = hit.distance > 0.1f
                ? hit.distance
                : Vector3.Distance(origin, hit.collider.bounds.center);
            aimDistance = Mathf.Clamp(aimDistance, 0.3f, maxDistance);
        }

        // Mantém a linha de mira 100% reta na direção exata do cano da arma (forward)
        Vector3 endPoint = origin + forward * aimDistance;

        Color targetColor = lockedOnTarget ? aimLockedColor : aimIdleColor;
        SetMaterialColor(aimLineMat, targetColor);
        SetMaterialColor(aimReticleMat, lockedOnTarget ? new Color(0.2f, 1f, 0.45f, 1f) : new Color(0.25f, 0.9f, 1f, 0.95f));

        aimLine.enabled = true;
        aimLine.SetPosition(0, origin);
        aimLine.SetPosition(1, endPoint);

        aimReticleObj.SetActive(true);
        aimReticleObj.transform.position = endPoint;

        float dist = Vector3.Distance(origin, endPoint);
        float worldScale = Mathf.Clamp(dist * 0.015f, 0.035f, 0.45f);
        if (lockedOnTarget)
        {
            worldScale *= 1.25f + Mathf.Sin(Time.time * 12f) * 0.15f;
        }

        Vector3 parentLossy = transform.lossyScale;
        float invScale = parentLossy.x > 0.0001f ? (1f / parentLossy.x) : 33.33f;
        aimReticleObj.transform.localScale = Vector3.one * (worldScale * invScale);
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
        if (aimLine != null) aimLine.enabled = false;
        if (aimReticleObj != null) aimReticleObj.SetActive(false);
    }

    private void OnDestroy()
    {
        if (cachedGrabInteractable != null)
        {
            cachedGrabInteractable.selectEntered.RemoveListener(OnGrabSelected);
            cachedGrabInteractable.selectExited.RemoveListener(OnGrabDeselected);
            cachedGrabInteractable.activated.RemoveListener(OnGrabActivated);
        }

        if (Instance == this) Instance = null;
        ActiveControllers.Remove(this);
        if (laserMat != null) Destroy(laserMat);
        if (aimLineMat != null) Destroy(aimLineMat);
        if (aimReticleMat != null) Destroy(aimReticleMat);
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

    private void LateUpdate()
    {
        if (IsGrabbableWeapon)
        {
            UpdateAimSight();
        }
    }

    private void Update()
    {
        // Se for a arma física e ainda não estiver equipada na mão, permite equipá-la ao apontar e apertar Grip ou Trigger
        if (IsGrabbableWeapon && !isEquippedInHand)
        {
            if (cachedGrabInteractable != null && cachedGrabInteractable.isHovered && cachedGrabInteractable.interactorsHovering.Count > 0)
            {
                var hoveringInteractor = cachedGrabInteractable.interactorsHovering[0];
                if (hoveringInteractor != null && CheckHoverPickupInput(hoveringInteractor))
                {
                    EquipToControllerHand(hoveringInteractor);
                }
            }
            return;
        }

        if (Time.time < nextFireTime) return;

        // Mesmo quando o disparo de combate está desativado (Introdução, Vitória ou Derrota),
        // permite clicar nos botões dos Canvas apontando e pressionando o gatilho
        if (!canShoot)
        {
            if (IsGrabbableWeapon)
            {
                if (!isEquippedInHand || Time.time - lastGrabTime < 0.25f) return;
            }
            else if (IsAnyGrabbableWeaponEquipped())
            {
                return;
            }

            if (CheckFireInput())
            {
                TryClickUIButtonOnly();
            }
            return;
        }

        if (IsGrabbableWeapon)
        {
            if (!isEquippedInHand) return;
            if (Time.time - lastGrabTime < 0.25f) return;
        }
        else
        {
            // Se existir uma arma física pegável na cena (ex: 'arma'), o jogador só atira em detritos com a arma equipada na mão
            if (HasGrabbableWeaponInScene() || IsHoldingWeapon())
            {
                if (!IsAnyGrabbableWeaponEquipped() && CheckFireInput())
                {
                    TryClickUIButtonOnly();
                }
                return;
            }
        }

        if (CheckFireInput())
        {
            Fire();
        }
    }

    private static bool IsAnyGrabbableWeaponEquipped()
    {
        for (int i = 0; i < ActiveControllers.Count; i++)
        {
            var c = ActiveControllers[i];
            if (c != null && c.gameObject.activeInHierarchy && c.IsGrabbableWeapon && c.IsGrabbed)
            {
                return true;
            }
        }
        return false;
    }

    private void TryClickUIButtonOnly()
    {
        Vector3 origin;
        Vector3 forward;

        if (muzzlePoint != null)
        {
            origin = muzzlePoint.position;
            forward = muzzlePoint.forward;
        }
        else if (cachedRayProvider != null)
        {
            Transform rayOrigin = cachedRayProvider.GetOrCreateRayOrigin();
            origin = rayOrigin != null ? rayOrigin.position : transform.position;
            forward = rayOrigin != null ? rayOrigin.forward : transform.forward;
        }
        else
        {
            origin = transform.position;
            forward = transform.forward;
        }

        RaycastHit[] hits = Physics.RaycastAll(origin, forward, maxDistance, hitLayers, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].distance <= 0.05f) continue;
            Collider col = hits[i].collider;
            if (col == null) continue;

            UnityEngine.UI.Button hitBtn = col.GetComponentInParent<UnityEngine.UI.Button>();
            if (hitBtn != null && hitBtn.interactable)
            {
                nextFireTime = Time.time + fireRate;
                if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
                TriggerHaptics(0.45f, 0.08f);
                hitBtn.onClick.Invoke();
                return;
            }
        }
    }

    private bool CheckHoverPickupInput(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor)
    {
        if (interactor is XRBaseInputInteractor inputInteractor)
        {
            if (inputInteractor.selectInput != null &&
                (inputInteractor.selectInput.ReadWasPerformedThisFrame() || inputInteractor.selectInput.ReadIsPerformed()))
                return true;

            if (inputInteractor.activateInput != null &&
                (inputInteractor.activateInput.ReadWasPerformedThisFrame() || inputInteractor.activateInput.ReadIsPerformed()))
                return true;
        }
#if UNITY_EDITOR
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return false;
    }

    private static bool HasGrabbableWeaponInScene()
    {
        for (int i = 0; i < ActiveControllers.Count; i++)
        {
            var c = ActiveControllers[i];
            if (c != null && c.gameObject.activeInHierarchy && c.IsGrabbableWeapon)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsHoldingWeapon()
    {
        if (cachedInteractor != null && cachedInteractor.hasSelection)
        {
            for (int i = 0; i < cachedInteractor.interactablesSelected.Count; i++)
            {
                var sel = cachedInteractor.interactablesSelected[i];
                if (sel is MonoBehaviour mb && mb.GetComponent<ArmaVR>() != null)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Avalia múltiplos canais de entrada dos controladores VR para detectar disparo imediato.
    /// Quando empunhando uma arma física (XRGrabInteractable), usa o Gatilho (Trigger/Activate) ou botões primários,
    /// mantendo o Grip (Select) exclusivo para segurar a arma.
    /// </summary>
    private bool CheckFireInput()
    {
        bool isPhysicalWeapon = IsGrabbableWeapon;

        // 1. Checa a Input Action de Gatilho (Activate / Trigger) e Select (Grip)
        if (triggerAction.action != null)
        {
            if (triggerAction.action.WasPressedThisFrame() || triggerAction.action.ReadValue<float>() > 0.45f)
            {
                return true;
            }
        }

        if (selectAction.action != null)
        {
            if (selectAction.action.WasPressedThisFrame() || (!isPhysicalWeapon && selectAction.action.ReadValue<float>() > 0.45f))
            {
                return true;
            }
        }

        // 2. Checa o Interactor da mão onde a arma está equipada (ou o interactor do próprio controle)
        XRBaseInputInteractor activeInteractor = isPhysicalWeapon ? holdingInputInteractor : cachedInteractor;
        if (activeInteractor != null)
        {
            if (activeInteractor.activateInput != null)
            {
                if (activeInteractor.activateInput.ReadWasPerformedThisFrame() || 
                    activeInteractor.activateInput.ReadIsPerformed() || 
                    activeInteractor.activateInput.ReadValue() > 0.45f)
                {
                    return true;
                }
            }

            if (activeInteractor.selectInput != null)
            {
                if (activeInteractor.selectInput.ReadWasPerformedThisFrame() || 
                    (!isPhysicalWeapon && (activeInteractor.selectInput.ReadIsPerformed() || activeInteractor.selectInput.ReadValue() > 0.45f)))
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
                IsControlActive(device, "primaryButton") ||
                IsControlActive(device, "secondaryButton"))
            {
                return true;
            }

            if (!isPhysicalWeapon && (IsControlActive(device, "grip") || IsControlActive(device, "gripButton")))
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
            if (!isPhysicalWeapon)
            {
                if (xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gb) && gb) return true;
                if (xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float gv) && gv > 0.45f) return true;
            }
            if (xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pb) && pb) return true;
            if (xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool sb) && sb) return true;
        }

#if UNITY_EDITOR
        // Suporte para testes rápidos no Unity Editor através do XR Device Simulator / cliques do mouse
        if (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasPressedThisFrame))
        {
            if (isPhysicalWeapon || isRightHand)
            {
                return true;
            }
        }
        else if (!isPhysicalWeapon && !isRightHand && Mouse.current != null && (Mouse.current.rightButton.isPressed || Mouse.current.rightButton.wasPressedThisFrame))
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
        try
        {
            var control = device.TryGetChildControl<InputControl>(controlName);
            if (control == null) return false;

            if (control is ButtonControl btn)
            {
                return btn.isPressed || btn.wasPressedThisFrame;
            }
            if (control is AxisControl axis)
            {
                return axis.ReadValue() > 0.45f;
            }
        }
        catch
        {
            return false;
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

        // 2. Origem e direção do disparo alinhados ao cano da arma (muzzlePoint)
        Vector3 origin;
        Vector3 forward;

        if (muzzlePoint != null)
        {
            origin = muzzlePoint.position;
            forward = muzzlePoint.forward;
        }
        else if (cachedRayProvider != null)
        {
            Transform rayOrigin = cachedRayProvider.GetOrCreateRayOrigin();
            origin = rayOrigin != null ? rayOrigin.position : transform.position;
            forward = rayOrigin != null ? rayOrigin.forward : transform.forward;
        }
        else
        {
            origin = transform.position;
            forward = transform.forward;
        }

        // 3. Detecção de Colisão: RaycastAll direto + SphereCastAll generoso (apenas para alvos reais)
        Vector3 targetPoint = origin + forward * maxDistance;
        GameObject hitObject = null;

        if (TryFindValidTarget(origin, forward, 0f, out RaycastHit validHit) ||
            TryFindValidTarget(origin, forward, 0.45f, out validHit))
        {
            float hitDist = validHit.distance > 0.1f
                ? validHit.distance
                : Vector3.Distance(origin, validHit.collider.bounds.center);
            targetPoint = origin + forward * Mathf.Clamp(hitDist, 0.3f, maxDistance);
            hitObject = validHit.collider.gameObject;
        }

        // 4. Renderiza o raio laser luminoso no espaço 3D
        if (beamRoutine != null) StopCoroutine(beamRoutine);
        beamRoutine = StartCoroutine(LaserBeamRoutine(origin, targetPoint));

        // 5. Vibração tátil no controlador VR (Haptics)
        TriggerHaptics(0.65f, 0.12f);

        // 6. Avalia acerto com o alvo (Botões de Interface VR no Cockpit ou Detritos Espaciais)
        if (hitObject != null)
        {
            // Interação direta com botões da interface (ex: RETORNAR, RECOMEÇAR, NOVA RODADA) ao atirar neles
            UnityEngine.UI.Button hitBtn = hitObject.GetComponentInParent<UnityEngine.UI.Button>();
            if (hitBtn != null && hitBtn.interactable)
            {
                if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
                hitBtn.onClick.Invoke();
                return;
            }

            Detrito detrito = hitObject.GetComponentInParent<Detrito>();
            if (detrito != null)
            {
                GerenteDefesa def = GerenteDefesa.Instance != null ? GerenteDefesa.Instance : FindAnyObjectByType<GerenteDefesa>();
                if (def != null)
                {
                    def.OnDebrisHit(detrito);
                }
                return;
            }
        }
    }

    private bool TryFindValidTarget(Vector3 origin, Vector3 forward, float radius, out RaycastHit bestHit)
    {
        bestHit = default;
        RaycastHit[] hits = radius <= 0.001f
            ? Physics.RaycastAll(origin, forward, maxDistance, hitLayers, QueryTriggerInteraction.Collide)
            : Physics.SphereCastAll(origin, radius, forward, maxDistance, hitLayers, QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            // Ignora colisões sobrepostas na origem da esfera (onde Unity retorna point = (0,0,0) e distance = 0)
            if (hits[i].distance <= 0.05f)
                continue;

            Collider col = hits[i].collider;
            if (col == null) continue;

            // Ignora colisores da própria arma ou dos controladores VR
            if (col.transform.IsChildOf(transform) || col.GetComponentInParent<ArmaVR>() != null)
                continue;

            // Aceita apenas alvos válidos: Detritos espaciais ou Botões de UI do Cockpit
            Detrito detrito = col.GetComponentInParent<Detrito>();
            if (detrito != null)
            {
                bestHit = hits[i];
                return true;
            }

            // Botões do painel só são ativados no raio direto (radius == 0), sem o SphereCast largo
            if (radius <= 0.001f)
            {
                UnityEngine.UI.Button btn = col.GetComponentInParent<UnityEngine.UI.Button>();
                if (btn != null && btn.interactable)
                {
                    bestHit = hits[i];
                    return true;
                }
            }
        }

        return false;
    }

    private void TriggerHaptics(float amplitude, float duration)
    {
        if (!UnityEngine.XR.XRSettings.isDeviceActive)
            return;

        if (currentHoldingInteractor is XRBaseInputInteractor grabInputInteractor)
        {
            grabInputInteractor.SendHapticImpulse(amplitude, duration);
        }

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

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
public static class ArmaVREditorAutoSetup
{
    static ArmaVREditorAutoSetup()
    {
        UnityEditor.EditorApplication.delayCall += TrySetupArmaInActiveScene;
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, mode) => TrySetupArmaInActiveScene();
    }

    private static void TrySetupArmaInActiveScene()
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root != null && root.name.Equals("arma", System.StringComparison.OrdinalIgnoreCase))
            {
                bool needsSave = (root.GetComponent<ArmaVR>() == null ||
                                  root.GetComponent<XRGrabInteractable>() == null ||
                                  root.GetComponent<Rigidbody>() == null ||
                                  root.GetComponent<BoxCollider>() == null);

                ArmaVR.ConfigureWeaponObject(root);

                if (needsSave)
                {
                    UnityEditor.EditorUtility.SetDirty(root);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                }
                break;
            }
        }
    }
}
#endif
