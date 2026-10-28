using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla o sistema visual do Visor Holográfico (MathVisor) no cockpit da nave e o efeito
/// cinematográfico de Viagem Espacial (Warp Drive / Dobra Estelar) ao vencer a defesa.
/// </summary>
public class HologramVisorEffect : MonoBehaviour
{
    public static HologramVisorEffect Instance { get; private set; }

    [Header("Componentes do Holograma")]
    [Tooltip("Imagem de fundo do campo de plasma holográfico")]
    [SerializeField] private Image visorBackground;

    [Tooltip("Feixe cônico de projeção luminosa que emana do painel")]
    [SerializeField] private Image projectionBeam;

    [Tooltip("Emissor ou lente de projeção no painel")]
    [SerializeField] private Image emitterLens;

    [Tooltip("Linha horizontal de varredura que percorre o holograma")]
    [SerializeField] private RectTransform scanlineBar;

    [Header("Pulso e Flicker de Luz")]
    [SerializeField] private bool enableLightPulse = true;
    [SerializeField] private float pulseFrequency = 2.2f;
    [SerializeField] private float pulseAmplitude = 0.07f;
    [SerializeField] private float microFlickerIntensity = 0.025f;

    [Header("Micro-Flutuação no Espaço (Holo Bobbing)")]
    [SerializeField] private bool enableBobbing = true;
    [SerializeField] private float bobFrequency = 1.5f;
    [SerializeField] private float bobAmplitude = 2.2f;

    [Header("Varredura de Scanline")]
    [SerializeField] private bool enableScanline = true;
    [SerializeField] private float scanlineSpeed = 85f;
    [SerializeField] private float scanlineTopY = 52f;
    [SerializeField] private float scanlineBottomY = -52f;

    private RectTransform visorRect;
    private Vector3 initialLocalPos;
    private float currentScanlineY;
    private float roundPulseBoost = 0f;

    private Material mathVisorHoloMat;
    private Material projectionBeamHoloMat;
    private Material emitterLensHoloMat;

    private static Texture2D cachedStarTexture;
    private static Material cachedStarMaterial;
    private static ParticleSystem warpTravelParticleSystem;

    private static readonly int PropVisorMode = Shader.PropertyToID("_VisorMode");
    private static readonly int PropPrimaryColor = Shader.PropertyToID("_PrimaryColor");
    private static readonly int PropSecondaryColor = Shader.PropertyToID("_SecondaryColor");
    private static readonly int PropBgColor = Shader.PropertyToID("_BgColor");
    private static readonly int PropPulseBoost = Shader.PropertyToID("_PulseBoost");

    private void Awake()
    {
        Instance = this;
        EnforceBlackSpaceSky();

        visorRect = GetComponent<RectTransform>();
        if (visorRect != null)
        {
            initialLocalPos = visorRect.localPosition;
        }

        if (visorBackground == null)
        {
            visorBackground = GetComponent<Image>();
        }

        ApplyHologramShaderToVisor();
        currentScanlineY = scanlineTopY;
    }

    private void Start()
    {
        EnforceBlackSpaceSky();
        EnsureDeepSpaceStarfield();
        SetSpaceTravelActive(false);
        StartCoroutine(EnsureBlackSkyRoutine());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (mathVisorHoloMat != null) Destroy(mathVisorHoloMat);
        if (projectionBeamHoloMat != null) Destroy(projectionBeamHoloMat);
        if (emitterLensHoloMat != null) Destroy(emitterLensHoloMat);
    }

    private IEnumerator EnsureBlackSkyRoutine()
    {
        yield return null;
        EnforceBlackSpaceSky();
        EnsureDeepSpaceStarfield();
    }

