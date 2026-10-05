using UnityEngine;

/// <summary>
/// Controla o movimento lateral dos detritos espaciais através do campo de visão da cabine/cockpit,
/// mantendo distância segura e estável à frente da nave com rotação lenta, velocidades variadas
/// e oscilações orgânicas sutis em gravidade zero.
/// Recicla o detrito continuamente de um lado a outro do visor espacial.
/// </summary>
public class DebrisMovement : MonoBehaviour
{
    [Header("Configurações Principais de Movimento Lateral")]
    [Tooltip("Velocidade média de deslocamento lateral no espaço")]
    [SerializeField] private float speed = 1.3f;

    [Tooltip("Velocidade de rotação contínua do detrito")]
    [SerializeField] private float rotationSpeed = 20.0f;

    [Tooltip("Sentido lateral: +1 para Direita (→), -1 para Esquerda (←)")]
    [SerializeField] private float moveDirection = 1f;

    [Tooltip("Distância horizontal limite na visão da cabine para reciclagem e reentrada contínua")]
    [SerializeField] private float horizontalBound = 10.5f;

    [Tooltip("Referência de orientação da cabine/nave (XR Origin, Assento ou Câmera)")]
    [SerializeField] private Transform cockpitReference;

    [Header("Variação Natural (Legível e confortável em VR)")]
    [Tooltip("Adiciona pequenas diferenças de velocidade e oscilações sutis")]
    [SerializeField] private bool addNaturalVariation = true;
    [SerializeField] private float speedVariationRange = 0.35f;

    [Tooltip("Oscilação vertical suave (~10% do movimento principal)")]
    [SerializeField] private float verticalWobbleAmplitude = 0.2f;

    [Tooltip("Flutuação cósmica em profundidade (~10% suave, sem avançar contra a cabine)")]
    [SerializeField] private float depthWobbleAmplitude = 0.35f;
    [SerializeField] private float wobbleFrequency = 0.8f;

    [Header("Área de Jogo / Posição de Spawn")]
    [SerializeField] private Vector3 initialSpawnPosition;
    [SerializeField] private bool autoStoreInitialPosition = true;

    // Campos de compatibilidade para serialização existente
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 movementDirection = Vector3.right;
    [SerializeField] private float minDistanceToReset = 3.8f;
    [SerializeField] private float lateralWobbleAmplitude = 0.25f;
    [SerializeField] private float lateralWobbleFrequency = 0.8f;

    private float effectiveSpeed;
    private float effectiveRotationSpeed;
    private Vector3 rotationAxis;
    private float timeOffset;
    private Vector3 currentSpawnPosition;
    private Vector3 initialLocalPosition;
    private bool isInitialized = false;

    // Propriedades públicas para configuração no Inspector ou via código
    public float Speed { get => speed; set => speed = value; }
    public float RotationSpeed { get => rotationSpeed; set => rotationSpeed = value; }
    public Transform Target { get => cockpitReference != null ? cockpitReference : target; set { target = value; cockpitReference = value; } }
    public Vector3 MovementDirection { get => movementDirection; set => movementDirection = value; }
    public Vector3 InitialSpawnPosition { get => initialSpawnPosition; set => initialSpawnPosition = value; }
    public float MoveDirection { get => moveDirection; set => moveDirection = value; }
    public float HorizontalBound { get => horizontalBound; set => horizontalBound = value; }
    public Transform CockpitReference { get => cockpitReference; set => cockpitReference = value; }

    private void Awake()
    {
        FindCockpitReference();
        InitializeParameters();
    }

    private void Start()
    {
        FindCockpitReference();
    }

    private void FindCockpitReference()
    {
        if (cockpitReference != null) return;

        if (target != null)
        {
            cockpitReference = target;
            return;
        }

        AssentoCockpit cockpit = FindAnyObjectByType<AssentoCockpit>();
        if (cockpit != null)
        {
            cockpitReference = cockpit.transform;
            return;
        }

        var origin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null)
        {
            cockpitReference = origin.transform;
            return;
        }

        if (Camera.main != null)
        {
            cockpitReference = Camera.main.transform;
            return;
        }

