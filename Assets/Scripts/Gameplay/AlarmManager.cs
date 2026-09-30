using System.Collections;
using UnityEngine;

/// <summary>
/// Gerencia o alarme da espaçonave, acionando alertas visuais, luzes de emergência e som de sirene.
/// </summary>
public class AlarmManager : MonoBehaviour
{
    public static AlarmManager Instance { get; private set; }

    [Header("Configurações do Alarme")]
    [SerializeField] private float delayBeforeAlarm = 3.5f;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private Light[] emergencyLights;
    [SerializeField] private float pulseSpeed = 4.0f;
    [SerializeField] private float maxLightIntensity = 2.5f;

    [Header("Mensagens de Alerta")]
    [TextArea(2, 4)]
    [SerializeField] private string alertMessage = "ALERTA!\nOBJETOS ESPACIAIS DETECTADOS\nDIRIJA-SE AO PAINEL DE DEFESA";

    private bool isAlarmActive = false;
    private Coroutine alarmRoutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        if (emergencyLights != null)
        {
            foreach (var light in emergencyLights)
            {
                if (light != null)
                {
                    light.shadows = LightShadows.None;
                    light.color = Color.red;
                }
            }
        }
    }

    private void Start()
    {
        if (autoStart)
        {
            Invoke(nameof(TriggerAlarm), delayBeforeAlarm);
        }
    }

    public void TriggerAlarm()
    {
        if (isAlarmActive) return;
        isAlarmActive = true;

        Debug.Log("[AlarmManager] Alarme acionado!");

        // 1. Toca som do alarme
        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.StartAlarm();
        }

        // 2. Exibe alerta na interface
        if (GameUI.Instance != null)
        {
            GameUI.Instance.ShowAlarmAlert(alertMessage);
        }
        if (VRWorldSpaceUI.Instance != null)
        {
            VRWorldSpaceUI.Instance.ShowShipAlarm(alertMessage);
        }

        // 3. Inicia efeito visual de luzes de emergência
        if (alarmRoutine != null) StopCoroutine(alarmRoutine);
        alarmRoutine = StartCoroutine(EmergencyLightsRoutine());
    }

    public void StopAlarm()
    {
        if (!isAlarmActive) return;
        isAlarmActive = false;

        if (alarmRoutine != null)
        {
            StopCoroutine(alarmRoutine);
            alarmRoutine = null;
        }

        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.StopAlarm();
        }

        if (GameUI.Instance != null)
        {
            GameUI.Instance.HideAlarmAlert();
        }
        if (VRWorldSpaceUI.Instance != null)
        {
            VRWorldSpaceUI.Instance.HideShipAlarm();
        }

        // Desliga ou reseta as luzes de emergência
        if (emergencyLights != null)
        {
            foreach (var light in emergencyLights)
            {
                if (light != null) light.intensity = 0f;
            }
        }
    }

    private IEnumerator EmergencyLightsRoutine()
    {
        var wait = new WaitForSeconds(0.02f);
        while (isAlarmActive)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float currentIntensity = Mathf.Lerp(0.2f, maxLightIntensity, t);

            if (emergencyLights != null)
            {
                foreach (var light in emergencyLights)
                {
                    if (light != null)
                    {
                        light.intensity = currentIntensity;
                    }
                }
            }

            yield return wait;
        }
    }
}
