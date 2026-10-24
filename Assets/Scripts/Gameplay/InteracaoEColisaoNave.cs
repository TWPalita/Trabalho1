using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

/// <summary>
/// Garante colisão física sólida em todas as paredes, portas e móveis da espaçonave
/// (impedindo que o jogador atravesse paredes tanto pelo analógico VR quanto pelo WASD do Simulador XR)
/// e torna os objetos espalhados pela nave manipuláveis (pegáveis/arremessáveis via XRGrabInteractable).
/// </summary>
[DefaultExecutionOrder(200)]
public class InteracaoEColisaoNave : MonoBehaviour
{
    public static InteracaoEColisaoNave Instance { get; private set; }

    public struct WallSegmentDef
    {
        public string Name;
        public Vector3 Position;
        public Vector3 EulerAngles;
        public Vector3 Size;

        public WallSegmentDef(string name, Vector3 position, Vector3 size, float angleY = 0f)
        {
            Name = name;
            Position = position;
            EulerAngles = new Vector3(0f, angleY, 0f);
            Size = size;
        }

        public static WallSegmentDef FromEndpoints(string name, float x1, float z1, float x2, float z2, float thickness = 0.25f, float height = 3.4f, float yCenter = 1.6f)
        {
            float dx = x2 - x1;
            float dz = z2 - z1;
            float length = Mathf.Sqrt(dx * dx + dz * dz);
            Vector3 center = new Vector3((x1 + x2) * 0.5f, yCenter, (z1 + z2) * 0.5f);

            if (Mathf.Abs(dx) < 0.02f)
            {
                return new WallSegmentDef(name, center, new Vector3(thickness, height, length + 0.12f), 0f);
            }
            if (Mathf.Abs(dz) < 0.02f)
            {
                return new WallSegmentDef(name, center, new Vector3(length + 0.12f, height, thickness), 0f);
            }

            float angleY = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            return new WallSegmentDef(name, center, new Vector3(thickness, height, length + 0.12f), angleY);
        }
    }

