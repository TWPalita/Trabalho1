using UnityEngine;

/// <summary>
/// Gerenciador de efeitos sonoros com suporte a AudioClips personalizados
/// e geração procedural de som caso nenhum AudioClip seja fornecido no Inspector.
/// </summary>
public class SoundEffectsManager : MonoBehaviour
{
    public static SoundEffectsManager Instance { get; private set; }

    [Header("Clipes de Áudio (Opcionais - gera som procedural se vazio)")]
    [SerializeField] private AudioClip alarmClip;
    [SerializeField] private AudioClip laserClip;
    [SerializeField] private AudioClip correctClip;
    [SerializeField] private AudioClip errorClip;
    [SerializeField] private AudioClip clickClip;

    private AudioSource audioSource;
    private AudioSource alarmSource;

    private AudioClip cachedClickClip;
    private AudioClip cachedLaserClip;
    private AudioClip cachedCorrectClip;
    private AudioClip cachedErrorClip;
    private AudioClip cachedSirenClip;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        alarmSource = gameObject.AddComponent<AudioSource>();
        alarmSource.playOnAwake = false;
        alarmSource.loop = true;

        // Pre-cria e armazena os clipes de áudio uma única vez na inicialização
        cachedClickClip = CreateProceduralBeep(800f, 0.08f, 0.4f);
        cachedLaserClip = CreateProceduralLaser();
        cachedCorrectClip = CreateProceduralCorrect();
        cachedErrorClip = CreateProceduralError();
        cachedSirenClip = CreateSirenClip();
    }

    public void PlayClick()
    {
        audioSource.PlayOneShot(clickClip != null ? clickClip : cachedClickClip);
    }

    public void PlayLaser()
    {
        audioSource.PlayOneShot(laserClip != null ? laserClip : cachedLaserClip);
    }

    public void PlayCorrect()
    {
        audioSource.PlayOneShot(correctClip != null ? correctClip : cachedCorrectClip);
    }

    public void PlayError()
    {
        audioSource.PlayOneShot(errorClip != null ? errorClip : cachedErrorClip);
    }

    public void StartAlarm()
    {
        alarmSource.clip = alarmClip != null ? alarmClip : cachedSirenClip;
        if (!alarmSource.isPlaying)
        {
            alarmSource.Play();
        }
    }

    public void StopAlarm()
    {
        if (alarmSource != null && alarmSource.isPlaying)
        {
            alarmSource.Stop();
        }
    }

    #region Geração Procedural de Áudio

    private AudioClip CreateProceduralBeep(float freq, float duration, float volume)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (t / duration);
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * volume;
        }

        AudioClip clip = AudioClip.Create("Beep", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateProceduralLaser()
    {
        int sampleRate = 44100;
        float duration = 0.18f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(1200f, 250f, t / duration);
            float envelope = 1f - (t / duration);
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.5f;
        }

        AudioClip clip = AudioClip.Create("Laser", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateProceduralCorrect()
    {
        int sampleRate = 44100;
        float duration = 0.45f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        float[] notes = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }; // Dó, Mi, Sol, Dó (C5-E5-G5-C6)
        float noteDuration = duration / notes.Length;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            int noteIndex = Mathf.Min((int)(t / noteDuration), notes.Length - 1);
            float noteT = t - (noteIndex * noteDuration);
            float envelope = 1f - (noteT / noteDuration);
            samples[i] = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * t) * envelope * 0.4f;
        }

        AudioClip clip = AudioClip.Create("Correct", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateProceduralError()
    {
        int sampleRate = 44100;
        float duration = 0.35f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float freq = 140f;
            float envelope = 1f - (t / duration);
            // Onda quadrada / dente de serra para som áspero de erro
            float sample = Mathf.Sin(2f * Mathf.PI * freq * t) > 0 ? 0.35f : -0.35f;
            samples[i] = sample * envelope;
        }

        AudioClip clip = AudioClip.Create("Error", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateSirenClip()
    {
        int sampleRate = 44100;
        float duration = 1.2f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            // Modulação periódica de frequência para efeito de sirene
            float freq = Mathf.Lerp(600f, 950f, (Mathf.Sin(2f * Mathf.PI * 1.5f * t) + 1f) * 0.5f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f;
        }

        AudioClip clip = AudioClip.Create("Siren", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    #endregion
}
