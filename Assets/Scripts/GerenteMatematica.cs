using UnityEngine;
using TMPro;

public class GerenteMatematica : MonoBehaviour
{
    public TMP_Text telaDaConta; // Onde aparece o "2 + 2 = ?"
    public GameObject lixoMolde; // O Prefab do lixo que criamos
    public Transform[] lugaresParaNascer; // Pontos de onde o lixo vai surgir

    private int respostaCerta;

    void Start()
    {
        CriarNovaConta();
    }

    public void CriarNovaConta()
    {
        // Sorteia dois números de 1 a 5
        int numero1 = Random.Range(1, 6);
        int numero2 = Random.Range(1, 6);

        // Calcula a resposta
        respostaCerta = numero1 + numero2;

        // Mostra no painel
        telaDaConta.text = numero1 + " + " + numero2 + " = ?";

        // Cria os lixos no espaço
        CriarLixos();
    }

    void CriarLixos()
    {
        // Escolhe qual lixo vai ter a resposta certa (0, 1 ou 2)
        int lixoPremiado = Random.Range(0, lugaresParaNascer.Length);

        // Para cada lugar de nascimento que você configurar, ele cria um lixo
        for (int i = 0; i < lugaresParaNascer.Length; i++)
        {
            // Cria um clone do lixo
            GameObject novoLixo = Instantiate(lixoMolde, lugaresParaNascer[i].position, Quaternion.identity);

            // Pega o script do lixo para passarmos o número
            LixoEspacial scriptDoLixo = novoLixo.GetComponent<LixoEspacial>();

            if (i == lixoPremiado)
            {
                // Se for o escolhido, dá a resposta certa
                scriptDoLixo.ConfigurarLixo(respostaCerta);
            }
            else
            {
                // Se não, inventa um número errado (soma + 2, ou -1, etc)
                // Para não complicar, vamos só sortear um número aleatório diferente
                int numeroErrado = Random.Range(1, 15);
                if (numeroErrado == respostaCerta) numeroErrado += 1; // Garante que não é igual a resposta certa

                scriptDoLixo.ConfigurarLixo(numeroErrado);
            }
        }
    }

    // A arma vai avisar o gerente quando acertar um alvo
    public void TestarAcerto(int numeroAtingido, GameObject lixoDestruido)
    {
        if (numeroAtingido == respostaCerta)
        {
            Debug.Log("ACERTOU MIZERAVI!");
            Destroy(lixoDestruido); // Destrói o lixo
            CriarNovaConta(); // Gera uma nova rodada
        }
        else
        {
            Debug.Log("ERROU! Perdeu vida!");
            Destroy(lixoDestruido); // Destrói o lixo errado também
        }
    }
}