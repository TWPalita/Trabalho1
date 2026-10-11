using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Controla a sequência de introdução narrativa na DefenseScene antes do início da batalha espacial:
/// 1. Exibe o Canvas de Protocolo de Interceptação (explicação matemática das coordenadas dos detritos);
/// 2. Exibe o Canvas de Controle da Arma com a arma 3D física disponível no console em frente ao jogador;
/// 3. Bloqueia os disparos até que o jogador clique em 'INICIAR DEFESA';
/// 4. Inicia a defesa espacial e libera os disparos.
/// </summary>
public class IntroducaoDefesaVR : MonoBehaviour
{
    public static IntroducaoDefesaVR Instance { get; private set; }

    [Header("Canvases de Instrução (World Space)")]
    [SerializeField] private GameObject canvas1Protocolo;
    [SerializeField] private GameObject canvas2Arma;
    [SerializeField] private CanvasGroup group1;
    [SerializeField] private CanvasGroup group2;

    [Header("Arma Física do Cockpit")]
    [SerializeField] private GameObject physicalWeaponObj;
    [SerializeField] private Transform weaponSpawnPoint;

    private bool isIntroActive = true;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        // 1. Bloqueia disparos de laser durante as instruções
        ArmaVR.SetAllCanShoot(false);

        // 2. Garante posicionamento da arma física e canvases
        EnsureWeaponInCockpit();
        EnsureInstructionCanvases();

        // 3. Oculta temporariamente o HUD de combate para foco na narrativa
        if (InterfaceVR.Instance != null)
        {
            InterfaceVR.Instance.SetCockpitHUDVisible(false);
        }

