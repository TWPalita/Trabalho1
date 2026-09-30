using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla o escurecimento e clareamento da tela (Fade In / Fade Out)
/// para transições suaves entre áreas e cenas.
/// </summary>
public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Header("Configurações")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float defaultFadeDuration = 0.8f;
    [SerializeField] private bool fadeInOnStart = true;

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

        if (canvasGroup == null)
        {
            canvasGroup = GetComponentInChildren<CanvasGroup>();
        }
    }

    private void Start()
    {
        if (fadeInOnStart && canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            StartCoroutine(FadeRoutine(1f, 0f, defaultFadeDuration, null));
        }
    }

    public void FadeIn(float duration = -1f, Action onComplete = null)
    {
        float d = duration > 0 ? duration : defaultFadeDuration;
        StartCoroutine(FadeRoutine(1f, 0f, d, onComplete));
    }

    public void FadeOut(float duration = -1f, Action onComplete = null)
    {
        float d = duration > 0 ? duration : defaultFadeDuration;
        StartCoroutine(FadeRoutine(0f, 1f, d, onComplete));
    }

    private IEnumerator FadeRoutine(float from, float to, float duration, Action onComplete)
    {
        if (canvasGroup == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        canvasGroup.blocksRaycasts = (to > 0.05f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
        canvasGroup.blocksRaycasts = (to > 0.95f);
        onComplete?.Invoke();
    }
}
