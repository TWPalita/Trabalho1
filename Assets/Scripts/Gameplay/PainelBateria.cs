using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

/// <summary>
/// Controla o sistema de substituição de bateria na CabinePiloto e monitoramento de energia na SampleScene.
/// O jogador retira a bateria danificada na cabine de comando, procura qualquer bateria reserva
/// espalhada pela nave e a encaixa no mesmo local original da cabine para restaurar a energia da espaçonave.
/// </summary>
public class PainelBateria : MonoBehaviour
{
    public static PainelBateria Instance { get; private set; }

    [Header("Referências da Interface do Painel")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Local de Encaixe na Cabine")]
    [Tooltip("Transform de referência do compartimento na CabinePiloto onde a bateria danificada estava")]
    [SerializeField] private Transform socketTransform;

    [Header("Baterias")]
    [SerializeField] private ItemBateria damagedBattery;

    [Header("Configurações")]
    [Tooltip("Distância em metros para magnetização/encaixe da bateria reserva no local da cabine")]
    [SerializeField] private float socketSnapDistance = 0.50f;

    private bool isDamagedBatteryRemoved = false;
    private bool isEnergyRestored = false;
    private Coroutine sequenceRoutine;
    private Coroutine pulseAnimRoutine;

    private ItemBateria[] cachedBatteries;
    private Image borderLineImage;
    private Image outerFrameImage;
    private TMP_Text titleTextComp;

    private const string MsgInitial =
        "<color=#F87171><b>[!] ENERGIA INDISPONÍVEL</b></color>\n\n" +
        "A bateria principal na cabine foi danificada.\n\n" +
        "<color=#94A3B8>Retire a bateria danificada da cabine e encaixe uma bateria reserva.</color>";

    private const string MsgAwaiting =
        "<color=#FBBF24><b>[!] COMPARTIMENTO VAZIO</b></color>\n\n" +
        "Aproxime uma <color=#38BDF8><b>Bateria Reserva</b></color> do encaixe na cabine para restaurar a energia.";

    private const string MsgRestored =
        "<size=122%><color=#34D399><b>[OK] ENERGIA RESTAURADA</b></color></size>\n\n" +
        "<color=#A7F3D0>Estabilizando rede elétrica da espaçonave...</color>";

    private const string MsgRebooting =
        "<size=112%><color=#38BDF8><b>SISTEMAS REINICIANDO...</b></color></size>\n\n" +
        "<color=#BAE6FD>Carregando telemetria e sensores de defesa...</color>";

    public bool IsDamagedBatteryRemoved => isDamagedBatteryRemoved;
    public bool IsEnergyRestored => isEnergyRestored;
    public Transform SocketTransform => socketTransform;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsurePanelStructure();
    }

    private void Start()
    {
        RefreshBatteryCache();
        EnsureSocketAtCabin();

        if (damagedBattery != null)
        {
            damagedBattery.InitializeComponents();
        }

        SetStatusMessage(MsgInitial, new Color(0.97f, 0.44f, 0.44f, 0.9f));
    }