        // 4. Inicia exibindo o primeiro canvas (Protocolo de Interceptação)
        ShowCanvas1();
    }

    private void ShowCanvas1()
    {
        if (canvas1Protocolo != null) canvas1Protocolo.SetActive(true);
        if (canvas2Arma != null) canvas2Arma.SetActive(false);
        if (group1 != null) StartCoroutine(FadeRoutine(group1, 0f, 1f, 0.35f));
    }

    public void OnClickEntendiProtocolo()
    {
        if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
        StartCoroutine(TransitionToCanvas2Routine());
    }

    private IEnumerator TransitionToCanvas2Routine()
    {
        if (group1 != null) yield return StartCoroutine(FadeRoutine(group1, 1f, 0f, 0.25f));
        if (canvas1Protocolo != null) canvas1Protocolo.SetActive(false);

        if (canvas2Arma != null) canvas2Arma.SetActive(true);
        if (group2 != null) yield return StartCoroutine(FadeRoutine(group2, 0f, 1f, 0.35f));
    }

    public void OnClickIniciarDefesa()
    {
        if (!isIntroActive) return;
        isIntroActive = false;

        if (GerenteSom.Instance != null) GerenteSom.Instance.PlayClick();
        StartCoroutine(StartDefenseSequenceRoutine());
    }

    private IEnumerator StartDefenseSequenceRoutine()
    {
        if (group2 != null) yield return StartCoroutine(FadeRoutine(group2, 1f, 0f, 0.3f));
        if (canvas2Arma != null) canvas2Arma.SetActive(false);

        // 1. Libera disparos de arma
        ArmaVR.SetAllCanShoot(true);

        // 2. Exibe o HUD do Cockpit
        if (InterfaceVR.Instance != null)
        {
            InterfaceVR.Instance.SetCockpitHUDVisible(true);
        }

        // 3. Inicia a defesa no GerenteDefesa
        if (GerenteDefesa.Instance != null)
        {
            GerenteDefesa.Instance.StartDefense();
        }

        Debug.Log("[IntroducaoDefesaVR] Defesa iniciada com sucesso!");
    }

    private IEnumerator FadeRoutine(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    /// <summary>
    /// Garante que a arma física 3D do projeto (arma.obj) esteja instanciada,
    /// com XRGrabInteractable e ArmaVR no suporte do cockpit em frente ao piloto.
    /// </summary>
    private void EnsureWeaponInCockpit()
    {
        if (physicalWeaponObj != null) return;

        // Procura se já existe no cenário
        Transform existing = transform.Find("ArmaLaser_Cockpit");
        if (existing != null)
        {
            physicalWeaponObj = existing.gameObject;
            return;
        }

        // Cria o suporte / suporte da arma
        Vector3 weaponPos = new Vector3(0f, 0.72f, 0.95f);
        Quaternion weaponRot = Quaternion.Euler(-12f, 0f, 0f);

        GameObject weaponObj = new GameObject("ArmaLaser_Cockpit");
        weaponObj.transform.position = weaponPos;
        weaponObj.transform.rotation = weaponRot;

        // Carrega o mesh da arma existente
        Mesh weaponMesh = null;
#if UNITY_EDITOR
        weaponMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Componentes/ArmaLaser/source/arma.obj");
#endif
        if (weaponMesh == null)
        {
            // Fallback para modelos carregados
            var meshes = Resources.FindObjectsOfTypeAll<Mesh>();
            foreach (var m in meshes)
            {
                if (m.name.ToLower().Contains("arma") || m.name.ToLower().Contains("gun"))
                {
                    weaponMesh = m;
                    break;
                }
            }
        }

        GameObject visualModel = new GameObject("VisualModel");
        visualModel.transform.SetParent(weaponObj.transform, false);

        if (weaponMesh != null)
        {
            MeshFilter mf = visualModel.AddComponent<MeshFilter>();
            mf.sharedMesh = weaponMesh;

            MeshRenderer mr = visualModel.AddComponent<MeshRenderer>();
            Material mat = Resources.Load<Material>("Assets/Componentes/ArmaLaser/ArmaLaser_Mat");
#if UNITY_EDITOR
            if (mat == null) mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Componentes/ArmaLaser/ArmaLaser_Mat.mat");
#endif
            if (mat != null) mr.sharedMaterial = mat;
            else mr.material.color = new Color(0.2f, 0.75f, 0.9f);

            visualModel.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
            visualModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
        else
        {
            // Fallback estilizado com primitivas caso o mesh ainda não tenha sido linkado
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "CorpoLaser";
            body.transform.SetParent(visualModel.transform, false);
            body.transform.localScale = new Vector3(0.08f, 0.18f, 0.08f);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.GetComponent<Renderer>().material.color = new Color(0.1f, 0.6f, 0.85f);
            var bc = body.GetComponent<Collider>();
            if (bc != null) Destroy(bc);
        }

        // Colisor e Rigidbody
        BoxCollider box = weaponObj.AddComponent<BoxCollider>();
        box.size = new Vector3(0.22f, 0.28f, 0.45f);
        box.center = new Vector3(0f, 0.04f, 0.08f);

        Rigidbody rb = weaponObj.AddComponent<Rigidbody>();
        rb.mass = 1.8f;
        rb.isKinematic = true; // Permanece no suporte até ser agarrada pelo jogador

        // XRGrabInteractable
        XRGrabInteractable grab = weaponObj.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = true;
        grab.selectEntered.AddListener((args) =>
        {
            rb.isKinematic = false;
        });

        // Script de Tiro ArmaVR
        ArmaVR armaScript = weaponObj.AddComponent<ArmaVR>();
        armaScript.SetCanShoot(false);

        // Ponto de saída do laser (Muzzle)
        GameObject muzzle = new GameObject("PontoDisparoLaser");
        muzzle.transform.SetParent(weaponObj.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.06f, 0.32f);

        physicalWeaponObj = weaponObj;
    }

    /// <summary>
    /// Constrói os dois World Space Canvases com o padrão estético do cockpit.
    /// </summary>
    private void EnsureInstructionCanvases()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        Vector3 canvasPos = new Vector3(0f, 1.25f, 1.65f);
        Quaternion canvasRot = Quaternion.Euler(12f, 0f, 0f);

        // === CANVAS 1: PROTOCOLO DE INTERCEPTAÇÃO ===
        if (canvas1Protocolo == null)
        {
            canvas1Protocolo = CreateInstructionCanvas(
                "Canvas1_ProtocoloInterceptacao",
                canvasPos,
                canvasRot,
                "PROTOCOLO DE INTERCEPTAÇÃO",
                "SISTEMA DE DEFESA ATIVADO",
                "Os sensores detectaram detritos espaciais entrando na órbita da nave.\n\n" +
                "Os sistemas de mira foram danificados durante o ataque.\n\n" +
                "Para localizar corretamente os detritos, o sistema utiliza operações matemáticas para representar suas coordenadas.\n\n" +
                "Resolva a operação exibida no painel e identifique o detrito que possui o resultado correto.\n\n" +
                "Acerte o alvo correto para proteger a nave.",
                "Um disparo incorreto consumirá energia da nave.",
                "ENTENDI",
                OnClickEntendiProtocolo,
                font,
                out group1
            );
        }

        // === CANVAS 2: CONTROLE DA ARMA ===
        if (canvas2Arma == null)
        {
            canvas2Arma = CreateInstructionCanvas(
                "Canvas2_ControleDaArma",
                canvasPos + new Vector3(0f, 0.05f, 0f),
                canvasRot,
                "CONTROLE DA ARMA",
                "PREPARE-SE PARA A INTERCEPTAÇÃO",
                "A defesa da nave será realizada manualmente.\n\n" +
                "Pegue a arma disponível à sua frente.\n\n" +
                "Use o controle VR para segurá-la.\n\n" +
                "Mire nos detritos e pressione o gatilho para disparar.\n\n" +
                "Resolva a operação matemática e atinja somente o detrito que possui a resposta correta.\n\n" +
                "<color=#38BDF8><b>COMO JOGAR:</b></color>\n" +
                "1. Pegue a arma.   2. Resolva a operação.   3. Localize o detrito com a resposta.\n" +
                "4. Mire no alvo.   5. Pressione o gatilho para disparar.",
                "ERRAR O ALVO CONSUME ENERGIA.",
                "INICIAR DEFESA",
                OnClickIniciarDefesa,
                font,
                out group2
            );
            canvas2Arma.SetActive(false);
        }
    }

    private GameObject CreateInstructionCanvas(
        string name,
        Vector3 pos,
        Quaternion rot,
        string title,
        string subtitle,
        string body,
        string warning,
        string buttonText,
        UnityEngine.Events.UnityAction buttonAction,
        TMP_FontAsset font,
        out CanvasGroup group)
    {
        GameObject cObj = new GameObject(name);
        cObj.transform.position = pos;
        cObj.transform.rotation = rot;

        Canvas canvas = cObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 120;
        canvas.worldCamera = Camera.main;

        var raycasterType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (raycasterType != null) cObj.AddComponent(raycasterType);

        RectTransform rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(850f, 600f);
        rt.localScale = new Vector3(0.0022f, 0.0022f, 0.0022f);

        group = cObj.AddComponent<CanvasGroup>();
        group.alpha = 1f;

        // Fundo
        GameObject bgObj = new GameObject("Fundo");
        bgObj.transform.SetParent(cObj.transform, false);
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.03f, 0.06f, 0.15f, 0.95f);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // Borda Superior
        GameObject borderTop = new GameObject("BordaTop");
        borderTop.transform.SetParent(cObj.transform, false);
        var bImg = borderTop.AddComponent<Image>();
        bImg.color = new Color(0.22f, 0.74f, 0.97f, 0.85f);
        RectTransform bRt = borderTop.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.04f, 0.96f);
        bRt.anchorMax = new Vector2(0.96f, 0.97f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        // Título e Subtítulo
        GameObject titleObj = new GameObject("Titulo");
        titleObj.transform.SetParent(cObj.transform, false);
        var tTxt = titleObj.AddComponent<TextMeshProUGUI>();
        if (font != null) tTxt.font = font;
        tTxt.fontSize = 28f;
        tTxt.fontStyle = FontStyles.Bold;
        tTxt.alignment = TextAlignmentOptions.Center;
        tTxt.color = new Color(0.22f, 0.74f, 0.97f);
        tTxt.text = title;
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0.85f);
        tRt.anchorMax = new Vector2(1f, 0.95f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        GameObject subObj = new GameObject("Subtitulo");
        subObj.transform.SetParent(cObj.transform, false);
        var sTxt = subObj.AddComponent<TextMeshProUGUI>();
        if (font != null) sTxt.font = font;
        sTxt.fontSize = 18f;
        sTxt.alignment = TextAlignmentOptions.Center;
        sTxt.color = new Color(0.6f, 0.85f, 1f, 0.9f);
        sTxt.text = subtitle;
        RectTransform sRt = subObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0.78f);
        sRt.anchorMax = new Vector2(1f, 0.85f);
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;

        // Corpo do Texto
        GameObject bodyObj = new GameObject("CorpoTexto");
        bodyObj.transform.SetParent(cObj.transform, false);
        var bTxt = bodyObj.AddComponent<TextMeshProUGUI>();
        if (font != null) bTxt.font = font;
        bTxt.fontSize = 19f;
        bTxt.alignment = TextAlignmentOptions.Center;
        bTxt.textWrappingMode = TextWrappingModes.Normal;
        bTxt.color = Color.white;
        bTxt.text = body;
        RectTransform bTextRt = bodyObj.GetComponent<RectTransform>();
        bTextRt.anchorMin = new Vector2(0.06f, 0.24f);
        bTextRt.anchorMax = new Vector2(0.94f, 0.76f);
        bTextRt.offsetMin = Vector2.zero;
        bTextRt.offsetMax = Vector2.zero;

        // Aviso
        GameObject warnObj = new GameObject("Aviso");
        warnObj.transform.SetParent(cObj.transform, false);
        var wTxt = warnObj.AddComponent<TextMeshProUGUI>();
        if (font != null) wTxt.font = font;
        wTxt.fontSize = 17f;
        wTxt.fontStyle = FontStyles.Bold;
        wTxt.alignment = TextAlignmentOptions.Center;
        wTxt.color = new Color(1f, 0.35f, 0.35f);
        wTxt.text = warning;
        RectTransform wRt = warnObj.GetComponent<RectTransform>();
        wRt.anchorMin = new Vector2(0f, 0.16f);
        wRt.anchorMax = new Vector2(1f, 0.23f);
        wRt.offsetMin = Vector2.zero;
        wRt.offsetMax = Vector2.zero;

        // Botão
        GameObject btnObj = new GameObject("BotaoAcao");
        btnObj.transform.SetParent(cObj.transform, false);
        RectTransform btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = new Vector2(0f, -240f);
        btnRt.sizeDelta = new Vector2(280f, 54f);

        btnObj.AddComponent<CanvasRenderer>();
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.08f, 0.35f, 0.65f, 0.92f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.10f, 0.38f, 0.65f, 0.90f);
        cb.highlightedColor = new Color(0.22f, 0.74f, 0.98f, 1.00f);
        cb.pressedColor = new Color(0.06f, 0.22f, 0.40f, 1.00f);
        cb.selectedColor = new Color(0.18f, 0.60f, 0.85f, 1.00f);
        btn.colors = cb;
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(buttonAction);

        BoxCollider col = btnObj.AddComponent<BoxCollider>();
        col.size = new Vector3(280f, 54f, 8f);
        col.isTrigger = true;

        GameObject btnTxtObj = new GameObject("Text");
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        RectTransform btRt = btnTxtObj.AddComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.offsetMin = Vector2.zero;
        btRt.offsetMax = Vector2.zero;

        var btnTmp = btnTxtObj.AddComponent<TextMeshProUGUI>();
        if (font != null) btnTmp.font = font;
        btnTmp.fontSize = 22f;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.alignment = TextAlignmentOptions.Center;
        btnTmp.color = Color.white;
        btnTmp.raycastTarget = false;
        btnTmp.text = buttonText;

        return cObj;
    }
}
