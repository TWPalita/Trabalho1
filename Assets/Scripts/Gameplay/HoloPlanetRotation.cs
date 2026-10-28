using UnityEngine;

public class HoloPlanetRotation : MonoBehaviour
{
    [SerializeField] private string supportObjectName = "HoloSupport";
    [SerializeField] private string planetObjectName = "Globe";
    [SerializeField] private float degreesPerSecond = 18f;

    private Transform planet;

    private void Start()
    {
        GameObject support = GameObject.Find(supportObjectName);
        if (support == null)
        {
            Debug.LogWarning($"[HoloPlanetRotation] Objeto '{supportObjectName}' não encontrado.");
            return;
        }

        Transform[] children = support.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == planetObjectName)
            {
                planet = children[i];
                break;
            }
        }

        if (planet == null)
            Debug.LogWarning($"[HoloPlanetRotation] Objeto '{planetObjectName}' não encontrado sob '{supportObjectName}'.");
    }

    private void Update()
    {
        if (planet != null)
            planet.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
