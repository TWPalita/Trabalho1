using TMPro;
using UnityEngine;

/// <summary>
/// Controlador do sistema de objetivos dinâmicos.
/// Redireciona automaticamente as mensagens para o HUD compacto no canto da tela
/// (em GerenteAlarme) para evitar múltiplos Canvases flutuando na frente do jogador.
/// </summary>
public class PainelObjetivo : MonoBehaviour
{
    public static PainelObjetivo Instance { get; private set; }

    [Header("Elementos de UI do Objetivo (Legado)")]
    [SerializeField] private GameObject canvasObjetivo;
    [SerializeField] private TMP_Text textoDescricao;
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
    }

    private void Start()
    {
        if (canvasObjetivo != null && canvasObjetivo != gameObject)
        {
            canvasObjetivo.SetActive(false);
        }
    }

    /// <summary>
    /// Exibe o objetivo diretamente no HUD no canto da tela sem abrir um Canvas bloqueando a visão.
    /// </summary>
    public void Mostrar(string novoTexto)
    {
        if (canvasObjetivo != null && canvasObjetivo != gameObject)
        {
            canvasObjetivo.SetActive(false);
        }

        if (GerenteAlarme.Instance != null)
        {
            GerenteAlarme.Instance.AtualizarObjetivoNoCanto(
                "OBJETIVO ATUAL",
                novoTexto,
                "",
                GerenteAlarme.HologramTheme.InfoCyan
            );
            return;
        }

        if (textoDescricao != null)
        {
            textoDescricao.text = novoTexto;
        }
    }

    public void Esconder()
    {
        if (canvasObjetivo != null && canvasObjetivo != gameObject)
        {
            canvasObjetivo.SetActive(false);
        }
    }
}
