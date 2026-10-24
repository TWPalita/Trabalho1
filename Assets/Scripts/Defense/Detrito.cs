using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Representa um objeto espacial / detrito com um número associado no Sistema de Defesa.
/// Mantém o número posicionado diretamente abaixo do objeto enquanto ambos flutuam juntos,
/// garantindo visibilidade clara para o jogador na cabine do piloto em VR.
/// </summary>
public class Detrito : MonoBehaviour
{
    [Header("Exibição do Número")]
    [Tooltip("Distância vertical do número abaixo do centro do objeto")]
    [SerializeField] private float numberYOffset = 1.65f;
    [SerializeField] private TMP_Text textoDoNumero;
    [SerializeField] private Transform labelContainer;
    [SerializeField] private Image badgeBackground;
    [SerializeField] private Image badgeBorder;
    [SerializeField] private bool showNumberLabel = false;

    [Header("Escala para Câmera Fixa na Cabine")]
    [SerializeField] private Vector3 cockpitViewScale = new Vector3(1.75f, 1.75f, 1.75f);

    [Header("Componentes")]
    [SerializeField] private Renderer meshRenderer;
    [SerializeField] private XRSimpleInteractable xrInteractable;
    [SerializeField] private DebrisMovement debrisMovement;

    [Header("Comportamento Espacial")]
    [SerializeField] private float floatSpeed = 1.0f;
    [SerializeField] private float floatAmplitude = 0.35f;
    [SerializeField] private float rotationSpeed = 25.0f;
    [SerializeField] private bool billboardText = true;

    [Header("Cores e Destaque VR")]
    [SerializeField] private Color normalTextColor = new Color(0.9f, 0.98f, 1f, 1f);
    [SerializeField] private Color hoverTextColor = new Color(0.3f, 1f, 0.95f, 1f);
    [SerializeField] private Color badgeNormalColor = new Color(0.02f, 0.05f, 0.12f, 0.9f);
    [SerializeField] private Color badgeHoverColor = new Color(0.08f, 0.35f, 0.6f, 0.95f);
    [SerializeField] private Color borderNormalColor = new Color(0.18f, 0.72f, 0.95f, 0.75f);
    [SerializeField] private Color borderHoverColor = new Color(0.35f, 1f, 0.95f, 1f);
    [SerializeField] private Color hoverHighlightColor = new Color(0.3f, 1f, 0.9f, 1f);

    public int Number { get; private set; }
    public bool IsHovered { get; private set; } = false;
    public bool IsHit { get; private set; } = false;

    public void MarkHit() => IsHit = true;
    public void SetShowNumberLabel(bool show)
    {
        showNumberLabel = show;
        if (labelContainer != null) labelContainer.gameObject.SetActive(show);
    }

    private Vector3 initialPosition;
    private float timeOffset;
    private GerenteDefesa manager;
    private Material targetMaterial;
    private Color originalMeshColor = Color.white;
    private Transform cachedCamTransform;
    private float currentBadgeScale = 0.011f;
    private float spawnAnimProgress = 0f;

    private static Mesh cachedMeteorMesh;
    private static Material cachedMeteorMaterial;

