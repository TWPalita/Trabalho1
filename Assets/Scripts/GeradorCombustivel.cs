using UnityEngine;

public class GeradorCombustivel : MonoBehaviour
{
    private bool temCombustivel = false;
    private bool geradorAtivado = false;

    public void ReceberCombustivel()
    {
        if (temCombustivel)
            return;

        temCombustivel = true;

        Debug.Log("Gerador abastecido!");
    }

    public void AoClicarBotao()
    {
        // Verifica se o gerador recebeu combustível.
        if (!temCombustivel)
        {
            Debug.Log("Coloque gasolina no gerador primeiro!");
            return;
        }

        // Impede que o objetivo seja exibido repetidamente.
        if (geradorAtivado)
        {
            Debug.Log("O gerador já foi ativado!");
            return;
        }

        geradorAtivado = true;

        Debug.Log("Gerador ativado!");

        // Inicia a contagem e exibe o objetivo.
        if (GerenteAlarme.Instance != null)
        {
            GerenteAlarme.Instance.IniciarContagemAlerta();
            GerenteAlarme.Instance.MostrarObjetivo();
        }
        else
        {
            Debug.LogWarning(
                "GerenteAlarme não encontrado na cena."
            );
        }
    }
}
