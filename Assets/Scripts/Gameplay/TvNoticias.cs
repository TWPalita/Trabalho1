using System.Collections;
using TMPro;
using UnityEngine;

public class TvNoticias : MonoBehaviour
{
    [SerializeField] TMP_Text texto;
    [SerializeField] AudioSource som;
    [SerializeField, TextArea(2, 4)] string[] manchetes =
    {
        "ÚLTIMA HORA: estação de mineração destruída no setor 7.",
        "Detritos da explosão cruzam a rota de naves civis nos próximos minutos.",
        "Naves na região: mantenham os sistemas de defesa prontos."
    };
    [SerializeField] float segundosPorManchete = 5f;

    public void Transmitir()
    {
        StopAllCoroutines();
        StartCoroutine(Rotina());
    }

    IEnumerator Rotina()
    {
        if (som) som.Play();
        foreach (var m in manchetes)
        {
            if (texto) texto.text = m;
            yield return new WaitForSeconds(segundosPorManchete);
        }
    }
}
