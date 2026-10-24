using UnityEngine;

public class GeradorCombustivel : MonoBehaviour
{
    public static GeradorCombustivel Instance { get; private set; }

    private bool temCombustivel = false;
    private bool geradorAtivado = false;

    public bool TemCombustivel => temCombustivel;
    public bool GeradorAtivado => geradorAtivado;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    public void ReceberCombustivel()
    {
        if (temCombustivel)
            return;

        temCombustivel = true;

        Debug.Log("Gerador abastecido!");

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayClick();
        }

        if (GerenteAlarme.Instance != null)
        {
            GerenteAlarme.Instance.MostrarAvisoAbastecimentoConcluido();
        }
    }

    public void AoClicarBotao()
    {
        // Verifica se o gerador recebeu combustível.
        if (!temCombustivel)
        {
            Debug.Log("Coloque gasolina no gerador primeiro!");

            if (GerenteSom.Instance != null)
            {
                GerenteSom.Instance.PlayClick();
            }

            if (GerenteAlarme.Instance != null)
            {
                GerenteAlarme.Instance.MostrarAvisoGeradorSemCombustivel();
            }

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

        if (GerenteSom.Instance != null)
        {
            GerenteSom.Instance.PlayCorrect();
        }

        // Dispara o alarme de emergência e atualiza o objetivo no canto da tela de forma única e imediata.
        if (GerenteAlarme.Instance != null)
        {
            GerenteAlarme.Instance.TriggerAlarm();
        }
        else
        {
            Debug.LogWarning(
                "GerenteAlarme não encontrado na cena."
            );
        }
    }
}
