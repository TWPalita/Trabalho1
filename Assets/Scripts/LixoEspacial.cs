using UnityEngine;
using TMPro; // Usado para mexer com textos modernos da Unity

public class LixoEspacial : MonoBehaviour
{
    public TMP_Text textoDoNumero; // Onde vamos arrastar o texto 3D
    public float velocidade = 2f; // Velocidade que o lixo voa até você

    [HideInInspector] // Esconde no Inspector para não bagunçar
    public int valorDesteLixo; // O número que este lixo representa

    // O Gerente vai chamar essa função para dar um número a este lixo
    public void ConfigurarLixo(int numero)
    {
        valorDesteLixo = numero;
        textoDoNumero.text = numero.ToString(); // Muda o texto na tela
    }

    void Update()
    {
        // Faz o lixo voar para frente (na direção do jogador) aos poucos
        transform.Translate(Vector3.back * velocidade * Time.deltaTime);
    }
}