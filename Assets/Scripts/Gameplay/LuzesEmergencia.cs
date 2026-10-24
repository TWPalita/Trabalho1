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

    // Luzes funcionais de ambientação espacial e guia de objetivos (sem sombras para alta performance em VR)
    private Light roomGuideLight;
    private Light generatorGuideLight;
    private Light cabinGuideLight;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        FindAndCacheNeons();
        EnsureDefaultLights();
        EnsureShipAtmosphereLights();
        ResetToNormal();
    }

    private void Start()
    {
        EnsurePostProcessingOnCameras();
        ConfigureSciFiSpaceAmbient();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < originalDataList.Count; i++)
        {
            if (originalDataList[i].material != null)
            {
                Destroy(originalDataList[i].material);
            }
        }
        originalDataList.Clear();
    }

    /// <summary>
    /// Configura a iluminação ambiente da nave para uma atmosfera tecnológica no espaço profundo.
    /// </summary>
    private void ConfigureSciFiSpaceAmbient()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.16f, 0.24f, 0.36f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.12f, 0.16f, 0.24f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.06f, 0.08f, 0.13f, 1f);
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

    private void EnsureShipAtmosphereLights()
    {
        // 1. Luz tecnológica suave no Quarto da Tripulação (onde o jogador desperta)
        if (roomGuideLight == null)
        {
            roomGuideLight = CreateOrGetGuideLight(
                "LuzAmbiente_Quarto",
                new Vector3(-3.57f, 2.35f, -6.80f),
                new Color(0.28f, 0.68f, 0.98f, 1f),
                1.15f,
                7.5f
            );
        }

        // 2. Luz guia sobre o Gerador Auxiliar e o Galão de Combustível na sala lateral
        if (generatorGuideLight == null)
        {
            generatorGuideLight = CreateOrGetGuideLight(
                "LuzGuia_Gerador",
                new Vector3(-9.60f, 2.15f, -1.40f),
                new Color(0.98f, 0.65f, 0.15f, 1f),
                1.75f,
                8.5f
            );
        }

        // 3. Luz guia sobre o Console da Cabine de Comando
        if (cabinGuideLight == null)
        {
            cabinGuideLight = CreateOrGetGuideLight(
                "LuzGuia_Cabine",
                new Vector3(-3.39f, 2.15f, 8.20f),
                new Color(0.98f, 0.65f, 0.15f, 1f),
                1.15f,
                7.5f
            );
        }
    }

    private Light CreateOrGetGuideLight(string lightName, Vector3 worldPos, Color color, float intensity, float range)
    {
        Transform existing = transform.Find(lightName);
        GameObject lightObj = existing != null ? existing.gameObject : new GameObject(lightName);
        if (existing == null)
        {
            lightObj.transform.SetParent(transform, false);
        }
        lightObj.transform.position = worldPos;

        Light l = lightObj.GetComponent<Light>();
        if (l == null) l = lightObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        l.enabled = true;
        return l;
    }

    /// <summary>
    /// Inicia o pisca-pisca dos neons e luzes com a cor vermelha e blur luminoso.
    /// </summary>
    public void StartEmergencyLights()
    {
        if (isEmergencyActive) return;
        isEmergencyActive = true;

        // Quando o gerador é ligado, a luz guia do gerador passa para verde/ciano (energia ativa)
        // e a luz da cabine destaca o console frontal para o jogador correr até lá.
        if (generatorGuideLight != null)
        {
            generatorGuideLight.color = new Color(0.20f, 0.88f, 0.58f, 1f);
            generatorGuideLight.intensity = 1.9f;
        }

        if (cabinGuideLight != null)
        {
            cabinGuideLight.color = new Color(0.22f, 0.80f, 1.0f, 1f);
            cabinGuideLight.intensity = 2.1f;
        }

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
    /// Restaura todos os neons e luzes para o estado inicial (meia-luz tecnológica com luzes guia nos equipamentos).
    /// </summary>
    public void ResetToNormal()
    {
        Color standbyEmission = new Color(0.08f, 0.22f, 0.38f, 1f) * 1.25f;

        // 1. Restaura os materiais de cada neon com brilho tecnológico sutil de standby
        foreach (var data in originalDataList)
        {
            if (data.material != null)
            {
                if (data.material.HasProperty(BaseColorId)) data.material.SetColor(BaseColorId, data.baseColor);
                if (data.material.HasProperty(ColorId)) data.material.SetColor(ColorId, data.baseColor);
                if (data.material.HasProperty(EmissionColorId))
                {
                    Color targetEmission = data.hadEmissionKeyword ? data.emissionColor : standbyEmission;
                    data.material.SetColor(EmissionColorId, targetEmission);
                    data.material.EnableKeyword("_EMISSION");
                }
            }
        }

        // 2. Desliga as luzes pontuais de alarme vermelho
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

        // 3. Mantém as luzes guia em estado de "aguardando energia" (âmbar no gerador e na cabine)
        if (generatorGuideLight != null)
        {
            generatorGuideLight.color = new Color(0.98f, 0.65f, 0.15f, 1f);
            generatorGuideLight.intensity = 1.75f;
        }
        if (cabinGuideLight != null)
        {
            cabinGuideLight.color = new Color(0.98f, 0.65f, 0.15f, 1f);
            cabinGuideLight.intensity = 1.15f;
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

        float effIntensity = Mathf.Max(maxIntensity, 3.5f);
        float effMultiplier = Mathf.Max(hdrGlowMultiplier, 4.0f);
        float cycleDuration = Mathf.Max(0.25f, flashInterval * 2f);
        float elapsed = 0f;

        while (isEmergencyActive)
        {
            elapsed += Time.deltaTime;
            float phase = (Mathf.Sin((elapsed / cycleDuration) * Mathf.PI * 2f) + 1f) * 0.5f;
            float smoothPulse = Mathf.SmoothStep(0.08f, 1f, phase);

            Color currentBaseColor = Color.Lerp(normalColor, emergencyColor, smoothPulse);
            Color currentEmission = emergencyColor * (effIntensity * effMultiplier * smoothPulse);
            float currentLightIntensity = Mathf.Lerp(0.25f, effIntensity, smoothPulse);

            // 1. Atualiza todos os neons de nave/Scene/Lamp com pulsação suave
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

            yield return null;
        }

        ResetToNormal();
    }
}

public class EmergencyLightsController : LuzesEmergencia { }

