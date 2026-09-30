using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controla a mira, disparo de laser e detecção de acerto por Raycast no Sistema de Defesa.
/// </summary>
public class WeaponController : MonoBehaviour
{
    [Header("Configurações da Arma")]
    [SerializeField] private float fireRate = 0.3f;
    [SerializeField] private float maxRange = 200f;
    [SerializeField] private LayerMask hitLayer = ~0;
    [SerializeField] private Transform[] muzzlePoints; // Origem dos lasers (canhões da nave)
    [SerializeField] private LineRenderer laserTracerPrefab;

    [Header("Mira")]
    [SerializeField] private RectTransform crosshairUI;
    [SerializeField] private bool followMouse = true;
    [SerializeField] private Camera aimCamera;

    private float nextFireTime = 0f;
    private bool canShoot = true;

    private void Awake()
    {
        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }
    }

    private void Start()
    {
        // No sistema de defesa com mouse, cursor visível na janela
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false; // A mira UI substitui o cursor
    }

    private void Update()
    {
        UpdateCrosshairPosition();
        CheckFireInput();
    }

    private void UpdateCrosshairPosition()
    {
        if (crosshairUI == null) return;

        Vector2 mousePos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            mousePos = Mouse.current.position.ReadValue();
        }
#else
        mousePos = Input.mousePosition;
#endif

        if (followMouse)
        {
            crosshairUI.position = mousePos;
        }
    }

    private void CheckFireInput()
    {
        if (!canShoot || Time.time < nextFireTime) return;

        bool firePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            firePressed = true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            firePressed = true;
        }
#endif

        if (firePressed)
        {
            Fire();
        }
    }

    public void SetCanShoot(bool enable)
    {
        canShoot = enable;
    }

    private void Fire()
    {
        nextFireTime = Time.time + fireRate;

        // Toca som de tiro
        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayLaser();
        }

        Vector3 screenPoint = crosshairUI != null ? crosshairUI.position : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        Ray ray = aimCamera != null ? aimCamera.ScreenPointToRay(screenPoint) : new Ray(transform.position, transform.forward);

        Vector3 targetPoint = ray.origin + ray.direction * maxRange;
        GameObject hitObject = null;

        if (Physics.Raycast(ray, out RaycastHit hit, maxRange, hitLayer, QueryTriggerInteraction.Collide))
        {
            targetPoint = hit.point;
            hitObject = hit.collider.gameObject;
        }

        // Renderiza feixe de laser visual
        ShowLaserTracer(targetPoint);

        // Processa acerto se atingiu um alvo
        if (hitObject != null && DefenseManager.Instance != null)
        {
            Debris debris = hitObject.GetComponentInParent<Debris>();
            if (debris != null)
            {
                DefenseManager.Instance.OnDebrisHit(debris);
                return;
            }

            // Fallback para o script legado LixoEspacial, se houver
            LixoEspacial lixo = hitObject.GetComponentInParent<LixoEspacial>();
            if (lixo != null)
            {
                DefenseManager.Instance.OnLegacyLixoHit(lixo);
            }
        }
    }

    private void ShowLaserTracer(Vector3 targetPoint)
    {
        if (muzzlePoints != null && muzzlePoints.Length > 0)
        {
            foreach (var muzzle in muzzlePoints)
            {
                if (muzzle != null) StartCoroutine(LaserBeamRoutine(muzzle.position, targetPoint));
            }
        }
        else
        {
            Vector3 origin = aimCamera != null ? aimCamera.transform.position + aimCamera.transform.forward * 0.5f + aimCamera.transform.up * -0.2f : transform.position;
            StartCoroutine(LaserBeamRoutine(origin, targetPoint));
        }
    }

    private IEnumerator LaserBeamRoutine(Vector3 start, Vector3 end)
    {
        GameObject lineObj = new GameObject("LaserBeam");
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.startWidth = 0.08f;
        line.endWidth = 0.04f;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.useWorldSpace = true;

        // Cria material emissivo verde/ciano brilhante
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        mat.color = new Color(0.2f, 1f, 0.4f, 1f);
        line.material = mat;

        yield return new WaitForSeconds(0.08f);

        Destroy(mat);
        Destroy(lineObj);
    }
}
