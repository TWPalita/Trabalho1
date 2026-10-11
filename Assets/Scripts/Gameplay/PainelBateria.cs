using System.Collections;
using UnityEngine;
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
    [SerializeField] private float socketSnapDistance = 0.45f;

    private bool isDamagedBatteryRemoved = false;
    private bool isEnergyRestored = false;
    private Coroutine sequenceRoutine;

    private const string MsgInitial =
        "<color=#F87171><b>Energia indisponível.</b></color>\n\n" +
        "A bateria principal na cabine foi danificada.\n\n" +
        "Retire a bateria danificada da cabine de comando e procure uma bateria reserva para restaurar o sistema.";

    private const string MsgAwaiting =
        "<color=#FBBF24><b>Compartimento da cabine vazio.</b></color>\n\n" +
        "Insira a bateria reserva no local da cabine para restaurar o sistema.";

    private const string MsgRestored =
        "<size=125%><color=#34D399><b>ENERGIA RESTAURADA</b></color></size>";

    private const string MsgRebooting =
        "<size=110%><color=#60A5FA><b>SISTEMAS DA NAVE REINICIANDO...</b></color></size>";

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
        EnsureSocketAtCabin();

        if (damagedBattery != null)
        {
            damagedBattery.InitializeComponents();
        }

        SetStatusMessage(MsgInitial);
    }

    /// <summary>
    /// Garante que o ponto de encaixe do socket corresponda exatamente à posição da Bateria Danificada na CabinePiloto.
    /// </summary>
    public void EnsureSocketAtCabin()
    {
        if (damagedBattery == null)
        {
            ItemBateria[] all = FindObjectsByType<ItemBateria>(FindObjectsInactive.Include);
            foreach (var b in all)
            {
                if (b.IsDamaged)
                {
                    damagedBattery = b;
                    break;
                }
            }
        }

        if (damagedBattery != null)
        {
            // Se o socket ainda não existe ou se estava apontando para o painel antigo da parede (> 1 metro de distância)
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
            // Dispara quando o jogador pega a bateria no VR e a afasta do ponto de encaixe
            if (damagedBattery.WasGrabbedOnce && dist > socketSnapDistance * 1.1f)
            {
                OnDamagedBatteryRemoved();
            }
        }

        // 2. Detecta inserção de QUALQUER bateria reserva no mesmo local da cabine
        if (isDamagedBatteryRemoved && !isEnergyRestored && socketTransform != null)
        {
            ItemBateria[] allBatteries = FindObjectsByType<ItemBateria>(FindObjectsInactive.Exclude);
            foreach (var bat in allBatteries)
            {
                if (bat != null && !bat.IsDamaged)
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
        SetStatusMessage(MsgAwaiting);

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

        SetStatusMessage(MsgRestored);

        yield return new WaitForSeconds(1.4f);

        SetStatusMessage(MsgRebooting);

        yield return new WaitForSeconds(1.6f);

        // Dispara a narrativa do alarme de detritos no GerenteAlarme existente
        if (GerenteAlarme.Instance != null)
        {
            GerenteAlarme.Instance.TriggerAlarm();
        }
    }

    private void SetStatusMessage(string msg)
    {
        if (statusText != null)
        {
            statusText.text = msg;
        }
    }

    /// <summary>
    /// Localiza e assegura as referências de interface e baterias na nave sem sobrescrever posições manuais do usuário.
    /// </summary>
    private void EnsurePanelStructure()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        // 1. Painel TextMeshPro
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

            RectTransform rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(450f, 320f);
            rt.localScale = new Vector3(0.0022f, 0.0022f, 0.0022f);

            panelCanvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // Fundo escuro do painel
            GameObject bgObj = new GameObject("Fundo");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgImg = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.03f, 0.07f, 0.16f, 0.94f);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Borda decorativa
            GameObject borderObj = new GameObject("Borda");
            borderObj.transform.SetParent(canvasObj.transform, false);
            var borderImg = borderObj.AddComponent<UnityEngine.UI.Image>();
            borderImg.color = new Color(0.22f, 0.74f, 0.97f, 0.85f);
            RectTransform bRt = borderObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.05f, 0.78f);
            bRt.anchorMax = new Vector2(0.95f, 0.80f);
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            // Título
            GameObject titleObj = new GameObject("Titulo");
            titleObj.transform.SetParent(canvasObj.transform, false);
            var titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) titleTxt.font = font;
            titleTxt.fontSize = 24f;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = new Color(0.22f, 0.74f, 0.97f);
            titleTxt.text = "SISTEMA DE ENERGIA";
            RectTransform tRt = titleObj.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 0.82f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            // Texto de status
            GameObject textObj = new GameObject("TextoStatus");
            textObj.transform.SetParent(canvasObj.transform, false);
            statusText = textObj.AddComponent<TextMeshProUGUI>();
            if (font != null) statusText.font = font;
            statusText.fontSize = 20f;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.textWrappingMode = TextWrappingModes.Normal;
            statusText.color = Color.white;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.08f, 0.05f);
            textRt.anchorMax = new Vector2(0.92f, 0.75f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }

        // 2. Garante Bateria Danificada existente
        if (damagedBattery == null)
        {
            var allBat = FindObjectsByType<ItemBateria>(FindObjectsInactive.Include);
            foreach (var b in allBat)
            {
                if (b.IsDamaged)
                {
                    damagedBattery = b;
                    break;
                }
            }
        }
    }
}