    /// <summary>
    /// Retorna a lista completa de colisores das paredes, chão, teto e console da espaçonave na SampleScene.
    /// Extraído com precisão milimétrica da geometria de Room_Room e DoorWay1..5.
    /// </summary>
    public static WallSegmentDef[] GetSampleSceneWallDefinitions()
    {
        return new WallSegmentDef[]
        {
            // Chão e Teto cobrindo toda a extensão da nave (incluindo Sala do Gerador e Ala Direita)
            new WallSegmentDef("ColisorChao", new Vector3(0.75f, -0.22f, 1.5f), new Vector3(30f, 0.4f, 26f)),
            new WallSegmentDef("ColisorTeto", new Vector3(0.75f, 3.35f, 1.5f), new Vector3(30f, 0.4f, 26f)),
            new WallSegmentDef("ObstaculoConsole", new Vector3(-3.39f, 0.4f, 8.8f), new Vector3(2.2f, 0.8f, 1.4f)),

            // 1. Quarto (Bedroom)
            WallSegmentDef.FromEndpoints("ColisorParedeQuarto_Traseira", -7.33f, -9.30f, -0.18f, -9.30f),
            WallSegmentDef.FromEndpoints("ColisorParedeQuarto_Esquerda", -7.33f, -9.30f, -7.33f, -5.17f),
            WallSegmentDef.FromEndpoints("ColisorParedeQuarto_Direita", -0.18f, -9.30f, -0.18f, -5.17f),
            WallSegmentDef.FromEndpoints("ColisorParedeQuarto_FrontalEsq", -7.33f, -5.17f, -4.19f, -5.17f),
            WallSegmentDef.FromEndpoints("ColisorParedeQuarto_FrontalDir", -2.58f, -5.17f, -0.18f, -5.17f),

            // 2. Corredor Central
            WallSegmentDef.FromEndpoints("ColisorParedeCorredor_Esquerda", -4.51f, -5.17f, -4.51f, 2.49f),
            WallSegmentDef.FromEndpoints("ColisorParedeCorredor_Direita", -2.25f, -5.17f, -2.25f, 4.75f),

            // 3. Passagem e Sala do Gerador (Ala Esquerda)
            WallSegmentDef.FromEndpoints("ColisorParedePassagemGerador_Sul", -8.26f, 2.49f, -4.51f, 2.49f),
            WallSegmentDef.FromEndpoints("ColisorParedePassagemGerador_Norte", -10.51f, 4.74f, -4.51f, 4.74f),
            WallSegmentDef.FromEndpoints("ColisorParedePassagemGerador_Oeste", -10.51f, 0.99f, -10.51f, 4.74f),
            WallSegmentDef.FromEndpoints("ColisorParedePassagemGerador_Leste", -8.26f, 0.99f, -8.26f, 2.49f),
            WallSegmentDef.FromEndpoints("ColisorParedeSalaGerador_NorteDir", -8.59f, 0.99f, -5.23f, 0.99f),
            WallSegmentDef.FromEndpoints("ColisorParedeSalaGerador_NorteEsq", -12.73f, 0.99f, -10.20f, 0.99f),
            WallSegmentDef.FromEndpoints("ColisorParedeSalaGerador_Leste", -5.23f, -3.51f, -5.23f, 0.99f),
            WallSegmentDef.FromEndpoints("ColisorParedeSalaGerador_Sul", -12.73f, -3.51f, -5.23f, -3.51f),
            WallSegmentDef.FromEndpoints("ColisorParedeSalaGerador_Oeste", -12.73f, -3.51f, -12.73f, 0.99f),

            // 4. Cabine de Comando / Sala Frontal e Divisória Central
            WallSegmentDef.FromEndpoints("ColisorParedeCabine_Esquerda", -4.51f, 4.74f, -4.51f, 10.75f),
            WallSegmentDef.FromEndpoints("ColisorParedeCabine_ArcoEsq1", -4.51f, 10.75f, -3.39f, 11.81f),
            WallSegmentDef.FromEndpoints("ColisorParedeCabine_ArcoEsq2", -3.39f, 11.81f, -2.26f, 12.25f),
            WallSegmentDef.FromEndpoints("ColisorParedeCabine_ArcoEsq3", -2.26f, 12.25f, -1.14f, 12.58f),
            WallSegmentDef.FromEndpoints("ColisorParedeCabine_Frontal", -1.14f, 12.65f, 1.12f, 12.65f),
            WallSegmentDef.FromEndpoints("ColisorParedeCabine_ArcoDir3", 1.12f, 12.58f, 2.24f, 12.25f),
            WallSegmentDef.FromEndpoints("ColisorParedeCabine_ArcoDir2", 2.24f, 12.25f, 3.37f, 11.81f),
            WallSegmentDef.FromEndpoints("ColisorParedeCabine_ArcoDir1", 3.37f, 11.81f, 4.49f, 10.75f),
            WallSegmentDef.FromEndpoints("ColisorParedeCabine_Direita", 4.49f, 0.24f, 4.49f, 10.75f),
            WallSegmentDef.FromEndpoints("ColisorParedeDivisoria_DiagEsq", -2.26f, 4.75f, -1.14f, 6.26f),
            WallSegmentDef.FromEndpoints("ColisorParedeDivisoria_Norte", -1.14f, 6.26f, 1.12f, 6.26f),
            WallSegmentDef.FromEndpoints("ColisorParedeDivisoria_DiagDir", 1.12f, 6.26f, 2.24f, 4.75f),
            WallSegmentDef.FromEndpoints("ColisorParedeDivisoria_Direita", 2.24f, -7.25f, 2.24f, 4.75f),

            // 5. Corredor Direito e Sala Direita (Ala Direita)
            WallSegmentDef.FromEndpoints("ColisorParedeCorredorDir_Sul", 2.24f, -7.25f, 4.49f, -7.25f),
            WallSegmentDef.FromEndpoints("ColisorParedeCorredorDir_Leste", 4.49f, -7.25f, 4.49f, -2.01f),
            WallSegmentDef.FromEndpoints("ColisorParedePassagemDir_Sul", 4.49f, -2.01f, 6.74f, -2.01f),
            WallSegmentDef.FromEndpoints("ColisorParedePassagemDir_Norte", 4.49f, 0.24f, 6.74f, 0.24f),
            WallSegmentDef.FromEndpoints("ColisorParedeSalaDir_OesteSul", 6.74f, -5.75f, 6.74f, -1.68f),
            WallSegmentDef.FromEndpoints("ColisorParedeSalaDir_OesteNorte", 6.74f, -0.08f, 6.74f, 3.25f),
            WallSegmentDef.FromEndpoints("ColisorParedeSalaDir_Norte", 6.74f, 3.25f, 14.24f, 3.25f),
            WallSegmentDef.FromEndpoints("ColisorParedeSalaDir_Leste", 14.24f, -5.75f, 14.24f, 3.25f),
            WallSegmentDef.FromEndpoints("ColisorParedeSalaDir_Sul", 6.74f, -5.75f, 14.24f, -5.75f)
        };
    }

