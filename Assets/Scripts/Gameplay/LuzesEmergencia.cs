using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controla o efeito de luzes e LEDs vermelhos de emergência na espaçonave.
/// Conecta-se diretamente aos neons e lâmpadas localizados em nave/Scene/Lamp (e LampeALerte),
/// fazendo com que pisquem com a cor vermelha e emitam um blur no brilho (Bloom/Glow HDR) como luz real.
/// </summary>
public class LuzesEmergencia : MonoBehaviour
{
    [Header("Configurações do Pisca-Pisca")]
    [Tooltip("Intervalo em segundos entre cada alternância da luz (vermelho / normal)")]
    [SerializeField] private float flashInterval = 0.5f;

    [Tooltip("Intensidade máxima das luzes de emergência")]
    [SerializeField] private float maxIntensity = 3.5f;

    [Tooltip("Multiplicador de emissão HDR para criar o efeito de blur luminoso (Bloom)")]
    [SerializeField] private float hdrGlowMultiplier = 4.0f;

    [Tooltip("Cor da luz de emergência")]
    [SerializeField] private Color emergencyColor = Color.red;

    [Tooltip("Cor dos neons em estado normal/desligado durante o ciclo")]
    [SerializeField] private Color normalColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    [Header("Neons da Nave (nave/Scene/Lamp)")]
    [Tooltip("Objeto pai dos neons (ex: nave/Scene/Lamp ou nave/Scene/Lampe)")]
    [SerializeField] private Transform lampContainer;

    [Tooltip("Renderers dos neons que piscarão em vermelho")]
    [SerializeField] private Renderer[] neonRenderers;

    [Header("Luzes de Apoio do Cenário")]
    [Tooltip("Luzes que piscarão em sincronia com os neons")]
    [SerializeField] private Light[] emergencyLights;

    private struct OriginalMaterialData
    {
        public Material material;
        public Color baseColor;
        public Color emissionColor;
        public bool hadEmissionKeyword;
    }