    private void EnsureMeteorMeshAndMaterial()
    {
        MeshFilter mf = GetComponentInChildren<MeshFilter>();
        if (mf != null)
        {
#if UNITY_EDITOR
            if (cachedMeteorMesh == null)
            {
                cachedMeteorMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Componentes/meteoro/mesh.obj");
                if (cachedMeteorMesh == null)
                {
                    Object[] allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Componentes/meteoro/mesh.obj");
                    foreach (Object asset in allAssets)
                    {
                        if (asset is Mesh m)
                        {
                            cachedMeteorMesh = m;
                            break;
                        }
                    }
                }
            }
            if (cachedMeteorMaterial == null)
            {
                cachedMeteorMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Componentes/meteoro/Materials/albedo.mat");
            }
#endif
            if (cachedMeteorMesh != null && (mf.sharedMesh == null || mf.sharedMesh.name.IndexOf("Cube", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                mf.sharedMesh = cachedMeteorMesh;
            }
        }

        if (meshRenderer == null) meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer != null && cachedMeteorMaterial != null)
        {
            if (meshRenderer.sharedMaterial == null ||
                meshRenderer.sharedMaterial.name.IndexOf("Lit", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                meshRenderer.sharedMaterial.name.IndexOf("Default", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                meshRenderer.sharedMaterial = cachedMeteorMaterial;
            }
        }
    }

    private void Awake()
    {
        if (debrisMovement == null) debrisMovement = GetComponent<DebrisMovement>();
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<Renderer>();
        EnsureMeteorMeshAndMaterial();

        if (xrInteractable == null)
        {
            xrInteractable = GetComponent<XRSimpleInteractable>();
            if (xrInteractable == null)
            {
                xrInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            }
        }

        if (meshRenderer != null)
        {
            targetMaterial = meshRenderer.material;
            originalMeshColor = targetMaterial.color;
            ApplyIdleSpaceRelief();
        }

        // Ajusta a escala do detrito para leitura e mira confortáveis a partir da câmera fixa na cadeira
        if (transform.localScale.x < cockpitViewScale.x)
        {
            transform.localScale = cockpitViewScale;
        }
        if (numberYOffset < 1.65f)
        {
            numberYOffset = 1.65f;
        }

        // Configura o colisor para cobrir tanto o objeto quanto a etiqueta numérica abaixo dele
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            col.center = new Vector3(0f, -0.45f, 0f);
            col.size = new Vector3(1.55f, 2.0f, 1.55f);
        }

        SetupNumberLabel();
    }

    private void SetupNumberLabel()
    {
        // 1. Localiza ou cria o Canvas da etiqueta numérica
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("NumberCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObj.AddComponent<CanvasScaler>();
        }

        labelContainer = canvas.transform;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 2.5f;

        RectTransform canvasRT = canvas.GetComponent<RectTransform>();
        if (canvasRT != null)
        {
            canvasRT.sizeDelta = new Vector2(174f, 92f);
            canvasRT.localScale = new Vector3(0.011f, 0.011f, 0.011f);
            canvasRT.anchoredPosition = Vector2.zero;
            canvasRT.localPosition = new Vector3(0f, -numberYOffset / Mathf.Max(1f, transform.localScale.y), 0f);
        }

        // 2. Moldura e Fundo tático de mira (HUD de transponder espacial)
        Transform borderTransform = canvas.transform.Find("BadgeBorder");
        if (borderTransform == null)
        {
            GameObject borderObj = new GameObject("BadgeBorder");
            borderObj.transform.SetParent(canvas.transform, false);
            borderObj.transform.SetAsFirstSibling();
            badgeBorder = borderObj.AddComponent<Image>();
            badgeBorder.color = borderNormalColor;

            RectTransform borderRT = borderObj.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = new Vector2(8f, 8f);
            borderRT.anchoredPosition = Vector2.zero;
        }
        else
        {
            badgeBorder = borderTransform.GetComponent<Image>();
            if (badgeBorder != null) badgeBorder.color = borderNormalColor;
        }

        Transform bgTransform = canvas.transform.Find("BadgeBackground");
        if (bgTransform == null)
        {
            GameObject badge = new GameObject("BadgeBackground");
            badge.transform.SetParent(canvas.transform, false);
            badge.transform.SetSiblingIndex(1);
            badgeBackground = badge.AddComponent<Image>();
            badgeBackground.color = badgeNormalColor;

            RectTransform bgRT = badge.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            bgRT.anchoredPosition = Vector2.zero;
        }
        else
        {
            badgeBackground = bgTransform.GetComponent<Image>();
            if (badgeBackground != null) badgeBackground.color = badgeNormalColor;
        }

        if (badgeBorder != null) badgeBorder.raycastTarget = false;
        if (badgeBackground != null) badgeBackground.raycastTarget = false;

        // 3. Localiza e configura o TMP_Text
        if (textoDoNumero == null)
        {
            textoDoNumero = canvas.GetComponentInChildren<TMP_Text>();
        }

        if (textoDoNumero != null)
        {
            textoDoNumero.alignment = TextAlignmentOptions.Center;
            textoDoNumero.fontSize = 66;
            textoDoNumero.fontStyle = FontStyles.Bold;
            textoDoNumero.color = normalTextColor;
            textoDoNumero.raycastTarget = false;

            RectTransform textRT = textoDoNumero.rectTransform;
            if (textRT != null)
            {
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.sizeDelta = Vector2.zero;
                textRT.anchoredPosition = Vector2.zero;
            }
        }

        if (labelContainer != null)
        {
            labelContainer.gameObject.SetActive(showNumberLabel);
        }
    }

    private void OnEnable()
    {
        if (xrInteractable != null)
        {
            xrInteractable.firstHoverEntered.AddListener(OnXRHoverEntered);
            xrInteractable.lastHoverExited.AddListener(OnXRHoverExited);
            xrInteractable.selectEntered.AddListener(OnXRSelected);
            xrInteractable.activated.AddListener(OnXRActivated);
        }
    }

    private void OnDisable()
    {
        if (xrInteractable != null)
        {
            xrInteractable.firstHoverEntered.RemoveListener(OnXRHoverEntered);
            xrInteractable.lastHoverExited.RemoveListener(OnXRHoverExited);
            xrInteractable.selectEntered.RemoveListener(OnXRSelected);
            xrInteractable.activated.RemoveListener(OnXRActivated);
        }
    }

    public void Init(int value, GerenteDefesa defManager)
    {
        Number = value;
        manager = defManager;
        IsHit = false;
        IsHovered = false;
        spawnAnimProgress = 0f;
        initialPosition = transform.position;
        timeOffset = Random.Range(0f, 10f);

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        SetHighlight(false);

        if (textoDoNumero != null)
        {
            textoDoNumero.text = value.ToString();
        }
    }

    public void DeactivateToPool()
    {
        if (errorFlashRoutine != null)
        {
            StopCoroutine(errorFlashRoutine);
            errorFlashRoutine = null;
        }
        IsHit = true;
        IsHovered = false;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        // Se possui DebrisMovement ativo, o movimento espacial em direção à nave é controlado por ele
        if (debrisMovement != null) return;

        // Flutuação espacial do objeto (sobe e desce em seno)
        float yOffset = Mathf.Sin((Time.time + timeOffset) * floatSpeed) * floatAmplitude;
        transform.position = initialPosition + new Vector3(0f, yOffset, 0f);

        // Rotação sutil do detrito em torno do próprio eixo
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }

    private void LateUpdate()
    {
        if (spawnAnimProgress < 1f)
        {
            spawnAnimProgress = Mathf.Min(1f, spawnAnimProgress + Time.deltaTime * 4.5f);
            float ease = 1f - Mathf.Pow(1f - spawnAnimProgress, 3f);
            transform.localScale = Vector3.LerpUnclamped(cockpitViewScale * 0.25f, cockpitViewScale, ease);
        }

        if (!showNumberLabel) return;

        // Garante que o número fique fixado diretamente abaixo do objeto,
        // flutuando junto com ele mas sem girar em órbita quando o detrito roda.
        if (labelContainer != null)
        {
            labelContainer.position = transform.position - Vector3.up * numberYOffset;

            float targetBadgeScale = IsHovered ? 0.0128f : 0.011f;
            currentBadgeScale = Mathf.Lerp(currentBadgeScale, targetBadgeScale, Time.deltaTime * 14f);
            float parentScale = Mathf.Max(0.25f, transform.localScale.x);
            float compScale = (currentBadgeScale * cockpitViewScale.x) / parentScale;
            labelContainer.localScale = new Vector3(compScale, compScale, compScale);

            // Mantém a etiqueta de número sempre voltada para os olhos do jogador na cabine VR
            if (billboardText)
            {
                if (cachedCamTransform == null && Camera.main != null)
                {
                    cachedCamTransform = Camera.main.transform;
                }
                if (cachedCamTransform != null)
                {
                    Vector3 lookDir = labelContainer.position - cachedCamTransform.position;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        labelContainer.rotation = Quaternion.LookRotation(lookDir);
                    }
                }
            }
        }
    }

    private void OnXRHoverEntered(HoverEnterEventArgs args)
    {
        SetHighlight(true);
    }

    private void OnXRHoverExited(HoverExitEventArgs args)
    {
        SetHighlight(false);
    }

    private void OnXRSelected(SelectEnterEventArgs args)
    {
        TriggerHit();
    }

    private void OnXRActivated(ActivateEventArgs args)
    {
        TriggerHit();
    }

    private Coroutine errorFlashRoutine;

    private void TriggerHit()
    {
        if (IsHit) return;

        if (manager != null)
        {
            manager.OnDebrisHit(this);
        }
    }

    /// <summary>
    /// Feedback visual rápido (vermelho) para tiro em resposta incorreta sem destruir o detrito
    /// </summary>
    public void FlashError()
    {
        if (errorFlashRoutine != null) StopCoroutine(errorFlashRoutine);
        errorFlashRoutine = StartCoroutine(ErrorFlashRoutine());
    }

    private System.Collections.IEnumerator ErrorFlashRoutine()
    {
        if (badgeBorder != null) badgeBorder.color = new Color(1f, 0.2f, 0.2f, 1f);
        if (badgeBackground != null) badgeBackground.color = new Color(0.85f, 0.1f, 0.1f, 0.95f);
        if (textoDoNumero != null) textoDoNumero.color = new Color(1f, 0.3f, 0.3f, 1f);

        yield return new WaitForSeconds(0.45f);

        if (badgeBorder != null) badgeBorder.color = borderNormalColor;
        if (badgeBackground != null) badgeBackground.color = badgeNormalColor;
        if (textoDoNumero != null) textoDoNumero.color = normalTextColor;
    }

    private void ApplyIdleSpaceRelief()
    {
        if (targetMaterial == null) return;
        if (targetMaterial.HasProperty("_EmissionColor"))
        {
            targetMaterial.SetColor("_EmissionColor", new Color(0.04f, 0.08f, 0.14f, 1f));
            targetMaterial.EnableKeyword("_EMISSION");
        }
    }

    public void SetHighlight(bool highlighted)
    {
        IsHovered = highlighted;

        if (targetMaterial != null)
        {
            targetMaterial.color = highlighted ? hoverHighlightColor : originalMeshColor;
            if (targetMaterial.HasProperty("_EmissionColor"))
            {
                targetMaterial.SetColor("_EmissionColor", highlighted ? hoverHighlightColor * 0.8f : new Color(0.04f, 0.08f, 0.14f, 1f));
                targetMaterial.EnableKeyword("_EMISSION");
            }
        }

        if (textoDoNumero != null)
        {
            textoDoNumero.color = highlighted ? hoverTextColor : normalTextColor;
        }

        if (badgeBorder != null)
        {
            badgeBorder.color = highlighted ? borderHoverColor : borderNormalColor;
        }

        if (badgeBackground != null)
        {
            badgeBackground.color = highlighted ? badgeHoverColor : badgeNormalColor;
        }
    }

    private void OnDestroy()
    {
        if (targetMaterial != null)
        {
            Destroy(targetMaterial);
            targetMaterial = null;
        }
    }

    public void Explode(bool isCorrect = true)
    {
        IsHit = true;

        // Desativa o colisor e a etiqueta de número imediatamente
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        if (labelContainer != null) labelContainer.gameObject.SetActive(false);

        if (isCorrect)
        {
            if (GerenteSom.Instance != null)
            {
                GerenteSom.Instance.PlayHit();
            }
            SpawnExplosionFragments();
            DeactivateToPool();
        }
        else
        {
            StartCoroutine(HitFeedbackAndDestroyRoutine());
        }
    }

    private void SpawnExplosionFragments()
    {
        Vector3 center = meshRenderer != null ? meshRenderer.bounds.center : transform.position;
        Vector3 boundsSize = meshRenderer != null ? meshRenderer.bounds.size : Vector3.one * 1.2f;
        float baseSize = Mathf.Clamp(boundsSize.magnitude * 0.16f, 0.12f, 0.45f);

        Material refMat = meshRenderer != null ? meshRenderer.sharedMaterial : targetMaterial;
        MeshFilter mf = GetComponentInChildren<MeshFilter>();
        Mesh refMesh = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh : cachedMeteorMesh;

        DebrisShatterEffect.PlayAt(center, refMat, originalMeshColor, baseSize, refMesh);
    }

    private System.Collections.IEnumerator HitFeedbackAndDestroyRoutine()
    {
        SetHighlight(true);
        if (targetMaterial != null)
        {
            targetMaterial.color = Color.white;
            if (targetMaterial.HasProperty("_EmissionColor"))
            {
                targetMaterial.SetColor("_EmissionColor", Color.white * 2.0f);
                targetMaterial.EnableKeyword("_EMISSION");
            }
        }
        yield return new WaitForSeconds(0.1f);
        DeactivateToPool();
    }
}

/// <summary>
/// Efeito visual de explosão em pedaços 3D quando o detrito correto é destruído.
/// Usa Object Pooling estático para zero alocações e zero travadas durante o combate.
/// </summary>
public class DebrisShatterEffect : MonoBehaviour
{
    private struct PieceData
    {
        public Transform transform;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public Vector3 initialScale;
    }

