using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public static class ConfiguradorNarrativaCenas
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string DefenseScenePath = "Assets/Scenes/DefenseScene.unity";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string ArmaModelPath = "Assets/Componentes/ArmaLaser/source/arma.obj";
    private const string ArmaMatPath = "Assets/Componentes/ArmaLaser/ArmaLaser_Mat.mat";

    [MenuItem("MathBlaster/Configurar Objetos Narrativos nas Cenas")]
    public static void ExecutarConfiguracaoCompleta()
    {
        Debug.Log(">>> [ConfiguradorNarrativa] Iniciando configuração física dos objetos narrativos nas cenas...");

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        ConfigurarSampleScene(font);
        ConfigurarDefenseScene(font);

        // Retorna para a SampleScene no editor
        EditorSceneManager.OpenScene(SampleScenePath);

        Debug.Log(">>> [ConfiguradorNarrativa] Configuração física concluída com sucesso! Os objetos agora existem estaticamente nas cenas.");
    }

    private static void ConfigurarSampleScene(TMP_FontAsset font)
    {
        Debug.Log(">>> [ConfiguradorNarrativa] Configurando SampleScene...");
        var scene = EditorSceneManager.OpenScene(SampleScenePath);

        // 1. Remove qualquer objeto duplicado prévio gerado em runtime
        GameObject oldPainel = GameObject.Find("PainelBateria_Nave");
        if (oldPainel != null) Object.DestroyImmediate(oldPainel);

        // 2. Cria ou localiza o PainelEnergia_Nave
        GameObject painelObj = GameObject.Find("PainelEnergia_Nave");
        if (painelObj == null)
        {
            painelObj = new GameObject("PainelEnergia_Nave");
            painelObj.transform.position = new Vector3(-1.85f, 1.35f, -4.5f);
            painelObj.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        }

        // Modelo 3D da carcaça do painel
        Transform visualMesh = painelObj.transform.Find("VisualPainel");
        if (visualMesh == null)
        {
            GameObject consoleBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            consoleBody.name = "VisualPainel";
            consoleBody.transform.SetParent(painelObj.transform, false);
            consoleBody.transform.localPosition = new Vector3(0f, 0f, 0f);
            consoleBody.transform.localScale = new Vector3(0.95f, 0.85f, 0.14f);

            var mr = consoleBody.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material darkMetal = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                darkMetal.name = "PainelMetalico";
                darkMetal.color = new Color(0.10f, 0.13f, 0.18f);
                mr.sharedMaterial = darkMetal;
            }
        }

        // Borda/Moldura luminosa
        Transform moldura = painelObj.transform.Find("MolduraLuminosa");
        if (moldura == null)
        {
            GameObject molObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            molObj.name = "MolduraLuminosa";
            molObj.transform.SetParent(painelObj.transform, false);
            molObj.transform.localPosition = new Vector3(0f, 0.40f, 0.08f);
            molObj.transform.localScale = new Vector3(0.96f, 0.04f, 0.04f);

            var col = molObj.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var mr = molObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material glow = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                glow.name = "MolduraGlow";
                Color c = new Color(0.2f, 0.75f, 1.0f);
                glow.color = c;
                glow.EnableKeyword("_EMISSION");
                glow.SetColor("_EmissionColor", c * 1.5f);
                mr.sharedMaterial = glow;
            }
        }

        // 3. Compartimento/Socket para a bateria
        Transform sockTrans = painelObj.transform.Find("CompartimentoSocket");
        if (sockTrans == null)
        {
            GameObject sockObj = new GameObject("CompartimentoSocket");
            sockObj.transform.SetParent(painelObj.transform, false);
            sockObj.transform.localPosition = new Vector3(0f, -0.22f, 0.08f);
            sockObj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            sockTrans = sockObj.transform;

            // Visual do nicho do socket
            GameObject nicho = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nicho.name = "NichoSocketVisual";
            nicho.transform.SetParent(sockObj.transform, false);
            nicho.transform.localScale = new Vector3(0.20f, 0.05f, 0.20f);
            var col = nicho.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var mr = nicho.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material nichoMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                nichoMat.color = new Color(0.04f, 0.06f, 0.09f);
                mr.sharedMaterial = nichoMat;
            }

            // Collider de trigger para detecção
            BoxCollider triggerCol = sockObj.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector3(0.35f, 0.35f, 0.35f);
        }

        // 4. World Space Canvas do Painel
        Transform canvasTrans = painelObj.transform.Find("PainelEnergiaCanvas");
        TMP_Text statusTxt = null;
        CanvasGroup canvasGroup = null;

        if (canvasTrans == null)
        {
            GameObject canvasObj = new GameObject("PainelEnergiaCanvas");
            canvasObj.transform.SetParent(painelObj.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 0.12f, 0.08f);
            canvasObj.transform.localRotation = Quaternion.identity;

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 90;

            RectTransform rt = canvasObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(460f, 300f);
            rt.localScale = new Vector3(0.0018f, 0.0018f, 0.0018f);

            canvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // Fundo do display
            GameObject bgObj = new GameObject("Fundo");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.03f, 0.06f, 0.14f, 0.95f);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Linha decorativa
            GameObject lineObj = new GameObject("LinhaTitulo");
            lineObj.transform.SetParent(canvasObj.transform, false);
            var lineImg = lineObj.AddComponent<Image>();
            lineImg.color = new Color(0.2f, 0.75f, 1f, 0.85f);
            RectTransform lRt = lineObj.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0.06f, 0.78f);
            lRt.anchorMax = new Vector2(0.94f, 0.80f);
            lRt.offsetMin = Vector2.zero;
            lRt.offsetMax = Vector2.zero;

            // Título
            GameObject titleObj = new GameObject("Titulo");
            titleObj.transform.SetParent(canvasObj.transform, false);
            var titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) titleTxt.font = font;
            titleTxt.fontSize = 24f;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = new Color(0.25f, 0.80f, 1.0f);
            titleTxt.text = "SISTEMA DE ENERGIA";
            RectTransform tRt = titleObj.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 0.81f);
            tRt.anchorMax = new Vector2(1f, 0.98f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            // Texto de status da energia
            GameObject textObj = new GameObject("TextoStatus");
            textObj.transform.SetParent(canvasObj.transform, false);
            statusTxt = textObj.AddComponent<TextMeshProUGUI>();
            if (font != null) statusTxt.font = font;
            statusTxt.fontSize = 19f;
            statusTxt.alignment = TextAlignmentOptions.Center;
            statusTxt.textWrappingMode = TextWrappingModes.Normal;
            statusTxt.color = Color.white;
            statusTxt.text =
                "<color=#F87171><b>Energia indisponível.</b></color>\n\n" +
                "A bateria principal foi danificada durante o ataque.\n\n" +
                "Uma bateria reserva pode ser utilizada para restaurar o sistema.";
            RectTransform txtRt = textObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0.08f, 0.05f);
            txtRt.anchorMax = new Vector2(0.92f, 0.76f);
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
        }
        else
        {
            statusTxt = canvasTrans.Find("TextoStatus")?.GetComponent<TMP_Text>();
            canvasGroup = canvasTrans.GetComponent<CanvasGroup>();
        }

        // 5. Bateria Danificada (Preserva a posição definida pelo usuário na CabinePiloto)
        GameObject damBatObj = GameObject.Find("Bateria_Danificada");
        if (damBatObj == null)
        {
            damBatObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            damBatObj.name = "Bateria_Danificada";
            damBatObj.transform.position = new Vector3(-3.9039f, 0.8006f, 8.1052f);
            damBatObj.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            damBatObj.transform.localScale = new Vector3(0.095f, 0.09f, 0.12f);
        }

        ConfigurarComponentesBateria(damBatObj, true, new Color(0.85f, 0.22f, 0.18f));
        ItemBateria damItem = damBatObj.GetComponent<ItemBateria>();

        // Socket na CabinePiloto no mesmo local da bateria danificada
        GameObject cabinSocket = GameObject.Find("SocketBateria_Cabine");
        if (cabinSocket == null)
        {
            cabinSocket = new GameObject("SocketBateria_Cabine");
        }
        cabinSocket.transform.position = damBatObj.transform.position;
        cabinSocket.transform.rotation = damBatObj.transform.rotation;

        // 6. Bateria Reserva (Posicionada na bancada no corredor da nave)
        GameObject spareBatObj = GameObject.Find("Bateria_Reserva");
        if (spareBatObj == null)
        {
            spareBatObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spareBatObj.name = "Bateria_Reserva";
            spareBatObj.transform.position = new Vector3(-5.15f, 0.90f, -4.5f);
            spareBatObj.transform.rotation = Quaternion.Euler(0f, 45f, 90f);
            spareBatObj.transform.localScale = new Vector3(0.12f, 0.16f, 0.12f);
        }

        ConfigurarComponentesBateria(spareBatObj, false, new Color(0.2f, 0.80f, 1.0f));
        ItemBateria spareItem = spareBatObj.GetComponent<ItemBateria>();

        // 7. Componente PainelBateria serializado no GameObject
        PainelBateria painelComp = painelObj.GetComponent<PainelBateria>();
        if (painelComp == null) painelComp = painelObj.AddComponent<PainelBateria>();

        SerializedObject so = new SerializedObject(painelComp);
        so.FindProperty("statusText").objectReferenceValue = statusTxt;
        so.FindProperty("panelCanvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("socketTransform").objectReferenceValue = cabinSocket.transform;
        so.FindProperty("damagedBattery").objectReferenceValue = damItem;
        so.FindProperty("spareBattery").objectReferenceValue = spareItem;
        so.FindProperty("socketSnapDistance").floatValue = 0.40f;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(painelObj);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(">>> [ConfiguradorNarrativa] SampleScene salva com sucesso.");
    }

    private static void ConfigurarComponentesBateria(GameObject batObj, bool isDamaged, Color coreColor)
    {
        var mr = batObj.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.name = isDamaged ? "Mat_BateriaDanificada" : "Mat_BateriaReserva";
            mat.color = coreColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", coreColor * (isDamaged ? 0.7f : 1.2f));
            mr.sharedMaterial = mat;
        }

        var col = batObj.GetComponent<Collider>();
        if (col != null) col.isTrigger = false;

        var rb = batObj.GetComponent<Rigidbody>();
        if (rb == null) rb = batObj.AddComponent<Rigidbody>();
        rb.mass = 1.5f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;

        var grab = batObj.GetComponent<XRGrabInteractable>();
        if (grab == null) grab = batObj.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = true;

        var item = batObj.GetComponent<ItemBateria>();
        if (item == null) item = batObj.AddComponent<ItemBateria>();
        item.SetDamaged(isDamaged, batObj.name);

        Transform luz = batObj.transform.Find("LuzIndicadora");
        if (luz == null)
        {
            GameObject luzObj = new GameObject("LuzIndicadora");
            luzObj.transform.SetParent(batObj.transform, false);
            Light l = luzObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 0.8f;
            l.intensity = 1.0f;
            l.color = coreColor;
        }
    }

    private static void ConfigurarDefenseScene(TMP_FontAsset font)
    {
        Debug.Log(">>> [ConfiguradorNarrativa] Configurando DefenseScene...");
        var scene = EditorSceneManager.OpenScene(DefenseScenePath);

        // 1. Arma física posicionada sobre o console do cockpit
        GameObject armaObj = GameObject.Find("ArmaCockpit");
        if (armaObj == null)
        {
            armaObj = new GameObject("ArmaCockpit");
        }
        armaObj.transform.position = new Vector3(0f, 0.84f, 0.75f);
        armaObj.transform.rotation = Quaternion.Euler(-10f, 180f, 0f);

        // Modelo 3D da arma
        Transform visualArma = armaObj.transform.Find("VisualArma");
        if (visualArma == null)
        {
            visualArma = new GameObject("VisualArma").transform;
            visualArma.SetParent(armaObj.transform, false);

            Mesh meshArma = AssetDatabase.LoadAssetAtPath<Mesh>(ArmaModelPath);
            if (meshArma != null)
            {
                MeshFilter mf = visualArma.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = meshArma;

                MeshRenderer mr = visualArma.gameObject.AddComponent<MeshRenderer>();
                Material matArma = AssetDatabase.LoadAssetAtPath<Material>(ArmaMatPath);
                if (matArma != null) mr.sharedMaterial = matArma;
            }
        }

        // Colisor e componentes VR da arma
        BoxCollider boxCol = armaObj.GetComponent<BoxCollider>();
        if (boxCol == null)
        {
            boxCol = armaObj.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(0.18f, 0.22f, 0.45f);
            boxCol.center = new Vector3(0f, 0.05f, 0f);
        }

        Rigidbody rbArma = armaObj.GetComponent<Rigidbody>();
        if (rbArma == null) rbArma = armaObj.AddComponent<Rigidbody>();
        rbArma.isKinematic = true;

        XRGrabInteractable grabArma = armaObj.GetComponent<XRGrabInteractable>();
        if (grabArma == null) grabArma = armaObj.AddComponent<XRGrabInteractable>();
        grabArma.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grabArma.throwOnDetach = true;

        ArmaVR armaVR = armaObj.GetComponent<ArmaVR>();
        if (armaVR == null) armaVR = armaObj.AddComponent<ArmaVR>();

        // Luz de destaque suave sobre a arma
        Transform luzArma = armaObj.transform.Find("LuzDestaque");
        if (luzArma == null)
        {
            GameObject lObj = new GameObject("LuzDestaque");
            lObj.transform.SetParent(armaObj.transform, false);
            lObj.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            Light l = lObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 1.0f;
            l.intensity = 1.5f;
            l.color = new Color(0.2f, 0.85f, 1.0f);
        }

        // 2. Canvases de Introdução da Defesa
        GameObject canvasHolder = GameObject.Find("Canvas_IntroducaoDefesa");
        if (canvasHolder == null)
        {
            canvasHolder = new GameObject("Canvas_IntroducaoDefesa");
        }
        canvasHolder.transform.position = new Vector3(0f, 1.35f, 1.35f);
        canvasHolder.transform.rotation = Quaternion.identity;

        Canvas rootCanvas = canvasHolder.GetComponent<Canvas>();
        if (rootCanvas == null) rootCanvas = canvasHolder.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.WorldSpace;
        rootCanvas.sortingOrder = 95;

        RectTransform rootRt = canvasHolder.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(750f, 480f);
        rootRt.localScale = new Vector3(0.0018f, 0.0018f, 0.0018f);

        CanvasScaler scaler = canvasHolder.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasHolder.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 2f;

        GraphicRaycaster raycaster = canvasHolder.GetComponent<GraphicRaycaster>();
        if (raycaster == null) raycaster = canvasHolder.AddComponent<GraphicRaycaster>();

        // --- PAINEL 1: PROTOCOLO DE INTERCEPTAÇÃO ---
        Transform pan1 = canvasHolder.transform.Find("Painel_Protocolo");
        Button btnEntendi = null;
        CanvasGroup group1 = null;

        if (pan1 == null)
        {
            GameObject p1 = new GameObject("Painel_Protocolo");
            p1.transform.SetParent(canvasHolder.transform, false);
            group1 = p1.AddComponent<CanvasGroup>();
            RectTransform p1Rt = p1.AddComponent<RectTransform>();
            p1Rt.anchorMin = Vector2.zero;
            p1Rt.anchorMax = Vector2.one;
            p1Rt.offsetMin = Vector2.zero;
            p1Rt.offsetMax = Vector2.zero;

            // Fundo
            GameObject bg1 = new GameObject("Fundo");
            bg1.transform.SetParent(p1.transform, false);
            var bgImg = bg1.AddComponent<Image>();
            bgImg.color = new Color(0.03f, 0.07f, 0.16f, 0.96f);
            RectTransform bgRt = bg1.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Borda decorativa
            GameObject border = new GameObject("Borda");
            border.transform.SetParent(p1.transform, false);
            var bImg = border.AddComponent<Image>();
            bImg.color = new Color(0.2f, 0.75f, 1.0f, 0.85f);
            RectTransform bRt = border.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.04f, 0.80f);
            bRt.anchorMax = new Vector2(0.96f, 0.81f);
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            // Título
            GameObject titObj = new GameObject("Titulo");
            titObj.transform.SetParent(p1.transform, false);
            var titTxt = titObj.AddComponent<TextMeshProUGUI>();
            if (font != null) titTxt.font = font;
            titTxt.fontSize = 24f;
            titTxt.fontStyle = FontStyles.Bold;
            titTxt.alignment = TextAlignmentOptions.Center;
            titTxt.color = new Color(0.25f, 0.85f, 1.0f);
            titTxt.text = "PROTOCOLO DE INTERCEPTAÇÃO\n<size=75%><color=#93C5FD>SISTEMA DE DEFESA ATIVADO</color></size>";
            RectTransform titRt = titObj.GetComponent<RectTransform>();
            titRt.anchorMin = new Vector2(0.05f, 0.82f);
            titRt.anchorMax = new Vector2(0.95f, 0.98f);
            titRt.offsetMin = Vector2.zero;
            titRt.offsetMax = Vector2.zero;

            // Texto descritivo
            GameObject txtObj = new GameObject("Texto");
            txtObj.transform.SetParent(p1.transform, false);
            var txt = txtObj.AddComponent<TextMeshProUGUI>();
            if (font != null) txt.font = font;
            txt.fontSize = 18f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.textWrappingMode = TextWrappingModes.Normal;
            txt.color = Color.white;
            txt.text =
                "Os sensores detectaram detritos espaciais entrando na órbita da nave.\n\n" +
                "Os sistemas de mira foram danificados durante o ataque.\n\n" +
                "Para localizar corretamente os detritos, o sistema utiliza <color=#38BDF8><b>operações matemáticas</b></color> para representar suas coordenadas.\n\n" +
                "Resolva a operação exibida no painel e identifique o detrito que possui o resultado correto.\n" +
                "Acerte o alvo correto para proteger a nave.\n\n" +
                "<color=#F87171><b>Aviso: Um disparo incorreto consumirá energia da nave.</b></color>";
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0.06f, 0.18f);
            txtRt.anchorMax = new Vector2(0.94f, 0.79f);
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            // Botão ENTENDI
            GameObject btnObj = new GameObject("BotaoEntendi");
            btnObj.transform.SetParent(p1.transform, false);
            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.12f, 0.45f, 0.85f);
            btnEntendi = btnObj.AddComponent<Button>();
            RectTransform btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.35f, 0.04f);
            btnRt.anchorMax = new Vector2(0.65f, 0.15f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;

            GameObject btnTxtObj = new GameObject("TextoBotao");
            btnTxtObj.transform.SetParent(btnObj.transform, false);
            var bTxt = btnTxtObj.AddComponent<TextMeshProUGUI>();
            if (font != null) bTxt.font = font;
            bTxt.fontSize = 20f;
            bTxt.fontStyle = FontStyles.Bold;
            bTxt.alignment = TextAlignmentOptions.Center;
            bTxt.color = Color.white;
            bTxt.text = "ENTENDI";
            RectTransform bTxtRt = btnTxtObj.GetComponent<RectTransform>();
            bTxtRt.anchorMin = Vector2.zero;
            bTxtRt.anchorMax = Vector2.one;
            bTxtRt.offsetMin = Vector2.zero;
            bTxtRt.offsetMax = Vector2.zero;

            pan1 = p1.transform;
        }
        else
        {
            group1 = pan1.GetComponent<CanvasGroup>();
            btnEntendi = pan1.Find("BotaoEntendi")?.GetComponent<Button>();
        }

        // --- PAINEL 2: CONTROLE DA ARMA ---
        Transform pan2 = canvasHolder.transform.Find("Painel_ControleArma");
        Button btnIniciar = null;
        CanvasGroup group2 = null;

        if (pan2 == null)
        {
            GameObject p2 = new GameObject("Painel_ControleArma");
            p2.transform.SetParent(canvasHolder.transform, false);
            group2 = p2.AddComponent<CanvasGroup>();
            RectTransform p2Rt = p2.AddComponent<RectTransform>();
            p2Rt.anchorMin = Vector2.zero;
            p2Rt.anchorMax = Vector2.one;
            p2Rt.offsetMin = Vector2.zero;
            p2Rt.offsetMax = Vector2.zero;

            // Fundo
            GameObject bg2 = new GameObject("Fundo");
            bg2.transform.SetParent(p2.transform, false);
            var bgImg = bg2.AddComponent<Image>();
            bgImg.color = new Color(0.03f, 0.07f, 0.16f, 0.96f);
            RectTransform bgRt = bg2.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Borda decorativa
            GameObject border = new GameObject("Borda");
            border.transform.SetParent(p2.transform, false);
            var bImg = border.AddComponent<Image>();
            bImg.color = new Color(0.2f, 0.85f, 0.4f, 0.85f);
            RectTransform bRt = border.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.04f, 0.80f);
            bRt.anchorMax = new Vector2(0.96f, 0.81f);
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            // Título
            GameObject titObj = new GameObject("Titulo");
            titObj.transform.SetParent(p2.transform, false);
            var titTxt = titObj.AddComponent<TextMeshProUGUI>();
            if (font != null) titTxt.font = font;
            titTxt.fontSize = 24f;
            titTxt.fontStyle = FontStyles.Bold;
            titTxt.alignment = TextAlignmentOptions.Center;
            titTxt.color = new Color(0.3f, 0.9f, 0.5f);
            titTxt.text = "CONTROLE DA ARMA\n<size=75%><color=#86EFAC>PREPARE-SE PARA A INTERCEPTAÇÃO</color></size>";
            RectTransform titRt = titObj.GetComponent<RectTransform>();
            titRt.anchorMin = new Vector2(0.05f, 0.82f);
            titRt.anchorMax = new Vector2(0.95f, 0.98f);
            titRt.offsetMin = Vector2.zero;
            titRt.offsetMax = Vector2.zero;

            // Texto de instruções
            GameObject txtObj = new GameObject("Texto");
            txtObj.transform.SetParent(p2.transform, false);
            var txt = txtObj.AddComponent<TextMeshProUGUI>();
            if (font != null) txt.font = font;
            txt.fontSize = 17f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.textWrappingMode = TextWrappingModes.Normal;
            txt.color = Color.white;
            txt.text =
                "A defesa da nave será realizada manualmente.\n\n" +
                "<b>Pegue a arma disponível à sua frente</b> com o controle VR.\n" +
                "Mire nos detritos e pressione o gatilho para disparar.\n\n" +
                "<color=#FDE047><b>COMO JOGAR:</b></color>\n" +
                "1. Pegue a arma | 2. Resolva a operação\n" +
                "3. Localize o detrito correto | 4. Mire no alvo | 5. Pressione o gatilho\n\n" +
                "<color=#F87171><b>ERRAR O ALVO CONSUME ENERGIA.</b></color>";
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0.06f, 0.18f);
            txtRt.anchorMax = new Vector2(0.94f, 0.79f);
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            // Botão INICIAR DEFESA
            GameObject btnObj = new GameObject("BotaoIniciarDefesa");
            btnObj.transform.SetParent(p2.transform, false);
            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.16f, 0.65f, 0.35f);
            btnIniciar = btnObj.AddComponent<Button>();
            RectTransform btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.30f, 0.04f);
            btnRt.anchorMax = new Vector2(0.70f, 0.15f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;

            GameObject btnTxtObj = new GameObject("TextoBotao");
            btnTxtObj.transform.SetParent(btnObj.transform, false);
            var bTxt = btnTxtObj.AddComponent<TextMeshProUGUI>();
            if (font != null) bTxt.font = font;
            bTxt.fontSize = 20f;
            bTxt.fontStyle = FontStyles.Bold;
            bTxt.alignment = TextAlignmentOptions.Center;
            bTxt.color = Color.white;
            bTxt.text = "INICIAR DEFESA";
            RectTransform bTxtRt = btnTxtObj.GetComponent<RectTransform>();
            bTxtRt.anchorMin = Vector2.zero;
            bTxtRt.anchorMax = Vector2.one;
            bTxtRt.offsetMin = Vector2.zero;
            bTxtRt.offsetMax = Vector2.zero;

            pan2 = p2.transform;
            p2.SetActive(false); // Inicia desativado até o clique em ENTENDI
        }
        else
        {
            group2 = pan2.GetComponent<CanvasGroup>();
            btnIniciar = pan2.Find("BotaoIniciarDefesa")?.GetComponent<Button>();
        }

        // 3. Configura IntroducaoDefesaVR no GerenteDefesa
        GerenteDefesa gerente = Object.FindAnyObjectByType<GerenteDefesa>();
        if (gerente != null)
        {
            IntroducaoDefesaVR intro = gerente.GetComponent<IntroducaoDefesaVR>();
            if (intro == null) intro = gerente.gameObject.AddComponent<IntroducaoDefesaVR>();

            SerializedObject introSo = new SerializedObject(intro);
            introSo.FindProperty("canvas1Protocolo").objectReferenceValue = pan1.gameObject;
            introSo.FindProperty("canvas2Arma").objectReferenceValue = pan2.gameObject;
            introSo.FindProperty("group1").objectReferenceValue = group1;
            introSo.FindProperty("group2").objectReferenceValue = group2;
            introSo.FindProperty("physicalWeaponObj").objectReferenceValue = armaObj;
            introSo.ApplyModifiedProperties();

            // Configura os UnityEvents dos botões se ainda não configurados
            if (btnEntendi != null)
            {
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(btnEntendi.onClick, intro.OnClickEntendiProtocolo);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(btnEntendi.onClick, intro.OnClickEntendiProtocolo);
            }
            if (btnIniciar != null)
            {
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(btnIniciar.onClick, intro.OnClickIniciarDefesa);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(btnIniciar.onClick, intro.OnClickIniciarDefesa);
            }

            EditorUtility.SetDirty(gerente.gameObject);
        }

        EditorUtility.SetDirty(canvasHolder);
        EditorUtility.SetDirty(armaObj);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(">>> [ConfiguradorNarrativa] DefenseScene salva com sucesso.");
    }
}
