using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Gerente de matemática integrado ao sistema VR, compatível com a cena existente.
/// </summary>
public class GerenteMatematica : MonoBehaviour
{
    public static GerenteMatematica Instance { get; private set; }

    [Header("Referências da Cena")]
    public TMP_Text telaDaConta;
    public GameObject lixoMolde;
    public Transform[] lugaresParaNascer;

    [Header("Configuração de Operações")]
    public MathOperationType tipoOperacao = MathOperationType.Mixed;
    public int numeroMinimo = 1;
    public int numeroMaximo = 12;

    private int respostaCerta;
    private List<GameObject> lixosAtivos = new List<GameObject>();
    private bool aguardandoProximaRodada = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        CriarNovaConta();
    }

    public void CriarNovaConta()
    {
        aguardandoProximaRodada = false;
        LimparLixos();

        // 1. Gera operação usando MathManager se disponível ou internamente
        int numero1 = 0;
        int numero2 = 0;
        string sinal = "+";

        int op = (int)tipoOperacao;
        if (tipoOperacao == MathOperationType.Mixed)
        {
            op = Random.Range(0, 3);
        }

        switch (op)
        {
            case 0: // Adição
                numero1 = Random.Range(numeroMinimo, numeroMaximo + 1);
                numero2 = Random.Range(numeroMinimo, numeroMaximo + 1);
                respostaCerta = numero1 + numero2;
                sinal = "+";
                break;

            case 1: // Subtração
                numero1 = Random.Range(numeroMinimo, numeroMaximo + 1);
                numero2 = Random.Range(numeroMinimo, numeroMaximo + 1);
                if (numero1 < numero2)
                {
                    int tmp = numero1;
                    numero1 = numero2;
                    numero2 = tmp;
                }
                respostaCerta = numero1 - numero2;
                sinal = "-";
                break;

            case 2: // Multiplicação
                numero1 = Random.Range(2, Mathf.Min(numeroMaximo, 10));
                numero2 = Random.Range(2, Mathf.Min(numeroMaximo, 10));
                respostaCerta = numero1 * numero2;
                sinal = "×";
                break;
        }

        string expressao = $"{numero1} {sinal} {numero2} = ?";

        if (telaDaConta != null)
        {
            telaDaConta.text = expressao;
        }

        if (VRWorldSpaceUI.Instance != null)
        {
            VRWorldSpaceUI.Instance.UpdateMathQuestion(expressao);
        }

        CriarLixos();
    }

    private void CriarLixos()
    {
        if (lugaresParaNascer == null || lugaresParaNascer.Length == 0 || lixoMolde == null) return;

        int lixoPremiado = Random.Range(0, lugaresParaNascer.Length);
        List<int> opcoesErradas = new List<int>();

        for (int i = 0; i < lugaresParaNascer.Length; i++)
        {
            if (lugaresParaNascer[i] == null) continue;

            GameObject novoLixo = Instantiate(lixoMolde, lugaresParaNascer[i].position, Quaternion.identity);
            lixosAtivos.Add(novoLixo);

            int valorParaLixo;
            if (i == lixoPremiado)
            {
                valorParaLixo = respostaCerta;
            }
            else
            {
                int candidato = respostaCerta + 1;
                int attempts = 0;
                do
                {
                    attempts++;
                    int offset = Random.Range(-10, 11);
                    if (offset == 0) offset = Random.Range(0, 2) == 0 ? 1 : -1;
                    candidato = respostaCerta + offset;
                    if (candidato < 0) candidato = Mathf.Abs(candidato) + 1;
                } while ((candidato == respostaCerta || opcoesErradas.Contains(candidato)) && attempts < 50);

                if (candidato == respostaCerta || opcoesErradas.Contains(candidato))
                {
                    candidato = respostaCerta + opcoesErradas.Count + 3;
                }

                opcoesErradas.Add(candidato);
                valorParaLixo = candidato;
            }

            LixoEspacial scriptLixo = novoLixo.GetComponent<LixoEspacial>();
            if (scriptLixo != null)
            {
                scriptLixo.ConfigurarLixo(valorParaLixo);
            }

            Debris deb = novoLixo.GetComponent<Debris>();
            if (deb != null)
            {
                deb.Init(valorParaLixo, DefenseManager.Instance);
            }
        }
    }

    public void TestarAcerto(int numeroAtingido, GameObject lixoDestruido)
    {
        if (aguardandoProximaRodada) return;

        if (numeroAtingido == respostaCerta)
        {
            // === ACERTO ===
            aguardandoProximaRodada = true;
            Debug.Log("[GerenteMatematica] ACERTOU!");

            if (ScoreManager.Instance != null) ScoreManager.Instance.AddCorrectHit();
            if (SoundEffectsManager.Instance != null) SoundEffectsManager.Instance.PlayCorrect();
            if (VRWorldSpaceUI.Instance != null) VRWorldSpaceUI.Instance.ShowFeedback(true, "CORRETO!\n+100 PONTOS");

            lixosAtivos.Remove(lixoDestruido);
            Destroy(lixoDestruido);

            StartCoroutine(NovaRodadaRoutine());
        }
        else
        {
            // === ERRO ===
            Debug.Log("[GerenteMatematica] ERROU!");

            if (ScoreManager.Instance != null) ScoreManager.Instance.AddWrongHit();
            if (SoundEffectsManager.Instance != null) SoundEffectsManager.Instance.PlayError();
            if (VRWorldSpaceUI.Instance != null) VRWorldSpaceUI.Instance.ShowFeedback(false, "ERRADO!\n-20 ENERGIA");

            lixosAtivos.Remove(lixoDestruido);
            Destroy(lixoDestruido);
        }
    }

    private IEnumerator NovaRodadaRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        LimparLixos();
        yield return new WaitForSeconds(1.1f);
        CriarNovaConta();
    }

    private void LimparLixos()
    {
        foreach (var lixo in lixosAtivos)
        {
            if (lixo != null) Destroy(lixo);
        }
        lixosAtivos.Clear();
    }
}