    private const int PieceCount = 14;
    private const float Duration = 0.95f;

    private PieceData[] pieces;
    private Transform shockwaveTransform;
    private Material fragmentMat;
    private Material glowMat;
    private Material shockwaveMat;
    private float elapsed = 0f;
    private bool isBuilt = false;

    private static Shader cachedUnlitShader;
    private static Shader cachedLitShader;
    private static Mesh cachedSphereMesh;
    private static Mesh cachedCubeMesh;
    private static readonly System.Collections.Generic.List<DebrisShatterEffect> s_Pool = new System.Collections.Generic.List<DebrisShatterEffect>(4);

    public static void PrewarmPool(Material defaultMat = null, Mesh defaultMesh = null)
    {
        EnsureCachedPrimitiveAssets();

        // Remove referências destruídas na troca de cena
        for (int i = s_Pool.Count - 1; i >= 0; i--)
        {
            if (s_Pool[i] == null) s_Pool.RemoveAt(i);
        }

        while (s_Pool.Count < 2)
        {
            GameObject root = new GameObject("ExplosaoDetrito_Pooled");
            DebrisShatterEffect fx = root.AddComponent<DebrisShatterEffect>();
            fx.BuildReusableHierarchy(defaultMat, Color.gray, defaultMesh);
            root.SetActive(false);
            s_Pool.Add(fx);
        }
    }