    /// <summary>
    /// Aplica o Shader de Holograma SDF (Custom/CockpitHologramVisor) ao visor da rodada/conta,
    /// ao feixe cônico de projeção e à lente emissora, removendo o fundo retangular opaco antigo.
    /// </summary>
    public void ApplyHologramShaderToVisor()
    {
        Shader holoShader = Shader.Find("Custom/CockpitHologramVisor");
        if (holoShader == null) return;

        // 1. Remove a cor sólida opaca do MathVisor_Border pai que antes lavava o visor
        if (transform.parent != null && transform.parent.name.IndexOf("MathVisor_Border", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Image parentBorderImg = transform.parent.GetComponent<Image>();
            if (parentBorderImg != null)
            {
                parentBorderImg.color = new Color(1f, 1f, 1f, 0f);
                parentBorderImg.raycastTarget = false;
            }
        }

        // 2. Configura o corpo principal do Holograma (MathVisor_Inner) com cantos chanfrados + vidro translúcido
        if (visorBackground != null)
        {
            if (mathVisorHoloMat == null)
            {
                mathVisorHoloMat = new Material(holoShader) { name = "MathVisor_HoloSDF_Mat" };
            }
            mathVisorHoloMat.SetFloat(PropVisorMode, 0f);
            mathVisorHoloMat.SetColor(PropPrimaryColor, new Color(0.18f, 0.94f, 1.00f, 1f));
            mathVisorHoloMat.SetColor(PropSecondaryColor, new Color(0.10f, 0.50f, 1.00f, 1f));
            mathVisorHoloMat.SetColor(PropBgColor, new Color(0.01f, 0.09f, 0.20f, 0.42f));

            visorBackground.material = mathVisorHoloMat;
            visorBackground.color = Color.white;
            visorBackground.raycastTarget = false;
        }

        // Oculta as cantoneiras retangulares antigas pois o shader SDF já desenha cantos chanfrados anti-aliased
        Transform cornerTL = transform.Find("HoloCorner_TL");
        if (cornerTL != null) cornerTL.gameObject.SetActive(false);
        Transform cornerTR = transform.Find("HoloCorner_TR");
        if (cornerTR != null) cornerTR.gameObject.SetActive(false);

        // 3. Configura o feixe cônico de luz (HoloProjection_Beam)
        if (projectionBeam != null)
        {
            if (projectionBeamHoloMat == null)
            {
                projectionBeamHoloMat = new Material(holoShader) { name = "HoloBeam_Cone_Mat" };
            }
            projectionBeamHoloMat.SetFloat(PropVisorMode, 1f);
            projectionBeamHoloMat.SetColor(PropPrimaryColor, new Color(0.22f, 0.96f, 1.00f, 1f));
            projectionBeamHoloMat.SetColor(PropSecondaryColor, new Color(0.08f, 0.52f, 0.98f, 1f));

            projectionBeam.material = projectionBeamHoloMat;
            projectionBeam.color = Color.white;
            projectionBeam.raycastTarget = false;

            RectTransform beamRt = projectionBeam.GetComponent<RectTransform>();
            if (beamRt != null)
            {
                beamRt.anchoredPosition = new Vector2(0f, -68f);
                beamRt.sizeDelta = new Vector2(460f, 34f);
            }
        }

        // 4. Configura a lente emissora no console (HoloEmitter_Lens)
        if (emitterLens != null)
        {
            if (emitterLensHoloMat == null)
            {
                emitterLensHoloMat = new Material(holoShader) { name = "HoloEmitter_Lens_Mat" };
            }
            emitterLensHoloMat.SetFloat(PropVisorMode, 2f);
            emitterLensHoloMat.SetColor(PropPrimaryColor, new Color(0.30f, 0.98f, 1.00f, 1f));

            emitterLens.material = emitterLensHoloMat;
            emitterLens.color = Color.white;
            emitterLens.raycastTarget = false;

            RectTransform lensRt = emitterLens.GetComponent<RectTransform>();
            if (lensRt != null)
            {
                lensRt.sizeDelta = new Vector2(260f, 8f);
            }
        }
    }

    public void TriggerNewRoundPulse()
    {
        roundPulseBoost = 1.0f;
    }

    private void Update()
    {
        float time = Time.time;

        if (roundPulseBoost > 0f)
        {
            roundPulseBoost = Mathf.Max(0f, roundPulseBoost - Time.deltaTime * 2.8f);
        }

        if (enableLightPulse)
        {
            float sinePulse = 0.5f + 0.5f * Mathf.Sin(time * pulseFrequency);
            float flicker = (Mathf.PerlinNoise(time * 31f, 0.5f) - 0.5f) * 2f * microFlickerIntensity;
            float totalBoost = Mathf.Clamp01(sinePulse * pulseAmplitude + roundPulseBoost * 0.75f + flicker);

            if (mathVisorHoloMat != null)
            {
                mathVisorHoloMat.SetFloat(PropPulseBoost, totalBoost);
            }
            if (projectionBeamHoloMat != null)
            {
                projectionBeamHoloMat.SetFloat(PropPulseBoost, totalBoost);
            }
        }

        // Micro-Flutuação holográfica no ar (Holo Bobbing)
        if (enableBobbing && visorRect != null)
        {
            float bobOffset = Mathf.Sin(time * bobFrequency) * bobAmplitude;
            visorRect.localPosition = new Vector3(
                initialLocalPos.x,
                initialLocalPos.y + bobOffset,
                initialLocalPos.z
            );
        }

        // Varredura suave da Scanline de cima para baixo
        if (enableScanline && scanlineBar != null)
        {
            currentScanlineY -= scanlineSpeed * Time.deltaTime;
            if (currentScanlineY < scanlineBottomY)
            {
                currentScanlineY = scanlineTopY;
            }

            Vector2 anchored = scanlineBar.anchoredPosition;
            anchored.y = currentScanlineY;
            scanlineBar.anchoredPosition = anchored;
        }
    }

    /// <summary>
    /// Ativa ou desativa o efeito cinematográfico de Viagem Espacial (Dobra Estelar / Warp Drive)
    /// quando o jogador vence todas as rodadas e a nave retoma sua jornada pelo espaço.
    /// </summary>
    public static void SetSpaceTravelActive(bool active)
    {
        EnsureStarMaterial();
        EnsureWarpTravelParticleSystem();

        if (warpTravelParticleSystem != null)
        {
            if (active)
            {
                warpTravelParticleSystem.gameObject.SetActive(true);
                if (!warpTravelParticleSystem.isPlaying)
                {
                    warpTravelParticleSystem.Clear();
                    warpTravelParticleSystem.Play();
                }
            }
            else
            {
                if (warpTravelParticleSystem.isPlaying)
                {
                    warpTravelParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                warpTravelParticleSystem.gameObject.SetActive(false);
            }
        }

        // Acelera também as partículas de poeira estelar da cena durante a viagem espacial
        GameObject movingStarsObj = GameObject.Find("StarfieldParticles");
        if (movingStarsObj != null)
        {
            ParticleSystem ps = movingStarsObj.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.simulationSpeed = active ? 5.5f : 1.0f;
            }
        }
    }

    private static void EnsureWarpTravelParticleSystem()
    {
        if (warpTravelParticleSystem != null) return;

        GameObject existing = GameObject.Find("ViagemEspacial_WarpDrive");
        if (existing != null)
        {
            warpTravelParticleSystem = existing.GetComponent<ParticleSystem>();
            if (warpTravelParticleSystem != null) return;
        }

        GameObject warpObj = new GameObject("ViagemEspacial_WarpDrive");
        // Posicionado à frente do para-brisa da nave, emitindo feixes de estrelas na direção -Z (rumo ao cockpit)
        warpObj.transform.position = new Vector3(0f, 1.8f, 68f);
        warpObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        warpTravelParticleSystem = warpObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = warpObj.GetComponent<ParticleSystemRenderer>();

        warpTravelParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = warpTravelParticleSystem.main;
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 750;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(42f, 78f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.36f);
        main.gravityModifier = 0f;

        // Gradiente de cores estelares em velocidade de dobra (Ciano Brilhante, Azul-Gelo, Branco Puro e Dourado Estelar)
        Gradient warpGrad = new Gradient();
        warpGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.28f, 0.95f, 1f), 0.0f),
                new GradientColorKey(new Color(0.75f, 0.92f, 1f), 0.45f),
                new GradientColorKey(new Color(1.0f, 0.98f, 0.90f), 0.80f),
                new GradientColorKey(new Color(0.45f, 0.78f, 1f), 1.0f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0.0f),
                new GradientAlphaKey(0.95f, 0.18f),
                new GradientAlphaKey(0.95f, 0.82f),
                new GradientAlphaKey(0f, 1.0f)
            }
        );
        var colOverLife = warpTravelParticleSystem.colorOverLifetime;
        colOverLife.enabled = true;
        colOverLife.color = new ParticleSystem.MinMaxGradient(warpGrad);

        var emission = warpTravelParticleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 340f;

        // Emissão em túnel cilíndrico ao redor do cockpit (donut radius) para envolver o para-brisa sem bater na câmera
        var shape = warpTravelParticleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.ConeVolume;
        shape.angle = 2.5f;
        shape.radius = 28f;
        shape.radiusThickness = 0.88f; // Deixa o centro (onde está a cabine) limpo
        shape.length = 18f;

        if (psr != null)
        {
            // Modo Stretch cria os rastros alongados de estrelas em hiperespaço!
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.cameraVelocityScale = 0f;
            psr.velocityScale = 0.14f;
            psr.lengthScale = 3.8f;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            if (cachedStarMaterial != null)
            {
                psr.sharedMaterial = cachedStarMaterial;
            }
        }

        warpObj.SetActive(false);
    }

    /// <summary>
    /// Garante que o fundo do céu seja 100% preto (#000000), mantendo uma luz ambiente espacial fria sutil.
    /// </summary>
    public static void EnforceBlackSpaceSky()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.08f, 0.14f, 0.24f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.05f, 0.09f, 0.16f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.02f, 0.04f, 0.08f, 1f);
        RenderSettings.ambientLight = new Color(0.06f, 0.11f, 0.20f, 1f);
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.reflectionIntensity = 0.25f;
        RenderSettings.fog = false;

        Camera[] cameras = Camera.allCameras;
        foreach (Camera cam in cameras)
        {
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }
        }

        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Color.black;
        }
    }

    /// <summary>
    /// Cria e configura um campo panorâmico de estrelas brilhantes ao fundo do espaço (dome estelar + poeira cósmica).
    /// </summary>
    public static void EnsureDeepSpaceStarfield()
    {
        EnsureStarMaterial();

        // 1. Aprimora o StarfieldParticles existente na cena
        GameObject movingStarsObj = GameObject.Find("StarfieldParticles");
        if (movingStarsObj != null)
        {
            ParticleSystem ps = movingStarsObj.GetComponent<ParticleSystem>();
            ParticleSystemRenderer psr = movingStarsObj.GetComponent<ParticleSystemRenderer>();
            if (psr != null && cachedStarMaterial != null)
            {
                psr.sharedMaterial = cachedStarMaterial;
            }
            if (ps != null)
            {
                var main = ps.main;
                main.prewarm = true;
                main.maxParticles = 600;
                main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.28f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);

                var emission = ps.emission;
                emission.rateOverTime = 45f;

                if (!ps.isPlaying) ps.Play();
            }
        }

        // 2. Cria a abóbada de estrelas de fundo profundo (CampoEstrelasProfundo) se ainda não existir
        if (GameObject.Find("CampoEstrelasProfundo") != null)
            return;

        GameObject domeObj = new GameObject("CampoEstrelasProfundo");
        domeObj.transform.position = new Vector3(0f, 2.0f, 0f);

        ParticleSystem domePs = domeObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer domeRenderer = domeObj.GetComponent<ParticleSystemRenderer>();

        domePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var domeMain = domePs.main;
        domeMain.loop = false;
        domeMain.playOnAwake = false;
        domeMain.simulationSpace = ParticleSystemSimulationSpace.World;
        domeMain.maxParticles = 900;
        domeMain.startLifetime = float.MaxValue;
        domeMain.startSpeed = 0f;
        domeMain.gravityModifier = 0f;

        var domeEmission = domePs.emission;
        domeEmission.enabled = false;

        var domeShape = domePs.shape;
        domeShape.enabled = false;

        if (domeRenderer != null)
        {
            domeRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            domeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            domeRenderer.receiveShadows = false;
            if (cachedStarMaterial != null)
            {
                domeRenderer.sharedMaterial = cachedStarMaterial;
            }
        }

        Color32[] starPalette = new Color32[]
        {
            new Color32(255, 255, 255, 255),
            new Color32(185, 225, 255, 255),
            new Color32(130, 235, 255, 245),
            new Color32(255, 232, 175, 240),
            new Color32(195, 205, 255, 235)
        };

        Random.State oldState = Random.state;
        Random.InitState(42077);

        const int starCount = 850;
        for (int i = 0; i < starCount; i++)
        {
            float yawDeg = (i < 600)
                ? Random.Range(-105f, 105f)
                : Random.Range(-180f, 180f);
            float pitchDeg = (i < 600)
                ? Random.Range(-16f, 68f)
                : Random.Range(-25f, 80f);

            Quaternion rot = Quaternion.Euler(-pitchDeg, yawDeg, 0f);
            Vector3 dir = rot * Vector3.forward;
            float dist = Random.Range(38f, 135f);
            Vector3 starPos = domeObj.transform.position + dir * dist;

            bool isBrightStar = (i % 8 == 0);
            float size = isBrightStar
                ? Random.Range(0.42f, 0.82f)
                : Random.Range(0.14f, 0.34f);

            Color32 col = starPalette[i % starPalette.Length];
            if (!isBrightStar)
            {
                col.a = (byte)Random.Range(145, 235);
            }

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = starPos,
                velocity = Vector3.zero,
                startSize = size,
                startLifetime = 999999f,
                startColor = col
            };
            domePs.Emit(emitParams, 1);
        }

        Random.state = oldState;
    }

    private static void EnsureStarMaterial()
    {
        if (cachedStarTexture == null)
        {
            const int res = 32;
            cachedStarTexture = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                name = "StarGlowRadial_Tex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float center = (res - 1) * 0.5f;
            float maxR = center;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = (x - center) / maxR;
                    float dy = (y - center) / maxR;
                    float dist = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));

                    float core = Mathf.Pow(1f - dist, 3.2f);
                    float halo = Mathf.Pow(1f - dist, 1.6f) * 0.55f;
                    float alpha = Mathf.Clamp01(core + halo);
                    cachedStarTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            cachedStarTexture.Apply(false, false);
        }

        if (cachedStarMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default")
                        ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Particles/Standard Unlit");
            cachedStarMaterial = new Material(sh)
            {
                name = "DeepSpaceStars_Mat"
            };
            cachedStarMaterial.mainTexture = cachedStarTexture;
            if (cachedStarMaterial.HasProperty("_BaseMap"))
            {
                cachedStarMaterial.SetTexture("_BaseMap", cachedStarTexture);
            }
            if (cachedStarMaterial.HasProperty("_MainTex"))
            {
                cachedStarMaterial.SetTexture("_MainTex", cachedStarTexture);
            }
        }
    }
}