    private void RefreshBatteryCache()
    {
        cachedBatteries = FindObjectsByType<ItemBateria>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Garante que o ponto de encaixe do socket corresponda exatamente à posição da Bateria Danificada na CabinePiloto.
    /// </summary>
    public void EnsureSocketAtCabin()
    {
        if (damagedBattery == null)
        {
            if (cachedBatteries == null || cachedBatteries.Length == 0)
                RefreshBatteryCache();

            foreach (var b in cachedBatteries)
            {
                if (b != null && b.IsDamaged)
                {
                    damagedBattery = b;
                    break;
                }
            }
        }

        if (damagedBattery != null)
        {
            if (socketTransform == null || Vector3.Distance(socketTransform.position, damagedBattery.transform.position) > 0.8f)
            {
                GameObject socketObj = GameObject.Find("SocketBateria_Cabine");
                if (socketObj == null)
                {
                    socketObj = new GameObject("SocketBateria_Cabine");
                }

                socketObj.transform.position = damagedBattery.transform.position;
                socketObj.transform.rotation = damagedBattery.transform.rotation;
                socketTransform = socketObj.transform;
            }
        }
        else if (socketTransform == null)
        {
            GameObject socketObj = GameObject.Find("SocketBateria_Cabine");
            if (socketObj == null)
            {
                socketObj = new GameObject("SocketBateria_Cabine");
                socketObj.transform.position = new Vector3(-3.9039f, 0.8006f, 8.1052f);
                socketObj.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
            socketTransform = socketObj.transform;
        }
    }

    private void Update()
    {
        if (isEnergyRestored) return;

        // 1. Detecta retirada da bateria danificada da cabine
        if (!isDamagedBatteryRemoved && damagedBattery != null && socketTransform != null)
        {
            float dist = Vector3.Distance(damagedBattery.transform.position, socketTransform.position);
            if (damagedBattery.WasGrabbedOnce && dist > socketSnapDistance * 1.05f)
            {
                OnDamagedBatteryRemoved();
            }
        }

        // 2. Detecta inserção de QUALQUER bateria reserva no mesmo local da cabine (sem alocar memória por frame)
        if (isDamagedBatteryRemoved && !isEnergyRestored && socketTransform != null)
        {
            if (cachedBatteries == null || cachedBatteries.Length == 0)
            {
                RefreshBatteryCache();
            }

            for (int i = 0; i < cachedBatteries.Length; i++)
            {
                ItemBateria bat = cachedBatteries[i];
                if (bat != null && bat.gameObject.activeInHierarchy && !bat.IsDamaged)
                {
                    float dist = Vector3.Distance(bat.transform.position, socketTransform.position);
                    if (dist <= socketSnapDistance)
                    {
                        SnapSpareBatteryAndRestore(bat);
                        break;
                    }
                }
            }
        }
    }

    public void OnDamagedBatteryRemoved()
    {
        if (isDamagedBatteryRemoved) return;
        isDamagedBatteryRemoved = true;

        Debug.Log("[PainelBateria] Bateria danificada retirada da cabine de comando.");
        SetStatusMessage(MsgAwaiting, new Color(0.98f, 0.75f, 0.14f, 0.9f));

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayClick();
        }
    }

    public void SnapSpareBatteryAndRestore(ItemBateria spare)
    {
        if (isEnergyRestored || spare == null || socketTransform == null) return;
        isEnergyRestored = true;

        Debug.Log($"[PainelBateria] Bateria reserva '{spare.BatteryName}' inserida com sucesso no local da cabine! Restaurando energia da nave...");

        spare.LockInSocket(socketTransform);

        if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
        sequenceRoutine = StartCoroutine(EnergyRestorationSequence());
    }

    private IEnumerator EnergyRestorationSequence()
    {
        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayCorrect();
        }

        SetStatusMessage(MsgRestored, new Color(0.20f, 0.83f, 0.60f, 0.95f));

        yield return new WaitForSeconds(1.2f);

        SetStatusMessage(MsgRebooting, new Color(0.22f, 0.74f, 0.97f, 0.95f));

        yield return new WaitForSeconds(1.3f);

        if (GerenteAlarme.Instance != null)
        {
            GerenteAlarme.Instance.TriggerAlarm();
        }
    }

    private void SetStatusMessage(string msg, Color accentColor)
    {
        if (statusText != null)
        {
            statusText.text = msg;
        }

        if (borderLineImage != null)
        {
            borderLineImage.color = accentColor;
        }

        if (outerFrameImage != null)
        {
            outerFrameImage.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f);
        }

        if (titleTextComp != null)
        {
            titleTextComp.color = accentColor;
        }