    public static void PlayAt(Vector3 worldPos, Material baseMat, Color fallbackColor, float baseSize, Mesh customMesh = null)
    {
        EnsureCachedPrimitiveAssets();

        DebrisShatterEffect chosen = null;
        for (int i = s_Pool.Count - 1; i >= 0; i--)
        {
            if (s_Pool[i] == null)
            {
                s_Pool.RemoveAt(i);
                continue;
            }
            if (!s_Pool[i].gameObject.activeSelf)
            {
                chosen = s_Pool[i];
                break;
            }
        }

        if (chosen == null)
        {
            GameObject root = new GameObject("ExplosaoDetrito_Pooled");
            chosen = root.AddComponent<DebrisShatterEffect>();
            chosen.BuildReusableHierarchy(baseMat, fallbackColor, customMesh);
            s_Pool.Add(chosen);
        }

        chosen.transform.position = worldPos;
        chosen.TriggerReusableExplosion(baseMat, fallbackColor, baseSize, customMesh);
    }

    private static void EnsureCachedPrimitiveAssets()
    {
        if (cachedUnlitShader == null)
        {
            cachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        }
        if (cachedLitShader == null)
        {
            cachedLitShader = Shader.Find("Universal Render Pipeline/Lit") ?? cachedUnlitShader;
        }
        if (cachedSphereMesh == null)
        {
            GameObject tmpSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            MeshFilter mf = tmpSphere.GetComponent<MeshFilter>();
            if (mf != null) cachedSphereMesh = mf.sharedMesh;
            Destroy(tmpSphere);
        }
        if (cachedCubeMesh == null)
        {
            GameObject tmpCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            MeshFilter mf = tmpCube.GetComponent<MeshFilter>();
            if (mf != null) cachedCubeMesh = mf.sharedMesh;
            Destroy(tmpCube);
        }
    }