    private readonly List<OriginalMaterialData> originalDataList = new List<OriginalMaterialData>();
    private bool isEmergencyActive = false;
    private Coroutine flashRoutine;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        FindAndCacheNeons();
        EnsureDefaultLights();
        ResetToNormal();
    }

    private void Start()
    {
        EnsurePostProcessingOnCameras();
    }

    /// <summary>
    /// Garante que as câmeras (incluindo o headset VR) tenham HDR e Post-Processing habilitados
    /// para renderizar o efeito óptico de blur/halo luminoso (Bloom) ao redor dos neons.
    /// </summary>
    private void EnsurePostProcessingOnCameras()
    {
        Camera[] cameras = Camera.allCameras;
        foreach (var cam in cameras)
        {
            if (cam == null) continue;

            cam.allowHDR = true;
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
            {
                camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            camData.renderPostProcessing = true;
        }

        Debug.Log("[EmergencyLightsController] Post-Processing (Bloom/Blur) e HDR configurados nas câmeras para brilho de neon realista.");
    }

    /// <summary>
    /// Localiza automaticamente os neons sob nave/Scene/Lamp (ou Lampe / LampeALerte)
    /// e armazena os materiais originais para restauração perfeita.
    /// </summary>
    private void FindAndCacheNeons()
    {
        originalDataList.Clear();

        // 1. Localiza o contêiner Lamp se não configurado no Inspector
        if (lampContainer == null)
        {
            lampContainer = FindLampContainer();
        }

        List<Renderer> foundRenderers = new List<Renderer>();

        if (lampContainer != null)
        {
            Renderer[] rList = lampContainer.GetComponentsInChildren<Renderer>(true);
            foundRenderers.AddRange(rList);
            Debug.Log($"[EmergencyLightsController] Contêiner de lâmpadas encontrado: '{lampContainer.name}' com {rList.Length} renderers.");
        }

        // Também procura por LampeALerte sob a nave
        Transform alertLamp = FindAlertLamp();
        if (alertLamp != null && alertLamp != lampContainer)
        {
            Renderer[] alertRends = alertLamp.GetComponentsInChildren<Renderer>(true);
            foundRenderers.AddRange(alertRends);
            Debug.Log($"[EmergencyLightsController] LampeALerte encontrada com {alertRends.Length} renderers.");
        }

        // Se o usuário vinculou renderers manualmente no Inspector, adiciona-os também
        if (neonRenderers != null && neonRenderers.Length > 0)
        {
            foreach (var r in neonRenderers)
            {
                if (r != null && !foundRenderers.Contains(r))
                {
                    foundRenderers.Add(r);
                }
            }
        }

        neonRenderers = foundRenderers.ToArray();

        // 2. Armazena os dados originais dos materiais dos neons
        foreach (var rend in neonRenderers)
        {
            if (rend == null) continue;

            foreach (var mat in rend.materials)
            {
                if (mat == null) continue;

                OriginalMaterialData data = new OriginalMaterialData
                {
                    material = mat,
                    baseColor = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : (mat.HasProperty(ColorId) ? mat.GetColor(ColorId) : Color.white),
                    emissionColor = mat.HasProperty(EmissionColorId) ? mat.GetColor(EmissionColorId) : Color.black,
                    hadEmissionKeyword = mat.IsKeywordEnabled("_EMISSION")
                };

                originalDataList.Add(data);

                // Habilita a palavra-chave de emissão no material para permitir brilho em URP
                mat.EnableKeyword("_EMISSION");
            }
        }

        Debug.Log($"[EmergencyLightsController] Total de {originalDataList.Count} materiais de neons registrados para o efeito de alarme.");
    }

    private Transform FindLampContainer()
    {
        // Procura por "Nave" ou "nave" na cena
        GameObject naveObj = GameObject.Find("Nave");
        if (naveObj == null) naveObj = GameObject.Find("nave");

        if (naveObj != null)
        {
            Transform lamp = naveObj.transform.Find("Scene/Lamp");
            if (lamp == null) lamp = naveObj.transform.Find("Scene/Lampe");
            if (lamp == null) lamp = naveObj.transform.Find("Lamp");
            if (lamp == null) lamp = naveObj.transform.Find("Lampe");

            if (lamp != null) return lamp;

            // Busca recursiva por qualquer filho chamado "Lamp" ou "Lampe"
            foreach (Transform child in naveObj.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Equals("Lamp", System.StringComparison.OrdinalIgnoreCase) ||
                    child.name.Equals("Lampe", System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
        }

        // Busca global na cena como fallback
        GameObject globalLamp = GameObject.Find("Lamp");
        if (globalLamp == null) globalLamp = GameObject.Find("Lampe");
        return globalLamp != null ? globalLamp.transform : null;
    }

    private Transform FindAlertLamp()
    {
        GameObject naveObj = GameObject.Find("Nave");
        if (naveObj == null) naveObj = GameObject.Find("nave");

        if (naveObj != null)
        {
            Transform alert = naveObj.transform.Find("Scene/LampeALerte");
            if (alert == null) alert = naveObj.transform.Find("LampeALerte");
            if (alert != null) return alert;

            foreach (Transform child in naveObj.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.IndexOf("LampeALerte", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    child.name.IndexOf("LampAlert", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
            }
        }

        return null;
    }

    private void EnsureDefaultLights()
    {
        if (emergencyLights == null || emergencyLights.Length == 0)
        {
            GameObject lightObj1 = new GameObject("EmergencyLight_Spawn");
            lightObj1.transform.SetParent(transform, false);
            lightObj1.transform.position = new Vector3(-3.5f, 2.2f, -4.5f);
            Light l1 = lightObj1.AddComponent<Light>();
            l1.type = LightType.Point;
            l1.range = 14f;
            l1.shadows = LightShadows.None;

            GameObject lightObj2 = new GameObject("EmergencyLight_Cockpit");
            lightObj2.transform.SetParent(transform, false);
            lightObj2.transform.position = new Vector3(-3.5f, 2.2f, 4.5f);
            Light l2 = lightObj2.AddComponent<Light>();
            l2.type = LightType.Point;
            l2.range = 14f;
            l2.shadows = LightShadows.None;

            emergencyLights = new Light[] { l1, l2 };
        }

        foreach (var light in emergencyLights)
        {
            if (light != null)
            {
                light.color = emergencyColor;
                light.intensity = 0f;
                light.enabled = false;
            }
        }
    }

    /// <summary>
    /// Inicia o pisca-pisca dos neons e luzes com a cor vermelha e blur luminoso.
    /// </summary>
    public void StartEmergencyLights()
    {
        if (isEmergencyActive) return;
        isEmergencyActive = true;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(EmergencyFlashLoop());
    }

    /// <summary>
    /// Interrompe o efeito de emergência e retorna os neons e luzes ao estado normal.
    /// </summary>
    public void StopEmergencyLights()
    {
        if (!isEmergencyActive && flashRoutine == null) return;
        isEmergencyActive = false;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        ResetToNormal();
    }

    /// <summary>
    /// Restaura todos os neons e luzes para o estado normal (cores originais, sem emissão vermelha).
    /// </summary>
    public void ResetToNormal()
    {
        // 1. Restaura os materiais originais de cada neon
        foreach (var data in originalDataList)
        {
            if (data.material != null)
            {
                if (data.material.HasProperty(BaseColorId)) data.material.SetColor(BaseColorId, data.baseColor);
                if (data.material.HasProperty(ColorId)) data.material.SetColor(ColorId, data.baseColor);
                if (data.material.HasProperty(EmissionColorId)) data.material.SetColor(EmissionColorId, data.emissionColor);

                if (!data.hadEmissionKeyword)
                {
                    data.material.DisableKeyword("_EMISSION");
                }
            }
        }

        // 2. Desliga as luzes pontuais
        if (emergencyLights != null)
        {
            foreach (var light in emergencyLights)
            {
                if (light != null)
                {
                    light.intensity = 0f;
                    light.enabled = false;
                }
            }
        }
    }

    private IEnumerator EmergencyFlashLoop()
    {
        if (emergencyLights != null)
        {
            foreach (var light in emergencyLights)
            {
                if (light != null) light.enabled = true;
            }
        }

        bool isRedState = false;
        var wait = new WaitForSeconds(flashInterval);

        while (isEmergencyActive)
        {
            isRedState = !isRedState;

            Color currentBaseColor = isRedState ? emergencyColor : normalColor;
            // Emissão HDR multiplicada para ativar o Bloom e criar o blur luminoso de luz real
            float effIntensity = Mathf.Max(maxIntensity, 3.5f);
            float effMultiplier = Mathf.Max(hdrGlowMultiplier, 4.0f);
            Color currentEmission = isRedState ? (emergencyColor * (effIntensity * effMultiplier)) : Color.black;
            float currentLightIntensity = isRedState ? effIntensity : 0.1f;

            // 1. Atualiza todos os neons de nave/Scene/Lamp
            foreach (var data in originalDataList)
            {
                if (data.material != null)
                {
                    if (data.material.HasProperty(BaseColorId)) data.material.SetColor(BaseColorId, currentBaseColor);
                    if (data.material.HasProperty(ColorId)) data.material.SetColor(ColorId, currentBaseColor);
                    if (data.material.HasProperty(EmissionColorId)) data.material.SetColor(EmissionColorId, currentEmission);
                    data.material.EnableKeyword("_EMISSION");
                }
            }

            // 2. Atualiza as luzes pontuais sincronizadas
            if (emergencyLights != null)
            {
                foreach (var light in emergencyLights)
                {
                    if (light != null)
                    {
                        light.intensity = currentLightIntensity;
                        light.color = emergencyColor;
                    }
                }
            }

            yield return wait;
        }

        ResetToNormal();
    }
}

public class EmergencyLightsController : LuzesEmergencia { }

