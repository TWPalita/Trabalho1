using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using TMPro;

[InitializeOnLoad]
public static class ConstrutorCena
{
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string RoomModelPath = "Assets/Componentes/space-smugglers-club-house-dark-version/source/SpaceSMuggler/Scene_final.fbx";
    private const string CockpitModelPath = "Assets/Componentes/spaceship-cockpit-seat/source/plane cockpit+seat.fbx";
    private const string PanelModelPath = "Assets/Componentes/PainelMesa.fbx";
    private const string LixoPrefabPath = "Assets/Componentes/Lixo.prefab";

    private const string XROriginPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    private const string XRSimulatorPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/XR Device Simulator/XR Device Simulator.prefab";
    private const string PushButtonPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/DemoAssets/Prefabs/Interactables/Push Button.prefab";
    private const string InputActionsAssetPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/XRI Default Input Actions.inputactions";

    static ConstrutorCena()
    {
        EditorApplication.delayCall += CheckAndAutoBuild;
    }

    private static void CheckAndAutoBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        string versionFile = "Temp/SceneBuilder_v11.tmp";
        if (!System.IO.File.Exists(versionFile))
        {
            System.IO.File.WriteAllText(versionFile, "v11");
            BuildAllScenes();
        }
    }

    [MenuItem("MathBlaster/Build All VR Scenes")]
    public static void BuildAllScenes()
    {
        Debug.Log(">>> [SceneBuilder] Iniciando construção completa dos cenários VR...");

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"Fonte TMP não encontrada em: {FontPath}");
        }

        BuildNaveSceneVR(font);
        BuildDefenseSceneVR(font);
        ConfigureBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(">>> [SceneBuilder] Todas as cenas VR foram geradas e configuradas com sucesso!");
    }

    public static void ConfigureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene("Assets/Scenes/NaveScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/DefenseScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true)
        };
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[SceneBuilder] EditorBuildSettings configurado com NaveScene (0), DefenseScene (1) e SampleScene (2).");
    }

    private static void BuildNaveSceneVR(TMP_FontAsset font)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Iluminação da Nave
        GameObject lightObj = new GameObject("LuzInteriorNave");
        Light dirLight = lightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(0.85f, 0.9f, 1f);
        dirLight.intensity = 0.85f;
        dirLight.shadows = LightShadows.Hard;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 2. Modelo da Espaçonave (Interior)
        GameObject roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomModelPath);
        if (roomPrefab != null)
        {
            GameObject roomInstance = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab);
            roomInstance.name = "InteriorNave";
            roomInstance.transform.position = Vector3.zero;
            roomInstance.transform.rotation = Quaternion.identity;

            PrefabUtility.UnpackPrefabInstance(roomInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            ConfigureSpaceshipColliders(roomInstance);
        }

        // Chão e limites físicos leves (sem os 197 MeshColliders pesados)
        GameObject floorSafety = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorSafety.name = "ColisorChao";
        floorSafety.transform.position = new Vector3(-3.5f, -0.05f, 0f);
        floorSafety.transform.localScale = new Vector3(25f, 0.1f, 35f);
        floorSafety.GetComponent<MeshRenderer>().enabled = false;

        // 3. Painel de Controle com Botão Físico VR
        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelModelPath);
        GameObject panelInstance = null;
        if (panelPrefab != null)
        {
            panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab);
            panelInstance.name = "ConsolePainelControle";
            panelInstance.transform.position = new Vector3(-3.57f, 0f, 6.5f);
            panelInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            panelInstance.transform.localScale = Vector3.one;

            BoxCollider col = panelInstance.GetComponent<BoxCollider>();
            if (col == null) col = panelInstance.AddComponent<BoxCollider>();
            col.size = new Vector3(2.5f, 1.8f, 1.5f);
            col.center = new Vector3(0f, 0.9f, 0f);
        }

        // Botão Físico Push Button no Painel
        GameObject pushButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PushButtonPrefabPath);
        GameObject buttonInstance = null;
        if (pushButtonPrefab != null)
        {
            buttonInstance = (GameObject)PrefabUtility.InstantiatePrefab(pushButtonPrefab);
            buttonInstance.name = "BotaoDefesa";
            buttonInstance.transform.position = new Vector3(-3.57f, 1.05f, 6.2f);
            buttonInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            buttonInstance.transform.localScale = Vector3.one;

            var vrBtn = buttonInstance.AddComponent<BotaoTeleporte>();
            var interactable = buttonInstance.GetComponent<XRSimpleInteractable>();
            if (interactable == null) interactable = buttonInstance.AddComponent<XRSimpleInteractable>();
        }

        // Rótulo Holográfico em World Space sobre o Painel
        CreateWorldSpacePanelLabel(new Vector3(-3.57f, 1.9f, 6.5f), font);

        // 4. Jogador VR posicionado no chão livre (XR Origin 100% nativo VR)
        GameObject xrRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
        GameObject xrRigInstance = null;
        if (xrRigPrefab != null)
        {
            xrRigInstance = (GameObject)PrefabUtility.InstantiatePrefab(xrRigPrefab);
            xrRigInstance.name = "OrigemXR";
            xrRigInstance.transform.position = new Vector3(-3.57f, 0.05f, 3.5f);
            xrRigInstance.transform.rotation = Quaternion.identity;

            var originComp = xrRigInstance.GetComponent<XROrigin>();
            if (originComp != null)
            {
                originComp.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            }
        }

        // 5. XR Interaction Manager & EventSystem
        EnsureXRInfrastructure();

        // 6. Sistema de Alarme com Luzes sem sombras dinâmicas pesadas
        GameObject alarmObj = new GameObject("GerenteAlarme");
        GerenteAlarme alarmManager = alarmObj.AddComponent<GerenteAlarme>();

        GameObject eLight1 = new GameObject("LuzEmergencia1");
        eLight1.transform.position = new Vector3(-6f, 3.5f, 2.5f);
        Light el1 = eLight1.AddComponent<Light>();
        el1.type = LightType.Point;
        el1.color = Color.red;
        el1.range = 14f;
        el1.intensity = 0f;
        el1.shadows = LightShadows.None; // Sem mapas de sombra pesados em luz pontual dinâmica

        GameObject eLight2 = new GameObject("LuzEmergencia2");
        eLight2.transform.position = new Vector3(-1f, 3.5f, -2.5f);
        Light el2 = eLight2.AddComponent<Light>();
        el2.type = LightType.Point;
        el2.color = Color.red;
        el2.range = 14f;
        el2.intensity = 0f;
        el2.shadows = LightShadows.None; // Sem mapas de sombra pesados em luz pontual dinâmica

        SerializedObject alarmSO = new SerializedObject(alarmManager);
        var lightsProp = alarmSO.FindProperty("emergencyLights");
        lightsProp.arraySize = 2;
        lightsProp.GetArrayElementAtIndex(0).objectReferenceValue = el1;
        lightsProp.GetArrayElementAtIndex(1).objectReferenceValue = el2;
        alarmSO.ApplyModifiedProperties();

        // Holograma de Alarme 3D na parede da nave
        CreateWorldSpaceAlarmHologram(new Vector3(-3.57f, 2.6f, 7.8f), font);

        // 7. Gerenciador de Áudio
        GameObject audioObj = new GameObject("GerenteSom");
        audioObj.AddComponent<GerenteSom>();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/NaveScene.unity");
        Debug.Log("[ConstrutorCena] NaveScene VR construída com sucesso!");
    }

    private static void BuildDefenseSceneVR(TMP_FontAsset font)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Iluminação Espacial
        GameObject lightObj = new GameObject("LuzEspacial");
        Light dirLight = lightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(0.75f, 0.88f, 1f);
        dirLight.intensity = 0.9f;
        lightObj.transform.rotation = Quaternion.Euler(30f, 50f, 0f);

        // 2. Modelo do Cockpit
        GameObject cockpitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CockpitModelPath);
        if (cockpitPrefab != null)
        {
            GameObject cockpitInstance = (GameObject)PrefabUtility.InstantiatePrefab(cockpitPrefab);
            cockpitInstance.name = "AmbienteCockpit";
            cockpitInstance.transform.position = Vector3.zero;
            cockpitInstance.transform.rotation = Quaternion.identity;
        }

        // Chão físico sob o assento do cockpit para sustentação
        GameObject cockpitFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cockpitFloor.name = "ColisorChaoCockpit";
        cockpitFloor.transform.position = new Vector3(0f, 0.10f, 0.3f);
        cockpitFloor.transform.localScale = new Vector3(8f, 0.20f, 8f);
        cockpitFloor.GetComponent<MeshRenderer>().enabled = false;

        // 3. XR Origin posicionado exatamente no assento do piloto com trava de assento
        GameObject xrRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
        GameObject xrRigInstance = null;
        ArmaVR weapRightInstance = null;
        if (xrRigPrefab != null)
        {
            xrRigInstance = (GameObject)PrefabUtility.InstantiatePrefab(xrRigPrefab);
            xrRigInstance.name = "OrigemXR";
            xrRigInstance.transform.position = new Vector3(0f, 0.2f, 0.3f);
            xrRigInstance.transform.rotation = Quaternion.identity;

            xrRigInstance.AddComponent<AssentoCockpit>();

            // Extrai referências de Input Action para Activate e Select dos controladores
            InputActionReference rActivateRef = null;
            InputActionReference rSelectRef = null;
            InputActionReference lActivateRef = null;
            InputActionReference lSelectRef = null;

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(InputActionsAssetPath);
            if (subAssets != null)
            {
                foreach (var asset in subAssets)
                {
                    if (asset is InputActionReference iar && iar.action != null)
                    {
                        string map = iar.action.actionMap?.name;
                        string act = iar.action.name;

                        if (map == "XRI Right Interaction")
                        {
                            if (act == "Activate") rActivateRef = iar;
                            else if (act == "Select") rSelectRef = iar;
                        }
                        else if (map == "XRI Left Interaction")
                        {
                            if (act == "Activate") lActivateRef = iar;
                            else if (act == "Select") lSelectRef = iar;
                        }
                    }
                }
            }

            // Anexa ArmaVR em ambos os controladores VR (Dual-Wielding)
            Transform rightController = FindChildRecursive(xrRigInstance.transform, "Right Controller");
            if (rightController != null)
            {
                var weapRight = rightController.gameObject.AddComponent<ArmaVR>();
                weapRightInstance = weapRight;
                SerializedObject rSO = new SerializedObject(weapRight);
                rSO.FindProperty("controllerNode").enumValueIndex = (int)UnityEngine.XR.XRNode.RightHand;
                rSO.FindProperty("laserColor").colorValue = new Color(0.2f, 1f, 0.4f, 1f); // Laser Verde
                if (rActivateRef != null)
                {
                    var trigProp = rSO.FindProperty("triggerAction").FindPropertyRelative("m_Reference");
                    if (trigProp != null) trigProp.objectReferenceValue = rActivateRef;
                }
                if (rSelectRef != null)
                {
                    var selProp = rSO.FindProperty("selectAction").FindPropertyRelative("m_Reference");
                    if (selProp != null) selProp.objectReferenceValue = rSelectRef;
                }
                rSO.ApplyModifiedProperties();
                rightController.gameObject.name = "ControladorDireito";
            }

            Transform leftController = FindChildRecursive(xrRigInstance.transform, "Left Controller");
            if (leftController != null)
            {
                var weapLeft = leftController.gameObject.AddComponent<ArmaVR>();
                SerializedObject lSO = new SerializedObject(weapLeft);
                lSO.FindProperty("controllerNode").enumValueIndex = (int)UnityEngine.XR.XRNode.LeftHand;
                lSO.FindProperty("laserColor").colorValue = new Color(0.2f, 0.85f, 1f, 1f); // Laser Ciano
                if (lActivateRef != null)
                {
                    var trigProp = lSO.FindProperty("triggerAction").FindPropertyRelative("m_Reference");
                    if (trigProp != null) trigProp.objectReferenceValue = lActivateRef;
                }
                if (lSelectRef != null)
                {
                    var selProp = lSO.FindProperty("selectAction").FindPropertyRelative("m_Reference");
                    if (selProp != null) selProp.objectReferenceValue = lSelectRef;
                }
                lSO.ApplyModifiedProperties();
                leftController.gameObject.name = "ControladorEsquerdo";
            }
        }

        GameObject simPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XRSimulatorPrefabPath);
        if (simPrefab != null)
        {
            GameObject simInstance = (GameObject)PrefabUtility.InstantiatePrefab(simPrefab);
            simInstance.name = "SimuladorXR";
        }

        // 4. XR Infrastructure
        EnsureXRInfrastructure();

        // 5. Pontos de Nascimento dos Detritos no Espaço 3D (Layout fiel ao diagrama ASCII)
        //          ☄ 17             ☄ 8
        //                   ☄ 12
        //       ☄ 5                       ☄ 10
        GameObject spawnParent = new GameObject("PontosNascimentoDetritos");
        Transform[] spawnPoints = new Transform[5];
        Vector3[] spawnPositions = new Vector3[]
        {
            new Vector3(-5.5f, 5.8f, 16.0f), // Alto Esq (17)
            new Vector3(5.5f, 5.8f, 16.0f),  // Alto Dir (8)
            new Vector3(0.0f, 4.6f, 17.5f),  // Centro (12)
            new Vector3(-6.5f, 3.4f, 14.5f), // Baixo/Médio Esq (5)
            new Vector3(6.5f, 3.4f, 14.5f)   // Baixo/Médio Dir (10)
        };

        for (int i = 0; i < 5; i++)
        {
            GameObject sp = new GameObject($"PontoNascimento{i + 1}");
            sp.transform.parent = spawnParent.transform;
            sp.transform.position = spawnPositions[i];
            spawnPoints[i] = sp.transform;
        }

        // 6. Poeira/Estrelas Espaciais
        CreateStarfield(new Vector3(0f, 3.5f, 12f));

        // 7. Gerenciador de Áudio
        GameObject audioObj = new GameObject("GerenteSom");
        audioObj.AddComponent<GerenteSom>();

        // 8. Cockpit Dashboard World Space UI
        CreateCockpitWorldSpaceHUD(new Vector3(0f, 1.15f, 1.85f), font);

        // 9. Managers (GeradorMatematica, GerentePontuacao, GerenteDefesa)
        GameObject managersObj = new GameObject("GerenciadoresDefesa");
        GeradorMatematica mathManager = managersObj.AddComponent<GeradorMatematica>();
        GerentePontuacao scoreManager = managersObj.AddComponent<GerentePontuacao>();
        GerenteDefesa defManager = managersObj.AddComponent<GerenteDefesa>();

        GameObject lixoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LixoPrefabPath);

        SerializedObject defSO = new SerializedObject(defManager);
        defSO.FindProperty("mathManager").objectReferenceValue = mathManager;
        defSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        defSO.FindProperty("debrisPrefab").objectReferenceValue = lixoPrefab;
        defSO.FindProperty("autoStartOnAwake").boolValue = true;
        var spProp = defSO.FindProperty("spawnPoints");
        spProp.arraySize = spawnPoints.Length;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            spProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        }
        if (weapRightInstance != null)
        {
            defSO.FindProperty("weaponController").objectReferenceValue = weapRightInstance;
        }
        defSO.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/DefenseScene.unity");
        Debug.Log("[ConstrutorCena] DefenseScene VR construída com sucesso com layout ASCII!");
    }

    private static void CreateCockpitWorldSpaceHUD(Vector3 position, TMP_FontAsset font)
    {
        // Canvas Principal em World Space no Cockpit
        GameObject hudCanvasObj = new GameObject("InterfaceHUDCockpit");
        hudCanvasObj.transform.position = position;
        hudCanvasObj.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

        Canvas canvas = hudCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRT = hudCanvasObj.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(900f, 650f);
        hudCanvasObj.transform.localScale = new Vector3(0.0022f, 0.0022f, 0.0022f);

        hudCanvasObj.AddComponent<GraphicRaycaster>();
        hudCanvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();

        // ========================================================
        // 1. TELA FLUTUANTE DA CONTA MATEMÁTICA (VISOR SUPERIOR)
        //              ┌──────────────┐
        //              │   7 + 5 = ?  │
        //              └──────────────┘
        // ========================================================
        GameObject visorFrameBorder = CreateWorldSpacePanel(hudCanvasObj.transform, "BordaVisorMatematica",
            new Color(0.2f, 0.8f, 1f, 0.85f), new Vector2(0f, 210f), new Vector2(500f, 150f));
        GameObject visorFrameInner = CreateWorldSpacePanel(visorFrameBorder.transform, "FundoVisorMatematica",
            new Color(0.02f, 0.06f, 0.14f, 0.95f), Vector2.zero, new Vector2(492f, 142f));

        TMP_Text roundText = CreateWorldSpaceText(visorFrameInner.transform, "TextoRodada", "RODADA 1",
            20f, new Color(0.4f, 0.85f, 1f, 1f), TextAlignmentOptions.Center, new Vector2(0f, 42f), new Vector2(460f, 30f), font);

        TMP_Text questionText = CreateWorldSpaceText(visorFrameInner.transform, "TextoPergunta", "7 + 5 = ?",
            64f, new Color(1f, 0.92f, 0.25f, 1f), TextAlignmentOptions.Center, new Vector2(0f, -10f), new Vector2(460f, 80f), font);

        // ========================================================
        // 2. PAINEL DE CONTROLE (CONSOLE DO COCKPIT)
        //       ┌─────────────────────────────┐
        //       │      PAINEL DE CONTROLE     │
        //       │                             │
        //       │ SCORE       ENERGY          │
        //       │ 0100        ████████        │
        //       │                             │
        //       │       🔘      🔘            │
        //       └─────────────────────────────┘
        // ========================================================
        GameObject consoleBorder = CreateWorldSpacePanel(hudCanvasObj.transform, "BordaPainelControle",
            new Color(0.12f, 0.3f, 0.55f, 0.9f), new Vector2(0f, -65f), new Vector2(860f, 360f));
        GameObject consolePanel = CreateWorldSpacePanel(consoleBorder.transform, "FundoPainelControle",
            new Color(0.04f, 0.08f, 0.16f, 0.96f), Vector2.zero, new Vector2(852f, 352f));

        // Título: "PAINEL DE CONTROLE"
        CreateWorldSpaceText(consolePanel.transform, "TituloPainel", "PAINEL DE CONTROLE",
            30f, new Color(0.35f, 0.9f, 1f, 1f), TextAlignmentOptions.Center, new Vector2(0f, 135f), new Vector2(800f, 40f), font);

        // Linha divisória estética
        CreateWorldSpacePanel(consolePanel.transform, "DivisorTitulo", new Color(0.2f, 0.5f, 0.8f, 0.5f),
            new Vector2(0f, 110f), new Vector2(780f, 2f));

        // --- COLUNA DA ESQUERDA: SCORE ---
        CreateWorldSpaceText(consolePanel.transform, "RotuloPontos", "SCORE",
            24f, new Color(0.55f, 0.75f, 0.95f, 1f), TextAlignmentOptions.Center, new Vector2(-220f, 75f), new Vector2(250f, 32f), font);

        TMP_Text scoreText = CreateWorldSpaceText(consolePanel.transform, "ValorPontos", "0000",
            44f, new Color(0.25f, 1f, 0.45f, 1f), TextAlignmentOptions.Center, new Vector2(-220f, 32f), new Vector2(250f, 50f), font);

        // --- COLUNA DA DIREITA: ENERGY ---
        CreateWorldSpaceText(consolePanel.transform, "RotuloEnergia", "ENERGY",
            24f, new Color(0.55f, 0.75f, 0.95f, 1f), TextAlignmentOptions.Center, new Vector2(220f, 75f), new Vector2(250f, 32f), font);

        // Barras Segmentadas: "████████"
        TMP_Text energySegText = CreateWorldSpaceText(consolePanel.transform, "SegmentosEnergia", "████████",
            34f, new Color(0.25f, 1f, 0.45f, 1f), TextAlignmentOptions.Center, new Vector2(220f, 36f), new Vector2(260f, 40f), font);

        // Barra gráfica contínua de preenchimento
        GameObject energyBarBg = CreateWorldSpacePanel(consolePanel.transform, "FundoBarraEnergia", new Color(0.15f, 0.2f, 0.3f, 1f),
            new Vector2(220f, 6f), new Vector2(240f, 12f));
        GameObject energyBarFill = CreateWorldSpacePanel(energyBarBg.transform, "PreenchimentoBarraEnergia", Color.green,
            Vector2.zero, new Vector2(240f, 12f));
        Image fillImage = energyBarFill.GetComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;

        TMP_Text energyPctText = CreateWorldSpaceText(consolePanel.transform, "PorcentagemEnergia", "100%",
            18f, new Color(0.4f, 0.85f, 1f, 1f), TextAlignmentOptions.Center, new Vector2(220f, -14f), new Vector2(100f, 25f), font);

        // Feedback central ("CORRETO! +100" / "ERRADO! -20")
        TMP_Text feedbackText = CreateWorldSpaceText(consolePanel.transform, "TextoRetorno", "CORRETO!\n+100 PONTOS",
            38f, Color.green, TextAlignmentOptions.Center, new Vector2(0f, 35f), new Vector2(500f, 70f), font);
        feedbackText.gameObject.SetActive(false);

        // --- BOTÕES FÍSICOS DO PAINEL DE CONTROLE (🔘 🔘) ---
        // Botão Esquerdo 🔘: "NOVA CONTA"
        Button btnConsoleNew = CreateWorldSpaceButton(consolePanel.transform, "BotaoNovaRodada", "🔘 NOVA CONTA",
            new Vector2(-160f, -95f), new Vector2(240f, 52f), font);

        // Botão Direito 🔘: "RETORNAR À NAVE"
        Button btnConsoleReturn = CreateWorldSpaceButton(consolePanel.transform, "BotaoRetornarNave", "🔘 RETORNAR",
            new Vector2(160f, -95f), new Vector2(240f, 52f), font);

        // ========================================================
        // 3. PAINEL DE FIM DE JOGO 3D (GAME OVER OVERLAY)
        // ========================================================
        GameObject gameOverPanel = CreateWorldSpacePanel(hudCanvasObj.transform, "PainelFimDeJogo",
            new Color(0.08f, 0.02f, 0.02f, 0.97f), Vector2.zero, new Vector2(720f, 420f));
        CreateWorldSpaceText(gameOverPanel.transform, "Title", "FIM DE JOGO",
            54f, new Color(1f, 0.25f, 0.25f, 1f), TextAlignmentOptions.Center, new Vector2(0f, 120f), new Vector2(500f, 80f), font);
        TMP_Text finalScoreText = CreateWorldSpaceText(gameOverPanel.transform, "PontuacaoFinal", "PONTUAÇÃO FINAL: 0",
            32f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 35f), new Vector2(500f, 50f), font);

        Button btnRestart = CreateWorldSpaceButton(gameOverPanel.transform, "BotaoReiniciar", "REINICIAR DEFESA",
            new Vector2(0f, -45f), new Vector2(300f, 55f), font);
        Button btnReturnShip = CreateWorldSpaceButton(gameOverPanel.transform, "BotaoVoltarNave", "VOLTAR PARA A NAVE",
            new Vector2(0f, -120f), new Vector2(300f, 55f), font);
        gameOverPanel.SetActive(false);

        // ========================================================
        // 4. ATRIBUIÇÃO AO COMPONENTE VRWorldSpaceUI
        // ========================================================
        var vrUI = hudCanvasObj.AddComponent<InterfaceVR>();
        SerializedObject uiSO = new SerializedObject(vrUI);
        uiSO.FindProperty("cockpitHUDParent").objectReferenceValue = consolePanel;
        uiSO.FindProperty("mathQuestionText").objectReferenceValue = questionText;
        uiSO.FindProperty("roundText").objectReferenceValue = roundText;
        uiSO.FindProperty("scoreText").objectReferenceValue = scoreText;
        uiSO.FindProperty("energyText").objectReferenceValue = energyPctText;
        uiSO.FindProperty("energySegmentedText").objectReferenceValue = energySegText;
        uiSO.FindProperty("energyFillImage").objectReferenceValue = fillImage;
        uiSO.FindProperty("feedbackText").objectReferenceValue = feedbackText;
        uiSO.FindProperty("btnConsoleNewRound").objectReferenceValue = btnConsoleNew;
        uiSO.FindProperty("btnConsoleReturnShip").objectReferenceValue = btnConsoleReturn;
        uiSO.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
        uiSO.FindProperty("gameOverScoreText").objectReferenceValue = finalScoreText;
        uiSO.FindProperty("btnRestart").objectReferenceValue = btnRestart;
        uiSO.FindProperty("btnReturnToShip").objectReferenceValue = btnReturnShip;
        uiSO.ApplyModifiedProperties();
    }

    private static void CreateWorldSpaceAlarmHologram(Vector3 position, TMP_FontAsset font)
    {
        GameObject holoObj = new GameObject("AlarmeMundo");
        holoObj.transform.position = position;
        holoObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        holoObj.transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);

        Canvas canvas = holoObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        holoObj.GetComponent<RectTransform>().sizeDelta = new Vector2(750f, 260f);

        GameObject panel = CreateWorldSpacePanel(holoObj.transform, "AlarmBanner", new Color(0.7f, 0.05f, 0.05f, 0.88f),
            Vector2.zero, new Vector2(750f, 260f));

        CreateWorldSpaceText(panel.transform, "Text",
            "ALERTA!\nOBJETOS ESPACIAIS DETECTADOS\nDIRIJA-SE AO PAINEL DE DEFESA",
            32f, Color.yellow, TextAlignmentOptions.Center, Vector2.zero, new Vector2(700f, 220f), font);
    }

    private static void CreateWorldSpacePanelLabel(Vector3 position, TMP_FontAsset font)
    {
        GameObject labelObj = new GameObject("RotuloPainelMundo");
        labelObj.transform.position = position;
        labelObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        labelObj.transform.localScale = new Vector3(0.0025f, 0.0025f, 0.0025f);

        Canvas canvas = labelObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        labelObj.GetComponent<RectTransform>().sizeDelta = new Vector2(650f, 160f);

        GameObject panel = CreateWorldSpacePanel(labelObj.transform, "LabelBanner", new Color(0.03f, 0.08f, 0.15f, 0.85f),
            Vector2.zero, new Vector2(650f, 160f));

        CreateWorldSpaceText(panel.transform, "Text",
            "SISTEMA DE DEFESA\nAponte o controlador e pressione o botão",
            26f, new Color(0.2f, 1f, 0.7f, 1f), TextAlignmentOptions.Center, Vector2.zero, new Vector2(620f, 140f), font);
    }

    private static GameObject CreateWorldSpacePanel(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image img = panel.AddComponent<Image>();
        img.color = color;
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return panel;
    }

    private static TMP_Text CreateWorldSpaceText(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions align, Vector2 pos, Vector2 size, TMP_FontAsset font)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return tmp;
    }

    private static Button CreateWorldSpaceButton(Transform parent, string name, string labelText, Vector2 pos, Vector2 size, TMP_FontAsset font)
    {
        GameObject btnObj = CreateWorldSpacePanel(parent, name, new Color(0.15f, 0.38f, 0.7f, 1f), pos, size);
        Button btn = btnObj.AddComponent<Button>();

        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.18f, 0.42f, 0.78f, 1f);
        cb.highlightedColor = new Color(0.3f, 0.6f, 1f, 1f);
        cb.pressedColor = new Color(0.12f, 0.3f, 0.6f, 1f);
        btn.colors = cb;

        CreateWorldSpaceText(btnObj.transform, "Rotulo", labelText, 24f, Color.white, TextAlignmentOptions.Center, Vector2.zero, size, font);
        return btn;
    }

    private static void CreateStarfield(Vector3 position)
    {
        GameObject starsObj = new GameObject("ParticulasEstrelas");
        starsObj.transform.position = position;

        ParticleSystem ps = starsObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 15f;
        main.startSpeed = 0.4f;
        main.startSize = 0.15f;
        main.maxParticles = 500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 25f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(35f, 20f, 35f);

        var renderer = starsObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
    }

    private static void EnsureXRInfrastructure()
    {
        var xrim = Object.FindAnyObjectByType<XRInteractionManager>();
        if (xrim == null)
        {
            GameObject xrimObj = new GameObject("GerenteInteracaoXR");
            xrim = xrimObj.AddComponent<XRInteractionManager>();
        }

        // Habilita as ações do XR (Rastreamento 6-DoF do HMD, controladores VR, locomoção por joystick e giro)
        var iam = Object.FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
        if (iam == null)
        {
            iam = xrim.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
            var actionsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputActionsAssetPath);
            if (actionsAsset != null)
            {
                iam.actionAssets = new List<UnityEngine.InputSystem.InputActionAsset> { actionsAsset };
            }
        }

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("SistemaEventos");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }

    private static void ConfigureSpaceshipColliders(GameObject roomInstance)
    {
        roomInstance.isStatic = true;

        int meshColliderCount = 0;
        int boxColliderCount = 0;
        int skippedCount = 0;

        MeshFilter[] meshFilters = roomInstance.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            GameObject go = mf.gameObject;
            go.isStatic = true;
            string name = go.name;
            Bounds b = mf.sharedMesh.bounds;

            // 1. Arquitetura principal (paredes, teto, chão da espaçonave: MeshCollider exato ultra-leve)
            if (name.StartsWith("Room", System.StringComparison.OrdinalIgnoreCase))
            {
                var mc = go.GetComponent<MeshCollider>();
                if (mc == null) mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                meshColliderCount++;
                continue;
            }

            // 2. Batentes e arcos de portas (MeshCollider não-convexo mantém o vão da porta aberto e transitável)
            if (name.StartsWith("DoorWay", System.StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Doorways", System.StringComparison.OrdinalIgnoreCase))
            {
                var mc = go.GetComponent<MeshCollider>();
                if (mc == null) mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                meshColliderCount++;
                continue;
            }

            // 3. Ignorar adereços do teto muito altos (> 2.8m) ou efeitos visuais de fumaça/planos
            if (b.min.y > 2.8f && (name.Contains("Lampe") || name.Contains("Neon") || name.Contains("Holo")))
            {
                skippedCount++;
                continue;
            }
            if (name.Contains("Fumer") || name.Contains("pPlane"))
            {
                skippedCount++;
                continue;
            }

            // 4. Ignorar micro-itens insignificantes (< 5cm, ex: pontas de cigarro, moedas soltas)
            if (b.size.magnitude < 0.05f)
            {
                skippedCount++;
                continue;
            }

            // 5. Todos os móveis e obstáculos (mesas, sofás, balcões, camas, caixas, armários, canos, consoles, etc.)
            var bc = go.GetComponent<BoxCollider>();
            if (bc == null) bc = go.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = new Vector3(
                Mathf.Max(b.size.x, 0.06f),
                Mathf.Max(b.size.y, 0.06f),
                Mathf.Max(b.size.z, 0.06f)
            );
            boxColliderCount++;
        }

        // 6. Barreira perimetral hermética ao redor da nave
        CreatePerimeterBounds(roomInstance);

        Debug.Log($"[SceneBuilder] Colisões da nave configuradas: {meshColliderCount} MeshColliders (Paredes/Portas), {boxColliderCount} BoxColliders (Mobílias/Obstáculos), {skippedCount} adereços decorativos ignorados.");
    }

    private static void CreatePerimeterBounds(GameObject roomInstance)
    {
        Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        foreach (var r in roomInstance.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds)
            {
                combinedBounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds) return;

        GameObject perimeterObj = new GameObject("LimitesNave");
        perimeterObj.transform.parent = roomInstance.transform;
        perimeterObj.isStatic = true;

        Vector3 center = combinedBounds.center;
        Vector3 size = combinedBounds.size;
        float wallThickness = 0.5f;

        // Parede Norte (+Z)
        CreateBoundaryWall(perimeterObj.transform, "ParedeNorte",
            new Vector3(center.x, center.y, combinedBounds.max.z + wallThickness * 0.5f),
            new Vector3(size.x, size.y, wallThickness));

        // Parede Sul (-Z)
        CreateBoundaryWall(perimeterObj.transform, "ParedeSul",
            new Vector3(center.x, center.y, combinedBounds.min.z - wallThickness * 0.5f),
            new Vector3(size.x, size.y, wallThickness));

        // Parede Leste (+X)
        CreateBoundaryWall(perimeterObj.transform, "ParedeLeste",
            new Vector3(combinedBounds.max.x + wallThickness * 0.5f, center.y, center.z),
            new Vector3(wallThickness, size.y, size.z));

        // Parede Oeste (-X)
        CreateBoundaryWall(perimeterObj.transform, "ParedeOeste",
            new Vector3(combinedBounds.min.x - wallThickness * 0.5f, center.y, center.z),
            new Vector3(wallThickness, size.y, size.z));

        // Teto (+Y)
        CreateBoundaryWall(perimeterObj.transform, "TetoNave",
            new Vector3(center.x, combinedBounds.max.y + wallThickness * 0.5f, center.z),
            new Vector3(size.x, wallThickness, size.z));
    }

    private static void CreateBoundaryWall(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        GameObject wall = new GameObject(name);
        wall.transform.parent = parent;
        wall.transform.position = pos;
        wall.isStatic = true;
        BoxCollider bc = wall.AddComponent<BoxCollider>();
        bc.size = size;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }
}
