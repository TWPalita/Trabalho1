using UnityEngine;

/// <summary>
/// Garante que o jogador fique fixo e seguro no assento do cockpit na DefenseScene,
/// desativando a gravidade e o CharacterController que causariam queda no espaço.
/// Permite rotação e movimentação da cabeça (HMD) e miras VR completas sem perder a posição do assento.
/// </summary>
public class AssentoCockpit : MonoBehaviour
{
    [Header("Configurações do Assento")]
    [SerializeField] private Vector3 seatedPosition = new Vector3(0f, 0.2f, 0.3f);
    [SerializeField] private bool lockPosition = true;

    private void Awake()
    {
        // 1. Desativa CharacterController para que a gravidade de locomoção não faça o jogador cair
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        // 2. Localiza e desativa qualquer componente ou GameObject de gravidade do XR Origin
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child.name.Equals("Gravity", System.StringComparison.OrdinalIgnoreCase))
            {
                child.gameObject.SetActive(false);
            }
        }

        // 3. Posiciona o XR Origin no assento
        if (lockPosition)
        {
            transform.position = seatedPosition;
        }
    }

    private void Update()
    {
        // Garante que nenhum provedor de movimento acidental desloque o assento do cockpit
        if (lockPosition && transform.position != seatedPosition)
        {
            transform.position = seatedPosition;
        }
    }
}

public class CockpitSeatedRig : AssentoCockpit { }

