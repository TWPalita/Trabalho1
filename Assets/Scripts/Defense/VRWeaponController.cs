using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Controla o disparo de laser em VR utilizando o gatilho (Trigger) do controlador VR.
/// Lança um feixe de laser no espaço, detecta colisão com alvos espaciais e valida respostas.
/// Suporta controladores esquerdo e direito (dual-wielding).
/// </summary>
public class VRWeaponController : MonoBehaviour
{
    public static VRWeaponController Instance { get; private set; }
    public static List<VRWeaponController> ActiveControllers { get; } = new List<VRWeaponController>();

    [Header("Mão do Controlador")]
    [SerializeField] private UnityEngine.XR.XRNode controllerNode = UnityEngine.XR.XRNode.RightHand;

    [Header("Configuração de Disparo")]
    [SerializeField] private Transform muzzlePoint; // Ponto de saída do laser no controlador
    [SerializeField] private float maxDistance = 150f;
    [SerializeField] private float fireRate = 0.25f;
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Input Action do Gatilho")]
    [SerializeField] private InputActionProperty triggerAction;

    [Header("Efeito Visual do Laser")]
    [SerializeField] private Color laserColor = new Color(0.2f, 1f, 0.4f, 1f);
    [SerializeField] private float laserBeamDuration = 0.08f;

    private float nextFireTime = 0f;
    private bool canShoot = true;
    private LineRenderer pooledLine;
    private Material laserMat;
    private Coroutine beamRoutine;

    private void Awake()
    {
        if (Instance == null && controllerNode == UnityEngine.XR.XRNode.RightHand)
        {
            Instance = this;
        }
        if (!ActiveControllers.Contains(this))
        {
            ActiveControllers.Add(this);
        }

        if (muzzlePoint == null) muzzlePoint = transform;

        // Pré-aloca o LineRenderer para o feixe laser sem instanciamento em tempo de execução
        GameObject beamObj = new GameObject("VRLaserBeam_Pooled");
        beamObj.transform.SetParent(transform);
        pooledLine = beamObj.AddComponent<LineRenderer>();
        pooledLine.startWidth = 0.05f;
        pooledLine.endWidth = 0.02f;
        pooledLine.positionCount = 2;
        pooledLine.useWorldSpace = true;

        laserMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        laserMat.color = laserColor;
        pooledLine.material = laserMat;
        pooledLine.enabled = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ActiveControllers.Remove(this);
        if (laserMat != null) Destroy(laserMat);
    }

    public static void SetAllCanShoot(bool enable)
    {
        foreach (var c in ActiveControllers)
        {
            if (c != null) c.SetCanShoot(enable);
        }
    }

    private void OnEnable()
    {
        if (triggerAction.action != null)
        {
            triggerAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (triggerAction.action != null)
        {
            triggerAction.action.Disable();
        }
    }

    private void Update()
    {
        if (!canShoot || Time.time < nextFireTime) return;

        bool triggerPressed = false;

        // 1. Checa a Input Action oficial configurada no XR
        if (triggerAction.action != null && triggerAction.action.WasPressedThisFrame())
        {
            triggerPressed = true;
        }

        // 2. Checa dispositivo XR diretamente pelo node correspondente (RightHand ou LeftHand)
        if (!triggerPressed)
        {
            var handDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(controllerNode);
            if (handDevice.isValid)
            {
                if (handDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool pressed) && pressed)
                {
                    triggerPressed = true;
                }
                else if (handDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float triggerVal) && triggerVal > 0.6f)
                {
                    triggerPressed = true;
                }
            }
        }

        if (triggerPressed)
        {
            Fire();
        }
    }

    public void SetCanShoot(bool enable)
    {
        canShoot = enable;
    }

    public void Fire()
    {
        nextFireTime = Time.time + fireRate;

        // 1. Toca som de laser
        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayLaser();
        }

        // 2. Trajetória do disparo (a partir do muzzle na direção do controlador)
        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position;
        Vector3 forward = muzzlePoint != null ? muzzlePoint.forward : transform.forward;

        Vector3 targetPoint = origin + forward * maxDistance;
        GameObject hitObject = null;

        if (Physics.Raycast(origin, forward, out RaycastHit hit, maxDistance, hitLayers, QueryTriggerInteraction.Collide))
        {
            targetPoint = hit.point;
            hitObject = hit.collider.gameObject;
        }

        // 3. Renderiza o raio laser luminoso no espaço 3D usando o LineRenderer em cache
        if (beamRoutine != null) StopCoroutine(beamRoutine);
        beamRoutine = StartCoroutine(LaserBeamRoutine(origin, targetPoint));

        // 4. Avalia acerto com o alvo
        if (hitObject != null && DefenseManager.Instance != null)
        {
            Debris debris = hitObject.GetComponentInParent<Debris>();
            if (debris != null)
            {
                DefenseManager.Instance.OnDebrisHit(debris);
                return;
            }

            LixoEspacial lixo = hitObject.GetComponentInParent<LixoEspacial>();
            if (lixo != null)
            {
                DefenseManager.Instance.OnLegacyLixoHit(lixo);
            }
        }
    }

    private IEnumerator LaserBeamRoutine(Vector3 start, Vector3 end)
    {
        if (pooledLine != null)
        {
            pooledLine.SetPosition(0, start);
            pooledLine.SetPosition(1, end);
            pooledLine.enabled = true;

            yield return new WaitForSeconds(laserBeamDuration);

            pooledLine.enabled = false;
        }
    }
}
