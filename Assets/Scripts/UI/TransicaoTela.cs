using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla o escurecimento e clareamento da tela (Fade In / Fade Out)
/// para transições suaves entre áreas e cenas em Realidade Virtual (VR).
/// Desativa o Canvas quando totalmente transparente para eliminar overdraw e manter alta fluidez.
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
    [SerializeField] private float defaultFadeDuration = 0.32f;
    [SerializeField] private bool fadeInOnStart = true;

    private GameObject fadeOverlayObject;
    private Canvas fadeCanvas;
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

        // Garante taxa de quadros fluida
        if (Application.targetFrameRate < 90)
        {
            Application.targetFrameRate = 90;
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
        if (fadeOverlayObject == null || !fadeOverlayObject.activeSelf) return;

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            Transform camTr = targetCamera.transform;
            fadeOverlayObject.transform.SetPositionAndRotation(
                camTr.position + camTr.forward * 0.12f,
                camTr.rotation
            );
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

            fadeCanvas = fadeOverlayObject.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.WorldSpace;
            fadeCanvas.sortingOrder = 9999;

            RectTransform rt = fadeCanvas.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(2.5f, 2.5f);
                rt.localScale = Vector3.one;
            }

            canvasGroup = fadeOverlayObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GameObject imgObj = new GameObject("FadeBlackOverlay");
            imgObj.transform.SetParent(fadeOverlayObject.transform, false);
            Image img = imgObj.AddComponent<Image>();
            img.color = new Color(0.01f, 0.02f, 0.05f, 1f);
            img.raycastTarget = false;

            RectTransform imgRt = img.GetComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.sizeDelta = new Vector2(20f, 20f);
        }
    }

    public void FadeIn(float duration = -1f, Action onComplete = null)
    {
        EnsureCanvasComponents();
        float d = duration > 0f ? duration : defaultFadeDuration;
        if (currentFadeRoutine != null) StopCoroutine(currentFadeRoutine);
        currentFadeRoutine = StartCoroutine(FadeRoutine(canvasGroup != null ? canvasGroup.alpha : 1f, 0f, d, onComplete));
    }

    public void FadeOut(float duration = -1f, Action onComplete = null)
    {
        EnsureCanvasComponents();
        float d = duration > 0f ? duration : defaultFadeDuration;
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

        if (fadeOverlayObject != null && !fadeOverlayObject.activeSelf)
        {
            fadeOverlayObject.SetActive(true);
        }

        canvasGroup.blocksRaycasts = (to > 0.5f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
        canvasGroup.blocksRaycasts = (to > 0.95f);

        // Desativa o GameObject quando totalmente transparente para zerar overdraw na câmera VR
        if (to <= 0.001f && fadeOverlayObject != null)
        {
            fadeOverlayObject.SetActive(false);
        }

        currentFadeRoutine = null;
        onComplete?.Invoke();
    }
}

public class SceneFader : TransicaoTela { }
