using TMPro;
using UnityEngine;

/// <summary>
/// Controlador gerenciador do sistema de objetivos dinâmicos que acompanha o jogador em VR.
/// Posicionado em um objeto gerenciador na cena (GerenciadorObjetivos), mantém referências
/// para o Canvas vermelho de objetivo, o texto descritivo e a câmera VR do jogador.
/// </summary>
public class PainelObjetivo : MonoBehaviour
{
    public static PainelObjetivo Instance { get; private set; }

    [Header("Elementos de UI do Objetivo")]
    [Tooltip("GameObject do Canvas de objetivo que será ativado e desativado")]
    [SerializeField] private GameObject canvasObjetivo;

    [Tooltip("Componente TMP_Text da descrição do objetivo")]
    [SerializeField] private TMP_Text textoDescricao;

    [Header("Referência da Câmera VR")]
    [Tooltip("Transform da câmera principal do jogador (Main Camera dentro do XR Origin)")]
    [SerializeField] private Transform cameraJogador;

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

        LocalizarCameraSeNecessario();
    }

    private void Start()
    {
        LocalizarCameraSeNecessario();

        // Inicia com o Canvas de objetivo desativado até a primeira chamada
        if (canvasObjetivo != null && canvasObjetivo != gameObject)
        {
            canvasObjetivo.SetActive(false);
        }
    }

    private void LocalizarCameraSeNecessario()
    {
        if (cameraJogador == null)
        {
            if (Camera.main != null)
            {
                cameraJogador = Camera.main.transform;
            }
            else
            {
                var camObj = GameObject.FindWithTag("MainCamera");
                if (camObj != null)
                {
                    cameraJogador = camObj.transform;
                }
            }
        }
    }

    /// <summary>
    /// Atualiza a descrição do objetivo, calcula o vetor forward nivelado horizontalmente
    /// (zerando o eixo Y), posiciona o Canvas a 1,6m à frente da cabeça do jogador,
    /// rotaciona-o para encarar o usuário e ativa o GameObject.
    /// </summary>
    /// <param name="novoTexto">Mensagem do objetivo a ser exibida</param>
    public void Mostrar(string novoTexto)
    {
        if (textoDescricao != null)
        {
            textoDescricao.text = novoTexto;
        }

        LocalizarCameraSeNecessario();

        if (canvasObjetivo != null)
        {
            if (cameraJogador != null)
            {
                // Calcula o vetor forward da câmera zerando o eixo Y (mantém nivelado horizontalmente)
                Vector3 forwardHorizontal = cameraJogador.forward;
                forwardHorizontal.y = 0f;

                // Proteção para o caso de o jogador estar olhando perfeitamente para cima ou para baixo
                if (forwardHorizontal.sqrMagnitude < 0.001f)
                {
                    forwardHorizontal = Vector3.ProjectOnPlane(cameraJogador.up, Vector3.up);
                    if (forwardHorizontal.sqrMagnitude < 0.001f)
                    {
                        forwardHorizontal = Vector3.forward;
                    }
                }

                forwardHorizontal.Normalize();

                // Posiciona o Canvas a exatamente 1,6 metro à frente da cabeça do jogador
                Vector3 posicaoAlvo = cameraJogador.position + forwardHorizontal * 1.6f;
                canvasObjetivo.transform.position = posicaoAlvo;

                // Rotaciona com Quaternion.LookRotation para ficar de frente para o jogador
                canvasObjetivo.transform.rotation = Quaternion.LookRotation(forwardHorizontal);
            }

            canvasObjetivo.SetActive(true);
        }
    }

    /// <summary>
    /// Desativa o GameObject do Canvas de objetivo quando a tarefa for cumprida.
    /// </summary>
    public void Esconder()
    {
        if (canvasObjetivo != null)
        {
            canvasObjetivo.SetActive(false);
        }
    }
}
