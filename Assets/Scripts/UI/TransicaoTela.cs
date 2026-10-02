using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla o escurecimento e clareamento da tela (Fade In / Fade Out)
/// para transições suaves entre áreas e cenas em Realidade Virtual (VR).
/// </summary>
public class TransicaoTela : MonoBehaviour
{
    public static TransicaoTela Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<TransicaoTela>();
                if (_instance == null)
                {
                    GameObject faderObj = new GameObject("TransicaoTela_VR");
                    _instance = faderObj.AddComponent<TransicaoTela>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }
    private static TransicaoTela _instance;

    [Header("Configurações")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float defaultFadeDuration = 0.4f;
    [SerializeField] private bool fadeInOnStart = true;

    private GameObject fadeOverlayObject;
    private Camera targetCamera;
    private Coroutine currentFadeRoutine;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(this);
            return;
        }

        EnsureCanvasComponents();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
        if (fadeOverlayObject != null)
        {
            Destroy(fadeOverlayObject);
        }
    }

    private void Start()
    {
        if (fadeInOnStart && canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            FadeIn(defaultFadeDuration, null);
        }
    }

    private void LateUpdate()
    {
        // Garante que o Canvas do fade siga rigorosamente a visão do headset VR em tempo real
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null && fadeOverlayObject != null)
        {
            fadeOverlayObject.transform.position = targetCamera.transform.position + targetCamera.transform.forward * 0.12f;
            fadeOverlayObject.transform.rotation = targetCamera.transform.rotation;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        targetCamera = Camera.main;
        FadeIn(defaultFadeDuration, null);
    }

    private void EnsureCanvasComponents()
    {
        if (fadeOverlayObject == null)
        {
            fadeOverlayObject = new GameObject("VR_FadeOverlay");
            fadeOverlayObject.transform.SetParent(null);
            DontDestroyOnLoad(fadeOverlayObject);

            Canvas canvas = fadeOverlayObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 9999;

            RectTransform rt = canvas.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(2.5f, 2.5f);
                rt.localScale = Vector3.one;
            }

            canvasGroup = fadeOverlayObject.AddComponent<CanvasGroup>();

            GameObject imgObj = new GameObject("FadeBlackOverlay");
            imgObj.transform.SetParent(fadeOverlayObject.transform, false);
            Image img = imgObj.AddComponent<Image>();
            img.color = Color.black;

            RectTransform imgRt = img.GetComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.sizeDelta = new Vector2(20f, 20f);
        }
    }

    public void FadeIn(float duration = -1f, Action onComplete = null)
    {
        float d = duration > 0 ? duration : defaultFadeDuration;
        if (currentFadeRoutine != null) StopCoroutine(currentFadeRoutine);
        currentFadeRoutine = StartCoroutine(FadeRoutine(canvasGroup != null ? canvasGroup.alpha : 1f, 0f, d, onComplete));
    }

    public void FadeOut(float duration = -1f, Action onComplete = null)
    {
        float d = duration > 0 ? duration : defaultFadeDuration;
        if (currentFadeRoutine != null) StopCoroutine(currentFadeRoutine);
        currentFadeRoutine = StartCoroutine(FadeRoutine(canvasGroup != null ? canvasGroup.alpha : 0f, 1f, d, onComplete));
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
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
        canvasGroup.blocksRaycasts = (to > 0.95f);
        currentFadeRoutine = null;
        onComplete?.Invoke();
    }
}

public class SceneFader : TransicaoTela { }