    // Prefixos dos objetos (props) da nave que devem ser manipuláveis pelo jogador em VR
    private static readonly string[] ManipulablePropPrefixes =
    {
        "Canette",
        "Bouteille",
        "Verre",
        "verre",
        "BacBrosse",
        "Brosseadent",
        "Tablette",
        "Boite",
        "FirstAidKit",
        "PlateauRepas",
        "Pizza",
        "Coussin",
        "Tabouret",
        "Fusil_assault",
        "Pistolet",
        "CouvercleCArme",
        "Cendrier",
        "PaquetCiga",
        "Cigarette",
        "Argent",
        "Globe",
        "Balais",
        "WetFloor"
    };

    [Header("Velocidade de Caminhada na Nave")]
    [SerializeField] private float walkSpeed = 2.6f;

    private XROrigin xrOrigin;
    private CharacterController characterController;
    private CapsuleCollider depenetrationCapsule;
    private Transform mainCameraTr;
    private readonly HashSet<Collider> wallAndStructureColliders = new HashSet<Collider>();
    private readonly HashSet<Collider> manipulableColliders = new HashSet<Collider>();
    private readonly RaycastHit[] nonAllocHits = new RaycastHit[32];
    private readonly Collider[] nonAllocOverlaps = new Collider[32];
    private Vector3 lastValidHeadFloorPos;
    private bool hasValidPlayerPosition = false;
    private bool isCockpitDefenseScene = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstanceInActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstanceInActiveScene();
    }

    private static void EnsureInstanceInActiveScene()
    {
        if (FindAnyObjectByType<InteracaoEColisaoNave>() != null) return;

        GameObject colisoresObj = GameObject.Find("ColisoresNave");
        if (colisoresObj != null)
        {
            colisoresObj.AddComponent<InteracaoEColisaoNave>();
            return;
        }

        GameObject go = new GameObject("InteracaoEColisaoNave_Manager");
        go.AddComponent<InteracaoEColisaoNave>();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        Physics.queriesHitBackfaces = true;
    }

    private void Start()
    {
        ConfigureSceneCollisionAndInteractables();
    }

    public void ConfigureSceneCollisionAndInteractables()
    {
        Physics.queriesHitBackfaces = true;

        wallAndStructureColliders.Clear();
        manipulableColliders.Clear();

        string activeSceneName = SceneManager.GetActiveScene().name;
        isCockpitDefenseScene = activeSceneName.IndexOf("Defense", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                FindAnyObjectByType<AssentoCockpit>() != null;

        if (walkSpeed < 2.2f)
        {
            walkSpeed = 2.6f;
        }

        // 1. Localiza XR Origin, CharacterController e velocidade de caminhada do jogador
        xrOrigin = FindAnyObjectByType<XROrigin>();
        if (xrOrigin != null)
        {
            characterController = xrOrigin.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = true;
                characterController.radius = 0.24f;
                characterController.stepOffset = 0.30f;
                characterController.skinWidth = 0.03f;
                characterController.minMoveDistance = 0.0001f;
            }

            if (xrOrigin.Camera != null)
            {
                mainCameraTr = xrOrigin.Camera.transform;
            }

            foreach (ContinuousMoveProvider moveProvider in xrOrigin.GetComponentsInChildren<ContinuousMoveProvider>(true))
            {
                if (moveProvider != null)
                {
                    moveProvider.moveSpeed = walkSpeed;
                }
            }
        }

        if (mainCameraTr == null && Camera.main != null)
        {
            mainCameraTr = Camera.main.transform;
        }

        EnsureDepenetrationCapsule();

        // 2. Garante e registra todos os BoxColliders de parede em ColisoresNave (se estivermos na nave)
        if (!isCockpitDefenseScene)
        {
            EnsureShipWallBoxColliders();
        }

        // 3. Configura colisões das paredes/estruturas e objetos manipuláveis na Nave
        GameObject naveObj = GameObject.Find("Nave") ?? GameObject.Find("nave");
        if (naveObj != null)
        {
            MeshCollider rootMc = naveObj.GetComponent<MeshCollider>();
            if (rootMc != null && rootMc.sharedMesh == null)
            {
                Destroy(rootMc);
            }

            ProcessShipHierarchy(naveObj.transform);
        }

        // 4. Configura colisão na CabinePiloto e no Gerador
        GameObject cabineObj = GameObject.Find("CabinePiloto");
        if (cabineObj != null)
        {
            ConfigureStaticStructureHierarchy(cabineObj.transform);
        }

        GameObject geradorObj = GameObject.Find("Gerador");
        if (geradorObj != null)
        {
            ConfigureGeneratorCollision(geradorObj);
        }

        // 5. Garante que o galão de Combustível e baterias sejam facilmente manipuláveis
        ConfigureFuelAndSpecialItems();

        // 6. Ignora colisão física entre o CharacterController do jogador e os pequenos objetos manipuláveis
        if (characterController != null)
        {
            foreach (Collider manipCol in manipulableColliders)
            {
                if (manipCol != null)
                {
                    Physics.IgnoreCollision(characterController, manipCol, true);
                }
            }
        }

        if (xrOrigin != null)
        {
            lastValidHeadFloorPos = GetPlayerHeadFloorPosition();
            hasValidPlayerPosition = true;
        }
    }

    private void EnsureDepenetrationCapsule()
    {
        if (depenetrationCapsule != null) return;

        Transform helperTr = transform.Find("PlayerDepenetrationProbe");
        GameObject helperObj;
        if (helperTr != null)
        {
            helperObj = helperTr.gameObject;
        }
        else
        {
            helperObj = new GameObject("PlayerDepenetrationProbe");
            helperObj.transform.SetParent(transform, false);
        }

        depenetrationCapsule = helperObj.GetComponent<CapsuleCollider>();
        if (depenetrationCapsule == null)
        {
            depenetrationCapsule = helperObj.AddComponent<CapsuleCollider>();
        }
        depenetrationCapsule.isTrigger = true;
        depenetrationCapsule.radius = 0.24f;
        depenetrationCapsule.height = 1.50f;
        depenetrationCapsule.direction = 1;
        depenetrationCapsule.center = new Vector3(0f, 0.85f, 0f);
    }

    public void EnsureShipWallBoxColliders()
    {
        GameObject colisoresNaveObj = GameObject.Find("ColisoresNave");
        if (colisoresNaveObj == null)
        {
            colisoresNaveObj = new GameObject("ColisoresNave");
        }

        colisoresNaveObj.transform.position = Vector3.zero;
        colisoresNaveObj.transform.rotation = Quaternion.identity;
        colisoresNaveObj.transform.localScale = Vector3.one;

        // Desativa ou remove colisores antigos desalinhados
        string[] obsoleteNames =
        {
            "ColisorParedeEsquerda",
            "ColisorParedeDireita",
            "ColisorParedeTraseira",
            "ColisorParedeFrontal"
        };
        for (int i = 0; i < obsoleteNames.Length; i++)
        {
            Transform obs = colisoresNaveObj.transform.Find(obsoleteNames[i]);
            if (obs != null)
            {
                Destroy(obs.gameObject);
            }
        }

        WallSegmentDef[] defs = GetSampleSceneWallDefinitions();
        for (int i = 0; i < defs.Length; i++)
        {
            WallSegmentDef def = defs[i];
            Transform childTr = colisoresNaveObj.transform.Find(def.Name);
            GameObject wallObj;
            if (childTr == null)
            {
                wallObj = new GameObject(def.Name);
                wallObj.transform.SetParent(colisoresNaveObj.transform, false);
            }
            else
            {
                wallObj = childTr.gameObject;
            }

            wallObj.SetActive(true);
            wallObj.transform.localPosition = def.Position;
            wallObj.transform.localRotation = Quaternion.Euler(def.EulerAngles);
            wallObj.transform.localScale = Vector3.one;

            BoxCollider bc = wallObj.GetComponent<BoxCollider>();
            if (bc == null)
            {
                bc = wallObj.AddComponent<BoxCollider>();
            }
            bc.center = Vector3.zero;
            bc.size = def.Size;
            bc.isTrigger = false;
            bc.enabled = true;

            if (!wallAndStructureColliders.Contains(bc))
            {
                wallAndStructureColliders.Add(bc);
            }
        }

        foreach (Collider col in colisoresNaveObj.GetComponentsInChildren<Collider>(false))
        {
            if (col != null && col.enabled && !col.isTrigger && !wallAndStructureColliders.Contains(col))
            {
                wallAndStructureColliders.Add(col);
            }
        }
    }

    private Vector3 GetPlayerHeadFloorPosition()
    {
        if (xrOrigin == null) return Vector3.zero;
        Transform rigTr = xrOrigin.transform;
        if (mainCameraTr != null)
        {
            return new Vector3(mainCameraTr.position.x, rigTr.position.y, mainCameraTr.position.z);
        }
        return rigTr.position;
    }

    private void ProcessShipHierarchy(Transform shipRoot)
    {
        MeshFilter[] meshFilters = shipRoot.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            GameObject obj = mf.gameObject;
            string objName = obj.name;

            if (IsManipulablePropName(objName))
            {
                ConfigureManipulableProp(obj, mf);
            }
            else
            {
                ConfigureStructuralMeshCollider(obj, mf);
            }
        }
    }

    private bool IsManipulablePropName(string objName)
    {
        if (string.IsNullOrEmpty(objName)) return false;

        if (objName.Equals("FirstAidKits", System.StringComparison.OrdinalIgnoreCase) ||
            objName.StartsWith("SupportArme", System.StringComparison.OrdinalIgnoreCase) ||
            objName.StartsWith("CaisseArme", System.StringComparison.OrdinalIgnoreCase) ||
            objName.StartsWith("TableBasse", System.StringComparison.OrdinalIgnoreCase) ||
            objName.Equals("Tables", System.StringComparison.OrdinalIgnoreCase) ||
            objName.Equals("Table", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (int i = 0; i < ManipulablePropPrefixes.Length; i++)
        {
            if (objName.StartsWith(ManipulablePropPrefixes[i], System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void ConfigureStructuralMeshCollider(GameObject obj, MeshFilter mf)
    {
        if (obj == null || mf == null || mf.sharedMesh == null) return;

        string n = obj.name;
        string meshName = mf.sharedMesh.name;
        if (n.StartsWith("Neon", System.StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Lampe", System.StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Lamp", System.StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Fumer", System.StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("pPlane", System.StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Holo", System.StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Ecran", System.StringComparison.OrdinalIgnoreCase) ||
            meshName.IndexOf("Room_Room", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            meshName.IndexOf("Ecran", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            MeshCollider existingBadMc = obj.GetComponent<MeshCollider>();
            if (existingBadMc != null)
            {
                existingBadMc.enabled = false;
            }
            return;
        }

        if (obj.GetComponent<GeradorCombustivel>() == null &&
            obj.GetComponent<BotaoTeleporte>() == null &&
            obj.GetComponent<XRBaseInteractable>() == null)
        {
            Rigidbody strayRb = obj.GetComponent<Rigidbody>();
            if (strayRb != null)
            {
                DestroyImmediate(strayRb);
            }
        }

        MeshCollider mc = obj.GetComponent<MeshCollider>();
        if (mc == null)
        {
            mc = obj.AddComponent<MeshCollider>();
        }

        mc.sharedMesh = mf.sharedMesh;
        mc.convex = false;
        mc.isTrigger = false;
        mc.enabled = true;

        wallAndStructureColliders.Add(mc);
    }

    private void ConfigureStaticStructureHierarchy(Transform root)
    {
        if (root == null) return;

        MeshFilter[] mfs = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter mf in mfs)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            GameObject obj = mf.gameObject;

            if (obj.GetComponentInParent<BotaoTeleporte>() != null ||
                obj.GetComponentInParent<XRBaseInteractable>() != null)
            {
                continue;
            }

            MeshCollider mc = obj.GetComponent<MeshCollider>();
            if (mc == null)
            {
                mc = obj.AddComponent<MeshCollider>();
            }
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
            mc.isTrigger = false;
            mc.enabled = true;

            if (!wallAndStructureColliders.Contains(mc))
            {
                wallAndStructureColliders.Add(mc);
            }
        }
    }

    private void ConfigureGeneratorCollision(GameObject geradorObj)
    {
        if (geradorObj == null) return;

        MeshCollider rootMc = geradorObj.GetComponent<MeshCollider>();
        if (rootMc != null && rootMc.sharedMesh == null)
        {
            Destroy(rootMc);
        }

        MeshFilter[] mfs = geradorObj.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter mf in mfs)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            GameObject obj = mf.gameObject;

            if (obj.GetComponentInParent<BotaoTeleporte>() != null) continue;

            MeshCollider mc = obj.GetComponent<MeshCollider>();
            if (mc == null)
            {
                mc = obj.AddComponent<MeshCollider>();
            }
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;
            mc.isTrigger = false;
            mc.enabled = true;

            if (!wallAndStructureColliders.Contains(mc))
            {
                wallAndStructureColliders.Add(mc);
            }
        }
    }

    private void ConfigureManipulableProp(GameObject obj, MeshFilter mf)
    {
        if (obj == null || mf == null || mf.sharedMesh == null) return;

        MeshCollider mc = obj.GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;
            mc.isTrigger = false;
            mc.enabled = true;
        }
        else
        {
            BoxCollider box = obj.GetComponent<BoxCollider>();
            if (box == null) box = obj.AddComponent<BoxCollider>();
            Bounds b = mf.sharedMesh.bounds;
            box.center = b.center;
            box.size = new Vector3(
                Mathf.Max(b.size.x, 0.06f),
                Mathf.Max(b.size.y, 0.06f),
                Mathf.Max(b.size.z, 0.06f)
            );
            box.isTrigger = false;
        }

        Bounds meshBounds = mf.sharedMesh.bounds;
        Vector3 worldSize = Vector3.Scale(meshBounds.size, obj.transform.lossyScale);
        if (worldSize.x < 0.08f || worldSize.y < 0.08f || worldSize.z < 0.08f)
        {
            BoxCollider grabBox = obj.GetComponent<BoxCollider>();
            if (grabBox == null)
            {
                grabBox = obj.AddComponent<BoxCollider>();
                grabBox.center = meshBounds.center;
                Vector3 ls = obj.transform.lossyScale;
                float minX = ls.x > 0.0001f ? Mathf.Max(meshBounds.size.x, 0.10f / ls.x) : meshBounds.size.x;
                float minY = ls.y > 0.0001f ? Mathf.Max(meshBounds.size.y, 0.10f / ls.y) : meshBounds.size.y;
                float minZ = ls.z > 0.0001f ? Mathf.Max(meshBounds.size.z, 0.10f / ls.z) : meshBounds.size.z;
                grabBox.size = new Vector3(minX, minY, minZ);
                grabBox.isTrigger = false;
            }
        }

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = obj.AddComponent<Rigidbody>();
        }
        rb.mass = 0.8f;
        rb.linearDamping = 0.15f;
        rb.angularDamping = 0.15f;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        XRGrabInteractable grab = obj.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            grab = obj.AddComponent<XRGrabInteractable>();
        }

        Collider[] allCols = obj.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in allCols)
        {
            if (c != null && !grab.colliders.Contains(c))
            {
                grab.colliders.Add(c);
            }
            if (c != null && !manipulableColliders.Contains(c))
            {
                manipulableColliders.Add(c);
            }
        }

        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.useDynamicAttach = true;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;
        grab.snapToColliderVolume = true;
        grab.throwOnDetach = true;
        grab.forceGravityOnDetach = true;
        grab.retainTransformParent = true;
        grab.farAttachMode = UnityEngine.XR.Interaction.Toolkit.Attachment.InteractableFarAttachMode.Near;

        Rigidbody capturedRb = rb;
        grab.selectEntered.AddListener(_ =>
        {
            if (capturedRb != null)
            {
                capturedRb.isKinematic = false;
                capturedRb.useGravity = true;
                capturedRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            if (GerenteSom.Instance != null)
            {
                GerenteSom.Instance.PlayClick();
            }
        });

        grab.selectExited.AddListener(_ =>
        {
            if (capturedRb != null)
            {
                capturedRb.isKinematic = false;
                capturedRb.useGravity = true;
            }
        });
    }

    private void ConfigureFuelAndSpecialItems()
    {
        GarrafaCombustivel[] galoes = FindObjectsByType<GarrafaCombustivel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var galao in galoes)
        {
            if (galao == null) continue;
            GameObject obj = galao.gameObject;

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb == null) rb = obj.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            XRGrabInteractable grab = obj.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = obj.AddComponent<XRGrabInteractable>();

            Collider[] cols = obj.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in cols)
            {
                if (c is MeshCollider mc)
                {
                    mc.convex = true;
                }
                if (c != null && !grab.colliders.Contains(c))
                {
                    grab.colliders.Add(c);
                }
                if (c != null && !manipulableColliders.Contains(c))
                {
                    manipulableColliders.Add(c);
                }
            }

            grab.useDynamicAttach = true;
            grab.farAttachMode = UnityEngine.XR.Interaction.Toolkit.Attachment.InteractableFarAttachMode.Near;
            grab.throwOnDetach = true;
            grab.forceGravityOnDetach = true;
        }
    }

    private void LateUpdate()
    {
        if (isCockpitDefenseScene) return;
        if (xrOrigin == null) return;

        EnforcePlayerShipWallCollision();
    }

    /// <summary>
    /// Garante velocidade de caminhada controlada e impede que o jogador atravesse as paredes da nave,
    /// seja andando pelo analógico VR (DynamicMoveProvider) ou movendo a câmera pelo WASD do Simulador XR.
    /// </summary>
    private void EnforcePlayerShipWallCollision()
    {
        Transform rigTr = xrOrigin.transform;
        Vector3 currentHeadFloorPos = GetPlayerHeadFloorPosition();

        if (!hasValidPlayerPosition)
        {
            lastValidHeadFloorPos = currentHeadFloorPos;
            hasValidPlayerPosition = true;
            return;
        }

        // 1. Calcula o deslocamento horizontal da cabeça/corpo no mundo desde o último frame
        Vector3 moveVec = currentHeadFloorPos - lastValidHeadFloorPos;
        Vector3 horizontalMove = new Vector3(moveVec.x, 0f, moveVec.z);
        float moveDist = horizontalMove.magnitude;

        // Limita apenas saltos anômalos por frame, preservando fluidez total de caminhada e rotação
        float maxStep = Mathf.Max(0.01f, walkSpeed * 1.65f * Time.deltaTime);
        if (moveDist > maxStep)
        {
            horizontalMove = (horizontalMove / moveDist) * maxStep;
            moveDist = maxStep;
        }

        float playerRadius = 0.24f;
        float skinWidth = 0.015f;
        Vector3 simulatedPos = new Vector3(lastValidHeadFloorPos.x, currentHeadFloorPos.y, lastValidHeadFloorPos.z);
        Vector3 remainingMove = horizontalMove;

        // 2. Collide-and-Slide em até 3 iterações com CapsuleCastNonAlloc (zero GC por frame)
        for (int iter = 0; iter < 3; iter++)
        {
            float dist = remainingMove.magnitude;
            if (dist < 0.0005f) break;

            Vector3 dir = remainingMove / dist;
            Vector3 pBottom = simulatedPos + Vector3.up * 0.50f;
            Vector3 pTop = simulatedPos + Vector3.up * 1.35f;

            int hitCount = Physics.CapsuleCastNonAlloc(
                pBottom,
                pTop,
                playerRadius,
                dir,
                nonAllocHits,
                dist + skinWidth,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            bool blocked = false;
            RaycastHit closestHit = default;
            float closestDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCol = nonAllocHits[i].collider;
                if (!IsBlockingWallOrFurniture(hitCol)) continue;

                // Ignora piso ou teto (normal predominantemente vertical)
                if (Mathf.Abs(nonAllocHits[i].normal.y) > 0.65f) continue;

                if (nonAllocHits[i].distance < closestDist)
                {
                    closestDist = nonAllocHits[i].distance;
                    closestHit = nonAllocHits[i];
                    blocked = true;
                }
            }

            if (!blocked)
            {
                simulatedPos += remainingMove;
                break;
            }

            float safeAdvance = Mathf.Max(0f, closestDist - skinWidth);
            simulatedPos += dir * safeAdvance;

            float leftoverDist = Mathf.Max(0f, dist - safeAdvance);
            Vector3 leftoverVec = dir * leftoverDist;

            Vector3 wallNormal = new Vector3(closestHit.normal.x, 0f, closestHit.normal.z);
            if (wallNormal.sqrMagnitude > 0.0001f)
            {
                wallNormal.Normalize();
                simulatedPos += wallNormal * 0.002f;
                remainingMove = Vector3.ProjectOnPlane(leftoverVec, wallNormal);
                remainingMove.y = 0f;
            }
            else
            {
                break;
            }
        }

        // 3. Depenetração direta por cápsula em até 3 iterações com OverlapCapsuleNonAlloc (zero GC por frame)
        EnsureDepenetrationCapsule();
        for (int depIter = 0; depIter < 3; depIter++)
        {
            Vector3 pBottom = simulatedPos + Vector3.up * 0.50f;
            Vector3 pTop = simulatedPos + Vector3.up * 1.35f;
            int overlapCount = Physics.OverlapCapsuleNonAlloc(pBottom, pTop, playerRadius, nonAllocOverlaps, ~0, QueryTriggerInteraction.Ignore);
            if (overlapCount == 0) break;

            bool anyPushed = false;
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = nonAllocOverlaps[i];
                if (!IsBlockingWallOrFurniture(col)) continue;

                if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider || (col is MeshCollider mc && mc.convex))
                {
                    if (depenetrationCapsule != null &&
                        Physics.ComputePenetration(
                            depenetrationCapsule, simulatedPos, Quaternion.identity,
                            col, col.transform.position, col.transform.rotation,
                            out Vector3 pushDir, out float pushDist))
                    {
                        Vector3 horizontalPush = new Vector3(pushDir.x, 0f, pushDir.z);
                        if (horizontalPush.sqrMagnitude > 0.0001f && pushDist > 0.0001f)
                        {
                            horizontalPush.Normalize();
                            simulatedPos += horizontalPush * (pushDist + 0.002f);
                            anyPushed = true;
                        }
                    }
                }
            }

            if (!anyPushed) break;
        }

        // 4. Verificação anti-tunneling final por RaycastNonAlloc entre a última posição válida e a nova posição
        Vector3 startCheck = new Vector3(lastValidHeadFloorPos.x, simulatedPos.y + 1.0f, lastValidHeadFloorPos.z);
        Vector3 endCheck = new Vector3(simulatedPos.x, simulatedPos.y + 1.0f, simulatedPos.z);
        Vector3 checkDelta = endCheck - startCheck;
        float checkDist = checkDelta.magnitude;
        if (checkDist > 0.005f)
        {
            int lineHitCount = Physics.RaycastNonAlloc(
                startCheck,
                checkDelta / checkDist,
                nonAllocHits,
                checkDist,
                ~0,
                QueryTriggerInteraction.Ignore
            );
            for (int i = 0; i < lineHitCount; i++)
            {
                if (IsBlockingWallOrFurniture(nonAllocHits[i].collider) && Mathf.Abs(nonAllocHits[i].normal.y) <= 0.65f)
                {
                    simulatedPos = new Vector3(lastValidHeadFloorPos.x, currentHeadFloorPos.y, lastValidHeadFloorPos.z);
                    break;
                }
            }
        }

        // 5. Aplica a correção na raiz do XR Origin
        Vector3 stepCorrection = simulatedPos - currentHeadFloorPos;
        stepCorrection.y = 0f;
        if (stepCorrection.sqrMagnitude > 0.0000001f)
        {
            rigTr.position += stepCorrection;
        }

        // Mantém o centro do CharacterController alinhado horizontalmente com a câmera
        if (characterController != null && mainCameraTr != null)
        {
            Vector3 localCam = rigTr.InverseTransformPoint(mainCameraTr.position);
            characterController.center = new Vector3(localCam.x, characterController.height * 0.5f, localCam.z);
        }

        lastValidHeadFloorPos = new Vector3(simulatedPos.x, rigTr.position.y, simulatedPos.z);
    }

    private bool IsBlockingWallOrFurniture(Collider col)
    {
        if (col == null || !col.enabled || col.isTrigger) return false;
        if (col == depenetrationCapsule) return false;
        if (xrOrigin != null && col.transform.IsChildOf(xrOrigin.transform)) return false;
        if (manipulableColliders.Contains(col)) return false;
        if (col.GetComponentInParent<XRGrabInteractable>() != null) return false;
        if (col.GetComponentInParent<Canvas>() != null) return false;

        string n = col.gameObject.name;
        if (n.IndexOf("Chao", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Teto", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return wallAndStructureColliders.Contains(col);
    }
}
