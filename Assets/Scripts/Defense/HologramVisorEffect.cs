using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efeito visual auxiliar para o visor holográfico (MathVisor) no cockpit da nave.
/// Simula pulso de emissão de luz, varredura de scanlines holográficas e micro-flutuação
/// de projeção tridimensional emanada do painel de instrumentos em Realidade Virtual.
/// Totalmente independente da lógica de jogo.
/// </summary>
public class HologramVisorEffect : MonoBehaviour
{
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
    [SerializeField] private float bobFrequency = 1.4f;
    [SerializeField] private float bobAmplitude = 2.5f;

    [Header("Varredura de Scanline")]
    [SerializeField] private bool enableScanline = true;
    [SerializeField] private float scanlineSpeed = 85f;
    [SerializeField] private float scanlineTopY = 48f;
    [SerializeField] private float scanlineBottomY = -48f;

    private RectTransform visorRect;
    private Vector3 initialLocalPos;
    private Color initialVisorColor;
    private Color initialBeamColor;
    private Color initialEmitterColor;
    private float currentScanlineY;

    private void Awake()
    {
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

        if (visorBackground != null)
        {
            initialVisorColor = visorBackground.color;
        }

        if (projectionBeam != null)
        {
            initialBeamColor = projectionBeam.color;
        }

        if (emitterLens != null)
        {
            initialEmitterColor = emitterLens.color;
        }

        currentScanlineY = scanlineTopY;
    }

    private void Start()
    {
        EnforceBlackSpaceSky();
        StartCoroutine(EnsureBlackSkyRoutine());
    }

    private IEnumerator EnsureBlackSkyRoutine()
    {
        yield return null;
        EnforceBlackSpaceSky();
    }

    private void Update()
    {
        float time = Time.time;

        // 1. Pulso de Luz e Micro-Flicker Holográfico
        if (enableLightPulse)
        {
            float sinePulse = Mathf.Sin(time * pulseFrequency) * pulseAmplitude;
            float flicker = (Mathf.PerlinNoise(time * 12f, 0f) - 0.5f) * microFlickerIntensity;
            float totalDelta = sinePulse + flicker;

            if (visorBackground != null)
            {
                Color c = initialVisorColor;
                c.a = Mathf.Clamp01(initialVisorColor.a + totalDelta);
                visorBackground.color = c;
            }

            if (projectionBeam != null)
            {
                Color bc = initialBeamColor;
                bc.a = Mathf.Clamp01(initialBeamColor.a + (totalDelta * 0.8f));
                projectionBeam.color = bc;
            }

            if (emitterLens != null)
            {
                Color ec = initialEmitterColor;
                ec.a = Mathf.Clamp01(initialEmitterColor.a + (totalDelta * 0.5f));
                emitterLens.color = ec;
            }
        }

        // 2. Micro-Flutuação no ar (Holo Bobbing)
        if (enableBobbing && visorRect != null)
        {
            float bobOffset = Mathf.Sin(time * bobFrequency) * bobAmplitude;
            visorRect.localPosition = new Vector3(
                initialLocalPos.x,
                initialLocalPos.y + bobOffset,
                initialLocalPos.z
            );
        }

        // 3. Varredura da Scanline de cima para baixo
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
    /// Garante que o céu e o ambiente sejam 100% pretos (espaço profundo absoluto #000000).
    /// </summary>
    public static void EnforceBlackSpaceSky()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientSkyColor = Color.black;
        RenderSettings.ambientEquatorColor = Color.black;
        RenderSettings.ambientGroundColor = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.reflectionIntensity = 0f;
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
}
