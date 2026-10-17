using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class RadarNave : MonoBehaviour
{
    [SerializeField] GameObject[] telas;
    [SerializeField] AudioSource somLigando;
    [SerializeField] TMP_Text avisoRadar;
    [SerializeField] float atrasoDeteccao = 4f;

    public UnityEvent aoDetectarDetritos;
    bool ligado;

    public void Ligar()
    {
        if (ligado) return;
        ligado = true;
        StartCoroutine(Rotina());
    }

    IEnumerator Rotina()
    {
        if (telas != null)
        {
            foreach (var t in telas)
            {
                if (t != null) t.SetActive(true);
            }
        }
        if (somLigando) somLigando.Play();
        if (avisoRadar) avisoRadar.text = "Radar: varrendo o setor...";
        yield return new WaitForSeconds(atrasoDeteccao);
        if (avisoRadar) avisoRadar.text = "ALERTA: campo de detritos em rota de colisão";
        aoDetectarDetritos.Invoke();
    }
}