        if (statusText != null && gameObject.activeInHierarchy)
        {
            if (pulseAnimRoutine != null) StopCoroutine(pulseAnimRoutine);
            pulseAnimRoutine = StartCoroutine(AnimateTextPop(statusText.transform));
        }
    }

    private IEnumerator AnimateTextPop(Transform target)
    {
        if (target == null) yield break;
        float duration = 0.2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f;
            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    /// <summary>
    /// Localiza e assegura as referências de interface e baterias na nave sem sobrescrever posições manuais do usuário.
    /// </summary>
    private void EnsurePanelStructure()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        if (statusText == null)
        {
            statusText = GetComponentInChildren<TMP_Text>();
        }

        if (statusText == null)
        {
            GameObject canvasObj = new GameObject("PainelEnergiaCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 0.15f, 0.02f);
            canvasObj.transform.localRotation = Quaternion.identity;

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 90;
            canvas.worldCamera = Camera.main;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2.5f;

            RectTransform rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(460f, 330f);
            rt.localScale = new Vector3(0.0022f, 0.0022f, 0.0022f);

            panelCanvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // Moldura externa brilhante
            GameObject outerObj = new GameObject("BordaExterna");
            outerObj.transform.SetParent(canvasObj.transform, false);
            outerFrameImage = outerObj.AddComponent<Image>();
            outerFrameImage.color = new Color(0.97f, 0.44f, 0.44f, 0.35f);
            outerFrameImage.raycastTarget = false;
            RectTransform outerRt = outerObj.GetComponent<RectTransform>();
            outerRt.anchorMin = Vector2.zero;
            outerRt.anchorMax = Vector2.one;
            outerRt.offsetMin = new Vector2(-5f, -5f);
            outerRt.offsetMax = new Vector2(5f, 5f);

            // Fundo escuro do painel
            GameObject bgObj = new GameObject("Fundo");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.05f, 0.12f, 0.95f);
            bgImg.raycastTarget = false;
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Borda decorativa divisória
            GameObject borderObj = new GameObject("Borda");
            borderObj.transform.SetParent(canvasObj.transform, false);
            borderLineImage = borderObj.AddComponent<Image>();
            borderLineImage.color = new Color(0.22f, 0.74f, 0.97f, 0.85f);
            borderLineImage.raycastTarget = false;
            RectTransform bRt = borderObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.05f, 0.78f);
            bRt.anchorMax = new Vector2(0.95f, 0.795f);
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            // Título
            GameObject titleObj = new GameObject("Titulo");
            titleObj.transform.SetParent(canvasObj.transform, false);
            titleTextComp = titleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) titleTextComp.font = font;
            titleTextComp.fontSize = 23f;
            titleTextComp.fontStyle = FontStyles.Bold;
            titleTextComp.alignment = TextAlignmentOptions.Center;
            titleTextComp.color = new Color(0.22f, 0.74f, 0.97f);
            titleTextComp.text = "SISTEMA DE ENERGIA";
            titleTextComp.raycastTarget = false;
            RectTransform tRt = titleObj.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 0.81f);
            tRt.anchorMax = new Vector2(1f, 0.98f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            // Texto de status
            GameObject textObj = new GameObject("TextoStatus");
            textObj.transform.SetParent(canvasObj.transform, false);
            statusText = textObj.AddComponent<TextMeshProUGUI>();
            if (font != null) statusText.font = font;
            statusText.fontSize = 19.5f;
            statusText.lineSpacing = 6f;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.textWrappingMode = TextWrappingModes.Normal;
            statusText.color = Color.white;
            statusText.raycastTarget = false;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.07f, 0.06f);
            textRt.anchorMax = new Vector2(0.93f, 0.75f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }
        else
        {
            Canvas existingCanvas = statusText.GetComponentInParent<Canvas>();
            if (existingCanvas != null)
            {
                CanvasScaler scaler = existingCanvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = existingCanvas.gameObject.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 2.5f;

                Transform borderTr = existingCanvas.transform.Find("Borda");
                if (borderTr != null) borderLineImage = borderTr.GetComponent<Image>();

                Transform titleTr = existingCanvas.transform.Find("Titulo");
                if (titleTr != null) titleTextComp = titleTr.GetComponent<TMP_Text>();
            }
        }

        // 2. Garante Bateria Danificada existente
        if (damagedBattery == null)
        {
            RefreshBatteryCache();
            foreach (var b in cachedBatteries)
            {
                if (b != null && b.IsDamaged)
                {
                    damagedBattery = b;
                    break;
                }
            }
        }
    }
}
