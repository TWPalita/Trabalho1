using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

/// <summary>
/// Garante que o jogador fique fixo e seguro no assento do cockpit na DefenseScene.
/// Bloqueia rigorosamente a locomoção (andar, teleportar, girar por analógico),
/// mantendo a cabeça (HMD) totalmente livre para olhar em 360° e as mãos/controladores livres
/// para interagir e disparar contra os detritos espaciais.
/// Não afeta a SampleScene de forma alguma, pois atua exclusivamente na DefenseScene.
/// </summary>
public class AssentoCockpit : MonoBehaviour
{
    [Header("Posição do Assento no Cockpit")]
    [Tooltip("Posição exata do XR Origin na cadeira do piloto")]
    [SerializeField] private Vector3 seatedPosition = new Vector3(0f, 0.2f, 0.3f);
    [Tooltip("Rotação inicial do XR Origin voltada para o para-brisa frontal")]
    [SerializeField] private Vector3 seatedEulerRotation = Vector3.zero;
    [SerializeField] private bool lockPosition = true;

    [Header("Bloqueio de Locomoção VR")]
    [Tooltip("Desativa provedores de locomoção (Move, Teleport, Snap Turn)")]
    [SerializeField] private bool disableLocomotionProviders = true;

    private void Awake()
    {
        ApplyCockpitRigLock();
    }

    private void Start()
    {
        ApplyCockpitRigLock();
        DisableAllLocomotion();
    }

    private void OnEnable()
    {
        DisableAllLocomotion();
    }

    /// <summary>
    /// Configura a posição inicial no assento e desativa CharacterController para não cair
    /// </summary>
    public void ApplyCockpitRigLock()
    {
        // 1. Desativa CharacterController para evitar quedas e deslocamentos físicos
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        // 2. Trava a posição e rotação na cadeira do cockpit
        if (lockPosition)
        {
            transform.position = seatedPosition;
            transform.rotation = Quaternion.Euler(seatedEulerRotation);
        }
    }

    /// <summary>
    /// Desativa todos os sistemas de locomoção SOMENTE nesta cena
    /// </summary>
    public void DisableAllLocomotion()
    {
        if (!disableLocomotionProviders) return;

        // 1. Desativa todos os componentes derivados de LocomotionProvider no XR Origin e filhos
        var allLocomotion = GetComponentsInChildren<LocomotionProvider>(true);
        foreach (var lp in allLocomotion)
        {
            if (lp != null) lp.enabled = false;
        }

        // 2. Desativa GameObjects específicos de locomoção padrão do Starter Assets do XRI
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            string n = child.name;
            if (n.Equals("Move", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Teleportation", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Turn", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Snap Turn", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Gravity", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Climb", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Climb Teleport", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Grab Move", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Jump", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Locomotion", System.StringComparison.OrdinalIgnoreCase))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void LateUpdate()
    {
        // Garante que o corpo do jogador fique permanentemente fixo no assento do cockpit,
        // enquanto a cabeça (Main Camera) e mãos continuam 100% livres para rastreamento 6DOF VR
        if (lockPosition)
        {
            if (transform.position != seatedPosition)
            {
                transform.position = seatedPosition;
            }
            if (transform.rotation != Quaternion.Euler(seatedEulerRotation))
            {
                transform.rotation = Quaternion.Euler(seatedEulerRotation);
            }
        }
    }
}

public class CockpitSeatedRig : AssentoCockpit { }
