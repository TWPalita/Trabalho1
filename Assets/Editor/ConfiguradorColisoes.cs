using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Garante e valida que todas as paredes, portas, mesas, consoles e objetos físicos
/// da espaçonave possuam colisores físicos ativos e precisos para o jogador em VR.
/// </summary>
[InitializeOnLoad]
public static class ConfiguradorColisoes
{
    static ConfiguradorColisoes()
    {
        EditorApplication.delayCall += GarantirColisoesNave;
    }

    [MenuItem("MathBlaster/Validar e Garantir Colisões da Nave")]
    public static void GarantirColisoesNave()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.path.Contains("SampleScene") && !activeScene.name.Contains("SampleScene")) return;

        GameObject nave = GameObject.Find("Nave");
        if (nave == null) nave = GameObject.Find("nave");
        if (nave == null) return;

        int meshColliderCount = 0;
        int skippedCount = 0;

        MeshFilter[] meshFilters = nave.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            GameObject go = mf.gameObject;
            string name = go.name;

            // Ignora planos de fumaça e efeitos visuais puramente transparentes
            if (name.Contains("Fumer") || name.Contains("pPlane"))
            {
                skippedCount++;
                continue;
            }

            MeshCollider mc = go.GetComponent<MeshCollider>();
            if (mc == null)
            {
                mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
            }
            else if (mc.sharedMesh == null)
            {
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
            }

            meshColliderCount++;
        }

        // Garante também o colisor na mesa do painel do botão
        GameObject cockpit = GameObject.Find("CabinePiloto");
        if (cockpit == null) cockpit = GameObject.Find("Cockpit");
        if (cockpit != null)
        {
            MeshFilter[] cockpitFilters = cockpit.GetComponentsInChildren<MeshFilter>(true);
            foreach (var cmf in cockpitFilters)
            {
                if (cmf.sharedMesh == null) continue;
                MeshCollider cmc = cmf.gameObject.GetComponent<MeshCollider>();
                if (cmc == null)
                {
                    cmc = cmf.gameObject.AddComponent<MeshCollider>();
                    cmc.sharedMesh = cmf.sharedMesh;
                    cmc.convex = false;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log($"[ConfiguradorColisoes] Sucesso! {meshColliderCount} paredes, móveis e objetos da espaçonave verificados com colisão física ativa (MeshCollider).");
    }
}
