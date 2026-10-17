using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

/// <summary>
/// Posiciona o jogador exatamente na cadeira do cockpit ao iniciar a DefenseScene
/// e restringe sua movimentação para que fique estritamente entre a cadeira e o painel de controle,
/// sem poder sair do campo do painel de controle.
/// </summary>
public class AssentoCockpit : MonoBehaviour
{
    [Header("Posição Inicial na Cadeira do Cockpit")]
    [Tooltip("Posição inicial padrão do XR Origin na cadeira do piloto (atualizada automaticamente pela malha da cadeira)")]
    [SerializeField] private Vector3 seatedPosition = new Vector3(-0.18f, 0.05f, -0.20f);
    [Tooltip("Rotação inicial do XR Origin voltada para o painel de controle")]
    [SerializeField] private Vector3 seatedEulerRotation = Vector3.zero;
    [SerializeField] private bool lockPosition = true;

    [Header("Limites Entre a Cadeira e o Painel de Controle")]
    [Tooltip("Limites calculados automaticamente entre a cadeira (ChairMain) e o painel (MainControlPanel)")]
    [SerializeField] private Vector2 boundsX = new Vector2(-0.55f, 0.35f);
    [SerializeField] private Vector2 boundsZ = new Vector2(-0.25f, 0.25f);
    [SerializeField] private float fixedRigY = 0.05f;
    [SerializeField] private float fixedEyeY = 1.38f;

    [Header("Configuração de Locomoção VR")]
    [Tooltip("Desativa gravidade, pulo e teleporte para manter o jogador seguro no piso do cockpit")]
    [SerializeField] private bool disableLocomotionProviders = true;

    private Camera mainCamera;
    private bool boundsCalculated = false;
    private bool initialChairPlacementDone = false;

    private void Awake()
    {
        CacheCameraReferences();
        CalculateCockpitBoundsFromScene();
        PlacePlayerAtChair();
    }

    private void Start()
    {
        CacheCameraReferences();
        CalculateCockpitBoundsFromScene();
        PlacePlayerAtChair();
        DisableUnsupportedLocomotion();
    }

    private void OnEnable()
    {
        CacheCameraReferences();
        DisableUnsupportedLocomotion();
        Application.onBeforeRender += OnBeforeRenderClamp;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRenderClamp;
    }

    private void CacheCameraReferences()
    {
        if (mainCamera == null)
        {
            mainCamera = GetComponentInChildren<Camera>(true);
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }
    }