        GameObject painel = GameObject.Find("Painel");
        if (painel != null)
        {
            cockpitReference = painel.transform;
        }
    }

    /// <summary>
    /// Configura a posição inicial de spawn e define a direção de travessia lateral
    /// </summary>
    public void SetSpawnPoint(Vector3 spawnPos, int spawnIndex = -1)
    {
        FindCockpitReference();

        initialSpawnPosition = spawnPos;
        currentSpawnPosition = spawnPos;
        transform.position = spawnPos;

        initialLocalPosition = cockpitReference != null
            ? cockpitReference.InverseTransformPoint(spawnPos)
            : spawnPos;

        // Se o índice de spawn não foi fornecido, deduz a partir da posição dos spawn points
        if (spawnIndex < 0)
        {
            spawnIndex = InferSpawnIndex(initialLocalPosition);
        }

        // Configura a direção lateral alternada conforme o padrão solicitado:
        // SpawnPoint_1 (0): → (+1, para a direita)
        // SpawnPoint_2 (1): ← (-1, para a esquerda)
        // SpawnPoint_3 (2): → (+1, para a direita)
        // SpawnPoint_4 (3): ← (-1, para a esquerda)
        // SpawnPoint_5 (4): → (+1, para a direita)
        moveDirection = (spawnIndex % 2 == 0) ? 1f : -1f;

        InitializeParameters();
    }

    private int InferSpawnIndex(Vector3 pos)
    {
        // Identifica o ponto de spawn com base nas coordenadas dos 5 pontos
        // SP1: (-5.5, 5.8) -> 0
        // SP2: (+5.5, 5.8) -> 1
        // SP3: ( 0.0, 4.6) -> 2
        // SP4: (-6.5, 3.4) -> 3
        // SP5: (+6.5, 3.4) -> 4
        if (pos.y > 5.0f)
        {
            return (pos.x < 0f) ? 0 : 1;
        }
        else if (pos.y > 4.0f)
        {
            return 2;
        }
        else
        {
            return (pos.x < 0f) ? 3 : 4;
        }
    }

    public void InitializeParameters()
    {
        FindCockpitReference();

        if (autoStoreInitialPosition && initialSpawnPosition == Vector3.zero)
        {
            initialSpawnPosition = transform.position;
            currentSpawnPosition = transform.position;
            initialLocalPosition = cockpitReference != null 
                ? cockpitReference.InverseTransformPoint(transform.position) 
                : transform.position;
            moveDirection = (initialLocalPosition.x <= 0f) ? 1f : -1f;
        }

        timeOffset = Random.Range(0f, 100f);

        if (addNaturalVariation)
        {
            // Velocidade variada para que cada detrito tenha seu próprio ritmo
            effectiveSpeed = Mathf.Max(0.7f, speed + Random.Range(-speedVariationRange, speedVariationRange));
            effectiveRotationSpeed = rotationSpeed * Random.Range(0.8f, 1.25f);

            // Rotação suave em eixo tridimensional aleatório
            rotationAxis = new Vector3(
                Random.Range(0.2f, 1f),
                Random.Range(0.5f, 1f),
                Random.Range(0.1f, 0.4f)
            ).normalized;
        }
        else
        {
            effectiveSpeed = speed;
            effectiveRotationSpeed = rotationSpeed;
            rotationAxis = Vector3.up;
        }

        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized) return;

        // 1. Determina os eixos relativos à orientação da nave/cockpit
        Vector3 lateralAxis = (cockpitReference != null) ? cockpitReference.right : Vector3.right;
        Vector3 forwardAxis = (cockpitReference != null) ? cockpitReference.forward : Vector3.forward;
        Vector3 upAxis = (cockpitReference != null) ? cockpitReference.up : Vector3.up;

        // 2. Movimento principal: 90% deslocamento lateral atravessando o campo de visão
        Vector3 lateralDisplacement = lateralAxis * (moveDirection * effectiveSpeed * Time.deltaTime);

        // 3. Flutuações cósmicas sutis (~10% variação suave de altura e profundidade, sem avançar na cabine)
        Vector3 organicWobble = Vector3.zero;
        if (addNaturalVariation)
        {
            float verticalWobble = Mathf.Sin((Time.time + timeOffset) * wobbleFrequency) * (verticalWobbleAmplitude * Time.deltaTime);
            float depthWobble = Mathf.Cos((Time.time + timeOffset) * (wobbleFrequency * 0.75f)) * (depthWobbleAmplitude * Time.deltaTime);
            organicWobble = (upAxis * verticalWobble) + (forwardAxis * depthWobble);
        }

        transform.position += lateralDisplacement + organicWobble;

        // 4. Rotação contínua suave no próprio eixo
        transform.Rotate(rotationAxis, effectiveRotationSpeed * Time.deltaTime, Space.Self);

        // 5. Verificação da área visível e reciclagem contínua
        CheckPlayAreaBounds();
    }

    /// <summary>
    /// Verifica se o detrito completou a travessia e saiu da área visível da cabine
    /// </summary>
    private void CheckPlayAreaBounds()
    {
        Vector3 localPos = (cockpitReference != null) 
            ? cockpitReference.InverseTransformPoint(transform.position) 
            : transform.position;

        bool outOfBounds = false;

        // Se está se movendo para a direita (+1) e ultrapassou a borda direita
        if (moveDirection > 0f && localPos.x >= horizontalBound)
        {
            outOfBounds = true;
        }
        // Se está se movendo para a esquerda (-1) e ultrapassou a borda esquerda
        else if (moveDirection < 0f && localPos.x <= -horizontalBound)
        {
            outOfBounds = true;
        }

        if (outOfBounds)
        {
            ResetToPlayArea();
        }
    }

    /// <summary>
    /// Recicla o detrito reposicionando-o na borda oposta para iniciar nova travessia
    /// </summary>
    public void ResetToPlayArea()
    {
        Vector3 localPos = (cockpitReference != null) 
            ? cockpitReference.InverseTransformPoint(transform.position) 
            : transform.position;

        // Se estava indo para a direita (+1), reentra pela borda esquerda (-horizontalBound)
        // Se estava indo para a esquerda (-1), reentra pela borda direita (+horizontalBound)
        float entryX = (moveDirection > 0f) ? -horizontalBound : horizontalBound;
        float xOffset = (moveDirection > 0f) ? Random.Range(-0.6f, 0f) : Random.Range(0f, 0.6f);
        localPos.x = entryX + xOffset;

        // Mantém a altura e profundidade de spawn correspondentes ao seu tier de visualização
        float yOffset = addNaturalVariation ? Random.Range(-0.2f, 0.2f) : 0f;
        float zOffset = addNaturalVariation ? Random.Range(-0.3f, 0.3f) : 0f;

        localPos.y = initialLocalPosition.y + yOffset;
        localPos.z = initialLocalPosition.z + zOffset;

        transform.position = (cockpitReference != null) 
            ? cockpitReference.TransformPoint(localPos) 
            : localPos;

        // Atualiza pequenas variações de velocidade e rotação para manter ciclo vivo
        if (addNaturalVariation)
        {
            effectiveSpeed = Mathf.Max(0.7f, speed + Random.Range(-speedVariationRange, speedVariationRange));
            effectiveRotationSpeed = rotationSpeed * Random.Range(0.8f, 1.25f);
        }
    }
}

public class MovimentoDetrito : DebrisMovement { }