    private void BuildReusableHierarchy(Material baseMat, Color fallbackColor, Mesh customMesh)
    {
        if (isBuilt) return;
        isBuilt = true;

        EnsureCachedPrimitiveAssets();

        fragmentMat = baseMat != null ? new Material(baseMat) : new Material(cachedLitShader) { color = fallbackColor };

        glowMat = new Material(cachedUnlitShader);
        Color glowCol = new Color(1f, 0.55f, 0.15f, 1f);
        glowMat.color = glowCol;
        if (glowMat.HasProperty("_BaseColor")) glowMat.SetColor("_BaseColor", glowCol);
        if (glowMat.HasProperty("_Color")) glowMat.SetColor("_Color", glowCol);

        shockwaveMat = new Material(cachedUnlitShader);
        Color flashColor = new Color(1f, 0.85f, 0.4f, 0.85f);
        shockwaveMat.color = flashColor;
        if (shockwaveMat.HasProperty("_BaseColor")) shockwaveMat.SetColor("_BaseColor", flashColor);
        if (shockwaveMat.HasProperty("_Color")) shockwaveMat.SetColor("_Color", flashColor);

        GameObject flashObj = new GameObject("ClarãoExplosao");
        flashObj.transform.SetParent(transform, false);
        MeshFilter flashMf = flashObj.AddComponent<MeshFilter>();
        flashMf.sharedMesh = cachedSphereMesh;
        MeshRenderer flashMr = flashObj.AddComponent<MeshRenderer>();
        flashMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flashMr.receiveShadows = false;
        flashMr.sharedMaterial = shockwaveMat;
        shockwaveTransform = flashObj.transform;

        pieces = new PieceData[PieceCount];
        for (int i = 0; i < PieceCount; i++)
        {
            GameObject piece = new GameObject($"Pedaco_{i}");
            piece.transform.SetParent(transform, false);

            MeshFilter pieceMf = piece.AddComponent<MeshFilter>();
            pieceMf.sharedMesh = (i % 3 == 0 && customMesh != null)
                ? customMesh
                : ((i % 2 == 0) ? cachedCubeMesh : cachedSphereMesh);

            MeshRenderer r = piece.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = (i % 4 == 0) ? glowMat : fragmentMat;

            pieces[i] = new PieceData
            {
                transform = piece.transform,
                meshFilter = pieceMf,
                meshRenderer = r
            };
        }
    }

