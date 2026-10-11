using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Identifica e controla o comportamento físico e de interação VR de uma bateria / célula de energia.
/// Suporta pegar e retirar da cabine, soltar, e encaixar firmemente no compartimento (socket).
/// </summary>
[DisallowMultipleComponent]
public class ItemBateria : MonoBehaviour
{
    [Header("Configuração da Bateria")]
    [Tooltip("Se verdadeiro, esta é a bateria danificada original que deve ser retirada.")]
    [SerializeField] private bool isDamaged = false;

    [Tooltip("Nome descritivo da bateria para feedback")]
    [SerializeField] private string batteryName = "Bateria Reserva";

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Collider col;
    private bool wasGrabbedOnce = false;

    public bool IsDamaged => isDamaged;
    public string BatteryName => batteryName;
    public bool WasGrabbedOnce => wasGrabbedOnce;
    public XRGrabInteractable GrabInteractable => grabInteractable;
    public Rigidbody RigidbodyComponent => rb;

    private void Awake()
    {
        InitializeComponents();
    }

    public void InitializeComponents()
    {
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.mass = 1.5f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;

        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null) grabInteractable = gameObject.AddComponent<XRGrabInteractable>();

        // Configuração de resposta imediata e natural para VR
        grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grabInteractable.useDynamicAttach = true;
        grabInteractable.throwOnDetach = true;

        if (col != null && !grabInteractable.colliders.Contains(col))
        {
            grabInteractable.colliders.Add(col);
        }

        // Se for a bateria danificada na cabine e ainda não foi retirada, mantém firme no início
        if (isDamaged && !wasGrabbedOnce)
        {
            rb.isKinematic = true;
        }

        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectEntered.AddListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        wasGrabbedOnce = true;
        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }

    /// <summary>
    /// Trava e ancora a bateria no socket (compartimento da cabine) após ser inserida.
    /// </summary>
    public void LockInSocket(Transform socketTransform)
    {
        if (grabInteractable != null)
        {
            grabInteractable.enabled = false;
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = socketTransform.position;
        transform.rotation = socketTransform.rotation;
        transform.SetParent(socketTransform, true);
    }

    public void SetDamaged(bool damaged, string name)
    {
        isDamaged = damaged;
        batteryName = name;
    }
}