    /// <summary>
    /// Localiza a cadeira (ChairMain / StoolBase) e o painel de controle (MainControlPanel)
    /// dentro do objeto Painel na cena para definir a posição inicial exata na cadeira
    /// e os limites de movimento entre a cadeira e o painel.
    /// </summary>
    public void CalculateCockpitBoundsFromScene()
    {
        GameObject painelObj = GameObject.Find("Painel");
        if (painelObj == null)
        {
            boundsCalculated = true;
            return;
        }

        Renderer[] renderers = painelObj.GetComponentsInChildren<Renderer>(true);

        Bounds chairSeatBounds = default;
        bool hasChairSeat = false;

        Bounds chairAllBounds = default;
        bool hasChairAll = false;

        Bounds mainPanelBounds = default;
        bool hasMainPanel = false;

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            string n = r.gameObject.name;

            // Assento principal da cadeira do piloto
            if (n.StartsWith("ChairMain", System.StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("ChairBase", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!hasChairSeat)
                {
                    chairSeatBounds = r.bounds;
                    hasChairSeat = true;
                }
                else
                {
                    chairSeatBounds.Encapsulate(r.bounds);
                }
            }

            // Estrutura completa da cadeira / base
            if (n.IndexOf("Chair", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.StartsWith("StoolBase", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!hasChairAll)
                {
                    chairAllBounds = r.bounds;
                    hasChairAll = true;
                }
                else
                {
                    chairAllBounds.Encapsulate(r.bounds);
                }
            }

            // Painel de controle frontal (MainControlPanel, ControlPanelExtras, Keyboard)
            if (n.StartsWith("MainControlPanel", System.StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("ControlPanelExtras", System.StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("Keyboard", System.StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("CockpitScreens", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!hasMainPanel)
                {
                    mainPanelBounds = r.bounds;
                    hasMainPanel = true;
                }
                else
                {
                    mainPanelBounds.Encapsulate(r.bounds);
                }
            }
        }

        Bounds effectiveChair = hasChairSeat ? chairSeatBounds : chairAllBounds;
        if (hasChairSeat || hasChairAll)
        {
            Vector3 chairCenter = effectiveChair.center;
            seatedPosition = new Vector3(chairCenter.x, fixedRigY, chairCenter.z);

            if (hasMainPanel)
            {
                // Altura dos olhos ligeramente acima do painel de controle (1.19m) para ver o painel e o espaço
                fixedEyeY = Mathf.Max(1.36f, mainPanelBounds.center.y + 0.22f);

                // Limite lateral (X): estritamente dentro da largura do painel de controle (e cobrindo a cadeira)
                float panelMinX = mainPanelBounds.min.x + 0.12f;
                float panelMaxX = mainPanelBounds.max.x - 0.12f;
                if (panelMinX > panelMaxX)
                {
                    panelMinX = mainPanelBounds.min.x;
                    panelMaxX = mainPanelBounds.max.x;
                }

                float minX = Mathf.Min(chairCenter.x - 0.30f, panelMinX);
                float maxX = Mathf.Max(chairCenter.x + 0.30f, panelMaxX);
                minX = Mathf.Max(minX, mainPanelBounds.min.x + 0.05f);
                maxX = Mathf.Min(maxX, mainPanelBounds.max.x - 0.05f);
                minX = Mathf.Min(minX, chairCenter.x - 0.22f);
                maxX = Mathf.Max(maxX, chairCenter.x + 0.22f);
                boundsX = new Vector2(minX, maxX);

                // Limite longitudinal (Z): estritamente ENTRE a cadeira e o painel de controle
                float chairZ = chairCenter.z;
                float panelCenterZ = mainPanelBounds.center.z;

                if (chairZ <= panelCenterZ)
                {
                    float minZ = chairZ - 0.05f;
                    float panelFrontStopZ = Mathf.Max(chairZ + 0.25f, mainPanelBounds.min.z + 0.12f);
                    panelFrontStopZ = Mathf.Min(panelFrontStopZ, mainPanelBounds.center.z - 0.05f);
                    float maxZ = Mathf.Max(chairZ + 0.20f, panelFrontStopZ);
                    boundsZ = new Vector2(minZ, maxZ);
                }
                else
                {
                    float maxZ = chairZ + 0.05f;
                    float panelFrontStopZ = Mathf.Min(chairZ - 0.25f, mainPanelBounds.max.z - 0.12f);
                    panelFrontStopZ = Mathf.Max(panelFrontStopZ, mainPanelBounds.center.z + 0.05f);
                    float minZ = Mathf.Min(chairZ - 0.20f, panelFrontStopZ);
                    boundsZ = new Vector2(minZ, maxZ);
                }
            }
            else
            {
                boundsX = new Vector2(chairCenter.x - 0.40f, chairCenter.x + 0.40f);
                boundsZ = new Vector2(chairCenter.z - 0.05f, chairCenter.z + 0.42f);
            }
        }

        boundsCalculated = true;
    }

    /// <summary>
    /// Posiciona o jogador exatamente onde está a cadeira ao começar na cena.
    /// </summary>
    public void PlacePlayerAtChair()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        if (!boundsCalculated)
        {
            CalculateCockpitBoundsFromScene();
        }

        transform.rotation = Quaternion.Euler(seatedEulerRotation);

        if (mainCamera != null)
        {
            Vector3 camWorld = mainCamera.transform.position;
            Vector3 desiredCamWorld = new Vector3(seatedPosition.x, fixedEyeY, seatedPosition.z);
            transform.position += (desiredCamWorld - camWorld);
        }
        else
        {
            transform.position = new Vector3(seatedPosition.x, fixedRigY, seatedPosition.z);
        }

        initialChairPlacementDone = false;
    }

    /// <summary>
    /// Mantém Compatibilidade com chamadas anteriores.
    /// </summary>
    public void ApplyCockpitRigLock()
    {
        PlacePlayerAtChair();
    }

    /// <summary>
    /// Desativa gravidade, pulo, escalada e teleporte para que o jogador não saia nem caia do cockpit.
    /// </summary>
    public void DisableUnsupportedLocomotion()
    {
        if (!disableLocomotionProviders) return;

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            string n = child.name;
            if (n.Equals("Teleportation", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Gravity", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Climb", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Climb Teleport", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Grab Move", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Jump", System.StringComparison.OrdinalIgnoreCase))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    public void DisableAllLocomotion()
    {
        DisableUnsupportedLocomotion();
    }

    private void LateUpdate()
    {
        EnforceCockpitBounds();
    }

    [BeforeRenderOrder(1000)]
    private void OnBeforeRenderClamp()
    {
        EnforceCockpitBounds();
    }

    /// <summary>
    /// Mantém o jogador estritamente entre a cadeira e o painel de controle deslocando o XR Origin
    /// como um todo (sem alterar o localPosition da Main Camera, para que os controles e a arma
    /// mantenham sempre o tamanho e a distância corretos em relação à câmera).
    /// </summary>
    private void EnforceCockpitBounds()
    {
        if (!lockPosition) return;

        if (mainCamera == null)
        {
            CacheCameraReferences();
        }

        if (mainCamera != null)
        {
            Vector3 camWorld = mainCamera.transform.position;

            // No primeiro frame após o TrackedPoseDriver inicializar, garante que o jogador comece exatamente na cadeira
            if (!initialChairPlacementDone)
            {
                initialChairPlacementDone = true;
                Vector3 chairTarget = new Vector3(seatedPosition.x, fixedEyeY, seatedPosition.z);
                transform.position += (chairTarget - camWorld);
                return;
            }

            float clampedX = Mathf.Clamp(camWorld.x, boundsX.x, boundsX.y);
            float clampedY = fixedEyeY;
            float clampedZ = Mathf.Clamp(camWorld.z, boundsZ.x, boundsZ.y);

            Vector3 targetCamWorld = new Vector3(clampedX, clampedY, clampedZ);
            Vector3 delta = targetCamWorld - camWorld;

            if (delta.sqrMagnitude > 0.000001f)
            {
                transform.position += delta;
            }
        }
        else
        {
            Vector3 rigPos = transform.position;
            rigPos.x = Mathf.Clamp(rigPos.x, boundsX.x, boundsX.y);
            rigPos.y = fixedRigY;
            rigPos.z = Mathf.Clamp(rigPos.z, boundsZ.x, boundsZ.y);
            transform.position = rigPos;
        }
    }
}

public class CockpitSeatedRig : AssentoCockpit { }