    public void TriggerReusableExplosion(Material baseMat, Color fallbackColor, float baseSize, Mesh customMesh)
    {
        BuildReusableHierarchy(baseMat, fallbackColor, customMesh);
        elapsed = 0f;

        if (shockwaveTransform != null)
        {
            shockwaveTransform.localPosition = Vector3.zero;
            shockwaveTransform.localScale = Vector3.one * (baseSize * 1.4f);
            shockwaveTransform.gameObject.SetActive(true);
        }

        for (int i = 0; i < PieceCount; i++)
        {
            PieceData p = pieces[i];
            if (p.meshFilter != null && i % 3 == 0 && customMesh != null)
            {
                p.meshFilter.sharedMesh = customMesh;
            }

            Vector3 dir = Random.onUnitSphere;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;
            dir.Normalize();

            p.transform.localPosition = dir * Random.Range(0.05f, baseSize * 0.6f);
            p.transform.localRotation = Random.rotation;

            Vector3 scale = new Vector3(
                baseSize * Random.Range(0.45f, 1.35f),
                baseSize * Random.Range(0.35f, 1.15f),
                baseSize * Random.Range(0.45f, 1.35f)
            );
            p.transform.localScale = scale;

            float speed = Random.Range(3.8f, 9.5f);
            p.velocity = dir * speed;
            p.angularVelocity = new Vector3(
                Random.Range(-520f, 520f),
                Random.Range(-520f, 520f),
                Random.Range(-520f, 520f)
            );
            p.initialScale = scale;
            pieces[i] = p;
        }

        gameObject.SetActive(true);
    }

    public void Init(Material baseMat, Color fallbackColor, float baseSize, Mesh customMesh = null)
    {
        TriggerReusableExplosion(baseMat, fallbackColor, baseSize, customMesh);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        elapsed += dt;

        float t = Mathf.Clamp01(elapsed / Duration);

        // Clarão central expande e some rapidamente nos primeiros 0.2s
        if (shockwaveTransform != null)
        {
            if (elapsed < 0.2f)
            {
                float st = elapsed / 0.2f;
                shockwaveTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 2.6f, st);
            }
            else if (shockwaveTransform.gameObject.activeSelf)
            {
                shockwaveTransform.gameObject.SetActive(false);
            }
        }

        // Atualiza posição, rotação e encolhimento dos pedaços no espaço
        float scaleFactor = 1f - (t * t);
        if (pieces != null)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                Transform tr = pieces[i].transform;
                if (tr == null) continue;

                tr.localPosition += pieces[i].velocity * dt;
                pieces[i].velocity *= (1f - 1.2f * dt); // Leve desaceleração espacial
                tr.Rotate(pieces[i].angularVelocity * dt, Space.Self);
                tr.localScale = pieces[i].initialScale * scaleFactor;
            }
        }

        if (elapsed >= Duration)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (fragmentMat != null) Destroy(fragmentMat);
        if (glowMat != null) Destroy(glowMat);
        if (shockwaveMat != null) Destroy(shockwaveMat);
    }
}

public class Debris : Detrito { }

