using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Garante e valida que todas as paredes, portas, mesas, consoles e objetos físicos
/// da espaçonave na SampleScene possuam colisores físicos ativos (BoxColliders nas paredes + MeshColliders)
/// para o jogador em VR.
/// </summary>
[InitializeOnLoad]
public static class ConfiguradorColisoes
{
    static ConfiguradorColisoes()
    {
        EditorApplication.delayCall += GarantirColisoesNave;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        GarantirColisoesNave();
    }

    [MenuItem("MathBlaster/Validar e Garantir Colisões da Nave")]
    public static void GarantirColisoesNave()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.IsValid()) return;
        if (!activeScene.path.Contains("SampleScene") && !activeScene.name.Contains("SampleScene")) return;

        // 1. Configura os BoxColliders precisos de todas as paredes da nave em ColisoresNave
        GameObject colisoresNaveObj = GameObject.Find("ColisoresNave");
        if (colisoresNaveObj == null)
        {
            colisoresNaveObj = new GameObject("ColisoresNave");
        }

        colisoresNaveObj.transform.position = Vector3.zero;
        colisoresNaveObj.transform.rotation = Quaternion.identity;
        colisoresNaveObj.transform.localScale = Vector3.one;

        // Remove os colisores antigos desalinhados/inativos
        string[] obsoleteNames =
        {
            "ColisorParedeEsquerda",
            "ColisorParedeDireita",
            "ColisorParedeTraseira",
            "ColisorParedeFrontal"
        };
        for (int i = 0; i < obsoleteNames.Length; i++)
        {
            Transform obs = colisoresNaveObj.transform.Find(obsoleteNames[i]);
            if (obs != null)
            {
                Object.DestroyImmediate(obs.gameObject);
            }
        }

        var wallDefs = InteracaoEColisaoNave.GetSampleSceneWallDefinitions();
        int wallBoxCount = 0;
        for (int i = 0; i < wallDefs.Length; i++)
        {
            var def = wallDefs[i];
            Transform childTr = colisoresNaveObj.transform.Find(def.Name);
            GameObject wallObj;
            if (childTr == null)
            {
                wallObj = new GameObject(def.Name);
                wallObj.transform.SetParent(colisoresNaveObj.transform, false);
            }
            else
            {
                wallObj = childTr.gameObject;
            }

            wallObj.SetActive(true);
            wallObj.transform.localPosition = def.Position;
            wallObj.transform.localRotation = Quaternion.Euler(def.EulerAngles);
            wallObj.transform.localScale = Vector3.one;

            BoxCollider bc = wallObj.GetComponent<BoxCollider>();
            if (bc == null)
            {
                bc = wallObj.AddComponent<BoxCollider>();
            }
            bc.center = Vector3.zero;
            bc.size = def.Size;
            bc.isTrigger = false;
            bc.enabled = true;
            wallBoxCount++;
        }

        if (colisoresNaveObj.GetComponent<InteracaoEColisaoNave>() == null)
        {
            colisoresNaveObj.AddComponent<InteracaoEColisaoNave>();
        }

        // 2. Garante MeshColliders nas malhas da Nave
        GameObject nave = GameObject.Find("Nave") ?? GameObject.Find("nave");
        int meshColliderCount = 0;
        if (nave != null)
        {
            MeshFilter[] meshFilters = nave.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                GameObject go = mf.gameObject;
                string name = go.name;

                if (name.Contains("Fumer") || name.Contains("pPlane"))
                {
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
        }

        // 3. Garante também o colisor na CabinePiloto
        GameObject cockpit = GameObject.Find("CabinePiloto") ?? GameObject.Find("Cockpit");
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

        EditorUtility.SetDirty(colisoresNaveObj);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log($"[ConfiguradorColisoes] Sucesso! {wallBoxCount} BoxColliders de parede/estrutura em ColisoresNave e {meshColliderCount} MeshColliders configurados na SampleScene.");
    }
}
